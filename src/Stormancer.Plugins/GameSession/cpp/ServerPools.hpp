#pragma once
#include "users/ClientAPI.hpp"
#include "users/Users.hpp"
#include "stormancer/IPlugin.h"
#include "stormancer/Configuration.h"
#include "GameSession.hpp"
#include <sstream>
#include <iostream>
#include <vector>

namespace Stormancer
{
	namespace ServerPools
	{
		/// <summary>
		/// Keys to use in Configuration::additionalParameters map to customize the plugin behavior.
		/// </summary>
		namespace ConfigurationKeys
		{
			/// <summary>
			/// Gets the server connection parameters from environment variables.
			/// </summary>
			/// <remarks>
			/// Env variables:
			/// Stormancer.Server.ClusterEndpoints		: Comma separated list of endpoints the server should use to communicate with the Stormancer cluster.
			/// Stormancer.Server.TransportEndpoint     : If set, forces the client to use a specific UDP transport endpoint.
			/// Stormancer.Server.Port					: The local port the server should bind to.
			/// Stormancer.Server.PublishedAddresses	: Comma separated list of public address the players can use to communicate with this server.
			/// Stormancer.Server.PublishedPort			: The public port mapped to the port the client is bound to.
			/// Stormancer.Server.AuthenticationToken	: Server authentication token.
			/// Stormancer.Server.Account
			/// Stormancer.Server.Application
			/// </remarks>
			constexpr const char* GetParametersFromEnv = "server.parameters.fromEnvironmentVariables";
		}
		namespace ServerPoolsConstants
		{
			constexpr const char* DevAuthProvider = "gameServer.dev";
			constexpr const char* AgentBasedAuthProvider = "gameServer";
		}

		enum class Status
		{
			Unknown,
			//Server initializing
			Initializing,
			//Server ready to accept a game
			Ready,
			//Game in progress
			InProgress,
			//Game complete
			Complete
		};
		struct Player
		{
			std::string data;
			SessionId sessionId;
			std::string userId;
			STRM_MSGPACK_DEFINE(data, sessionId, userId)
		};
		struct PlayerParty
		{
			std::string groupId;

			std::unordered_map<std::string, Player> playerIds;

			std::vector<byte> customData;

			msgpack::type::ext creationTimeUtc;

			int pastPasses;

			std::string partyLeaderId;
			STRM_MSGPACK_DEFINE(groupId, playerIds, customData, creationTimeUtc, pastPasses, partyLeaderId)
		};
		struct Team
		{
			std::string teamId;
			std::vector<PlayerParty> groups;

			STRM_MSGPACK_DEFINE(teamId, groups)
		};
		template<typename T>
		struct GameSessionConfiguration
		{
			STRM_MSGPACK_DEFINE()
		};

		template<typename T>
		struct GameSessionStartupParameters
		{
			std::string gameSessionConnectionToken;
			GameSessionConfiguration<T> config;
			std::string gameSessionId;

			STRM_MSGPACK_DEFINE(gameSessionConnectionToken, config)
		};

		class ServerPoolsPlugin;

		namespace details
		{
			class ServerPoolConfiguration
			{
			public:
				ServerPoolConfiguration(std::shared_ptr<Stormancer::Configuration> config, std::shared_ptr<Stormancer::ILogger> logger) :
					_config(config),
					_logger(logger)
				{

				}

				bool isServer = false;

