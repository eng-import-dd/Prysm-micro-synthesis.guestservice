using System;
using System.Collections.Generic;

namespace Synthesis.GuestService.Modules.Test.Extensions
{
    public static class IEnumerablExtensions
    {
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var item in source)
                action(item);
        }
    }
}
