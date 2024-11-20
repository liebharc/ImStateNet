namespace ImStateNet.Core
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;

    public sealed class CalculationNodesNetwork
    {
        public CalculationNodesNetwork(ImmutableList<INode> nodes)
        {
            Nodes = nodes;
            (Levels, _nodeToLevel) = GetLevelsAndReverseLevels(nodes);
        }

        public ImmutableList<INode> Nodes { get; }

        /// <summary>
        /// Returns the nodes organized in levels. Levels are so, that
        /// a node in a higher level can only depend on a node in a lower
        /// level. Therefore all nodes in a level can be calculated in parallel
        /// as soon as all nodes in the lower levels are done calculating.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<IDerivedNode>> Levels { get; }

        private IDictionary<INode, int> _nodeToLevel;

        public int GetLevel(INode node)
        {
            if (_nodeToLevel.TryGetValue(node, out var level))
            {
                return level;
            }

            throw new InvalidOperationException(node.Name + " is not part of state");
        }

        private static (IReadOnlyList<IReadOnlyList<IDerivedNode>> levels, IDictionary<INode, int> reverseLevels) GetLevelsAndReverseLevels(ImmutableList<INode> nodes)
        {
            var levels = new List<List<IDerivedNode>>();
            var nodeLevels = new Dictionary<INode, int>();
            var reverseLevels = new Dictionary<INode, int>(nodes.Count);

            foreach (var node in nodes)
            {
                if (node is not IDerivedNode derivedNode)
                {
                    continue;
                }

                int level = GetNextLevel(derivedNode.Dependencies);
                nodeLevels[node] = level;

                while (levels.Count <= level)
                    levels.Add(new List<IDerivedNode>());

                levels[level].Add(derivedNode);

                // Directly populate reverseLevels while building levels
                reverseLevels[node] = level;

                int GetNextLevel(IReadOnlyList<INode> dependencies)
                {
                    if (!dependencies.Any())
                    {
                        return 0;
                    }

                    return dependencies.Max(dep => GetLevel(dep) + 1);
                }

                int GetLevel(INode node)
                {
                    if (node is IDerivedNode)
                    {
                        return nodeLevels[node];
                    }

                    return 0;
                }
            }

            return (levels, reverseLevels);
        }
    }
}
