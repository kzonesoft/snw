using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kzone.Signal
{
    public static class TaskExtensions
    {

        public static async Task WaitHandle(TimeSpan delay, CancellationToken cancellationToken)
        {
            var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
#if NET40
            var task = new TaskCompletionSource<int>();
#else
            var task = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
#endif
            using (tokenSource.Token.Register(() => task.TrySetResult(0)))
            {
#if NET40
                await TaskEx.WhenAny(
                TaskEx.Delay(delay, CancellationToken.None),
                task.Task);

#else
                await Task.WhenAny(
                        Task.Delay(delay, CancellationToken.None),
                        task.Task);
#endif
            }
        }

        public static bool IsRunning(this Task task)
        {
            return task != null && !task.IsCompleted && !task.IsCanceled && !task.IsFaulted;
        }



        public static void StopIfRunning(this Task task, CancellationTokenSource cts, int timeoutMilliseconds = 10000)
        {
            if (!task.IsRunning()) return;

            cts?.Cancel();

            try
            {
                // Chờ Task hoàn thành hoặc hết thời gian chờ
                bool completedInTime = task.Wait(timeoutMilliseconds);
                if (!completedInTime)
                {
                    throw new TimeoutException("Task did not complete within the allotted timeout.");
                }
            }
            finally
            {
                cts?.Dispose();
            }
        }

        public static async Task StopIfRunningAsync(this Task task, CancellationTokenSource cts, int timeoutMilliseconds = 10000)
        {
            if (!task.IsRunning()) return;

            cts?.Cancel();

            try
            {
                var delayTask =
#if NET40
                    TaskEx.Delay
#else
                    Task.Delay
#endif
                    (timeoutMilliseconds);
                var completedTask = await
#if NET40
                    TaskEx.WhenAny
#else
                    Task.WhenAny
#endif
                    (task, delayTask);

                if (completedTask == delayTask && !task.IsCompleted)
                {
                    throw new TimeoutException("Task did not complete within the allotted timeout.");
                }
            }
            finally
            {
                cts?.Dispose();
            }
        }
    }
}
