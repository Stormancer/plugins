Galaxy
======

# Client configuration

```cpp
	auto configuration = Stormancer::Configuration::create(STORMANCER_ENDPOINT, STORMANCER_ACCOUNT, STORMANCER_APPLICATION);
    configuration->additionalParameters["platform"] = "galaxy";
    configuration->additionalParameters[Stormancer::Galaxy::ConfigurationKeys::InitPlatform] = "true";
    configuration->additionalParameters[Stormancer::Galaxy::ConfigurationKeys::AuthenticationEnabled] = "true";
    configuration->additionalParameters[Stormancer::Galaxy::ConfigurationKeys::ClientId] = "XXXXXXXXXXXXXXXXX";
    configuration->additionalParameters[Stormancer::Galaxy::ConfigurationKeys::ClientSecret] = "YYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYY";
    configuration->addPlugin(new Stormancer::Galaxy::GalaxyPlugin());
```

# Server configuration

```json
{
	"galaxy": {
        "productId": "XXXXXXXXXX",
        "ticketPrivateKey": "[GameOrPublisherAccount]/[SecretStoreName]/galaxy_ticketPrivateKey"
	},
}
```
