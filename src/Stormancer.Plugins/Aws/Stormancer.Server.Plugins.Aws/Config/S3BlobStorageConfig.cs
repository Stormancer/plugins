using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Aws.Config
{
    /// <summary>
    /// Configuration of an S3 storage account
    /// </summary>
    public class S3BlobStorageConfig
    {
        /// <summary>
        /// Gets or sets the type of the configuration.
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// If true use AWS default credentials (environment variables, EC2 instance profile, etc.)
        /// </summary>
        public bool UseDefaultCredentials { get; set; }

        ///// <summary>
        ///// If true, use AWS Roles Anywhere to obtain credentials (requires RolesAnywhereProfileArn).
        ///// </summary>
        //public bool UseRolesAnywhere { get; set; }

        ///// <summary>
        ///// Roles Anywhere profile ARN (if using Roles Anywhere).
        ///// </summary>
        //public string? RolesAnywhereProfileArn { get; set; }

        ///// <summary>
        ///// Path to the client certificate (PEM) to use with Roles Anywhere, if applicable.
        ///// </summary>
        //public string? RolesAnywhereCertificatePath { get; set; }

        /// <summary>
        /// If true, use an AWS access key and secret key
        /// </summary>
        public bool UseAccessKey { get; set; }

        /// <summary>
        /// Gets or sets the AWS access key ID (if using access key authentication).
        /// </summary>
        public string? AccessKeyId { get; set; }

        /// <summary>
        /// Gets or sets the AWS secret access key (if using access key authentication).
        /// </summary>
        public string? AccessKeySecretPath { get; set; }

        /// <summary>
        /// Gets or sets the bucket to use.
        /// </summary>
        public string? Bucket { get; set; }

        /// <summary>
        /// Gets or sets the AWS region to use (e.g., "us-east-1").
        /// </summary>
        public string? Region { get; set; }

        /// <summary>
        /// Validates the configuration
        /// </summary>
        /// <returns></returns>
        [MemberNotNullWhen(true, "Bucket", "Region")]
        public bool Validate()
        {
            if (Type != "s3" || String.IsNullOrWhiteSpace(Bucket) || String.IsNullOrWhiteSpace(Region))
            {
                return false;
            }
            return UseDefaultCredentials
                //|| (UseRolesAnywhere && !String.IsNullOrWhiteSpace(RolesAnywhereProfileArn) && String.IsNullOrWhiteSpace(RolesAnywhereCertificatePath))
                || (UseAccessKey && !String.IsNullOrWhiteSpace(AccessKeyId) && !String.IsNullOrWhiteSpace(AccessKeySecretPath));
        }
    }
}
