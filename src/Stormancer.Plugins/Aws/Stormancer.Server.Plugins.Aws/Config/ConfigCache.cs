using Amazon;
using Amazon.IAMRolesAnywhere;
using Amazon.IAMRolesAnywhere.Model;
using Amazon.Runtime;
using Amazon.Runtime.Internal.Util;
using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json;
using Stormancer.Server.Secrets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Aws.Config
{
    internal class ConfigCache
    {
        public DateTime _lastUpdated;

        private Task<AWSCredentials?>? _awsCredentialsTask;
        private readonly ISecretsStore _secretStore;
        private readonly object _syncRoot = new object();

        public ConfigCache(ISecretsStore secretStore)
        {
            _secretStore = secretStore;
        }

        public Task<AWSCredentials?> GetS3Credentials(S3BlobStorageConfig config)
        {
            lock (_syncRoot)
            {
                if (_awsCredentialsTask == null || _lastUpdated < DateTime.UtcNow - TimeSpan.FromMinutes(1))
                {
                    _lastUpdated = DateTime.UtcNow;
                    if (config.UseDefaultCredentials)
                    {
                        // Let the AWS SDK use the default provider chain
                        _awsCredentialsTask = Task.FromResult<AWSCredentials?>(null);
                    }
                    //else if (config.UseRolesAnywhere)
                    //{
                    //    // Obtain temporary credentials via IAM Roles Anywhere
                    //    _awsCredentialsTask = GetAwsCredentialsRolesAnywhere(config.RolesAnywhereProfileArn!, config.RolesAnywhereCertificatePath!, config.Region);
                    //}
                    else if (config.UseAccessKey)
                    {
                        _awsCredentialsTask = GetAwsCredentialsAccessKey(config.AccessKeyId!, config.AccessKeySecretPath!);
                    }
                    else
                    {
                        return Task.FromException<AWSCredentials?>(new InvalidOperationException("Invalid blob storage configuration"));
                    }
                }
            }
            return _awsCredentialsTask!;
        }

        private async Task<AWSCredentials?> GetAwsCredentialsRolesAnywhere(string rolesAnywhereProfileArn, string rolesAnywhereCertificatePath, string region)
        {
            throw new NotImplementedException("Roles Anywhere isn't implemented");
        }

        private async Task<AWSCredentials?> GetAwsCredentialsAccessKey(string accessKeyId, string accessKeySecretPath)
        {
            var accessKeySecret = await _secretStore.GetSecret(accessKeySecretPath);
            if (accessKeySecret.Value == null)
            {
                throw new InvalidOperationException($"Failed to retrieve access key secret from path: {accessKeySecretPath}");
            }

            var accessKey = Encoding.UTF8.GetString(accessKeySecret.Value);

            return new BasicAWSCredentials(accessKeyId, accessKey);
        }
    }
}