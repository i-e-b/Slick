using System;
using Windows.Foundation;
using JetBrains.Annotations;

namespace SlickUWP.CrossCutting
{
    public static class AsyncOperationExtension {
        [NotNull]public static IAsyncOperation<T> NotNull<T>([CanBeNull]this IAsyncOperation<T> op) {
            if (op == null) throw new Exception("Async operation container was null");
            return op;
        }
        
        [NotNull]public static IAsyncAction NotNull([CanBeNull]this IAsyncAction op) {
            if (op == null) throw new Exception("Async action container was null");
            return op;
        }
    }
}