				void applyConfig()
				{
					_logger->log(Stormancer::LogLevel::Info, "initialization", "Loading env...");



					/// Stormancer.Server.ClusterEndpoints	: Comma separated list of endpoints the server should use to communicate with the Stormancer cluster.

					std::string serverEndpoints;// = std::getenv("Stormancer_Server_ClusterEndpoints");
					if (tryGetEnvironmentVariable("Stormancer_Server_ClusterEndpoints", serverEndpoints))
					{
						_logger->log(Stormancer::LogLevel::Info, "initialization", "Stormancer_Server_ClusterEndpoints set", serverEndpoints);
						_config->clearServerEndpoints();

						std::istringstream f(serverEndpoints);
						std::string s;
						while (getline(f, s, ',')) {

							_config->addServerEndpoint(s);

						}

						_config->discoveryEnabled = false;
						isServer = true;
					}

					std::string transportEndpointStr;// = std::getenv("Stormancer_Server_TransportEndpoint");

					if (tryGetEnvironmentVariable("Stormancer_Server_TransportEndpoint", transportEndpointStr))
					{
						_logger->log(Stormancer::LogLevel::Info, "initialization", "Stormancer_Server_TransportEndpoint set", transportEndpointStr);
						_config->forceTransportEndpoint = transportEndpointStr;
					}

					/// Stormancer.Server.Port		: The local port the transport should bind to.
					std::string port;// = std::getenv("Stormancer_Server_Port");

					if (tryGetEnvironmentVariable("Stormancer_Server_Port", port))
					{
						_logger->log(Stormancer::LogLevel::Info, "initialization", "Stormancer_Server_Port set", port);
						_config->port = std::atoi(port.c_str());
					}

					/// Stormancer.Server.PublishedEndpoint	: The public endpoint the players can use to communicate with this server.
					std::string publishedAddresses;// = std::getenv("Stormancer_Server_PublishedAddresses");
					if (tryGetEnvironmentVariable("Stormancer_Server_PublishedAddresses", publishedAddresses))
					{
						_logger->log(Stormancer::LogLevel::Info, "initialization", "Stormancer_Server_PublishedAddresses set", publishedAddresses);
						//If there is a published address, the peer is directly reachable. We disable nat traversal. 
						_config->enableNatPunchthrough = false;

						std::istringstream f(publishedAddresses);
						std::string s;
						while (getline(f, s, ',')) {

							_config->publishedAddresses.push_back(s);
						}
						/// Stormancer.Server.PublishedPort			: The public port mapped to the port the client is bound to.
						std::string publishedPort;// = std::getenv("Stormancer_Server_PublishedPort");

						if (tryGetEnvironmentVariable("Stormancer_Server_PublishedPort", publishedPort))
						{
							_logger->log(Stormancer::LogLevel::Info, "initialization", "Stormancer_Server_PublishedPort set", publishedPort);
							_config->publishedPort = std::atoi(publishedPort.c_str());
						}
						else
						{
							_config->publishedPort = _config->port;
						}

					}

					/// Stormancer.Server.AuthenticationToken	: Server authentication token.
					std::string authTokenStr;// = std::getenv("Stormancer_Server_AuthenticationToken");

					if (tryGetEnvironmentVariable("Stormancer_Server_AuthenticationToken", authTokenStr))
					{
						_logger->log(Stormancer::LogLevel::Info, "initialization", "Stormancer_Server_AuthenticationToken set", "*******");
						this->authToken = authTokenStr;
					}

					/// Stormancer.Server.AuthenticationToken	: Server authentication token.
					std::string accountStr;// = std::getenv("Stormancer_Server_Account");

					if (tryGetEnvironmentVariable("Stormancer_Server_Account", accountStr))
					{
						_logger->log(Stormancer::LogLevel::Info, "initialization", "Stormancer_Server_Account set", accountStr);
						_config->account = accountStr;
					}

					/// Stormancer.Server.AuthenticationToken	: Server authentication token.
					std::string appStr;// = std::getenv("Stormancer_Server_Application");

					if (tryGetEnvironmentVariable("Stormancer_Server_Application", appStr))
					{
						_logger->log(Stormancer::LogLevel::Info, "initialization", "Stormancer_Server_Application set", appStr);
						_config->application = appStr;
					}

					_logger->log(Stormancer::LogLevel::Info, "initialization", "Env loaded.");

				}

			public:
				std::string authToken = "";
				bool getConfigFromEnvironmentVariables = false;

