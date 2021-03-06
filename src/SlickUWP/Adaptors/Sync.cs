﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using JetBrains.Annotations;

namespace SlickUWP.Adaptors
{
    /// <summary>
    /// Helper class to properly wait for async tasks
    /// </summary>
    public static class Sync  
    {
        [NotNull]private static readonly TaskFactory _taskFactory = new
            TaskFactory(CancellationToken.None,
                TaskCreationOptions.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);



        /// <summary>
        /// Run an async function synchronously and return the result
        /// </summary>
        public static TResult Run<TResult>(Func<IAsyncOperation<TResult>> func)
        {
            return _taskFactory.StartNew(() => func().AsTask()).Unwrap().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Run an async function synchronously and return the result
        /// </summary>
        public static TResult Run<TResult>(Func<Task<TResult>> func) => _taskFactory.StartNew(func).Unwrap().GetAwaiter().GetResult();

        /// <summary>
        /// Run an async function synchronously
        /// </summary>
        public static void Run(Func<Task> func) => _taskFactory.StartNew(func).Unwrap().GetAwaiter().GetResult();

        
        /// <summary>
        /// Run an async function synchronously and return the result
        /// </summary>
        public static void Run(Func<IAsyncAction> func)
        {
            _taskFactory.StartNew(() => func().AsTask()).Unwrap().GetAwaiter().GetResult();
        }
    }
}