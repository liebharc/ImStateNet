namespace ImStateNet.Mutable
{
    public class ChainTasksQueue
    {
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly object _lock = new();
        private Task _lastTask = Task.CompletedTask;

        /// <summary>
        /// Adds task to the queue.
        /// </summary>
        /// <typeparam name="T">Result type of the task</typeparam>
        /// <param name="task">The task to be executed.</param>
        /// <param name="cancellationTokenSource">A cancellation token for the task. If a task is queued, then the previous tokens are cancelled.</param>
        /// <returns>The async task</returns>
        public Task<T> EnqueueTask<T>(Func<Task<T>> task, CancellationTokenSource cancellationTokenSource)
        {
            lock (_lock)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource = cancellationTokenSource;
                var chainedTask = _lastTask.ContinueWith(async _ =>
                {
                    var result = await task();
                    return result;
                }).Unwrap();
                _lastTask = chainedTask;
                return chainedTask;
            }
        }
        public Task WaitUntilAllTasksUntilNowHaveBeenCompleted()
        {
            lock (_lock)
            {
                var chainedTask = _lastTask.ContinueWith(async _ =>
                {
                    // Nothing to do here, just wait until its your turn
                }).Unwrap();
                _lastTask = chainedTask;
                return chainedTask;
            }
        }
    }
}
