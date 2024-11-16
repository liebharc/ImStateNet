using ImStateNet.Core;
using ImStateNet.Extensions;
using ImStateNet.Mutable;

namespace ImStateNet.Test
{
    [TestClass]
    public class InterfacingWithEventHandlersTest
    {
        /// <summary>
        /// This test is an example of a network which is purely based on event handlers.
        /// </summary>
        [TestMethod]
        public async Task EventsTest()
        {
            var val1 = new InputProperty();
            var val2 = new InputProperty();
            using var sum = new SumEventHandler(new IValueChangeTrigger[] { val1, val2 });
            using var semaphore = new SemaphoreSlim(0, 2);
            sum.ValueChanged += (_, _) => semaphore.Release();
            val1.Value = 2;
            await semaphore.WaitAsync(5000);
            val2.Value = 3;
            await semaphore.WaitAsync(5000);
            Assert.AreEqual(sum.Value, 5);
        }

        [TestMethod]
        public async Task EventsBackedByStateTest()
        {
            var state = new StateMut();
            using var val1 = new InputPropertyWithState(state);
            using var val2 = new InputPropertyWithState(state);
            using var sum = new SumEventHandlerWithState(state, new IValueChangeTriggerWithState[] { val1, val2 });
            using var semaphore = new SemaphoreSlim(0, 2);
            sum.ValueChanged += (_, _) => semaphore.Release();
            val1.Value = 2;
            await semaphore.WaitAsync(5000);
            val2.Value = 3;
            await semaphore.WaitAsync(5000);
            Assert.AreEqual(sum.Value, 5);
        }

        [TestMethod]
        public async Task EventsBackedByStateInOneCommitTest()
        {
            var state = new StateMut();
            using var val1 = new InputPropertyWithState(state);
            using var val2 = new InputPropertyWithState(state);
            using var sum = new SumEventHandlerWithState(state, new IValueChangeTriggerWithState[] { val1, val2 });
            using var semaphore = new SemaphoreSlim(0, 1);
            sum.ValueChanged += (_, _) => semaphore.Release();
            await using (var _ = state.DisableAutoCommit())
            {
                val1.Value = 2;
                val2.Value = 3;
            }
            await semaphore.WaitAsync(5000);
            Assert.AreEqual(sum.Value, 5);
        }

        [TestMethod]
        public async Task WithFloatingValue()
        {
            var state = new StateMut();
            using var val1 = new InputPropertyWithState(state);
            using var val2 = new InputPropertyWithState(state);
            using var intermediateSum = new SumEventHandlerWithState(state, new IValueChangeTriggerWithState[] { val1, val2 });
            using var val3 = new FloatInputPropertyWithState(state);
            using var sum = new AddFloatWithIntNode(state, val3.Node, intermediateSum.Node);
            await val3.SetValueAsync((float)5.5);
            await val2.SetValueAsync(2);
            await val1.SetValueAsync(1);
            Assert.AreEqual(sum.Value, 8);
        }
    }
}
