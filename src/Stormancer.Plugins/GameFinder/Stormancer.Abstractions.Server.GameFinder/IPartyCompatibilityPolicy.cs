using Stormancer.Server.Plugins.Models;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Stormancer.Abstractions.Server.GameFinder
{
    /// <summary>
    /// Tests Player parties for compatibility
    /// </summary>
    public interface IPartyCompatibilityPolicy
    {
        /// <summary>
        /// Determines if two parties are compatible ?
        /// </summary>
        /// <param name="party1"></param>
        /// <param name="party2"></param>
        /// <returns></returns>
        Task<CompatibilityTestResult> AreCompatible(Party party1, Party party2, object context);


    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="success"></param>
    /// <param name="error"></param>
    public struct CompatibilityTestResult(bool success, string? error = null)
    {
        /// <summary>
        /// Gets a boolean value indicating if the players are compatible.
        /// </summary>
        [MemberNotNullWhen(true,nameof(Error))]
        public bool AreCompatible { get; } = success;

        /// <summary>
        /// Gets the error id if <see cref="AreCompatible"/> is false.
        /// </summary>
        public string? Error { get; } = error;
    }
}
