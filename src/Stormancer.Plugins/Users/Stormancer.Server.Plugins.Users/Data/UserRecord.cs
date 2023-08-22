using Microsoft.EntityFrameworkCore;
using Nest;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users
{

    /// <summary>
    /// User record in the database.
    /// </summary>
    public partial class UserRecord
    {
        /// <summary>
        /// Creates a model from the record.
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        [return: NotNullIfNotNull("record")]
        public static User? CreateUserFromRecord(UserRecord? record)
        {
            if (record == null) return null;
            return new User()
            {
                Id = record.Id.ToString(),
                Auth = JObject.Parse(record.Auth.RootElement.GetRawText()!),
                Channels = JObject.Parse(record.Channels.RootElement.GetRawText()!),
                Pseudonym = record.UserHandle,
                UserData = JObject.Parse(record.UserData.RootElement.GetRawText()!),
                CreatedOn = record.CreatedOn,
                LastLogin = record.LastLogin,
                LastPlatform = record.LastPlatform

            };
        }


        /// <summary>
        /// Creates a record from a model.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        [return: NotNullIfNotNull("user")]
        public static UserRecord? CreateRecordFromUser(User? user)
        {
            if (user == null) return null;
            return new UserRecord()
            {
                Auth = JsonDocument.Parse(user.Auth.ToString()),
                Channels = JsonDocument.Parse(user.Channels.ToString()),
                CreatedOn = user.CreatedOn,
                Id = Guid.Parse(user.Id),
                UserData = JsonDocument.Parse(user.UserData.ToString()),
                LastPlatform = user.LastPlatform,
                UserHandle = user.Pseudonym


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
        public JsonDocument Auth { get; set; } = default!;

        /// <summary>
        /// Gets or sets custom data about the user.
        /// </summary>
        [Column(TypeName = "jsonb")]
        public JsonDocument UserData { get; set; } = default!;

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
        public JsonDocument Channels { get; set; } = default!;

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
        public string? UserHandle { get; set; }
        public ICollection<IdentityRecord> MainIdentities { get; set; }
    }

    [PrimaryKey("Provider","Identity")]
    public partial class IdentityRecord
    {
        /// <summary>
        /// Gets or sets the <see cref="UserRecord"/>  the <see cref="IdentityRecord"/> authenticates to.
        /// </summary>
        public virtual ICollection<UserRecord> Users { get; set; } = default!;

        /// <summary>
        /// Gets or sets the main user account associated with this identity. 
        /// </summary>
        public UserRecord MainUser { get; set; } = default!;
        /// <summary>
        /// Gets or sets a string representing the identity provider.
        /// </summary>
        
        public string Provider { get; set; } = default!;

        /// <summary>
        /// Gets or sets a string identifying the user for the given provider.
        /// </summary>
       
        public string Identity { get; set; } = default!;
    }
}