			private:
				bool tryGetEnvironmentVariable(const char* key, std::string& result)
				{
#if _WIN32
					char* buffer = nullptr;
					size_t size = 0;
					if (_dupenv_s(&buffer, &size, key) == 0 && buffer != nullptr)
					{
						result = buffer;
						return true;
						free(buffer);
					}
					else
					{
						return false;
					}

#else
					char* buffer = std::getenv(key);
					if (buffer != nullptr)
					{
						result = buffer;
						return true;
					}
					else
					{
						return false;
					}
#endif
				}
				std::shared_ptr<Stormancer::Configuration> _config;
				std::shared_ptr<Stormancer::ILogger> _logger;
			};
			class ServerPoolsService :public std::enable_shared_from_this<ServerPoolsService>
			{
				friend class ::Stormancer::ServerPools::ServerPoolsPlugin;

			public:
				ServerPoolsService(std::shared_ptr<Stormancer::RpcService> rpc)
					: _rpcService(rpc)

				{}

				template<typename T>
				pplx::task<GameSessionStartupParameters<T>> waitGameSession()
				{
					auto rpc = _rpcService.lock();
					if (!rpc)
					{
						throw ::Stormancer::ObjectDeletedException("RpcService");
					}
					return rpc->rpc<GameSessionStartupParameters<T>>("ServerPool.WaitGameSession");
				}

				//Event fired when Stormancer requests a status update from the server
				std::function<Stormancer::ServerPools::Status()> getStatusCallback;


				Subscription subscribeShutdownReceived(std::function<void()> callback)
				{
					return shutdownReceived.subscribe(callback);
				}

			private:

				std::weak_ptr<Stormancer::RpcService> _rpcService;

				//Initializes the service
				void initialize(std::shared_ptr<Stormancer::Scene> scene)
				{
					//Capture a weak pointer of this in the route handler to make sure that:
					//* We don't prevent this from being destroyed (capturing a shared pointer)
					//* If destroyed, we don't try to use it in the handler (capturing a this reference directly)
					std::weak_ptr<ServerPoolsService> wService = this->shared_from_this();
					scene->addRoute("ServerPool.Shutdown", [wService](Stormancer::Packetisp_ptr)
						{
							auto service = wService.lock();
							//If service is valid, forward the event.
							if (service)
							{
								service->shutdownReceived();
							}
						});

					auto rpc = LockOrThrow(_rpcService);

					rpc->addProcedure("ServerPool.GetStatus", [wService](Stormancer::RpcRequestContext_ptr ctx)
						{
							auto service = wService.lock();
							if (!service)
							{
								ctx->sendValueTemplated(Stormancer::ServerPools::Status::Complete);
							}
							if (!service->getStatusCallback)
							{
								ctx->sendValueTemplated(Stormancer::ServerPools::Status::Unknown);
							}
							return pplx::task_from_result();
						});
				}

				//Event fired when the service client receives a shutdown request
				Stormancer::Event<> shutdownReceived;

			};

			class AgentBasedServerAuthenticationHandler : public Stormancer::Users::IAuthenticationProvider
			{
			public:
				AgentBasedServerAuthenticationHandler(std::shared_ptr<ServerPoolConfiguration> config, std::shared_ptr<ILogger> logger)
					: _config(config)
					, _logger(logger)
				{}

				std::string getProviderName() const
				{
					return Stormancer::ServerPools::ServerPoolsConstants::AgentBasedAuthProvider;
				}

				virtual pplx::task<void> retrieveCredentials(const Stormancer::Users::CredentialsContext& ctx)
				{
					if (ctx.tryUseProvider(Stormancer::ServerPools::ServerPoolsConstants::AgentBasedAuthProvider))
					{
						_logger->log(LogLevel::Info, "auth.dedicatedServer", "Auth token found. Authenticated as a game server");
						ctx.authParameters->type = Stormancer::ServerPools::ServerPoolsConstants::AgentBasedAuthProvider;
						ctx.authParameters->parameters["token"] = _config->authToken;

					}
					return pplx::task_from_result();
				}

