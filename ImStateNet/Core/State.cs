namespace ImStateNet.Core
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;

    public class State
    {
        private static readonly object LazyValue = new object();
        private readonly object LazyValueLock = new object();
        private readonly CalculationNodesNetwork _metaInfo;
        private ImmutableDictionary<INode, object?> _values;
        private readonly ImmutableHashSet<INode> _changes;
        private readonly ImmutableDictionary<INode, object?> _initialValues;
        private readonly Guid _versionId;

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
            bool hasValue = _values.TryGetValue(node, out var value);
            if (hasValue && value != LazyValue)
            {
                return _values[node];
            }

            lock (LazyValueLock)
            {
                var toBeCalculated = GetAllDependenciesRecursive((IDerivedNode)node);
                CalculateListOfLazyNodes(toBeCalculated);
                return _values[node];
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

        private void CalculateListOfLazyNodes(IList<IDerivedNode> nodes)
        {
            var groupedByLevel = nodes.GroupBy(n => _metaInfo.GetLevel(n)).OrderBy(g => g.Key);
            var values = _values;
            foreach (var level in groupedByLevel)
            {
                var results = level.AsParallel().Select(ProcessNode);
                foreach (var result in results)
                {
                    values = values.SetItem(result.Node, result.NewValue);
                }
            }

            _values = values;

            IntermediateCommitResult ProcessNode(IDerivedNode node)
            {
                var newValue = node.Calculate(node.Dependencies.Select(dep => values[dep]).ToList());
                return new IntermediateCommitResult
                {
                    Node = node,
                    NewValue = newValue,
                    HasChanged = true,
                    IsUnprocessed = false
                };
            }
        }

        public (State, ImmutableHashSet<INode>) Commit(CancellationToken? cancellationToken = null, bool parallel = true)
        {
            if (_changes.IsEmpty) return (this, ImmutableHashSet<INode>.Empty);

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


                IEnumerable<IntermediateCommitResult> resultsInThisLevel;
                if (parallel && !cancellation.IsCancellationRequested)
                {
                    resultsInThisLevel = level.AsParallel().Select(ProcessNode);
                }
                else
                {
                    resultsInThisLevel = level.Select(ProcessNode);
                }

                foreach (var result in resultsInThisLevel)
                {
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

            IntermediateCommitResult ProcessNode(IDerivedNode node)
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
                var newValue = node.Calculate(inputs);
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
