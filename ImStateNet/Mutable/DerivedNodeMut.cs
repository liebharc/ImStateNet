using ImStateNet.Core;

namespace ImStateNet.Mutable
{
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
        private readonly StateMut _state;
        private readonly InputNode<T> _node;
        private bool disposedValue;

        public DerivedNodeMut(StateMut state, InputNode<T> node)
        {
            _state = state;
            _node = node;
            _state.OnStateChanged += OnStateChanged;
        }

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
