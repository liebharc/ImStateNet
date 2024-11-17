using ImStateNet.Core;

namespace ImStateNet.Mutable
{
    /// <summary>
    /// Holds a <see cref="State"/> and provies mutable functions to change it. This makes it easier to
    /// use a state in a regular .NET application.
    /// </summary>
    public class StateMut
    {
        private readonly object _lock = new object();
        private readonly SequentialTaskQueue _commitDispatcher = new SequentialTaskQueue();

        private State _state;

        private int _numberOfAutoCommitSuspenders = 0;

        /// <summary>
        /// Creates a new mutable state. Use <see cref="RegisterDerived(IDerivedNode)"/>
        /// and <see cref="RegisterInput{T}(InputNode{T}, T)"/> to build the network.
        /// </summary>
        /// <param name="continueWithAbortedCalculations">Sets the value for <see cref="ContinueWithAbortedCalculations"/></param>
        public StateMut(bool continueWithAbortedCalculations = false)
        {
            _state = new StateBuilder().Build();
            ContinueWithAbortedCalculations = continueWithAbortedCalculations;
        }

        public bool IsConsistent => _state.IsConsistent;

        public EventHandler<StateChangedEventArgs>? OnStateChanged;

        /// <summary>
        /// The current state. This state will always be consistent (meaning that <see cref="State.Changes"/> is empty) if:
        /// 
        /// - There is no active <see cref="DisableAutoCommit"/>
        /// - <see cref="ContinueWithAbortedCalculations"/> is false and a calculation has been aborted.
        /// </summary>
        public State CurrentState => _state;

        /// <summary>
        /// If a commit happens while another commit is still in progress then the previuos commit will always be cancelled.
        /// If this value is set then the results of the cancelled commit will be used for the next commit - as far as they already had been completed.
        /// Otherwise the last clean commit will be used as basis for the next calculation.
        /// </summary>
        public bool ContinueWithAbortedCalculations { get; init; }

        /// <summary>
        /// Register a single input node.
        /// </summary>
        /// <typeparam name="T">Type of the input node.</typeparam>
        /// <param name="inputNode">The input node.</param>
        /// <param name="initialValue">Initial value.</param>
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

        /// <summary>
        /// Registers a derived node.
        /// </summary>
        /// <param name="derivedNode">Dervied node.</param>
        public void RegisterDerived(IDerivedNode derivedNode)
        {
            lock (_lock)
            {
                var builder = _state.ChangeConfiguration();
                builder.AddCalculation(derivedNode);
                _state = builder.Build();
            }
        }

        /// <summary>
        /// Removes a node. All nodes depending on this note will also be removed.
        /// </summary>
        /// <param name="node">Node to be removed.</param>
        public void RemoveNodeAndItsDependencies(INode node)
        {
            lock (_lock)
            {
                var builder = _state.ChangeConfiguration();
                builder.RemoveNodeAndAllDependents(node);
                _state = builder.Build();
            }
        }

        /// <summary>
        /// Registers multiple nodes at once. This is more efficient then registering multiple individual nodes
        /// as this way the state doesn't need to calculate the intermediate values.
        /// </summary>
        /// <param name="stateBuilder">Alter the state builder to change the configuration.</param>
        public void RegisterNodes(Action<StateBuilder> stateBuilder)
        {
            lock (_lock)
            {
                var builder = _state.ChangeConfiguration();
                stateBuilder(builder);
                _state = builder.Build();
            }
        }

        public T? GetValue<T>(AbstractNode<T> node)
        {
            return _state.GetValue(node);
        }

        /// <summary>
        /// Sets a value and returns the state after this operation. 
        /// 
        /// If you need the results of the changes consider setting allowCancellation to false and alwaysCommit to true.
        /// This way you can ensure that the state update will neither be skipped nor aborted.
        /// </summary>
        /// <typeparam name="T">The type of the node</typeparam>
        /// <param name="node">The node to change</param>
        /// <param name="value">The new value</param>
        /// <param name="allowCancellation">Indicates whether this calculation can be cancelled by the following calculation.</param>
        /// <param name="alwaysCommit">Forces a commit even if <see cref="DisableAutoCommit"/> disabled auto commits.</param>
        /// <returns></returns>
        public Task<State> SetValueAsync<T>(InputNode<T> node, T value, bool allowCancellation = true, bool alwaysCommit = false)
        {
            lock (_lock)
            {
                _state = _state.ChangeValue(node, value);
                if (!alwaysCommit && _numberOfAutoCommitSuspenders > 0)
                {
                    return Task.FromResult(_state);
                }

                return CommitAsync(allowCancellation);
            }
        }

        /// <summary>
        /// Waits until all calculations which are pending until now have been completed.
        /// Note that if more calculations get queued after the method was called then they
        /// will still be executed after this method returns.
        /// </summary>
        /// <returns></returns>
        public Task WaitForAllPendingCalculationsToFinish()
        {
            return _commitDispatcher.WaitUntilAllTasksUntilNowHaveBeenCompleted();
        }

        private Task<State> CommitAsync(bool allowCancellation = true)
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
                    if (!ContinueWithAbortedCalculations)
                    {
                        // This update got discarded by a cancellation
                        return stateUpdate;
                    }

                    stateUpdate = AddCurrentChangesToStateUpdate(stateUpdate);
                }

                _state = stateUpdate;
            }

            if (changes.Any())
            {
                OnStateChanged?.Invoke(this, new StateChangedEventArgs(changes, stateUpdate));
            }

            return stateUpdate;
        }

        /// <summary>
        /// Adds all changes which arrived meanwhile on <see cref="_state"/> to the state update.
        /// </summary>
        /// <param name="stateUpdate">A state update</param>
        /// <returns>The state update with the new changes applied</returns>
        private State AddCurrentChangesToStateUpdate(State stateUpdate)
        {
            foreach (var change in _state.Changes.OfType<IInputNode>())
            {
                stateUpdate = stateUpdate.ChangeObjectValue(change, _state.GetObjValue(change));
            }

            return stateUpdate;
        }

        /// <summary>
        /// Disables auto commit until disposed.
        /// </summary>
        /// <returns>Dispose this to enable auto commits again.</returns>
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
