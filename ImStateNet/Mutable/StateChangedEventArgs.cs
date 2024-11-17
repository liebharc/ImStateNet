using ImStateNet.Core;

namespace ImStateNet.Mutable
{
    /// <summary>
    /// This event is raised if the <see cref="StateMut"/> has changed.
    /// </summary>
    public sealed class StateChangedEventArgs
    {
        public StateChangedEventArgs(ISet<INode> changes, State state)
        {
            Changes = changes;
            State = state;
        }

        /// <summary>
        /// Gets all nodes which have been changed since the last event. Consider using <see cref="State"/> to
        /// get the state at this point as <see cref="StateMut"/> might already has received another update.
        /// </summary>
        public ISet<INode> Changes { get; init; }

        /// <summary>
        /// The state after this update. This state will always be consistent (meaning that <see cref="State.Changes"/> is empty) if:
        /// 
        /// <see cref="StateMut.ContinueWithAbortedCalculations"/> is false and this calculation was aborted.
        /// </summary>
        public State State { get; init; }
    }
}
