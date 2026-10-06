
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Stormancer.Server.Plugins.Users.OAuth;
using Stormancer.Server.Plugins.WebApi;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users.Web
{
    /// <summary>
    /// Provides apis required by OAuth.
    /// </summary>
    [WebApiType(WebApiType.Public)]
    [Route(".well-known")]
    public class OAuthMetadataController : ControllerBase
    {
        private readonly OAuthService _service;

        /// <summary>
        /// Creates a <see cref="OAuthMetadataController"/> object.
        /// </summary>
        /// <param name="service"></param>
        public OAuthMetadataController(OAuthService service)
        {
            _service = service;
        }

        /// <summary>
        /// Gets the JSON Web Key Set for the application.
        /// </summary>
        /// <returns>
        /// </returns>
        [Route("jwks.json")]
        [HttpGet]
        public async Task<ActionResult<JsonWebKeySet>> GetJwks()
        {
            var keySet = await _service.GetKeySet();

            return Ok(keySet);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    [WebApiType(WebApiType.Admin)]
    public class OAuthAdminController : ControllerBase
    {
        private readonly OAuthService _service;

        /// <summary>
        /// Creates a new <see cref="OAuthAdminController"/> object.
        /// </summary>
        /// <param name="service"></param>
        public OAuthAdminController(OAuthService service)
        {
            _service = service;
        }

        /// <summary>
        /// Creates a private key.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("")]
        public ActionResult<JsonWebKey> CreateKey([FromBody] CreateKeyArguments args)
        {
            return _service.CreatePrivateKey(args.KeyId);
        }
    }

    /// <summary>
    /// Result of a get jwks request.
    /// </summary>
    public class CreateKeyArguments
    {
        /// <summary>
        /// Gets or sets the id of the key.
        /// </summary>
        public string KeyId { get; set; }
    }
}
