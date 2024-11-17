using ImStateNet.Core;
using ImStateNet.Examples;
using ImStateNet.Extensions;

namespace ImStateNet.Test
{
    [TestClass]
    public class ReconfigureTest
    {
        [TestMethod]
        public async Task ReconfigureNetworkTest()
        {
            var builder = new StateBuilder();
            var val1 = builder.AddInput(new InputNode<int>(), 1);
            var val2 = builder.AddInput(new InputNode<int>(), 2);
            var result = builder.AddCalculation(LambdaCalcNode.Create(new[] { val1, val2 }, x => Task.FromResult(x[0] + x[1])));
            var state = await builder.BuildAndCommit();

            builder = state.ChangeConfiguration();
            var sum = builder.AddCalculation(new ProductNode<int>(new[] { val1, val2 }));
            var newState = builder.Build();

            CollectionAssert.AreEquivalent(state.Nodes, new List<INode> { val1, val2, result });
            CollectionAssert.AreEquivalent(newState.Nodes, new List<INode> { val1, val2, result, sum });
            CollectionAssert.AreEquivalent(newState.Changes, new List<INode> { sum });
        }

        [TestMethod]
        public async Task RemoveNodesFromNetworkTest()
        {
            var builder = new StateBuilder();
            var val1 = builder.AddInput(new InputNode<int>(), 1);
            var val2 = builder.AddInput(new InputNode<int>(), 2);
            var result = builder.AddCalculation(LambdaCalcNode.Create(new[] { val1, val2 }, x => Task.FromResult(x[0] + x[1])));
            var state = await builder.BuildAndCommit();

            builder = state.ChangeConfiguration();
            builder.RemoveNodeAndAllDependents(val1);
            CollectionAssert.AreEquivalent(builder.Build().Nodes, new List<INode> { val2 });
        }
    }
}
