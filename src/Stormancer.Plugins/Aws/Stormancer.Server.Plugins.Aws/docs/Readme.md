# Azure application support.

This plugin provides integration with AWS features for Stormancer applications

## Blob storage

Declare S3 blob storage based storage backends for use with the `Stormancer.Server.Plugins.BlobStorage` plugin.

Configuration:

    {
        "storage":{
            "blobStores":{
                "my-s3-storage":{
                    
                    //must be set to "s3" to use the Azure plugin.
                    "type":"azureBlob",
                    
                    //The S3 bucket to use for blob storage.
                    "bucket":"my-bucket",

                    //The AWS region to use for S3 storage.
                    "region":"us-east-1",

                    //One of the following must be set to true to specify how to authenticate with S3. If all are set to false, the corresponding backend will be disabled.
                    //If set to true, the plugin will use the default AWS credentials provider chain to authenticate with S3.
                    "useDefaultCredentials":true,

                    //If set to true, the plugin will use the accessKey and secretKey to authenticate with S3.
                    "useAccessKey":false,
                    
                    //The access key to use to authenticate with S3. Only used if useAccessKey is set to true.
                    "accessKeyId":"my-access-key",

                    // The access secret key is stored in the cluster secret store.
                    "accessKeySecretPath":"{account}/{secretStoreName}/{accessKeySecret}" 

                }
            }
        }
    }