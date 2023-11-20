using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.AdminApi;
using Stormancer.Server.Plugins.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stormancer.Core;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.IO;
using System.ComponentModel.DataAnnotations;

namespace Stormancer.Server.Plugins.Profile.Admin
{
    class AdminWebApiConfig : IAdminWebApiConfig
    {
        public void ConfigureApplicationParts(ApplicationPartManager apm)
        {
            apm.ApplicationParts.Add(new AssemblyPart(this.GetType().Assembly));
        }
    }


    /// <summary>
    /// Provide admin APIs for profiles.
    /// </summary>
    [ApiController]
    [Route("_profiles")]
    public class ProfilesAdminController : ControllerBase
    {
        private readonly ISceneHost _scene;
        private readonly IEnvironment _env;
        private readonly ISerializer _serializer;

        /// <summary>
        /// Creates a new <see cref="ProfilesAdminController"/> object.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="env"></param>
        /// <param name="serializer"></param>
        public ProfilesAdminController(ISceneHost scene, IEnvironment env, ISerializer serializer)
        {
            _scene = scene;
            _env = env;
            _serializer = serializer;
        }


        /// <summary>
        /// Gets profile parts for an user.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("{userId}")]
        public async Task<ActionResult<Dictionary<string, JObject>>> Get(string userId, CancellationToken cancellationToken)
        {
            await using var scope = _scene.CreateRequestScope();

            var profiles = scope.Resolve<IProfileService>();

            var displayMode = this.Request.Query.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.FirstOrDefault() ?? string.Empty);
            var profile = await profiles.GetProfile(userId, displayMode, null, cancellationToken);
            return Ok(profile);
        }


        /// <summary>
        /// Deletes a custom profile part.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="partId"></param>
        /// <returns></returns>
        [HttpDelete]
        [Route("{userId}/customParts/{partId}")]
        public async Task<ActionResult<DeleteAllResponse>> Delete(string userId, string partId)
        {
            await using var scope = _scene.CreateRequestScope();

            var profiles = scope.Resolve<IProfileService>();

            await profiles.DeleteCustomProfilePart(userId, partId, false);


            return new DeleteAllResponse { Success = true };
        }

        /// <summary>
        /// Updates a custom profile part.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="partId"></param>
        /// <param name="body"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("{userId}/customParts/{partId}")]
        public async Task<ActionResult<DeleteAllResponse>> Put(string userId, string partId, [FromBody] PutCustomProfilePartBody body)
        {
            await using var scope = _scene.CreateRequestScope();

            var profiles = scope.Resolve<IProfileService>();

            using var memoryStream = new MemoryStream();

            _serializer.Serialize(body.Content, memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);
            await profiles.UpdateCustomProfilePart(userId, partId,body.Version,false, memoryStream);


            return new DeleteAllResponse { Success = true };
        }


    }

    /// <summary>
    /// Body of an 'Update custom profile part' request
    /// </summary>
    public class PutCustomProfilePartBody
    {
        /// <summary>
        /// Version of the profile part format.
        /// </summary>
        [Required]
        public string Version { get; set; } = default!;

        /// <summary>
        /// Content of the profile part.
        /// </summary>
        [Required]
        public JObject Content { get; set; } = default!;
    }

    /// <summary>
    /// Response of a 'Delete custom profile part' request
    /// </summary>
    public class DeleteAllResponse
    {
        /// <summary>
        /// Indicates if profile 
        /// </summary>
        public bool Success { get; set; }
    }
}
