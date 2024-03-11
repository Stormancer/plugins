# Overview

The Users plugin provides basic authentication and identity features for peers connecting to a Stormancer server application.
The following features are supported:

- User ids scoped to the application, used by other plugins and clients.
- Extensible identity system. Plugins can implement `IAuthenticationProvider` to add new authentication capabilities to the application. The Steam, PSN, XBoxLive, Epic, Gog and Nintendo plugins integrate with the Users plugin out of the box. Dev, DeviceId and Login/password are also provided.
- Federated identity : an user of the application can authenticate with multiple providers
- Integration with the profile system to expose user related informations as a profile part.
- User search.
- In memory session data.
- Service locator functionalities (should be moved to another plugin)
- Integration with the analytics plugin.


# Persistence
By default, the plugin doesn't persist users in a database. They are attributed new user ids each time they connect, event if they are authenticated on a platform like steam. In the same way, login password authentication won't be functional without an additionnal persistence plugin. We provide [Stormancer.Server.Plugins.Users.EntityFramework](https://www.nuget.org/packages/Stormancer.Server.Plugins.Users.EntityFramework) to store users in an SQL database using EntityFramework as a storage framework.

