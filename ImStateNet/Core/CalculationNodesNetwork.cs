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
            Levels = GetLevels(nodes);
            _nodeToLevel = ReverseLevels(Levels);
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
            return _nodeToLevel[node];
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

        private static IDictionary<INode, int> ReverseLevels(IReadOnlyList<IReadOnlyList<IDerivedNode>> levels)
        {
            var result = new Dictionary<INode, int>();
            int levelNumber = 0;
            foreach (var level in levels)
            {
                foreach (var node in level)
                {
                    result.Add(node, levelNumber);
                }

                levelNumber++;
            }

            return result;
        }
    }
}
