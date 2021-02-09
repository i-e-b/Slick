using System.Collections.Generic;

namespace SlickWindows.Extensions
{
    public static class CollectionExtensions
    {
        public static IEnumerable<T> OrEmpty<T>(this IEnumerable<T>? src)
        {
            return src ?? new T[0];
        }
    }
}