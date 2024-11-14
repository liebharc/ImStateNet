namespace ImStateNet
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Runtime.Intrinsics.Arm;
    using System.Security.AccessControl;

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

        object Calculate(IReadOnlyList<object> inputs);
    }

    public abstract class DerivedNode<T> : AbstractNode<T>, IDerivedNode
    {
        protected IReadOnlyList<INode> _dependencies;

        protected DerivedNode(IReadOnlyList<INode> dependencies, string? name = null) : base(name)
        {
            _dependencies = dependencies;
        }

        public IReadOnlyList<INode> Dependencies => _dependencies;

        public abstract T Calculate(IReadOnlyList<object> inputs);

        object IDerivedNode.Calculate(IReadOnlyList<object> inputs)
        {
            return Calculate(inputs);
        }
    }

    public class State
    {
        private readonly ImmutableList<INode> _nodes;
        private readonly ImmutableDictionary<INode, object> _values;
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
            var oldValue = _initialValues.TryGetValue(node, out var old) ? (T)old : default;
            var values = _values.SetItem(node, newValue);

            var changes = _changes;
            if (old != null && node.AreValuesEqual(oldValue, newValue))
                changes = _changes.Remove(node);
            else
                changes = _changes.Add(node);

            return new State(Nodes, values, changes, _initialValues, _levels, _versionId);
        }

        public T GetValue<T>(AbstractNode<T> node)
        {
            return (T)_values[node];
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

        public async Task<(State, ImmutableHashSet<INode>)> Commit()
        {
            if (_changes.IsEmpty) return (this, ImmutableHashSet<INode>.Empty);

            var values = _values;
            var changes = new ConcurrentBag<INode>(_changes);

            foreach (var level in _levels)
            {
                if (!level.Any())
                {
                    continue;
                }

                await Parallel.ForEachAsync(level, async (node, _) =>
                {
                    var anyDepsChanged = !_changes.Intersect(node.Dependencies).IsEmpty;
                    if (anyDepsChanged)
                    {
                        var newValue = node.Calculate(node.Dependencies.Select(dep => values[dep]).ToList());
                        var oldValue = _initialValues.TryGetValue(node, out var old) ? old : null;
                        values = values.SetItem(node, newValue);

                        if (!node.AreValuesEqual(oldValue, newValue))
                            changes.Add(node);
                    }
                });
            }

            return (new State(Nodes, values, ImmutableHashSet<INode>.Empty, values, _levels), changes.ToImmutableHashSet());
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
                _initialValues[node] = node.Calculate(node.Dependencies.Select(dep => _initialValues[dep]).ToList());
            }

            return new State(nodes.ToImmutableList(), _initialValues.ToImmutableDictionary());
        }
    }

}