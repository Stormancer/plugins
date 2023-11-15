# Azure application support.

This plugin provides integration with Windows Azure features for Stormancer applications

## Blob storage

Declare Azure blob storage based storage backends for use with the `Stormancer.Server.Plugins.BlobStorage` plugin.

Configuration:

    {
        "storage":{
            "blobStores":{
                "my-azure-storage":{
                    
                    //must be set to "azureBlob" to use the Azure plugin.
                    "type":"azureBlob",
                    "container":"my-container",

                    //The connectionString is stored in the cluster secret store.
                    "connectionStringPath":"{account}/{secretStoreName}/{secretKey}" 

                }
            }
        }
    }