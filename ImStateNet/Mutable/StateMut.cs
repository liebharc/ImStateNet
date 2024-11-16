using ImStateNet.Core;

namespace ImStateNet.Mutable
{
    public class StateMut
    {
        private readonly object _lock = new object();
        private State _state;
        private CancellationTokenSource _cancelPreviousCommit = new CancellationTokenSource();

        private int _numberOfAutoCommitSuspenders = 0;

        public StateMut()
        {
            _state = new StateBuilder().Build();
        }

#pragma warning disable CS8601 // Possible null reference assignment.
        public void RegisterInput<T>(InputNode<T> inputNode, T initialValue = default)
#pragma warning restore CS8601 // Possible null reference assignment.
        {
            lock (_lock)
            {
                var builder = _state.ChangeConfiguration();
                builder.AddInput(inputNode, initialValue);
                _state = builder.Build();
            }
        }

        public void RegisterDerived(IDerivedNode derivedNode)
        {
            lock (_lock)
            {
                var builder = _state.ChangeConfiguration();
                builder.AddCalculation(derivedNode);
                _state = builder.Build();
            }
        }

        public void RemoveNodeAndItsDependencies(INode node)
        {
            lock (_lock)
            {
                var builder = _state.ChangeConfiguration();
                builder.RemoveNodeAndAllDependents(node);
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
            private readonly StateMut _state;

            public DisableAutoCommitScope(StateMut state)
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
}