			private:
				std::shared_ptr<ServerPoolConfiguration> _config;
				std::shared_ptr<ILogger> _logger;
			};

			class DevServerAuthenticationHandler : public Stormancer::Users::IAuthenticationProvider
			{
			public:
				DevServerAuthenticationHandler(std::shared_ptr<ILogger> logger)
					: _logger(logger)
				{}

				std::string getProviderName() const
				{
					return Stormancer::ServerPools::ServerPoolsConstants::DevAuthProvider;
				}

				virtual pplx::task<void> retrieveCredentials(const Stormancer::Users::CredentialsContext& ctx)
				{

					if (ctx.tryUseProvider(Stormancer::ServerPools::ServerPoolsConstants::DevAuthProvider))
					{
						_logger->log(LogLevel::Info, "auth.dedicatedServer", "Dev game server");
						ctx.authParameters->type = Stormancer::ServerPools::ServerPoolsConstants::DevAuthProvider;
					}
					return pplx::task_from_result();
				}

			private:
				std::shared_ptr<ServerPoolConfiguration> _config;
				std::shared_ptr<ILogger> _logger;
			};
		}

		class ServerPools : public Stormancer::ClientAPI<ServerPools, details::ServerPoolsService>
		{
			friend class ServerPoolsPlugin;

		public:

			ServerPools(
				std::weak_ptr<Stormancer::Users::UsersApi> auth,
				std::weak_ptr<Stormancer::ServerPools::details::ServerPoolConfiguration> config,
				std::weak_ptr<Stormancer::GameSessions::GameSessionApi> gamesession
				, std::shared_ptr<ILogger> logger)
				: Stormancer::ClientAPI<ServerPools, details::ServerPoolsService>(auth, "stormancer.plugins.serverPool")
				, _config(config)
				, _gameSessions(gamesession)
				, _logger(logger)
			{}

			const char* getAuthProvider()
			{
				if (auto config = _config.lock())
				{
					if (config->authToken.size() > 0)
					{

						return  Stormancer::ServerPools::ServerPoolsConstants::AgentBasedAuthProvider;
					}
					else
					{

						return  Stormancer::ServerPools::ServerPoolsConstants::DevAuthProvider;
					}
				}
				else
				{
					throw Stormancer::ObjectDeletedException("config");
				}
			}

			template<typename T>
			STORM_NODISCARD pplx::task<Stormancer::GameSessions::GameSessionConnectionArgs> waitGameSession()
			{
				return this->getService()
					.then([](std::shared_ptr<details::ServerPoolsService> service)
						{
							return service->waitGameSession<T>();
						})
					.then([gs = _gameSessions](GameSessionStartupParameters<T> p)
						{
							if (auto s = gs.lock())
							{
								return s->connectToGameSession(p.gameSessionConnectionToken).waitConnected();
							}
							else
							{
								throw Stormancer::ObjectDeletedException("gameSession");
							}
						});
			}

			Subscription subscribeShutdownReceived(std::function<void()> callback)
			{
				return shutdownReceived.subscribe(callback);
			}

			void setGetStatusCallback(std::function<Stormancer::ServerPools::Status()> callback)
			{
				getStatusCallback = callback;
			}

			bool isServer()
			{
				if (auto config = this->_config.lock())
				{
					return config->isServer;
				}
				else
				{
					return false;
				}
			}


		private:

			void onConnecting(std::shared_ptr <details::ServerPoolsService> service)
			{
				std::weak_ptr<ServerPools> wThis = this->shared_from_this();
				//Always capture weak references, and NEVER 'this'. As the callback is going to be executed asynchronously,
				//who knows what may have happened to the object behind the this pointer since it was captured?
				shutdownReceivedSubscription = service->subscribeShutdownReceived([wThis]()
					{
						auto that = wThis.lock();
						//If this is valid, forward the event.
						if (that)
						{
							that->shutdownReceived();
						}
					});
				service->getStatusCallback = [wThis]()
					{
						auto that = wThis.lock();
						if (that && that->getStatusCallback)
						{
							return that->getStatusCallback();
						}
						else
						{
							return Stormancer::ServerPools::Status::Unknown;
						}
					};
			}

