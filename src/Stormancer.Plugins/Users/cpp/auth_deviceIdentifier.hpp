#pragma once
#include "users/Users.hpp"


namespace Stormancer
{
	namespace Users
	{
		namespace Auth
		{
			struct AuthDeviceIdentifierConfiguration
			{
				static constexpr char* DeviceIdentifierConfigPath = "stormancer.auth.deviceIdentifier";
			};
			class AuthDeviceIdentifierPlugin;
			namespace details
			{
				class AuthDeviceIdentifier: public ::Stormancer::Users::IAuthenticationEventHandler
				{
				public:
					AuthDeviceIdentifier(std::shared_ptr<Configuration> config)
						:_config(config)
					{
					}

					virtual pplx::task<void> retrieveCredentials(const ::Stormancer::Users::CredentialsContext& ctx)
					{
						if (ctx.tryUseProvider("deviceidentifier"))
						{
							std::string identifier;
							if (tryGetDeviceIdentifier(identifier))
							{
								ctx.authParameters->type = "deviceidentifier";


								ctx.authParameters->parameters["deviceidentifier"] = identifier;
							}
						}
						return pplx::task_from_result();
					}

					bool tryGetDeviceIdentifier(std::string& deviceIdentifier)
					{
						auto configIt = _config->additionalParameters.find(AuthDeviceIdentifierConfiguration::DeviceIdentifierConfigPath);
						if (configIt != _config->additionalParameters.end())
						{
							deviceIdentifier = configIt->second;
							return true;
						}
						else
						{
							return false;
						}
					}

					std::shared_ptr<Configuration> _config;
				};

			}

			/// <summary>
			/// Enables Ephemeral auth
			/// </summary>
			class AuthDeviceIdentifierPlugin: public IPlugin
			{
				static constexpr const char* PLUGIN_NAME = "Users.Auth.DeviceIdentifier";
				static constexpr const char* PLUGIN_VERSION = "1.0.0";

				PluginDescription getDescription() override
				{
					return PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
				}

			private:

				void registerClientDependencies(ContainerBuilder& builder) override
				{
					builder.registerDependency<details::AuthDeviceIdentifier,Configuration>().as<IAuthenticationEventHandler>();
					
				}
			};
		}
	}
}