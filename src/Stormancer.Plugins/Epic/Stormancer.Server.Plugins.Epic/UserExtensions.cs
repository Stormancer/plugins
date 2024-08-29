using Stormancer.Server.Plugins.Epic;
using Stormancer.Server.Plugins.Users;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Stormancer
{
    /// <summary>
    /// Epic user extensions
    /// </summary>
    public static class EpicUserExtensions
    {
        /// <summary>
        /// Epic user extension to get the Epic Nsa Id of a stormancer user.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static string? GetAccountId(this User user)
        {
            if (user.UserData.ContainsKey(EpicConstants.PLATFORM_NAME))
            {
                return user.UserData[EpicConstants.PLATFORM_NAME]?[EpicConstants.ACCOUNTID_CLAIMPATH]?.ToString();
            }

            return null;
        }

        /// <summary>
        /// Try getting the Epic store account id.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="accountId"></param>
        /// <returns></returns>
        public static bool TryGetEpicAccountId(this User user,[NotNullWhen(true)] out string? accountId)
        {
            accountId = null;
            if (user.UserData.ContainsKey(EpicConstants.PLATFORM_NAME))
            {
                accountId = user.UserData[EpicConstants.PLATFORM_NAME]?[EpicConstants.ACCOUNTID_CLAIMPATH]?.ToString();
            }

            return accountId != null;

        }

        /// <summary>
        /// Epic user extension to get the Epic nickname of a stormancer user.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static string? GetDisplayName(this User user)
        {
            if (user.UserData.ContainsKey(EpicConstants.PLATFORM_NAME))
            {
                return user.UserData[EpicConstants.PLATFORM_NAME]?[EpicConstants.DISPLAYNAME]?.ToString();
            }

            return null;
        }

        /// <summary>
        /// GetAccessToken
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public static string? GetAccessToken(this Session session)
        {
            var epicAccessTokenData = session.SessionData["EpicAccessToken"];
            return epicAccessTokenData != null ? Encoding.UTF8.GetString(epicAccessTokenData) : null;
        }
    }
}
