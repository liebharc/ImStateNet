using ImStateNet.Mutable;
using System.Collections.Concurrent;

namespace ImStateNet.Test
{
    [TestClass]
    public class ConcurrencyTest
    {
        [TestMethod]
        public Task StateConcurrencyTest()
        {
            var state = new StateMut();
            return RunConcurrencyTestOnState(state);
        }

        [TestMethod]
        public Task StateWhichUsesCancelledCalculationsConcurrencyTest()
        {
            var state = new StateMut(true);
            return RunConcurrencyTestOnState(state);
        }


        private async Task RunConcurrencyTestOnState(StateMut state)
        {
            var inputs = Enumerable.Range(1, 128).Select(_ => new InputPropertyWithState(state)).ToList();
            using var allNodes = new DisposableList(inputs);
            List<IValueChangeTriggerWithState> lastLevel = new List<IValueChangeTriggerWithState>(inputs);
            List<IValueChangeTriggerWithState> nextLevel = new();
            while (lastLevel.Count > 1)
            {
                foreach (var chunk in lastLevel.Chunk(2))
                {
                    nextLevel.Add(new DelayedSumNode(state, chunk.ToArray()));
                }

                allNodes.AddRange(nextLevel);
                lastLevel = nextLevel;
                nextLevel = new List<IValueChangeTriggerWithState>();
            }

            var lastSum = lastLevel.Single();

            var lastValues = new ConcurrentDictionary<InputPropertyWithState, int>();
            const int threadCount = 64;
            const int iterationsPerThread = 100;
            var tasks = new List<Task>();

            for (int i = 0; i < threadCount; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    var random = new Random();
                    for (int j = 0; j < iterationsPerThread; j++)
                    {
                        SetRandomValue(random, inputs, lastValues);
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());
            await state.WaitForAllPendingCalculationsToFinish();

            var expectedSum = lastValues.Values.Sum();
            Assert.AreEqual(expectedSum, lastSum.Value);
        }

        private void SetRandomValue(Random random, IList<InputPropertyWithState> nodes, ConcurrentDictionary<InputPropertyWithState, int> lastValues)
        {
            var choice = random.Next(nodes.Count);
            var node = nodes[choice];
            var value = random.Next(100);
            node.Value = value;
            lastValues[node] = value;
            var sleepTime = random.Next(10);
            if (sleepTime > 0)
            {
                Thread.Sleep(sleepTime);
            }
        }
    }

    public class DelayedSumNode : SumEventHandlerWithState
    {
        private readonly Random _random = new Random();

        public DelayedSumNode(StateMut state, IValueChangeTriggerWithState[] triggers) : base(state, triggers)
        {
        }
        protected override async Task<int> CalculateSum(IReadOnlyList<int> inputs)
        {
            var sleep = _random.Next(10);
            if (sleep > 0)
            {
                await Task.Delay(sleep);
            }

            return await base.CalculateSum(inputs);
        }
    }

    public sealed class DisposableList : IDisposable
    {
        private List<IDisposable> _disposables = new List<IDisposable>();

        public DisposableList(IEnumerable<IDisposable> disposables)
        {
            _disposables.AddRange(disposables);
        }

        public void Add(IDisposable disposable)
        {
            _disposables.Add(disposable);
        }

        public void AddRange(IEnumerable<IDisposable> disposables)
        {
            _disposables.AddRange(disposables);
        }

        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }
        }
    }
}
