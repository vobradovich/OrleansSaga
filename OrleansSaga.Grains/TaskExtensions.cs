using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Orleans;

namespace OrleansSaga.Grains
{
    public static class TaskExtensions
    {
        /// <summary>
        /// Observes and ignores a potential exception on a given Task.
        /// If a Task fails and throws an exception which is never observed, it will be caught by the .NET finalizer thread.
        /// This function awaits the given task and if the exception is thrown, it observes this exception and simply ignores it.
        /// This will prevent the escalation of this exception to the .NET finalizer thread.
        /// </summary>
        /// <param name="task">The task to be ignored.</param>
        [SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId = "ignored")]
        public static void Ignore(this Task task)
        {
            if (task.IsCompleted)
            {
                var ignored = task.Exception;
            }
            else
            {
                task.ContinueWith(
                    t => { var ignored = t.Exception; },
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }

        /// <summary>
        /// This will apply a timeout delay to the task, allowing us to exit early
        /// </summary>
        /// <param name="taskToComplete">The task we will timeout after timeSpan</param>
        /// <param name="timeout">Amount of time to wait before timing out</param>
        /// <exception cref="TimeoutException">If we time out we will get this exception</exception>
        /// <returns>The completed task</returns>
        public static async Task WithTimeout(this Task taskToComplete, TimeSpan timeout)
        {
            if (taskToComplete.IsCompleted)
            {
                await taskToComplete;
                return;
            }

            var timeoutCancellationTokenSource = new CancellationTokenSource();
            var completedTask = await Task.WhenAny(taskToComplete, Task.Delay(timeout, timeoutCancellationTokenSource.Token));

            // We got done before the timeout, or were able to complete before this code ran, return the result
            if (taskToComplete == completedTask)
            {
                timeoutCancellationTokenSource.Cancel();
                // Await this so as to propagate the exception correctly
                await taskToComplete;
                return;
            }

            // We did not complete before the timeout, we fire and forget to ensure we observe any exceptions that may occur
            taskToComplete.Ignore();
            throw new TimeoutException(String.Format("WithTimeout has timed out after {0}.", timeout));
        }

        /// <summary>
        /// This will apply a timeout delay to the task, allowing us to exit early
        /// </summary>
        /// <param name="taskToComplete">The task we will timeout after timeSpan</param>
        /// <param name="timeSpan">Amount of time to wait before timing out</param>
        /// <exception cref="TimeoutException">If we time out we will get this exception</exception>
        /// <returns>The value of the completed task</returns>
        public static async Task<T> WithTimeout<T>(this Task<T> taskToComplete, TimeSpan timeSpan)
        {
            if (taskToComplete.IsCompleted)
            {
                return await taskToComplete;
            }

            var timeoutCancellationTokenSource = new CancellationTokenSource();
            var completedTask = await Task.WhenAny(taskToComplete, Task.Delay(timeSpan, timeoutCancellationTokenSource.Token));

            // We got done before the timeout, or were able to complete before this code ran, return the result
            if (taskToComplete == completedTask)
            {
                timeoutCancellationTokenSource.Cancel();
                // Await this so as to propagate the exception correctly
                return await taskToComplete;
            }

            // We did not complete before the timeout, we fire and forget to ensure we observe any exceptions that may occur
            taskToComplete.Ignore();
            throw new TimeoutException(String.Format("WithTimeout has timed out after {0}.", timeSpan));
        }

        public static async Task Retry(this Func<Task> action, int tryCount, IBackoffProvider backoffProvider)
        {
            await Retry(i => action().Box(), tryCount, backoffProvider);
        }

        public static async Task Retry(this Func<int, Task> action, int tryCount, IBackoffProvider backoffProvider)
        {
            await Retry(i => action(i).Box(), tryCount, backoffProvider);
        }

        public static async Task<T> Retry<T>(this Func<Task<T>> action, int tryCount, IBackoffProvider backoffProvider)
        {
            return await Retry(i => action(), tryCount, backoffProvider);
        }

        public static async Task<T> Retry<T>(Func<int, Task<T>> action, int tryCount, IBackoffProvider backoffProvider)
        {
            T result = default(T);
            for (int i = 0; i < tryCount; i++)
            {
                try
                {
                    result = await action(i);
                    return result;
                }
                catch (Exception ex)
                {
                    TimeSpan delay = backoffProvider.Next(i);
                    await Task.Delay(delay);
                }
            }
            return result;
        }

        public static Func<TMessage, Task<TResult>> WithRetries<TMessage, TResult>(this Func<TMessage, Task<TResult>> action, int tryCount = int.MaxValue, IBackoffProvider backoffProvider = null)
        {
            var provider = backoffProvider ?? FixedBackoff.Zero;
            return async m =>
            {
                TResult result = default(TResult);
                for (int i = 0; i < tryCount; i++)
                {
                    try
                    {
                        result = await action(m);
                        return result;
                    }
                    catch (Exception ex) when (i < tryCount - 1)
                    {
                        TimeSpan delay = provider.Next(i);
                        await Task.Delay(delay);
                    }
                }
                return result;
            };
        }

        public static Func<TMessage, Task> WithRetries<TMessage>(this Func<TMessage, Task> action, int tryCount = int.MaxValue, IBackoffProvider backoffProvider = null)
        {
            var provider = backoffProvider ?? FixedBackoff.Zero;
            return async m =>
            {
                for (int i = 0; i < tryCount; i++)
                {
                    try
                    {
                        await action(m);
                        return;
                    }
                    catch (Exception ex) when (i < tryCount - 1)
                    {
                        TimeSpan delay = provider.Next(i);
                        await Task.Delay(delay);
                    }
                }
            };
        }
    }


    // Allow multiple implementations of the backoff algorithm.
    // For instance, ConstantBackoff variation that always waits for a fixed timespan, 
    // or a RateLimitingBackoff that keeps makes sure that some minimum time period occurs between calls to some API 
    // (especially useful if you use the same instance for multiple potentially simultaneous calls to ExecuteWithRetries).
    // Implementations should be imutable.
    // If mutable state is needed, extend the next function to pass the state from the caller.
    // example: TimeSpan Next(int attempt, object state, out object newState);
    public interface IBackoffProvider
    {
        TimeSpan Next(int attempt);
    }

    public class FixedBackoff : IBackoffProvider
    {
        public static FixedBackoff Zero { get; private set; } = new FixedBackoff(TimeSpan.Zero);

        public static FixedBackoff Second { get; private set; } = new FixedBackoff(TimeSpan.FromSeconds(1));

        private readonly TimeSpan fixedDelay;

        public FixedBackoff(TimeSpan delay)
        {
            fixedDelay = delay;
        }

        public TimeSpan Next(int attempt)
        {
            return fixedDelay;
        }
    }

    public class FibonacciBackoff : IBackoffProvider
    {
        private readonly TimeSpan maxDelay;
        private readonly TimeSpan step;

        public FibonacciBackoff(TimeSpan step) : this(step, TimeSpan.MaxValue)
        {

        }

        public FibonacciBackoff(TimeSpan step, TimeSpan maxDelay)
        {
            if (step <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(step), step, "FibonacciBackoff step must be a positive number.");
            if (maxDelay <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maxDelay), maxDelay, "FibonacciBackoff max delay must be a positive number.");

            this.maxDelay = maxDelay;
            this.step = step;
        }

        public TimeSpan Next(int attempt)
        {
            if (attempt < 2)
            {
                return step;
            }
            long a = 1, b = 1, c = 0;
            for (int i = 1; i < attempt; i++)
            {
                c = a + b;
                a = b;
                b = c;
            }
            try
            {
                var delay = TimeSpan.FromMilliseconds(step.TotalMilliseconds * c);
                return delay > maxDelay ? maxDelay : delay;
            }
            catch (OverflowException)
            {
                return maxDelay;
            }
        }
    }

    public class LinearBackoff : IBackoffProvider
    {
        private readonly TimeSpan maxDelay;
        private readonly TimeSpan step;

        public LinearBackoff(TimeSpan step) : this(step, TimeSpan.MaxValue)
        {

        }

        public LinearBackoff(TimeSpan step, TimeSpan maxDelay)
        {
            if (step <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(step), step, "LinearBackoff step must be a positive number.");
            if (maxDelay <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maxDelay), maxDelay, "LinearBackoff max delay must be a positive number.");

            this.maxDelay = maxDelay;
            this.step = step;
        }

        public TimeSpan Next(int attempt)
        {
            try
            {
                var delay = TimeSpan.FromMilliseconds(step.TotalMilliseconds * attempt);
                return delay > maxDelay ? maxDelay : delay;
            }
            catch (OverflowException)
            {
                return maxDelay;
            }
        }
    }
}
