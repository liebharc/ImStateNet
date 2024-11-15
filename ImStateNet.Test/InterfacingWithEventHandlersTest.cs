using ImStateNet.Core;
using ImStateNet.Extensions;

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
            sum.Changed += (_, _) => semaphore.Release();
            val1.Value = 2;
            await semaphore.WaitAsync(5000);
            val2.Value = 3;
            await semaphore.WaitAsync(5000);
            Assert.AreEqual(sum.Value, 5);
        }

        [TestMethod]
        public async Task EventsBackedByStateTest()
        {
            using var val1 = new InputPropertyWithState();
            using var val2 = new InputPropertyWithState();
            using var sum = new SumEventHandlerWithState(new IValueChangeTriggerWithState[] { val1, val2 });
            using var semaphore = new SemaphoreSlim(0, 2);
            sum.Changed += (_, _) => semaphore.Release();
            val1.Value = 2;
            await semaphore.WaitAsync(5000);
            val2.Value = 3;
            await semaphore.WaitAsync(5000);
            Assert.AreEqual(sum.Value, 5);
        }

        [TestMethod]
        public async Task EventsBackedByStateInOneCommitTest()
        {
            using var val1 = new InputPropertyWithState();
            using var val2 = new InputPropertyWithState();
            using var sum = new SumEventHandlerWithState(new IValueChangeTriggerWithState[] { val1, val2 });
            using var semaphore = new SemaphoreSlim(0, 1);
            sum.Changed += (_, _) => semaphore.Release();
            await using (var _ = EventHandlerState.GlobalState.DisableAutoCommit())
            {
                val1.Value = 2;
                val2.Value = 3;
            }
            await semaphore.WaitAsync(5000);
            Assert.AreEqual(sum.Value, 5);
        }
    }
}
