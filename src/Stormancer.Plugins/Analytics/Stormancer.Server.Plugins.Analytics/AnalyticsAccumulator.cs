using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Analytics
{
    /// <summary>
    /// Computes aggregated metrics over a number of previous values.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TResult"></typeparam>
    /// <remarks>
    /// The class is not thread safe.
    /// </remarks>
    public class AnalyticsAccumulator<T,TResult>
    {
        /// <summary>
        /// Function computing the result of the current accumulation.
        /// </summary>
        /// <param name="span"></param>
        /// <returns></returns>
        public delegate TResult ComputeDelegate(ReadOnlySpan<T> span);

        /// <summary>
        /// Creates a new <see cref="AnalyticsAccumulator{T, TResult}"/>
        /// </summary>
        /// <param name="capacity"></param>
        /// <param name="compute"></param>
        public AnalyticsAccumulator(int capacity,ComputeDelegate compute)
        {
            _values = new T[capacity];
            _compute = compute;
        }

        /// <summary>
        /// Adds a new value.
        /// </summary>
        /// <param name="value"></param>
        public void Add(T value)
        {
            _values[_offset] = value;
            _offset = (_offset + 1) % _values.Length;
            
            if(_count < _values.Length)
            {
                _count = _count + 1;
            }
        }

        /// <summary>
        /// Gets the result associated with the accumulated values.
        /// </summary>
        public TResult Result=>_compute(_offset == 0 ? _values.AsSpan().Slice(0,_count) : _values.AsSpan());


        private T[] _values;
        private int _offset = 0;
        private int _count = 0;
        private readonly ComputeDelegate _compute;
    }
}
