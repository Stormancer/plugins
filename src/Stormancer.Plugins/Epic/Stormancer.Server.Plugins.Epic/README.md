Epic
====

# DevAuth

To auth with a normal account :
- Set the Stormancer client configuration key `LoginMode` to `ExchangeCode`

To auth with a dev account :
- Download the [Epic EOS SDK](https://dev.epicgames.com/portal)
- Start the executable `/EOS-SDK-WWWWWWWW-X.YY.Z.zip/SDK/Tools/EOS_DevAuthTool-win32-x64-X.Y.Z.zip/EOS_DevAuthTool.exe`
- Enter a port (for example `4567`)
- Click start
- Click Login
- Enter your dev account credentials
- Click Connect
- Enter a dev account name (for example `dev1`)
- Set the Stormancer client configuration key `LoginMode` to `DevAuth`
- Set the Stormancer client configuration key `DevAuthHost` to `localhost:4567`
- Set the Stormancer client configuration key `DevAuthCredentialsName` to `dev1`

In the final game, you must get an exchange code from the game process launch argument `AUTH_PASSWORD` (argv).  
Format : `-AUTH_LOGIN=unused -AUTH_PASSWORD=<password> -AUTH_TYPE=exchangecode -epicapp=<appid> -epicenv=Prod -EpicPortal  -epicusername=<username> -epicuserid=<userid> -epiclocale=en-US`

# Client configuration

```cpp
	auto configuration = Stormancer::Configuration::create(STORMANCER_ENDPOINT, STORMANCER_ACCOUNT, STORMANCER_APPLICATION);
	configuration->additionalParameters["platform"] = "epic";
	configuration->additionalParameters[Stormancer::Epic::ConfigurationKeys::ProductName] = "PRODUCT_NAME";
	configuration->additionalParameters[Stormancer::Epic::ConfigurationKeys::ProductVersion] = "PRODUCT_VERSION";
	configuration->additionalParameters[Stormancer::Epic::ConfigurationKeys::InitPlatform] = "true";
	configuration->additionalParameters[Stormancer::Epic::ConfigurationKeys::AuthenticationEnabled] = "true";
#if defined(_DEBUG)
	configuration->additionalParameters[Stormancer::Epic::ConfigurationKeys::Diagnostics] = "true";
	configuration->additionalParameters[Stormancer::Epic::ConfigurationKeys::LoginMode] = "DevAuth";
	configuration->additionalParameters[Stormancer::Epic::ConfigurationKeys::DevAuthHost] = "localhost:4567";
	configuration->additionalParameters[Stormancer::Epic::ConfigurationKeys::DevAuthCredentialsName] = "dev1";
#else
	configuration->additionalParameters[Stormancer::Epic::ConfigurationKeys::LoginMode] = "ExchangeCode";
	configuration->additionalParameters[Stormancer::Epic::ConfigurationKeys::ExchangeCode] = exchangeCode;
#endif
	configuration->additionalParameters[Stormancer::Epic::ConfigurationKeys::ProductId] = "UUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUU";
	configuration->additionalParameters[Stormancer::Epic::ConfigurationKeys::SandboxId] = "WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW";
	configuration->additionalParameters[Stormancer::Epic::ConfigurationKeys::DeploymentId] = "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX";
	configuration->additionalParameters[Stormancer::Epic::ConfigurationKeys::ClientId] = "YYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYY";
	configuration->additionalParameters[Stormancer::Epic::ConfigurationKeys::ClientSecret] = "ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ";
	configuration->addPlugin(new Stormancer::Epic::EpicPlugin());
```

## ProductUserId

The Epic profile part contains the `ProductUserId` associated with the player. However, if using it in the game client, it won't be recognized by the Epic SDK by default.  

To be able to use it on the client, you must call the client [connect API](https://dev.epicgames.com/docs/api-ref/functions/eos-connect-login) beforehand.  
This will fill the local cache of the Epic SDK.

# Server configuration

```json
{
	"auth": {
		"epic": {
			"enabled": true
		}
    },
	"epic": {
    	"productIds": [ "UUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUU" ],
    	"applicationIds": [ "VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV" ],
		"sandboxIds": [ "WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW" ],
		"deploymentIds": [ "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX" ],
		"clientId": "YYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYY",
		"clientSecret": "[GameOrPublisherAccount]/[SecretStoreName]/epic_clientSecret"
	},
}
```
