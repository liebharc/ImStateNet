namespace ImStateNet.Test
{
    using ImStateNet.Core;
    using ImStateNet.Examples;
    using ImStateNet.Extensions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public class MyBinaryNode : BinaryCalcNode<string, double, int>
    {
        public MyBinaryNode(AbstractNode<double> dependency1, AbstractNode<int> dependency2, string? name = null) : base(dependency1, dependency2, name)
        {
        }

        protected override Task<string?> Calculation(double value1, int value2)
        {
            return Task.FromResult<string?>((value1 + value2).ToString());
        }
    }

    public class SimpleSumState
    {
        public InputNode<int> Val1 { get; private set; }
        public InputNode<int> Val2 { get; private set; }
        public InputNode<double> Val3 { get; private set; }
        public AbstractNode<int> Calc { get; private set; }
        public AbstractNode<int> Sum { get; private set; }
        public AbstractNode<int> Product { get; private set; }
        public AbstractNode<string> StrNode { get; private set; }
        public State State { get; private set; }

        public SimpleSumState()
        {
            var builder = new StateBuilder();
            Val1 = builder.AddInput(new InputNode<int>(), 1);
            Val2 = builder.AddInput(new NumericMinMaxNode<int>(1, 5), 2);
            Val3 = builder.AddInput(new InputNode<double>(), 3.0);
            Calc = builder.AddCalculation(LambdaCalcNode.Create(new AbstractNode<int>[] { Val1, Val2 }, x => Task.FromResult(x[0] + x[1])));
            Sum = builder.AddCalculation(new SumNode<int>(new[] { Val1, Val2 }));
            Product = builder.AddCalculation(new ProductNode<int>(new[] { Val1, Val2 }));
            StrNode = builder.AddCalculation(new MyBinaryNode(Val3, Val1));
            State = builder.Build();
            Init().Wait();
        }

        private async Task Init()
        {
            (State, _) = await State.Commit();
        }

        public T? GetValue<T>(AbstractNode<T> node) => State.GetValue(node);

        public void SetValue<T>(InputNode<T> node, T value) => State = State.ChangeValue(node, value);

        public async Task Commit() => (State, _) = await State.Commit();

        public int NumberOfChanges() => State.Changes.Count;
    }

    [TestClass]
    public class StateTest
    {
        [TestMethod]
        public async Task TestValidState()
        {
            var state = new SimpleSumState();
            var initialId = state.State.VersionId;
            Assert.AreEqual(3, state.GetValue(state.Calc));

            state.SetValue(state.Val1, 3);
            Assert.AreEqual(initialId, state.State.VersionId);
            await state.Commit();
            Assert.AreNotEqual(initialId, state.State.VersionId);

            Assert.AreEqual(5, state.GetValue(state.Calc));
            Assert.AreEqual(5, state.GetValue(state.Sum));
            Assert.AreEqual(6, state.GetValue(state.Product));
            Assert.AreEqual("6", state.GetValue(state.StrNode));
        }

        [TestMethod]
        public async Task TestMissingDependency()
        {
            var builder = new StateBuilder();
            var val1 = builder.AddInput(new InputNode<int>(), 1);
            var val2 = new InputNode<int>();
            var result = builder.AddCalculation(LambdaCalcNode.Create(new[] { val1, val2 }, x => Task.FromResult(x[0] + x[1])));
            await Assert.ThrowsExceptionAsync<KeyNotFoundException>(async () => await builder.Build().Commit(parallel: false));
        }

        [TestMethod]
        public async Task TestChangeMinMaxNode()
        {
            var state = new SimpleSumState();
            Assert.AreEqual(2, state.GetValue(state.Val2));

            state.SetValue(state.Val2, 6);
            await state.Commit();

            Assert.AreEqual(5, state.GetValue(state.Val2));
        }

        [TestMethod]
        public void TestChangeToSameValue()
        {
            var state = new SimpleSumState();
            state.SetValue(state.Val1, 1);
            Assert.AreEqual(0, state.NumberOfChanges());
        }

        [TestMethod]
        public void TestRevertingChanges()
        {
            var state = new SimpleSumState();
            var value1 = state.GetValue(state.Val1);
            state.SetValue(state.Val1, value1 + 5);

            Assert.AreEqual(1, state.NumberOfChanges());

            state.SetValue(state.Val1, value1);

            Assert.AreEqual(0, state.NumberOfChanges());
        }

        [TestMethod]
        public async Task TestExample()
        {
            var builder = new StateBuilder();
            var val1 = builder.AddInput(new InputNode<int>(), 1);
            var val2 = builder.AddInput(new InputNode<int>(), 2);
            var result = builder.AddCalculation(LambdaCalcNode.Create(new[] { val1, val2 }, x => Task.FromResult(x[0] + x[1])));
            var state = await builder.BuildAndCommit();

            Assert.AreEqual(3, state.GetValue(result));

            state = state.ChangeValue(val1, 2);
            Assert.IsFalse(state.IsConsistent);

            state = state.ChangeValue(val1, 1);
            Assert.IsTrue(state.IsConsistent);

            (state, var changes) = await state.ChangeValue(val1, 2).Commit();
            Assert.IsTrue(state.IsConsistent);
            Assert.AreEqual(4, state.GetValue(result));
            CollectionAssert.AreEquivalent(new INode[] { val1, result }, changes);
        }

        [TestMethod]
        public void TestDefaultValues()
        {
            var builder = new StateBuilder();
            var val1 = builder.AddInput(new InputNode<int>(), 1);
            var val2 = builder.AddInput(new InputNode<int>(), 2);
            var result = builder.AddCalculation(LambdaCalcNode.Create(new[] { val1, val2 }, x => Task.FromResult(x[0] + x[1])));
            var state = builder.Build();

            Assert.AreEqual(0, state.GetValue(result));
        }
    }
}
