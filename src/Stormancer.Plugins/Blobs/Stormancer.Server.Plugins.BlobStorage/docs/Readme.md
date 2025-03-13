# Blob storage plugin.

This plugin adds a standardized API which can be leverage by applications and other plugins to store blobs without having to depend on the storage backend. Custom storage backends can be declared by other plugins.



## Configuration

The plugin supports configuring one or several storage backends, which are then used by other plugins and systems.

For instance, to declare a storage named `myStorage` using the Azure blob storage backend.

    "storage":{
        "myStorage":{
            "type":"azureBlob",
            "connectionStringPath":"secretStoreId/secretId",
            "container":"blobContainerId"
        }
    }