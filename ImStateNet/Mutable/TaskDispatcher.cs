using System.Threading.Tasks.Dataflow;

namespace ImStateNet.Mutable
{
    public class TaskDispatcher
    {
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly ActionBlock<Func<Task>> _actionBlock;

        public TaskDispatcher()
        {
            _actionBlock = new ActionBlock<Func<Task>>(async func =>
            {
                await func(); // Execute the provided function.
            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1 // Ensure only one task runs at a time.
            });
        }

        public Task<T> EnqueueTask<T>(Func<T> task, CancellationTokenSource cancellationTokenSource)
        {
            lock (_actionBlock)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource = cancellationTokenSource;
                var tcs = new TaskCompletionSource<T>();

                _actionBlock.Post(async () =>
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
                });

                return tcs.Task; // Return the Task representing the result.
            }
        }
    }
}