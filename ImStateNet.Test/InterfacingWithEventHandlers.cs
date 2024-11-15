using ImStateNet.Core;
using ImStateNet.Extensions;

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
        /// <summary>
        /// The state doesn't need to be global. We do this in this example
        /// to show that the interface can be the same (e.g. <see cref="InputProperty"/> and <see cref="InputPropertyWithState"/>).
        /// </summary>
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

        public DerivedNode<int> Register(IValueChangeTriggerWithState[] dependencies, Func<IList<int>, int> calculation)
        {
            lock (_lock)
            {
                var node = new LambdaCalcNode<int>((v) => calculation(v), dependencies.Select(d => d.Node).ToList());
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

        public bool IsConsistent => _state.IsConsistent;

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

    public class SumEventHandlerWithState : IValueChangeTriggerWithState
    {
        private readonly DerivedNode<int> _node;

        public SumEventHandlerWithState(IValueChangeTriggerWithState[] triggers)
        {
            _node = EventHandlerState.GlobalState.Register(triggers, CalculateSum);
            EventHandlerState.GlobalState.OnStateChanged += OnStateChanged;
        }

        protected virtual int CalculateSum(IList<int> inputs)
        {
            // We can't use triggers here as they only provide the last
            // committed changes, but here we need to provide a result for an ongoing
            // calculation
            var sum = inputs.Sum();
            return sum;
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

        public INode Node => _node;

        public event EventHandler? Changed;
    }
}
