using ImStateNet.Core;

namespace ImStateNet.Mutable
{
    public class StateMut
    {
        private readonly object _lock = new object();
        private readonly TaskDispatcher _commitDispatcher = new TaskDispatcher();

        private readonly bool _continueWithAbortedCalculations;
        private State _state;

        private int _numberOfAutoCommitSuspenders = 0;

        /// <summary>
        /// Creates a new mutable state. Use <see cref="RegisterDerived(IDerivedNode)"/>
        /// and <see cref="RegisterInput{T}(InputNode{T}, T)"/> to build the network.
        /// </summary>
        /// <param name="continueWithAbortedCalculations">
        /// If a commit happens while another commit is still in progress then the previuos commit will always be cancelled.
        /// If this value is set then the results of the cancelled commit will be used for the next commit - as far as they already had been completed.
        /// Otherwise the last clean commit will be used as basis for the next calculation.
        /// </param>
        public StateMut(bool continueWithAbortedCalculations = false)
        {
            _state = new StateBuilder().Build();
            _continueWithAbortedCalculations = continueWithAbortedCalculations;
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

        public Task<State> SetValueAsync<T>(InputNode<T> node, T value, bool allowCancellation = true)
        {
            lock (_lock)
            {
                _state = _state.ChangeValue(node, value);
                if (_numberOfAutoCommitSuspenders > 0)
                {
                    return Task.FromResult(_state);
                }

                return CommitAsync(allowCancellation);
            }
        }

        public Task<State> CommitAsync(bool allowCancellation = true)
        {
            lock (_lock)
            {
                var tokenSource = new CancellationTokenSource();
                var token = tokenSource.Token;
                return _commitDispatcher.EnqueueTask(() => UpdateState(token, allowCancellation), tokenSource);
            }
        }

        private State UpdateState(CancellationToken token, bool allowCancellation)
        {
            (var stateUpdate, var changes) = _state.Commit(allowCancellation ? token : null);
            lock (_lock)
            {
                if (token.IsCancellationRequested)
                {
                    if (!_continueWithAbortedCalculations)
                    {
                        return stateUpdate;
                    }

                    foreach (var change in _state.Changes.OfType<IInputNode>())
                    {
                        stateUpdate = stateUpdate.ChangeObjectValue(change, _state.GetObjValue(change));
                    }
                }
                else
                {
                    _state = stateUpdate;
                }
            }

            if (!token.IsCancellationRequested)
            {
                OnStateChanged?.Invoke(this, changes);
            }

            return stateUpdate;
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
