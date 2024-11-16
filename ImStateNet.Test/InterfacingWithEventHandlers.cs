using ImStateNet.Core;
using ImStateNet.Extensions;
using ImStateNet.Mutable;
using System.Xml.Linq;

namespace ImStateNet.Test
{
    /// <summary>
    /// This example shows how we mimic this interface.
    /// </summary>
    public interface IValueChangeTrigger
    {
        int Value { get; }

        event EventHandler ValueChanged;
    }

    /// <summary>
    /// Example class of an input property without state, this serves for comparison.
    /// </summary>
    public class InputProperty : IValueChangeTrigger
    {
        private int _value;

        public int Value
        {
            get => _value;
            set
            {
                _value = value;
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? ValueChanged;
    }

    /// <summary>
    /// Example class of a derivced property without state, this serves for comparison.
    /// </summary>
    public class SumEventHandler : IValueChangeTrigger, IDisposable
    {
        private readonly IValueChangeTrigger[] _triggers;

        public SumEventHandler(IValueChangeTrigger[] triggers)
        {
            _triggers = triggers;

            foreach (var trigger in _triggers)
            {
                trigger.ValueChanged += Trigger_Changed;
            }
        }

        private void Trigger_Changed(object? sender, EventArgs e)
        {
            Task.Run(() =>
            {
                Value = _triggers.Select(x => x.Value).Sum();
                ValueChanged?.Invoke(this, EventArgs.Empty);
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
                trigger.ValueChanged -= Trigger_Changed;
            }
        }

        public event EventHandler? ValueChanged;
    }

    public interface IValueChangeTriggerWithState : IValueChangeTrigger, IDisposable
    {
        AbstractNode<int> Node { get; }
    }

    public sealed class InputPropertyWithState : InputNodeMut<int>, IValueChangeTriggerWithState
    {
        public InputPropertyWithState(StateMut state) : base(state, new InputNode<int>())
        {
        }

        AbstractNode<int> IValueChangeTriggerWithState.Node => Node;
    }

    public class SumEventHandlerWithState : DerivedNodeMut<int>, IValueChangeTriggerWithState
    {
        public SumEventHandlerWithState(StateMut state, IValueChangeTriggerWithState[] triggers)
        {
            Init(state, new LambdaCalcNode<int>(CalculateSum, triggers.Select(t => t.Node).ToList()));
        }

        protected virtual int CalculateSum(IReadOnlyList<int> inputs)
        {
            // We can't use triggers here as they only provide the last
            // committed changes, but here we need to provide a result for an ongoing
            // calculation
            var sum = inputs.Sum();
            return sum;
        }

        AbstractNode<int> IValueChangeTriggerWithState.Node => Node;
    }

    public sealed class FloatInputPropertyWithState : InputNodeMut<float>
    {
        public FloatInputPropertyWithState(StateMut state) : base(state, new InputNode<float>())
        {
        }
    }

    /// <summary>
    /// Example of a node which takes parameters of different types.
    /// </summary>
    public class AddFloatWithIntNode: DerivedNodeMut<int>, IValueChangeTriggerWithState
    {
        public AddFloatWithIntNode(StateMut state, AbstractNode<float> floatNode, AbstractNode<int> intNode)
        {
            Init(state, LambdaCalcNode.Create(floatNode, intNode, (a, b) => (int)(a + b)));
        }

        AbstractNode<int> IValueChangeTriggerWithState.Node => Node;
    }
}
