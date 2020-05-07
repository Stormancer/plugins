using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stormancer.Server.Plugins.Utilities.Extensions
{
    /// <summary>
    /// 
    /// </summary>
    public static class IEnumerableExtensions
    {
        /// <summary>
        /// Filter out null values from a collection of nullables.
        /// </summary>
        /// <typeparam name="T">Type of the IEnumerable</typeparam>
        /// <param name="source">IEnumerable to be filtered</param>
        /// <returns>A copy of <paramref name="source"/> without null values.</returns>
        public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source)
            where T : class
        {
            return source.Where(x => x != null)!;
        }
    }
}
