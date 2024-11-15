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
        private ImmutableDictionary<INode, object> _values;
        private readonly ImmutableHashSet<INode> _changes;
        private readonly ImmutableDictionary<INode, object> _initialValues;
        private readonly Guid _versionId;

        public State(
            CalculationNodesNetwork metaInfo,
            ImmutableDictionary<INode, object> values,
            ImmutableHashSet<INode>? changes = null,
            ImmutableDictionary<INode, object>? initialValues = null,
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
        public State ChangeObjectValue(IInputNode node, object newValue)
        {
            newValue = node.Validate(newValue);
            var oldValue = _initialValues.TryGetValue(node, out var old);
            var values = _values.SetItem(node, newValue);

            var changes = _changes;
            if (old != null && oldValue && node.AreValuesEqual(old, newValue))
                changes = changes.Remove(node);
            else
                changes = changes.Add(node);

            return new State(_metaInfo, values, changes, _initialValues, _versionId);
        }

        public T GetValue<T>(AbstractNode<T> node)
        {
            return (T)GetObjValue(node);
        }

        /// <summary>
        /// Gets the value of a node. Prefer using <see cref="GetValue{T}(AbstractNode{T})"/> instead.
        /// </summary>
        /// <param name="node">A node</param>
        /// <returns>The current value of the node</returns>
        public object GetObjValue(INode node)
        {
            bool hasValue = _values.TryGetValue(node, out var value);
            if (hasValue && value != LazyValue)
            {
                return _values[node];
            }

            lock (LazyValueLock)
            {
                var toBeCalculated = GetAllDependencies((IDerivedNode)node);
                CalculateListOfLazyNodes(toBeCalculated);
                return _values[node];
            }
        }

        private IList<IDerivedNode> GetAllDependencies(IDerivedNode node)
        {
            var result = new List<IDerivedNode>();
            foreach (var dependency in node.Dependencies)
            {
                if (dependency is IDerivedNode derivedNode && derivedNode.IsLazy && (!_values.TryGetValue(node, out var value) || value == LazyValue))
                {
                    result.AddRange(GetAllDependencies(derivedNode));
                }
            }

            result.Add(node);
            return result;
        }

        private void CalculateListOfLazyNodes(IList<IDerivedNode> nodes)
        {
            foreach (var node in nodes)
            {
                var newValue = node.Calculate(node.Dependencies.Select(dep => _values[dep]).ToList());
                _values = _values.SetItem(node, newValue);
            }
        }

        public (State, ImmutableHashSet<INode>) Commit(CancellationToken? cancellationToken = null, bool parallel = true)
        {
            if (_changes.IsEmpty) return (this, ImmutableHashSet<INode>.Empty);

            var values = _values;
            var changes = _changes;
            var unprocessedChanges = ImmutableHashSet<INode>.Empty;
            var lockObj = new object();

            var cancellation = cancellationToken ?? CancellationToken.None;

            foreach (var level in _metaInfo.Levels)
            {
                if (!level.Any())
                {
                    continue;
                }


                IEnumerable<IntermediateCommitResult> nodesInThisLevel;
                if (parallel && !cancellation.IsCancellationRequested)
                {
                    nodesInThisLevel = level.AsParallel().Select(ProcessNode);
                }
                else
                {
                    nodesInThisLevel = level.Select(ProcessNode);
                }

                foreach (var result in nodesInThisLevel)
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
                if (node.IsLazy)
                {
                    return new IntermediateCommitResult
                    {
                        Node = node,
                        NewValue = LazyValue,
                        // We can't tell if the node has changed as it's lazy
                        HasChanged = false,
                        IsUnprocessed = false
                    };
                }

                var anyDepsChanged = changes.Contains(node) || !changes.Intersect(node.Dependencies).IsEmpty;
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

                var newValue = node.Calculate(node.Dependencies.Select(dep => values[dep]).ToList());
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

        public bool IsConsistent() => _changes.IsEmpty;

        public override string ToString()
        {
            var nodesAndValues = string.Join(", ", Nodes.Select(node => $"{node}: {_values.GetValueOrDefault(node)}"));
            var changes = _changes.IsEmpty ? "" : $" | changes={string.Join(", ", _changes)}";
            return $"State({nodesAndValues}{changes})";
        }

        public IDictionary<string, object> Dump()
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