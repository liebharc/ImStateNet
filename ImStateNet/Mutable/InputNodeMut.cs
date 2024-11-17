using ImStateNet.Core;

namespace ImStateNet.Mutable
{
    public interface IInputNodeMut : IDisposable, IValueChangedEventHandler
    {
        object Value { get; set; }
        Task SetObjectValueAsync(object value);
        IAsyncDisposable DisableAutoCommit();
    }

    public interface IInputNodeMut<T> : IInputNodeMut
    {
        new T Value { get; set; }
        Task SetValueAsync(T value);
    }

    public abstract class InputNodeMut<T> : IInputNodeMut<T>
    {
        private readonly StateMut _state;
        private readonly InputNode<T> _node;
        private bool disposedValue;

        public InputNodeMut(StateMut state, InputNode<T> node)
        {
            _state = state;
            _node = node;
            _state.RegisterInput<T>(node).Wait();
            _state.OnStateChanged += OnStateChanged;
        }

        private void OnStateChanged(object? sender, StateChangedEventArgs args)
        {
            if (!args.Changes.Contains(_node))
            {
                return;
            }

            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        public InputNode<T> Node => _node;

        public T Value
        {
#pragma warning disable CS8603 // Possible null reference return.
            get => _state.GetValue(_node);
#pragma warning restore CS8603 // Possible null reference return.
            set => _state.SetValueAsync(_node, value);
        }

        object IInputNodeMut.Value
        {
#pragma warning disable CS8603 // Possible null reference return.
            get => Value;
#pragma warning restore CS8603 // Possible null reference return.
            set => Value = (T)value;
        }

        public Task SetValueAsync(T value)
        {
            return _state.SetValueAsync(_node, value);
        }

        public Task SetObjectValueAsync(object value)
        {
            return SetValueAsync((T)value);
        }

        public event EventHandler? ValueChanged;

        public IAsyncDisposable DisableAutoCommit()
        {
            return _state.DisableAutoCommit();
        }

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
