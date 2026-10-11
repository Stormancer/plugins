
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Stormancer.Server.Plugins.Users.OAuth;
using Stormancer.Server.Plugins.WebApi;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
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
    /// Provides public Apis to check OAuth tokens.
    /// </summary>
    [WebApiType(WebApiType.Public)]
    [Route("oauth")]
    public class OAuthPublicController : ControllerBase
    {
        private readonly OAuthService _service;

        /// <summary>
        /// Creates a <see cref="OAuthPublicController"/> object.
        /// </summary>
        /// <param name="service"></param>
        public OAuthPublicController(OAuthService service)
        {
            _service = service;
        }

        /// <summary>
        /// Validates a JWT.
        /// </summary>
        /// <returns></returns>
        [Route("validateToken")]
        [HttpPost]
        public async Task<ActionResult<ValidateJwtResponse>> ValidateToken()
        {
            if (AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var authorization))
            {
                if (authorization.Scheme != "Bearer")
                {
                    return Unauthorized("Authorization scheme must be bearer token.");
                }
                if(string.IsNullOrEmpty(authorization.Parameter))
                {
                    return Unauthorized("Token null or empty.");
                }

                var validationResult = await _service.ValidateToken(authorization.Parameter);

                if(validationResult.IsValid)
                {
                    /*
                     *   ["sub"] = userId,
                        ["displayName"] = displayName
                     */
                    return new ValidateJwtResponse { DisplayName= (string)(validationResult.Claims["displayName"]), UserId = (string)(validationResult.Claims["sub"]) };
                }
                else
                {
                    return Unauthorized(validationResult.Exception.Message);
                }
            }
            else
            {
                return Unauthorized();
            }
        }
    }

    public class ValidateJwtResponse
    {
        [DataMember(Name ="userId")]
        public required string UserId { get; init; }

        [DataMember(Name = "displayName")]
        public required string DisplayName { get;init;  }
    }

    /// <summary>
    /// 
    /// </summary>
    [WebApiType(WebApiType.Admin)]
    [Route("_oauth")]
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
        [Route("createKey")]
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
