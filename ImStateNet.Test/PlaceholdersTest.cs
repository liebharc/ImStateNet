namespace ImStateNet.Test
{
    using System;
    using ImStateNet.Core;
    using ImStateNet.Examples;
    using ImStateNet.Extensions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PlaceholderNodeTests
    {
        [TestMethod]
        public void TestUnsetPlaceholder()
        {
            var builder = new StateBuilder();
            var val1 = builder.AddInput(new InputNode<int>(), 1);
            var val2 = builder.AddInput(new InputNode<int>(), 2);
            builder.AddCalculation(new PlaceholderNode<int>());
            builder.AddCalculation(new SumNode<int>(new AbstractNode<int>[] { val1, val2 }));
            Assert.ThrowsException<InvalidOperationException>(() => builder.Build());
        }

        [TestMethod]
        public async Task TestValidState()
        {
            var builder = new StateBuilder();
            var val1 = builder.AddInput(new InputNode<int>(), 1);
            var val2 = builder.AddInput(new InputNode<int>(), 2);
            var placeholder = builder.AddCalculation(new PlaceholderNode<int>());
            var result = builder.AddCalculation(new SumNode<int>(new AbstractNode<int>[] { placeholder, val2 }));
            placeholder.Assign(new ProductNode<int>(new AbstractNode<int>[] { val1, val2 }));
            var state = await builder.BuildAndCommit();

            Assert.AreEqual(4, state.GetValue(result));
        }

        [TestMethod]
        public void TestDirectCircularDependency()
        {
            var builder = new StateBuilder();
            var val1 = builder.AddInput(new InputNode<int>(), 1);
            var placeholder = builder.AddCalculation(new PlaceholderNode<int>());
            placeholder.Assign(new ProductNode<int>(new AbstractNode<int>[] { val1, placeholder }));
            Assert.ThrowsException<InvalidOperationException>(() => builder.Build());
        }

        [TestMethod]
        public void TestIndirectCircularDependency()
        {
            var builder = new StateBuilder();
            var val1 = builder.AddInput(new InputNode<int>(), 1);
            var val2 = builder.AddInput(new InputNode<int>(), 2);
            var placeholder = builder.AddCalculation(new PlaceholderNode<int>());
            var result = builder.AddCalculation(new SumNode<int>(new AbstractNode<int>[] { placeholder, val2 }));
            placeholder.Assign(new ProductNode<int>(new AbstractNode<int>[] { val1, result }));

            Assert.ThrowsException<InvalidOperationException>(() => builder.Build());
        }
    }

}
