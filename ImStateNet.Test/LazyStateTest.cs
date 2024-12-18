﻿namespace ImStateNet.Test
{
    using ImStateNet.Core;
    using ImStateNet.Examples;
    using ImStateNet.Extensions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Collections.Immutable;

    public class LazySumState
    {
        public InputNode<int> Val1 { get; private set; }
        public InputNode<int> Val2 { get; private set; }
        public InputNode<int> Val3 { get; private set; }
        public AbstractNode<int> Sum { get; private set; }
        public AbstractNode<int> Product { get; private set; }
        public AbstractNode<int> FinalSum { get; private set; }
        public State State { get; private set; }

        public LazySumState()
        {
            var builder = new StateBuilder();
            Val1 = builder.AddInput(new InputNode<int>(), 1);
            Val2 = builder.AddInput(new NumericMinMaxNode<int>(1, 5), 2);
            Val3 = builder.AddInput(new InputNode<int>(), 3);
            Sum = builder.AddCalculation(new LazyNode<int>(new SumNode<int>(new[] { Val1, Val2 })));
            Product = builder.AddCalculation(new ProductNode<int>(new[] { Val1, Val2 }));
            FinalSum = builder.AddCalculation(new SumNode<int>(new[] { Sum, Product }));
            State = builder.Build();
            Init().Wait();
        }

        private async Task Init()
        {
            (State, _) = await State.Commit();
        }

        public T? GetValue<T>(AbstractNode<T> node) => State.GetValue(node);

        public void SetValue<T>(InputNode<T> node, T value) => State = State.ChangeValue(node, value);

        public async Task<ImmutableHashSet<INode>> Commit()
        {
            (State, var changes) = await State.Commit();
            return changes;
        }

        public int NumberOfChanges() => State.Changes.Count;
    }

    [TestClass]
    public class LazyStateTest
    {
        [TestMethod]
        public async Task TestValidState()
        {
            var state = new LazySumState();

            state.SetValue(state.Val1, 3);
            await state.Commit();

            Assert.AreEqual(11, state.GetValue(state.FinalSum));
            Assert.AreEqual(5, state.GetValue(state.Sum));
            Assert.AreEqual(6, state.GetValue(state.Product));
        }

        [TestMethod]
        public async Task TestLazyNodesAreMarkedAsChangedIfInputChanges()
        {
            var state = new LazySumState();

            state.SetValue(state.Val1, 100);
            var changes = await state.Commit();
            CollectionAssert.AreEquivalent(changes, new INode[] { state.Val1, state.Sum, state.FinalSum, state.Product });
        }

        [TestMethod]
        public async Task TestLazyNodesAreNotMarkedAsChangedIfInputsRemainUnchanged()
        {
            var state = new LazySumState();

            state.SetValue(state.Val3, 100);
            var changes = await state.Commit();
            CollectionAssert.AreEquivalent(changes, new INode[] { state.Val3 });
        }

        [TestMethod]
        public async Task TestChangeMinMaxNode()
        {
            var state = new LazySumState();
            Assert.AreEqual(2, state.GetValue(state.Val2));

            state.SetValue(state.Val2, 6);
            await state.Commit();

            Assert.AreEqual(5, state.GetValue(state.Val2));
        }

        [TestMethod]
        public async Task TestChangeToSameValue()
        {
            var state = new LazySumState();
            state.SetValue(state.Val1, 1);
            Assert.AreEqual(0, state.NumberOfChanges());
        }

        [TestMethod]
        public async Task TestRevertingChanges()
        {
            var state = new LazySumState();
            var value1 = state.GetValue(state.Val1);
            state.SetValue(state.Val1, value1 + 5);

            Assert.AreEqual(1, state.NumberOfChanges());

            state.SetValue(state.Val1, value1);

            Assert.AreEqual(0, state.NumberOfChanges());
        }
    }

}
