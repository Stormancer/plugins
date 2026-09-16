# Collections System
This plugin enables players to get a list of collectibles from the game server with associated metadata, and to unlock them. 

The unlocking process can be customized by registering dependencies which implement the `ICollectionEventHandler` contract, for instance to spend resources on unlock. 

## Item definitions
Unlockable items are defined in the item definition file. A path to the file containing the item definitions in the cluster file storage system must be defined in the plugin configuration.

Item definitions are identified by an id provided as they key in the definition file. Additionally, you can declare a category, tags and custom metadata for each item definition. Use this data in server & client code to customize display and create custom logic. For instance, you could specify a cost in the metadata, then use it decide how much currency a player has to spend to unlock the item in their collection.

Example:

```
PUT: {adminApi}/_blobs/{accountId}/{containerName}/{fileId}
{
    "items": {
        "ring_of_cats": {
            "category":"equipment",
            "tags":["ring","cat"],
            "metadata": {
                "foo": 5000
            }
        },
        "item2": {
        ...
            },
        "item3":{
            }
        }
    }
}
```

## Configuration

```
{
    ...
   "collection": {
        "ItemDefinitionsPath": "accountId/blobcontainerName/fileId"
    },
}
```