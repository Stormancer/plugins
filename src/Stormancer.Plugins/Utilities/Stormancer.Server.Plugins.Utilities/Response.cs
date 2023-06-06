using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer
{
    /// <summary>
    /// the result of an operation.
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <typeparam name="TError"></typeparam>
    public struct Result<TResult, TError> 
        where TResult : class 
        where TError : class
    {
        private Result(TResult? value, TError? error)
        {
            Value = value;
            Error = error;
        }

        /// <summary>
        /// Creates an result in the error state.
        /// </summary>
        /// <param name="error"></param>
        /// <returns></returns>
        public static Result<TResult, TError> Failed(TError error) => new Result<TResult, TError>(null, error);


        /// <summary>
        /// Creates an result in the error state.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static Result<TResult, TError> Succeeded(TResult value) => new Result<TResult, TError>(value,null);

        /// <summary>
        /// Gets a value indicating if the operation represented by the <see cref="Result{TResult, TError}"/> was successful.
        /// </summary>
        [MemberNotNullWhen(true,"Value")]
        [MemberNotNullWhen(false,"Error")]
        public bool Success => Value != null;

        /// <summary>
        /// Gets the result of the operation.
        /// </summary>
        public TResult? Value { get; }

        /// <summary>
        /// Gets the error associated with the operation.
        /// </summary>
        public TError? Error { get; }
    }
}
