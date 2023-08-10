using Microsoft.EntityFrameworkCore;
using Nest;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users
{

    /// <summary>
    /// User record in the database.
    /// </summary>
    public class UserRecord
    {
        public static User CreateUserFromRecord(UserRecord record)
        {
            return new User()
            {
                Id = record.Id.ToString(),
                Auth = JObject.Parse(record.Auth),
                Channels = JObject.Parse(record.Channels),
                Pseudonym = record.Pseudonym,
                UserData = JObject.Parse(record.UserData),
                CreatedOn = record.CreatedOn,
                LastLogin = record.LastLogin,
                LastPlatform = record.LastPlatform

            };
        }
        public static UserRecord CreateRecordFromUser(User user)
        {
            return new UserRecord()
            {
                Auth = user.Auth.ToString(),
                Channels = user.Channels.ToString(),
                CreatedOn = user.CreatedOn,
                Id = Guid.Parse(user.Id),
                UserData = user.UserData.ToString(),
                LastPlatform = user.LastPlatform,
                Pseudonym = user.Pseudonym


            };
        }
        /// <summary>
        /// Gets or sets the id of the user.
        /// </summary>
        [Key]
        public Guid Id { get; set; }


        /// <summary>
        /// Gets or sets the auth informations of the user.
        /// </summary>
        [Column(TypeName = "jsonb")]
        public string Auth { get; set; } = default!;

        /// <summary>
        /// Gets or sets custom data about the user.
        /// </summary>
        [Column(TypeName = "jsonb")]
        public string UserData { get; set; } = default!;

        /// <summary>
        /// Gets or sets the date the user was created.
        /// </summary>
        public DateTime CreatedOn { get; set; }

        /// <summary>
        /// Gets or sets the date the user last logged in.
        /// </summary>
        public DateTime LastLogin { get; set; }

        /// <summary>
        /// Gets or sets informations about the channels the user can be contacted through.
        /// </summary>
        [Column(TypeName = "jsonb")]
        public string Channels { get; set; } = default!;

        /// <summary>
        /// Stores the last platform the user authenticated on.
        /// </summary>
        public string? LastPlatform { get; set; }

        /// <summary>
        /// Gets the identities of the user.
        /// </summary>
        public ICollection<IdentityRecord> Identities { get; set; } = default!;
      
        /// <summary>
        /// The pseudo
        /// </summary>
        public string? Pseudonym { get; set; }
    }

    public class IdentityRecord
    {
        /// <summary>
        /// Gets or sets the <see cref="UserRecord"/>  the <see cref="IdentityRecord"/> authenticates to.
        /// </summary>
        public ICollection<UserRecord> Users { get; set; } = default!;

        /// <summary>
        /// Gets or sets the main user account associated with this identity. 
        /// </summary>
        public UserRecord MainUser { get; set; } = default!;
        /// <summary>
        /// Gets or sets a string representing the identity provider.
        /// </summary>
        [Key]
        public string Provider { get; set; } = default!;

        /// <summary>
        /// Gets or sets a string identifying the user for the given provider.
        /// </summary>
        [Key]
        public string Identity { get; set; } = default!;
    }
}
