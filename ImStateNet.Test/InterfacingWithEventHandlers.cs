using ImStateNet.Core;
using ImStateNet.Extensions;
using ImStateNet.Mutable;

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
        public static StateMut GlobalState { get; } = new StateMut();
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
            _node = new InputNode<int>();
            EventHandlerState.GlobalState.Register(_node);
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
            _node = new LambdaCalcNode<int>(CalculateSum, triggers.Select(t => t.Node).ToList());
            EventHandlerState.GlobalState.Register(_node);
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
