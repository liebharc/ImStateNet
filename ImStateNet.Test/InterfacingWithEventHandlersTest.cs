using ImStateNet.Core;
using ImStateNet.Extensions;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;

namespace ImStateNet.Test
{
    public interface IValueChangeTrigger
    {
        int Value { get; }

        event EventHandler Changed;
    }

    public class InputProperty : IValueChangeTrigger
    {
        private int _value;

        public int Value
        {
            get => _value;
            set
            {
                _value = value;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? Changed;
    }

    public class SumEventHandler : IValueChangeTrigger, IDisposable
    {
        private readonly IValueChangeTrigger[] _triggers;

        public SumEventHandler(IValueChangeTrigger[] triggers)
        {
            _triggers = triggers;

            foreach (var trigger in _triggers)
            {
                trigger.Changed += Trigger_Changed;
            }
        }

        private void Trigger_Changed(object? sender, EventArgs e)
        {
            Task.Run(() =>
            {
                Value = _triggers.Select(x => x.Value).Sum();
                Changed?.Invoke(this, EventArgs.Empty);
            });
        }

        public int Value
        {
            get; private set;
        }

        public void Dispose()
        {

            foreach (var trigger in _triggers)
            {
                trigger.Changed -= Trigger_Changed;
            }
        }

        public event EventHandler? Changed;
    }

    public class EventHandlerState
    {
        public static EventHandlerState GlobalState { get; } = new EventHandlerState();

        private readonly object _lock = new object();
        private State _state;
        private CancellationTokenSource _cancelPreviousCommit = new CancellationTokenSource();

        private int _numberOfAutoCommitSuspenders = 0;

        private EventHandlerState()
        {
            _state = new StateBuilder().Build();
        }

        public InputNode<int> Register(InputPropertyWithState input)
        {
            lock (_lock)
            {
                var node = new InputNode<int>();
                var builder = _state.ChangeConfiguration();
                builder.AddInput(node, 0);
                _state = builder.Build();
                return node;
            }
        }

        public DerivedNode<int> Register(IValueChangeTriggerWithState[] dependencies, Func<int> calculation)
        {
            lock (_lock)
            {
                var node = new LambdaCalcNode<int>((_) => calculation(), dependencies.Select(d => d.Node).ToList());
                var builder = _state.ChangeConfiguration();
                builder.AddCalculation(node);
                _state = builder.Build();
                return node;
            }
        }

        public void RemoveNodeAndItsDependencies(INode node)
        {
            lock (_lock)
            {
                var builder = _state.ChangeConfiguration();
                builder.RemoveNodeAndAllDependencies(node);
                _state = builder.Build();
            }
        }

        public T? GetValue<T>(AbstractNode<T> node)
        {
            return _state.GetValue(node);
        }

        public Task SetValueAsync<T>(InputNode<T> node, T value)
        {
            lock (_lock)
            {
                _state = _state.ChangeValue(node, value);
                if (_numberOfAutoCommitSuspenders > 0)
                {
                    return Task.CompletedTask;
                }

                return CommitAsync();
            }
        }

        private Task CommitAsync()
        {
            _cancelPreviousCommit.Cancel();
            var tokenSource = new CancellationTokenSource();
            _cancelPreviousCommit = tokenSource;
            var token = tokenSource.Token;
            return Task.Run(() =>
            {
                (var stateUpdate, var changes) = _state.Commit(token);
                lock (_lock)
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    _state = stateUpdate;
                }

                OnStateChanged?.Invoke(this, changes);
            });
        }

        public EventHandler<ISet<INode>>? OnStateChanged;

        public IAsyncDisposable DisableAutoCommit()
        {
            return new DisableAutoCommitScope(this);
        }

        private sealed class DisableAutoCommitScope : IAsyncDisposable
        {
            private readonly EventHandlerState _state;

            public DisableAutoCommitScope(EventHandlerState state)
            {
                lock (state._lock)
                {
                    state._numberOfAutoCommitSuspenders++;
                }
                _state = state;
            }

            public ValueTask DisposeAsync()
            {

                lock (_state._lock)
                {
                    _state._numberOfAutoCommitSuspenders--;
                    return new ValueTask(_state.CommitAsync());
                }
            }
        }
    }

    public interface IValueChangeTriggerWithState : IValueChangeTrigger, IDisposable
    {
        INode Node { get; }
    }

    public sealed class InputPropertyWithState : IValueChangeTriggerWithState
    {
        private readonly InputNode<int> _node;

        public InputPropertyWithState()
        {
            _node = EventHandlerState.GlobalState.Register(this);
            EventHandlerState.GlobalState.OnStateChanged += OnStateChanged;
        }

        private void OnStateChanged(object? sender, ISet<INode> e)
        {
            if (e.Contains(_node))
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            EventHandlerState.GlobalState.RemoveNodeAndItsDependencies(_node);
        }

        public int Value
        {
            get => EventHandlerState.GlobalState.GetValue(_node);
            set
            {
                EventHandlerState.GlobalState.SetValueAsync(_node, value);
            }
        }

        public INode Node => _node;

        public event EventHandler? Changed;
    }

    public sealed class SumEventHandlerWithState : IValueChangeTriggerWithState
    {
        private readonly DerivedNode<int> _node;

        public SumEventHandlerWithState(IValueChangeTriggerWithState[] triggers)
        {
            _node = EventHandlerState.GlobalState.Register(triggers, () => triggers.Select(x => x.Value).Sum());
            EventHandlerState.GlobalState.OnStateChanged += OnStateChanged;
        }

        private void OnStateChanged(object? sender, ISet<INode> e)
        {
            if (e.Contains(_node))
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            EventHandlerState.GlobalState.RemoveNodeAndItsDependencies(_node);
        }

        public int Value
        {
            get => EventHandlerState.GlobalState.GetValue(_node);
        }

        public INode Node => throw new NotImplementedException();

        public event EventHandler? Changed;
    }


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
            val2.Value = 3;
            await semaphore.WaitAsync(5000);
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
