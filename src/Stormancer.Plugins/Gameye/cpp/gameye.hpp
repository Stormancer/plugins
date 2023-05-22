#pragma once
#include "Users/ClientAPI.hpp"
#include "Users/Users.hpp"
#include "stormancer/IPlugin.h"
#include "stormancer/Configuration.h"
#include <sstream>
#include <iostream>
#include <vector>

namespace Stormancer
{
	namespace Gameye
	{
		/// <summary>
		/// Keys to use in Configuration::additionalParameters map to customize the plugin behavior.
		/// </summary>
		namespace ConfigurationKeys
		{
			/// <summary>
			/// the id of the server port as configured in gameye. For Instance 7777.
			/// </summary>
			/// <remarks>
			/// Gameye provides a port as an environment variable for the gameserver to bind to. This port is identified by an id during config
			/// (for instance if the id is set as 7777, Gameye will provide an environment variable named GAMEYE_PORT_UDP_7777 containing the port to ///  use.
			/// </remarks>
			constexpr const char* GameyePortId = "gameye.parameters.portId";
		}



		class GameyePlugin;

		namespace details
		{
			class GameyeConfiguration
			{
			public:
				GameyeConfiguration(std::shared_ptr<Stormancer::Configuration> config, std::shared_ptr<Stormancer::ILogger> logger) :
					_config(config),
					_logger(logger)
				{

				}

				void applyConfig()
				{
					auto host = std::getenv("GAMEYE_HOST");
					auto it = _config->additionalParameters.find(ConfigurationKeys::GameyePortId);
					if (it == _config->additionalParameters.end())
					{
						_logger->log(Stormancer::LogLevel::Info, "initialization", "'gameye.parameters.portId' not set in additionalParameters. Gameye plugin disabled.");
						return;
					}

					auto portId = it->second;
					if (host) 
					{
						_logger->log(Stormancer::LogLevel::Info, "initialization", "Loading Gameye env...");
						_logger->log(Stormancer::LogLevel::Info, "initialization", "GAMEYE_HOST  set", host);
						//If there is a published address, the peer is directly reachable. We disable nat traversal. 
						_config->enableNatPunchthrough = false;
						_config->publishedAddresses.push_back(host);

						
						std::string port_env_var = "GAMEYE_PORT_UDP_" + portId;


						auto port = std::getenv(port_env_var.c_str());
						_logger->log(Stormancer::LogLevel::Info, "initialization", port_env_var +":", port);
						
						if (port)
						{
							_config->publishedPort = std::atoi(port);
						}

						_logger->log(Stormancer::LogLevel::Info, "initialization", "Gameye env loaded...");
					}
				
					
				}

			
			private:
				std::shared_ptr<Stormancer::Configuration> _config;
				std::shared_ptr<Stormancer::ILogger> _logger;
			};
			
		}

		

		class GameyePlugin : public Stormancer::IPlugin
		{
		public:

			static constexpr const char* PLUGIN_NAME = "ServerPools";
			static constexpr const char* PLUGIN_VERSION = "1.0.0";

			PluginDescription getDescription() override
			{
				return PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
			}

		private:

			

			void registerClientDependencies(Stormancer::ContainerBuilder& builder) override
			{
				
				builder.registerDependency<details::GameyeConfiguration, Stormancer::Configuration, Stormancer::ILogger>().singleInstance();
			}


			void clientCreating(std::shared_ptr<IClient> client) override
			{
				auto config = client->dependencyResolver().resolve<details::GameyeConfiguration>();

				//Applies the plugin config to the client configuration.
				config->applyConfig();


			}

			void clientCreated(std::shared_ptr<IClient> client) override
			{

			}
		};
	}
}