namespace ImStateNet
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading.Tasks;

    public interface INode
    {
        void OnBuild();
        string Name { get; }
        bool AreValuesEqual(object value1, object value2);
    }

    public abstract class AbstractNode<T> : INode
    {
        protected string _name;

        protected AbstractNode(string? name = null)
        {
            _name = name ?? Guid.NewGuid().ToString();
        }

        public virtual void OnBuild()
        {
            // Called when the state is built, a node can be part of multiple states.
        }

        public string Name => _name;

        public virtual bool AreValuesEqual(T value1, T value2)
        {
            // Compares two values and returns true if they are equal.
            // Can be overridden by subclasses to provide a custom comparison method, e.g., by using a tolerance for floats.
            return EqualityComparer<T>.Default.Equals(value1, value2);
        }

        public override string ToString() => _name;

        bool INode.AreValuesEqual(object value1, object value2)
        {
            return AreValuesEqual((T)value1, (T)value2);
        }
    }

    public class InputNode<T> : AbstractNode<T>
    {
        public InputNode(string? name = null) : base(name) { }

        public virtual T Validate(T value)
        {
            // Validates the value before setting it. It can coerce the value to a valid one or throw an exception if the value is invalid.
            return value;
        }
    }

    public interface IDerivedNode : INode
    {
        IReadOnlyList<INode> Dependencies { get; }

        bool IsLazy { get; }

        object Calculate(IReadOnlyList<object> inputs);
    }

    public abstract class DerivedNode<T> : AbstractNode<T>, IDerivedNode
    {
        private static bool AnyLazyDependencies(IReadOnlyList<INode> dependencies)
        {
            foreach (var dependency in dependencies)
            {
                if (dependency is IDerivedNode derivedNode && derivedNode.IsLazy)
                {
                    return true;
                }
            }

            return false;
        }

        protected IReadOnlyList<INode> _dependencies;

        protected DerivedNode(IReadOnlyList<INode> dependencies, string? name = null) : base(name)
        {
            _dependencies = dependencies;
            IsLazy = AnyLazyDependencies(dependencies);
        }

        public IReadOnlyList<INode> Dependencies => _dependencies;

        public bool IsLazy { get; protected set; } = false;

        public abstract T Calculate(IReadOnlyList<object> inputs);

        object IDerivedNode.Calculate(IReadOnlyList<object> inputs)
        {
            return Calculate(inputs);
        }
    }

    public class State
    {
        private static readonly object LazyValue = new object();
        private readonly object LazyValueLock = new object();
        private readonly ImmutableList<INode> _nodes;
        private ImmutableDictionary<INode, object> _values;
        private readonly ImmutableHashSet<INode> _changes;
        private readonly ImmutableDictionary<INode, object> _initialValues;
        private readonly Guid _versionId;
        private readonly IReadOnlyList<IReadOnlyList<IDerivedNode>> _levels;

        public State(
            ImmutableList<INode> nodes,
            ImmutableDictionary<INode, object> values,
            ImmutableHashSet<INode>? changes = null,
            ImmutableDictionary<INode, object>? initialValues = null,
            IReadOnlyList<IReadOnlyList<IDerivedNode>>? levels = null,
            Guid? versionId = null)
        {
            _nodes = nodes;
            _changes = changes ?? ImmutableHashSet<INode>.Empty;
            _values = values;
            _initialValues = initialValues ?? values;
            _versionId = versionId ?? Guid.NewGuid();
            _levels = levels ?? GetLevels(nodes);
        }

        public Guid VersionId => _versionId;
        public ImmutableHashSet<INode> Changes => _changes;

        public ImmutableList<INode> Nodes => _nodes;

        public State ChangeValue<T>(InputNode<T> node, T newValue)
        {
            newValue = node.Validate(newValue);
            var oldValue = _initialValues.TryGetValue(node, out var old);
            var values = _values.SetItem(node, newValue);

            var changes = _changes;
            if (old != null && oldValue && node.AreValuesEqual((T)old, newValue))
                changes = changes.Remove(node);
            else
                changes = changes.Add(node);

            return new State(Nodes, values, changes, _initialValues, _levels, _versionId);
        }

        public T GetValue<T>(AbstractNode<T> node)
        {
            bool hasValue = _values.TryGetValue(node, out var value);
            if (hasValue && value != LazyValue)
            {
                return (T)_values[node];
            }

            lock (LazyValueLock)
            {
                var toBeCalculated = GetAllDependencies((IDerivedNode)node);
                CalculateListOfLazyNodes(toBeCalculated);
                return (T)_values[node];
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

        private static IReadOnlyList<IReadOnlyList<IDerivedNode>> GetLevels(IEnumerable<INode> nodes)
        {
            var levels = new List<List<IDerivedNode>>();
            var nodeLevels = new Dictionary<INode, int>();

            foreach (var node in nodes)
            {
                if (node is not IDerivedNode derivedNode)
                {
                    continue;
                }

                int level = derivedNode.Dependencies.Max(dep => GetLevel(dep) + 1);
                nodeLevels[node] = level;

                while (levels.Count <= level)
                    levels.Add(new List<IDerivedNode>());

                levels[level].Add(derivedNode);
            }

            return levels;

            int GetLevel(INode node)
            {
                if (node is IDerivedNode derivedNode)
                {
                    return nodeLevels[node];
                }

                return 0;
            }
        }

        public (State, ImmutableHashSet<INode>) Commit(CancellationToken? cancellationToken = null)
        {
            if (_changes.IsEmpty) return (this, ImmutableHashSet<INode>.Empty);

            var values = _values;
            var changes = _changes;
            var unprocessedChanges = ImmutableHashSet<INode>.Empty;
            var lockObj = new object();

            var cancellation = cancellationToken ?? CancellationToken.None;

            bool parallel = _nodes.Count > 100;

            foreach (var level in _levels)
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

                foreach (var result in nodesInThisLevel) {
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

            var newState = new State(Nodes, values, unprocessedChanges, values, _levels);
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
    }

    public class StateBuilder
    {
        private readonly List<INode> _nodes = new();
        private readonly Dictionary<INode, object> _initialValues = new();

        public TInputNode AddInput<T, TInputNode>(TInputNode node, T value) where TInputNode : InputNode<T>
        {
            _nodes.Add(node);
            _initialValues[node] = value;
            return node;
        }

        public TDerivedNode AddCalculation<TDerivedNode>(TDerivedNode node) where TDerivedNode : IDerivedNode
        {
            _nodes.Add(node);
            return node;
        }

        private List<INode> SortedNodes()
        {
            var sortedNodes = new List<INode>();
            var visited = new HashSet<INode>();
            var visiting = new HashSet<INode>();

            void Visit(INode node)
            {
                if (visiting.Contains(node))
                    throw new InvalidOperationException("Circular dependency detected");

                if (!visited.Contains(node))
                {
                    visiting.Add(node);
                    if (node is IDerivedNode derivedNode)
                    {
                        foreach (var dependency in derivedNode.Dependencies)
                            Visit(dependency);
                    }

                    visiting.Remove(node);
                    visited.Add(node);
                    sortedNodes.Add(node);
                }
            }

            foreach (var node in _nodes)
                Visit(node);

            return sortedNodes;
        }

        public State Build()
        {
            var nodes = SortedNodes();
            foreach (var node in nodes.OfType<IDerivedNode>())
            {
                node.OnBuild();
            }

            var state = new State(nodes.ToImmutableList(), _initialValues.ToImmutableDictionary(), changes: nodes.ToImmutableHashSet());
            return state.Commit().Item1;
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