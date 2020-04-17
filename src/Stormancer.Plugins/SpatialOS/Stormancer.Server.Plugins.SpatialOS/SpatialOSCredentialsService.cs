using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Improbable.SpatialOS.Deployment.V1Beta1;
using Improbable.SpatialOS.Platform.Common;
using Improbable.SpatialOS.PlayerAuth.V2Alpha1;
using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.SpatialOS.Models;
using Stormancer.Server.Plugins.Users;

namespace Stormancer.Server.Plugins.SpatialOS
{
    internal class SpatialOSCredentialsService : ISpatialOSCredentialsService
    {
        private readonly PlayerAuthServiceClient _playerAuthServiceClient;
        private readonly DeploymentServiceClient _deploymentServiceClient;
        private readonly SpatialOSConfiguration _spatialOSConfiguration;
        public SpatialOSCredentialsService(IConfiguration settings)
        {
            var config = ((JObject)settings.Settings.spatialos).ToObject<SpatialOSConfiguration>();
            if(config == null)
            {
                throw new InvalidOperationException("spatialos config should not be null.");
            }
            _spatialOSConfiguration = config;

            _playerAuthServiceClient = PlayerAuthServiceClient.Create(credentials: new PlatformRefreshTokenCredential(_spatialOSConfiguration.ServiceKey));
            _deploymentServiceClient = DeploymentServiceClient.Create(credentials: new PlatformRefreshTokenCredential(_spatialOSConfiguration.ServiceKey));
        }

        public async Task<SpatialOsPlayerCredentials?> CreateSpatialOSToken(string userId, string providerName, string deploymentName, string workerType)
        {

            var playerIdentityTokenResponse = _playerAuthServiceClient.CreatePlayerIdentityToken(new CreatePlayerIdentityTokenRequest
            {
                Provider = providerName,
                PlayerIdentifier = userId,
                ProjectName = _spatialOSConfiguration.ProjectName
            });


            var suitableDeployment = (await _deploymentServiceClient.GetRunningDeploymentByNameAsync(new GetRunningDeploymentByNameRequest
            {
                ProjectName = _spatialOSConfiguration.ProjectName,
                DeploymentName = deploymentName
            })).Deployment;

            if (suitableDeployment == null)
            {
                return null;
            }

            var createLoginTokenResponse = _playerAuthServiceClient.CreateLoginToken(new CreateLoginTokenRequest
            {
                PlayerIdentityToken = playerIdentityTokenResponse.PlayerIdentityToken,
                DeploymentId = suitableDeployment.Id.ToString(CultureInfo.InvariantCulture),
                LifetimeDuration = Duration.FromTimeSpan(new TimeSpan(0, 0, _spatialOSConfiguration.LoginTokenDuration, 0)),
                WorkerType = workerType
            });

            return new SpatialOsPlayerCredentials(playerIdentityTokenResponse.PlayerIdentityToken, createLoginTokenResponse.LoginToken, DateTime.UtcNow.AddMinutes(_spatialOSConfiguration.LoginTokenDuration));
        }
    }
}
