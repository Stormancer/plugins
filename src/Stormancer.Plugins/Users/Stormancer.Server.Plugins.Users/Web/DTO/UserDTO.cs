using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users.Web.DTO
{
    public record UserDTO(
        string Id,
        object Auth,
        object UserData,
        DateTime CreatedOn,
        DateTime LastLogin,
        object Channels,
        string? LastPlatform,
        string? Pseudonym,
        Dictionary<string, string> Identities)
    {
        public static UserDTO? FromUser(User? user)
        {
            if (user == null)
            {
                return null;
            }
            else
            {
                return new UserDTO(
                    user.Id,
                    user.Auth.ToObject(),
                    user.UserData.ToObject(),
                    user.CreatedOn,
                    user.LastLogin,
                    user.Channels.ToObject(),
                    user.LastPlatform,
                    user.Pseudonym,
                    user.Identities
                );
            }
        }
    }
}
