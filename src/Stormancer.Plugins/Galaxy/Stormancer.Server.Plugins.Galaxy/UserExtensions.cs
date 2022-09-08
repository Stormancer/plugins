using Stormancer.Server.Plugins.Galaxy;
using Stormancer.Server.Plugins.Users;

namespace Stormancer
{
    /// <summary>
    /// Galaxy user extensions
    /// </summary>
    public static class GalaxyUserExtensions
    {
        /// <summary>
        /// Galaxy user extension to get the Galaxy Id of a stormancer user.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static string? GetGalaxyId(this User user)
        {
            if (user.UserData.ContainsKey(GalaxyConstants.PLATFORM_NAME))
            {
                return user.UserData[GalaxyConstants.PLATFORM_NAME]?[GalaxyConstants.GALAXYID_CLAIMPATH]?.ToString();
            }

            return null;
        }

        /// <summary>
        /// Galaxy user extension to get the Galaxy nickname of a stormancer user.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static string? GetUsername(this User user)
        {
            if (user.UserData.ContainsKey(GalaxyConstants.PLATFORM_NAME))
            {
                return user.UserData[GalaxyConstants.PLATFORM_NAME]?[GalaxyConstants.USERNAME]?.ToString();
            }

            return null;
        }
    }
}
