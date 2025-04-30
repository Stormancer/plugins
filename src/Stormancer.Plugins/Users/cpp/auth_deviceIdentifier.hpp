#pragma once
#include "users/Users.hpp"


namespace Stormancer
{
	namespace Users
	{
		namespace Auth
		{
			/// <summary>
			/// A device identifier created by the 
			/// </summary>
			class IDeviceIdentifier
			{
			public:
				
				virtual std::string get() = 0;

				virtual ~IDeviceIdentifier() {}
			};

			/// <summary>
			/// Platforms need to implement IDeviceIDentifierProvider
			/// </summary>
			class IDeviceIdentifierProvider
			{
			public:
				/// <summary>
				/// Captures a device identifier for use during this session.
				/// The backend MUST prevent the identifier from being captured again until it is released.
				/// </summary>
				/// <returns></returns>
				virtual IDeviceIdentifier* capture() = 0;
			};

			

			class AuthDeviceIdentifierPlugin;
			namespace details
			{
				class DeviceIdentifierStore
				{
				public:
					IDeviceIdentifier* currentIdentifier;

					~DeviceIdentifierStore()
					{
						if (currentIdentifier != nullptr)
						{
							delete currentIdentifier;
						}
					}
				};

				class AuthDeviceIdentifier: public ::Stormancer::Users::IAuthenticationProvider
				{
				public:

					std::string getProviderName() const override
					{
						return "deviceidentifier";
					}

					AuthDeviceIdentifier(std::shared_ptr<Configuration> config, std::shared_ptr<IDeviceIdentifierProvider> deviceIdentifierProvider,std::shared_ptr< DeviceIdentifierStore> store)
						:_config(config)
						, _deviceIdentifierProvider(deviceIdentifierProvider)
						, _store(store)
					{
					}

					pplx::task<void> retrieveCredentials(const ::Stormancer::Users::CredentialsContext& ctx) override
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
						if (!_deviceIdentifierProvider)
						{
							return false;
						}
						_store->currentIdentifier = _deviceIdentifierProvider->capture();

						if (!_store->currentIdentifier)
						{
							return false;
						}
						
						deviceIdentifier = _store->currentIdentifier->get();
						return true;
					}

					

					std::shared_ptr<Configuration> _config;
					std::shared_ptr<IDeviceIdentifierProvider> _deviceIdentifierProvider;
					std::shared_ptr< DeviceIdentifierStore> _store;
				};

				class AuthDeviceIdentifierAuthenticationEventHandler : public Users::IAuthenticationEventHandler
				{
				public:
					AuthDeviceIdentifierAuthenticationEventHandler(std::shared_ptr< DeviceIdentifierStore> store)
						:_store(store)
					{
					}

					pplx::task<void> OnLoggingOut() override
					{
						delete _store->currentIdentifier;
						_store->currentIdentifier = nullptr;

						return pplx::task_from_result();
					}
					std::shared_ptr< DeviceIdentifierStore> _store;
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
					builder.registerDependency<details::AuthDeviceIdentifier,Configuration, IDeviceIdentifierProvider, details::DeviceIdentifierStore>().as<IAuthenticationProvider>();
					builder.registerDependency<details::AuthDeviceIdentifierAuthenticationEventHandler, details::DeviceIdentifierStore>().as<Users::IAuthenticationEventHandler>();
					builder.registerDependency< details::DeviceIdentifierStore>().singleInstance();
				}
			};
		}
	}
}