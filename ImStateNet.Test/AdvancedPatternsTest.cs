namespace ImStateNet.Test
{
    using System;
    using System.Collections.Generic;
    using ImStateNet.Core;
    using ImStateNet.Examples;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class AdvancedPatternsTest
    {
        [TestMethod]
        public void TestMergeChanges()
        {
            var builder = new StateBuilder();
            var val1 = builder.AddInput(new InputNode<int>(), 1);
            var val2 = builder.AddInput(new InputNode<int>(), 2);
            var val3 = builder.AddInput(new InputNode<int>(), 3);
            var result = builder.AddCalculation(new SumNode<int>(new List<AbstractNode<int>> { val1, val2, val3 }));
            var state1 = builder.Build();

            var state2 = state1.ChangeValue(val1, 5);

            var state3 = state1.ChangeValue(val2, 6);

            var merged = MergeChanges(state2, state3);

            Assert.AreEqual(5, merged.GetValue(val1));
            Assert.AreEqual(6, merged.GetValue(val2));
            Assert.AreEqual(3, merged.GetValue(val3));

            (merged, _) = merged.Commit();

            var conflict = state1.ChangeValue(val2, 7);

            Assert.ThrowsException<InvalidOperationException>(() => MergeChanges(state1, merged));

            Assert.ThrowsException<InvalidOperationException>(() => MergeChanges(conflict, state3));
        }

        [TestMethod]
        public void TestRebaseChanges()
        {
            var builder = new StateBuilder();
            var val1 = builder.AddInput(new InputNode<int>(), 1);
            var val2 = builder.AddInput(new InputNode<int>(), 2);
            var val3 = builder.AddInput(new InputNode<int>(), 3);
            var result = builder.AddCalculation(new SumNode<int>(new List<AbstractNode<int>> { val1, val2, val3 }));
            var state1 = builder.Build();

            var (state2, _) = state1.ChangeValue(val1, 5).Commit();

            var state3 = state1.ChangeValue(val2, 6);

            var (state4, _) = state2.ChangeValue(val3, 3).Commit();

            var rebased = RebaseChanges(state3, state4);

            Assert.AreEqual(5, rebased.GetValue(val1));
            Assert.AreEqual(6, rebased.GetValue(val2));
            Assert.AreEqual(3, rebased.GetValue(val3));

            var differentStateBuilder = new StateBuilder();
            differentStateBuilder.AddInput(new InputNode<int>(), 1);

            Assert.ThrowsException<InvalidOperationException>(() => RebaseChanges(state4, state3));

            Assert.ThrowsException<InvalidOperationException>(() => RebaseChanges(differentStateBuilder.Build(), state4));
        }
        public static State MergeChanges(State state1, State state2)
        {
            if (state1.VersionId != state2.VersionId)
                throw new InvalidOperationException("States must be at the same version");

            bool areChangesDisjoint = !state1.Changes.Any(change => state2.Changes.Contains(change));
            if (!areChangesDisjoint)
                throw new InvalidOperationException("Changes are not disjoint");

            foreach (var change in state2.Changes)
            {
                if (change is IInputNode inputNode)
                {
                    state1 = state1.ChangeObjectValue(inputNode, state2.GetObjValue(change));
                }
            }

            return state1;
        }

        public static State RebaseChanges(State state, State baseState)
        {
            if (!ReferenceEquals(state.Nodes, baseState.Nodes))
                throw new InvalidOperationException("States must have the same nodes");

            if (!baseState.IsConsistent)
                throw new InvalidOperationException("Base state must not have any uncommitted changes");

            foreach (var change in state.Changes)
            {
                if (change is IInputNode inputNode)
                {
                    baseState = baseState.ChangeObjectValue(inputNode, state.GetObjValue(change));
                }
            }

            return baseState;
        }
    }
}