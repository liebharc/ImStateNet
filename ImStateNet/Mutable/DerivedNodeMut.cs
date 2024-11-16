using ImStateNet.Core;

namespace ImStateNet.Mutable
{
    public interface IValueChangedEventHandler
    {
        event EventHandler ValueChanged;
    }

    public interface IDerivedNodeMut : IDisposable, IValueChangedEventHandler
    {
        object Value { get; }
    }

    public interface IDerivedNodeMut<T> : IDerivedNodeMut
    {
        new T Value { get; }
    }

    public abstract class DerivedNodeMut<T> : IDerivedNodeMut<T>
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private StateMut _state;
        private DerivedNode<T> _node;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private bool disposedValue;

        public void Init(StateMut state, DerivedNode<T> node)
        {
            _state = state;
            _node = node;
            _state.RegisterDerived(node);
            _state.OnStateChanged += OnStateChanged;
        }

        public DerivedNode<T> Node => _node;

        private void OnStateChanged(object? sender, ISet<INode> changedNodes)
        {
            if (!changedNodes.Contains(_node))
            {
                return;
            }

            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        public T Value
        {
#pragma warning disable CS8603 // Possible null reference return.
            get => _state.GetValue(_node);
#pragma warning restore CS8603 // Possible null reference return.
        }

        object IDerivedNodeMut.Value
        {
#pragma warning disable CS8603 // Possible null reference return.
            get => Value;
#pragma warning restore CS8603 // Possible null reference return.
        }

        public event EventHandler? ValueChanged;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _state.OnStateChanged -= OnStateChanged;
                    _state.RemoveNodeAndItsDependencies(_node);
                }

                disposedValue = true;
            }
        }

        void IDisposable.Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
