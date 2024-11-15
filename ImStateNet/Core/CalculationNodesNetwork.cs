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
        }

        public ImmutableList<INode> Nodes { get; }

        public IReadOnlyList<IReadOnlyList<IDerivedNode>> Levels { get; }

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
    }
}