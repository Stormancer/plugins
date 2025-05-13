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
By default, the plugin doesn't persist users in a database. They are attributed new user ids each time they connect, event if they are authenticated on a platform like steam. In the same way, login password authentication won't be functional without an additional persistence plugin. We provide [Stormancer.Server.Plugins.Users.EntityFramework](https://www.nuget.org/packages/Stormancer.Server.Plugins.Users.EntityFramework) to store users in an SQL database using EntityFramework as a storage framework.


# Dev auth
During development, it might not always be possible to setup Steam, Epic store or similar authentication systems. For these situation, or when releasing a game that don't need strong player authentication, we provide 2 authentication systems out of the box:

## Ephemeral authentication

This authentication provider enables "anonymous" authentication in the game. Players logging in through the ephemeral auth provider are not authenticated. Their user id is scoped to their session, and changes each time they reconnect. No database is necessary to use the ephemeral auth provider. Furthermore, using it disables any features which might require account persistence (for instance cross platform friends, game history, etc...)

## Device identifier authentication

This authentication provider is an unsafe provider which enables account persistence without authentication. It's perfect during development, because contrary to most other persistence auth mechanisms: 
1. It does not require player input
2. It does not require an external SDK
3. It supports running several instances of the same application with different connected users on the same computer.
However should be used with caution in production environments because of the limited trust it provides. Device id authentication is performed by sending to the server a string (a device identifier) supposed to be unique to the player device. Long enough device identifiers can provide enough uniqueness guarantees to make using the mechanism in production a possibility, but be aware that a malicious user that could access the device identifier of a player could steal their account.


Applications which want to implement device id auth must provide an implementation of `Stormancer::Users::Auth::IDeviceIdentifierProvider` For more details, see the start of ViewModel.cpp in the Sample project. The sample project create such an implementation based on locking uniquely named files on the disk to provide persistent device identifiers and uniqueness when several clients are running concurrently on the same computer. This implementation can be reused to provide out of the box dev auth.
