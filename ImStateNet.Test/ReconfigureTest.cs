namespace ImStateNet.Test
{
    [TestClass]
    public class ReconfigureTest
    {
        [TestMethod]
        public void ReconfigureNetworkTest()
        {
            var builder = new StateBuilder();
            var val1 = builder.AddInput(new InputNode<int>(), 1);
            var val2 = builder.AddInput(new InputNode<int>(), 2);
            var result = builder.AddCalculation(new LambdaCalcNode<int>(x => x[0] + x[1], new[] { val1, val2 }));
            var state = builder.Build();

            builder = state.ChangeConfiguration();
            var sum = builder.AddCalculation(new ProductNode<int>(new[] { val1, val2 }));
            var newState = builder.Build(skipCalculation: true);

            CollectionAssert.AreEquivalent(state.Nodes, new List<INode> { val1, val2, result });
            CollectionAssert.AreEquivalent(newState.Nodes, new List<INode> { val1, val2, result, sum });
            CollectionAssert.AreEquivalent(newState.Changes, new List<INode> { sum });
        }

        [TestMethod]
        public void RemoveNodesFromNetworkTest()
        {
            var builder = new StateBuilder();
            var val1 = builder.AddInput(new InputNode<int>(), 1);
            var val2 = builder.AddInput(new InputNode<int>(), 2);
            var result = builder.AddCalculation(new LambdaCalcNode<int>(x => x[0] + x[1], new[] { val1, val2 }));
            var state = builder.Build();

            builder = state.ChangeConfiguration();
            builder.RemoveNodeAndAllDependencies(val1);
            CollectionAssert.AreEquivalent(builder.Build().Nodes, new List<INode> { val2 });
        }
    }
}
