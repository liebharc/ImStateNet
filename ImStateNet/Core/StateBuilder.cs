namespace ImStateNet.Core
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;

    public class StateBuilder
    {
        private readonly List<INode> _nodes;
        private readonly Dictionary<INode, object?> _initialValues;
        private readonly HashSet<INode> _removedNodes = new();
        private readonly ISet<INode> _nodesWithInitialValues;

        public StateBuilder()
        {
            _nodes = new List<INode>();
            _initialValues = new Dictionary<INode, object?>();
            _nodesWithInitialValues = new HashSet<INode>();
        }

        public StateBuilder(IEnumerable<INode> nodes, IDictionary<INode, object?> initialValues, ISet<INode> nodesWithInitialValues)
        {
            _nodes = nodes.ToList();
            _initialValues = new Dictionary<INode, object?>(initialValues);
            _nodesWithInitialValues = nodesWithInitialValues;
        }

        public TInputNode AddInput<T, TInputNode>(TInputNode node, T value) where TInputNode : InputNode<T>
        {
            _nodes.Add(node);
            _initialValues[node] = value;
            return node;
        }

        public TDerivedNode AddCalculation<TDerivedNode>(TDerivedNode node) where TDerivedNode : IDerivedNode
        {
            _nodes.Add(node);
            _initialValues[node] = node.DefaultObjectValue;
            return node;
        }

        public void RemoveNodeAndAllDependents(INode node)
        {
            _removedNodes.Add(node);
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

        private IList<INode> RemoveMarkedNodes(IList<INode> sortedNodes)
        {
            if (!_removedNodes.Any())
            {
                return sortedNodes;
            }

            var result = new List<INode>();
            foreach (var node in sortedNodes)
            {
                bool shouldRemove = _removedNodes.Contains(node);
                if (!shouldRemove && node is IDerivedNode derivedNode)
                {
                    shouldRemove = derivedNode.Dependencies.Any(d => _removedNodes.Contains(d));
                }

                if (shouldRemove)
                {
                    _removedNodes.Add(node);
                    _initialValues.Remove(node);
                }
                else
                {
                    result.Add(node);
                }
            }

            return result;
        }

        public State Build()
        {
            var nodes = RemoveMarkedNodes(SortedNodes());
            foreach (var node in nodes.OfType<IDerivedNode>())
            {
                node.OnBuild();
            }

            var metaInfo = new CalculationNodesNetwork(nodes.ToImmutableList());
            return new State(metaInfo, _initialValues.ToImmutableDictionary(), changes: nodes.Where(n => !_nodesWithInitialValues.Contains(n)).ToImmutableHashSet());
        }

        public async Task<State> BuildAndCommit()
        {
            var state = Build();
            var result = await state.Commit();
            return result.Item1;
        }
    }
}
