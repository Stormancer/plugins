using Stormancer.Server.Plugins.Epic;
using Stormancer.Server.Plugins.Users;

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
    }
}