			void onDisconnecting(std::shared_ptr <details::ServerPoolsService>)
			{
				//Unsubscribe by destroying the subscription
				shutdownReceivedSubscription = nullptr;
			}

			Stormancer::Event<std::string>::Subscription shutdownReceivedSubscription;
			//Event fired when Stormancer requests a status update from the server
			std::function<Stormancer::ServerPools::Status()> getStatusCallback;
			std::weak_ptr<Stormancer::ServerPools::details::ServerPoolConfiguration> _config;
			Stormancer::Event<> shutdownReceived;

			std::weak_ptr<Stormancer::GameSessions::GameSessionApi> _gameSessions;
			std::shared_ptr<ILogger> _logger;
		};

		class ServerPoolsPlugin : public Stormancer::IPlugin
		{
		public:

			static constexpr const char* PLUGIN_NAME = "ServerPools";
			static constexpr const char* PLUGIN_VERSION = "1.0.0";

			PluginDescription getDescription() override
			{
				return PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
			}

		private:

			void registerSceneDependencies(Stormancer::ContainerBuilder& builder, std::shared_ptr<Stormancer::Scene> scene) override
			{
				auto name = scene->getHostMetadata("stormancer.serverPool");

				if (!name.empty())
				{
					builder.registerDependency<details::ServerPoolsService, RpcService>().singleInstance();
				}
			}

			void registerClientDependencies(Stormancer::ContainerBuilder& builder) override
			{
				builder.registerDependency<ServerPools, Stormancer::Users::UsersApi, details::ServerPoolConfiguration, Stormancer::GameSessions::GameSessionApi, Stormancer::ILogger>().singleInstance();
				builder.registerDependency<details::ServerPoolConfiguration, Stormancer::Configuration, Stormancer::ILogger>().singleInstance();
				builder.registerDependency<details::AgentBasedServerAuthenticationHandler, details::ServerPoolConfiguration, Stormancer::ILogger>().as<Stormancer::Users::IAuthenticationProvider>();
				builder.registerDependency<details::DevServerAuthenticationHandler, Stormancer::ILogger>().as<Stormancer::Users::IAuthenticationProvider>();
			}

			void sceneCreated(std::shared_ptr<Scene> scene) override
			{
				auto name = scene->getHostMetadata("stormancer.serverPool");

				if (!name.empty())
				{
					auto service = scene->dependencyResolver().resolve<details::ServerPoolsService>();
					service->initialize(scene);
				}
			}

			void sceneConnecting(std::shared_ptr<Scene> scene) override
			{
				auto name = scene->getHostMetadata("stormancer.serverPool");

				if (!name.empty())
				{
					auto pools = scene->dependencyResolver().resolve<ServerPools>();
					auto service = scene->dependencyResolver().resolve<details::ServerPoolsService>();
					pools->onConnecting(service);
				}
			}

			void sceneDisconnecting(std::shared_ptr<Scene> scene) override
			{
				auto name = scene->getHostMetadata("stormancer.serverPool");

				if (!name.empty())
				{
					auto pools = scene->dependencyResolver().resolve<ServerPools>();
					auto service = scene->dependencyResolver().resolve<details::ServerPoolsService>();
					pools->onDisconnecting(service);
				}
			}

			void clientCreating(std::shared_ptr<IClient> client) override
			{
				auto config = client->dependencyResolver().resolve<details::ServerPoolConfiguration>();

				//Applies the plugin config to the client configuration.
				config->applyConfig();


			}

			void clientCreated(std::shared_ptr<IClient> client) override
			{

			}
		};
	}
}

STRM_MSGPACK_ADD_ENUM(Stormancer::ServerPools::Status);