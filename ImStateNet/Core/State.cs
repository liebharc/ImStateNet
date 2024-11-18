namespace ImStateNet.Core
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading;

    public class State
    {
        private static readonly object LazyValue = new object();
        private readonly SemaphoreSlim LazyValueLock = new SemaphoreSlim(1, 1);
        private readonly CalculationNodesNetwork _metaInfo;
        private ImmutableDictionary<INode, object?> _values;
        private readonly ImmutableHashSet<INode> _changes;
        private readonly ImmutableDictionary<INode, object?> _initialValues;
        private readonly Guid _versionId;

        public static State CreateEmptyState()
        {
            return new State(new CalculationNodesNetwork(ImmutableList<INode>.Empty), ImmutableDictionary<INode, object?>.Empty);
        }

        public State(
            CalculationNodesNetwork metaInfo,
            ImmutableDictionary<INode, object?> values,
            ImmutableHashSet<INode>? changes = null,
            ImmutableDictionary<INode, object?>? initialValues = null,
            Guid? versionId = null)
        {
            _changes = changes ?? ImmutableHashSet<INode>.Empty;
            _metaInfo = metaInfo;
            _values = values;
            _initialValues = initialValues ?? values;
            _versionId = versionId ?? Guid.NewGuid();
        }

        public Guid VersionId => _versionId;
        public ImmutableHashSet<INode> Changes => _changes;

        public ImmutableList<INode> Nodes => _metaInfo.Nodes;

        public State ChangeValue<T>(InputNode<T> node, T newValue)
        {
            return ChangeObjectValue(node, newValue);
        }

        /// <summary>
        /// Sets the value of a node. Prefer using <see cref="ChangeValue{T}(InputNode{T}, T)"/> instead.
        /// </summary>
        /// <param name="node">A node</param>
        /// <param name="newValue">The new value for the node</param>
        /// <returns>A new state, call <see cref="Commit(CancellationToken?, bool)"/> to perform the calculation of all dependencies. </returns>
        public State ChangeObjectValue(IInputNode node, object? newValue)
        {
            newValue = node.Validate(newValue);
            var oldValue = _initialValues.TryGetValue(node, out var old);
            var values = _values.SetItem(node, newValue);

            var changes = _changes;
            if (oldValue && node.AreValuesEqual(old, newValue))
                changes = changes.Remove(node);
            else
                changes = changes.Add(node);

            return new State(_metaInfo, values, changes, _initialValues, _versionId);
        }

        /// <summary>
        /// Marks a node as having changed. This can be useful if
        /// a node has an input which is not part of the state and this input 
        /// has changed.
        /// 
        /// The state pattern requires nodes to be pure function to guarantee thread
        /// safety and all inputs must be part of state. If this isn't true then the
        /// node itself must guarantee thread safetey.
        /// </summary>
        /// <param name="node">The node which will be considered as changed.</param>
        public State MarkAsChanged(INode node)
        {
            var changes = _changes.Add(node);
            return new State(_metaInfo, _values, changes, _initialValues, _versionId);
        }

        public T? GetValue<T>(AbstractNode<T> node)
        {
            return (T?)GetObjValue(node);
        }

        /// <summary>
        /// Gets the value of a node. Prefer using <see cref="GetValue{T}(AbstractNode{T})"/> instead.
        /// </summary>
        /// <param name="node">A node</param>
        /// <returns>The current value of the node</returns>
        public object? GetObjValue(INode node)
        {
            var asyncResult = GetObjValueAsync(node);
            asyncResult.Wait();
            return asyncResult.Result;
        }

        /// <summary>
        /// Async version of <see cref="GetValue{T}(AbstractNode{T})"/>. This only matters if you use lazy nodes.
        /// </summary>
        public async Task<T?> GetValueAsync<T>(AbstractNode<T> node)
        {
            return (T?)await GetObjValueAsync(node);
        }

        /// <summary>
        /// Async version of <see cref="GetObjValue(INode)"/>. This only matters if you use lazy nodes.
        /// </summary>
        public async Task<object?> GetObjValueAsync(INode node)
        {
            bool hasValue = _values.TryGetValue(node, out var value);
            if (hasValue && value != LazyValue)
            {
                return _values[node];
            }

            if (node is not IDerivedNode derivedNode || !derivedNode.IsLazy)
            {
                throw new InvalidOperationException(node.Name + " is not part of state");
            }

            await LazyValueLock.WaitAsync();
            try
            {

                var toBeCalculated = GetAllDependenciesRecursive((IDerivedNode)node);

                await CalculateListOfLazyNodes(toBeCalculated);
                return _values[node];
            }
            finally
            {
                LazyValueLock.Release();
            }
        }

        private IList<IDerivedNode> GetAllDependenciesRecursive(IDerivedNode node)
        {
            var result = new List<IDerivedNode>();
            foreach (var dependency in node.Dependencies)
            {
                if (dependency is IDerivedNode derivedNode && derivedNode.IsLazy && (!_values.TryGetValue(node, out var value) || value == LazyValue))
                {
                    result.AddRange(GetAllDependenciesRecursive(derivedNode));
                }
            }

            result.Add(node);
            return result;
        }

        private async Task CalculateListOfLazyNodes(IList<IDerivedNode> nodes)
        {
            var groupedByLevel = nodes.GroupBy(n => _metaInfo.GetLevel(n)).OrderBy(g => g.Key);
            var values = _values;
            foreach (var level in groupedByLevel)
            {
                var results = level.AsParallel().Select(ProcessNode);
                foreach (var resultTask in results)
                {
                    var result = await resultTask;
                    values = values.SetItem(result.Node, result.NewValue);
                }
            }

            _values = values;

            async Task<IntermediateCommitResult> ProcessNode(IDerivedNode node)
            {
                var newValue = await node.Calculate(node.Dependencies.Select(dep => values[dep]).ToList());
                return new IntermediateCommitResult
                {
                    Node = node,
                    NewValue = newValue,
                    HasChanged = true,
                    IsUnprocessed = false
                };
            }
        }

        /// <summary>
        /// Applies all <see cref="Changes"/> to the curent state by calculating all derived values.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token, if cancelled then the state will stop calculating more nodes and return.</param>
        /// <param name="parallel">Indicates whether or not the calculation should be parallized. Disable this for debugging if you want to see clearer stack traces.</param>
        /// <returns>A state with no more <see cref="Changes"/> - unless the calculation was cancelled.</returns>
        public async Task<(State, ImmutableHashSet<INode>)> Commit(CancellationToken? cancellationToken = null, bool parallel = true)
        {
            if (_changes.IsEmpty) 
            { 
                return (this, ImmutableHashSet<INode>.Empty); 
            }

            var values = _values;
            var changes = _changes;
            var unprocessedChanges = ImmutableHashSet<INode>.Empty;

            var cancellation = cancellationToken ?? CancellationToken.None;

            foreach (var level in _metaInfo.Levels)
            {
                if (!level.Any())
                {
                    continue;
                }


                IEnumerable<Task<IntermediateCommitResult>> resultsInThisLevel;
                if (parallel && !cancellation.IsCancellationRequested)
                {
                    resultsInThisLevel = level.AsParallel().Select(ProcessNode);
                }
                else
                {
                    resultsInThisLevel = level.Select(ProcessNode);
                }

                foreach (var resultTask in resultsInThisLevel)
                {
                    var result = await resultTask;
                    if (result.IsUnprocessed)
                    {
                        unprocessedChanges = unprocessedChanges.Add(result.Node);
                    }
                    else if (result.HasChanged)
                    {
                        values = values.SetItem(result.Node, result.NewValue);
                        changes = changes.Add(result.Node);
                    }
                }
            }

            var newState = new State(_metaInfo, values, unprocessedChanges, values);
            return (newState, changes);

            async Task<IntermediateCommitResult> ProcessNode(IDerivedNode node)
            {
                var anyDepsChanged = changes.Contains(node) || !changes.Intersect(node.Dependencies).IsEmpty;
                if (node.IsLazy)
                {
                    return new IntermediateCommitResult
                    {
                        Node = node,
                        // NewValue doesn't matter if HasChanged is set to false
                        NewValue = LazyValue,
                        // If the dependencies of a lazy node have changed, then the lazy node 
                        // might produce a different value
                        HasChanged = anyDepsChanged,
                        IsUnprocessed = false
                    };
                }

                if (!anyDepsChanged)
                {
                    return new IntermediateCommitResult
                    {
                        Node = node,
                        NewValue = null,
                        HasChanged = false,
                        IsUnprocessed = false
                    };
                }

                if (cancellation.IsCancellationRequested)
                {
                    return new IntermediateCommitResult
                    {
                        Node = node,
                        NewValue = null,
                        HasChanged = false,
                        IsUnprocessed = true
                    };
                }

                var inputs = node.Dependencies.Select(dep => values[dep]).ToList();
                var newValue = await node.Calculate(inputs);
                var oldValue = _initialValues.TryGetValue(node, out var old);
                var haveValuesChanged = !oldValue || !node.AreValuesEqual(old, newValue);
                return new IntermediateCommitResult
                {
                    Node = node,
                    NewValue = newValue,
                    HasChanged = haveValuesChanged,
                    IsUnprocessed = false
                };
            }
        }

        public bool IsConsistent => _changes.IsEmpty;

        public override string ToString()
        {
            var nodesAndValues = string.Join(", ", Nodes.Select(node => $"{node}: {_values.GetValueOrDefault(node)}"));
            var changes = _changes.IsEmpty ? "" : $" | changes={string.Join(", ", _changes)}";
            return $"State({nodesAndValues}{changes})";
        }

        public IDictionary<string, object?> Dump()
        {
            return Nodes.ToDictionary(node => node.Name, node => _values.GetValueOrDefault(node));
        }

        public StateBuilder ChangeConfiguration()
        {
            var nodes = _metaInfo.Nodes;
            return new StateBuilder(nodes, _initialValues, nodes.Where(n => !_changes.Contains(n)).ToHashSet());
        }
    }

    public readonly struct IntermediateCommitResult
    {
        public INode Node { get; init; }
        public bool HasChanged { get; init; }
        public bool IsUnprocessed { get; init; }
        public object? NewValue { get; init; }
    }
}
