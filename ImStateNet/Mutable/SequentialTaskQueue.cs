using System.Threading.Tasks.Dataflow;

namespace ImStateNet.Mutable
{
    /// <summary>
    /// Dispatches all calculations to another thread. Tasks 
    /// are executed one after the other.
    /// </summary>
    public class SequentialTaskQueue
    {
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly ActionBlock<Func<Task>> _actionBlock;

        public SequentialTaskQueue()
        {
            _actionBlock = new ActionBlock<Func<Task>>(async func =>
            {
                await func(); // Execute the provided function.
            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1 // Ensure only one task runs at a time.
            });
        }

        /// <summary>
        /// Adds task to the queue.
        /// </summary>
        /// <typeparam name="T">Result type of the task</typeparam>
        /// <param name="task">The task to be executed.</param>
        /// <param name="cancellationTokenSource">A cancellation token for the task. If a task is queued, then the previous tokens are cancelled.</param>
        /// <returns>The async task</returns>
        public Task<T> EnqueueTask<T>(Func<T> task, CancellationTokenSource cancellationTokenSource)
        {
            lock (_actionBlock)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource = cancellationTokenSource;
                var tcs = new TaskCompletionSource<T>();

                _actionBlock.Post(() =>
                {
                    try
                    {
                        var result = task();
                        tcs.SetResult(result);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }

                    return Task.CompletedTask;
                });

                return tcs.Task; // Return the Task representing the result.
            }
        }
        public Task WaitUntilAllTasksUntilNowHaveBeenCompleted()
        {
            lock (_actionBlock)
            {
                var tcs = new TaskCompletionSource();

                _actionBlock.Post(() =>
                {
                    try
                    {
                        tcs.SetResult();
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }

                    return Task.CompletedTask;
                });

                return tcs.Task; 
            }
        }
    }
}