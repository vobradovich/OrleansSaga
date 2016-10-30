using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrleansSaga.Grains
{
    /// <summary>
    /// Task Functional Extensions based on Result Extensions
    /// https://github.com/vkhorikov/CSharpFunctionalExtensions/blob/master/CSharpFunctionalExtensions/ResultExtensions.cs
    /// </summary>
    public static class TaskResultExtensions
    {
        public static Task<K> OnSuccess<T, K>(this Task<T> task, Func<T, K> func)
        {
            return task.ContinueWith(result =>
            {
                if (result.IsFaulted)
                    return Task.FromException<K>(result.Exception);

                return Task.FromResult<K>(func(result.Result));
            }).Unwrap();
        }

        public static Task<T> OnSuccess<T>(this Task task, Func<T> func)
        {
            return task.ContinueWith(result =>
            {

                if (result.IsFaulted)
                    return Task.FromException<T>(result.Exception);

                return Task.FromResult(func());
            }).Unwrap();
        }

        public static Task<K> OnSuccess<T, K>(this Task<T> task, Func<T, Task<K>> func)
        {
            return task.ContinueWith(result =>
            {
                if (result.IsFaulted)
                    return Task.FromException<K>(result.Exception);

                return func(result.Result);
            }).Unwrap();
        }

        public static Task<T> OnSuccess<T>(this Task task, Func<Task<T>> func)
        {
            return task.ContinueWith(result =>
            {
                if (result.IsFaulted)
                    return Task.FromException<T>(result.Exception);

                return func();
            }).Unwrap();
        }

        public static Task<K> OnSuccess<T, K>(this Task<T> task, Func<Task<K>> func)
        {
            return task.ContinueWith(result =>
            {

                if (result.IsFaulted)
                    return Task.FromException<K>(result.Exception);

                return func();
            }).Unwrap();
        }

        public static Task OnSuccess<T>(this Task<T> task, Func<T, Task> func)
        {
            return task.ContinueWith(result =>
            {
                if (result.IsFaulted)
                    return Task.FromException(result.Exception);

                return func(result.Result);
            }).Unwrap();
        }

        public static Task OnSuccess(this Task task, Func<Task> func)
        {
            return task.ContinueWith(result =>
            {
                if (result.IsFaulted)
                    return result;

                return func();
            }).Unwrap();
        }

        public static Task<T> Ensure<T>(this Task<T> task, Func<T, bool> predicate, Exception exception)
        {
            return task.ContinueWith(result =>
            {
                if (result.IsFaulted)
                    return Task.FromException<T>(result.Exception);

                if (!predicate(result.Result))
                    return Task.FromException<T>(exception);

                return Task.FromResult(result.Result);
            }).Unwrap();
        }

        public static Task Ensure(this Task task, Func<bool> predicate, Exception exception)
        {
            return task.ContinueWith(result =>
            {
                if (result.IsFaulted)
                    return Task.FromException(result.Exception);

                if (!predicate())
                    return Task.FromException(exception);

                return result;
            }).Unwrap();
        }

        public static Task<K> Map<T, K>(this Task<T> task, Func<T, K> func)
        {
            return task.ContinueWith(result =>
            {
                if (result.IsFaulted)
                    return Task.FromException<K>(result.Exception);

                return Task.FromResult(func(result.Result));
            }).Unwrap();
        }

        public static Task<T> Map<T>(this Task task, Func<T> func)
        {
            return task.ContinueWith(result =>
            {
                if (result.IsFaulted)
                    return Task.FromException<T>(result.Exception);

                return Task.FromResult(func());
            }).Unwrap();
        }

        public static Task<T> OnSuccess<T>(this Task<T> task, Action<T> action)
        {
            return task.ContinueWith(result =>
            {
                if (result.IsCompleted && !task.IsFaulted)
                {
                    action(result.Result);
                }
                return result;
            }).Unwrap();
        }

        public static Task OnSuccess(this Task task, Action action)
        {
            return task.ContinueWith(result =>
            {
                if (result.IsCompleted && !task.IsFaulted)
                {
                    action();
                }
                return result;
            }).Unwrap();
        }

        public static Task<T> OnBoth<T>(this Task task, Func<Task, T> func)
        {
            return task.ContinueWith(result =>
            {
                return Task.FromResult(func(result));
            }).Unwrap();
        }

        public static Task<K> OnBoth<T, K>(this Task<T> task, Func<Task<T>, K> func)
        {
            return task.ContinueWith(result =>
            {
                return Task.FromResult(func(result));
            }).Unwrap();
        }

        public static Task<T> OnFailure<T>(this Task<T> task, Action action)
        {
            return task.ContinueWith(result =>
            {
                if (result.IsFaulted)
                {
                    action();
                }
                return result;
            }).Unwrap();
        }

        public static Task OnFailure(this Task task, Action action)
        {
            return task.ContinueWith(result =>
            {
                if (result.IsFaulted)
                {
                    action();
                }
                return result;
            }).Unwrap();
        }

        public static Task<T> OnFailure<T>(this Task<T> task, Action<Exception> action)
        {
            return task.ContinueWith(result =>
            {
                if (result.IsFaulted)
                {
                    action(result.Exception);
                }
                return result;
            }).Unwrap();
        }
    }
}
