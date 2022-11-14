#pragma once

#include "Users/ClientAPI.hpp"
#include "Users/Users.hpp"

#include "stormancer/Configuration.h"
#include "stormancer/Scene.h"
#include "stormancer/IPlugin.h"

#include <mutex>

namespace Stormancer
{
	namespace GameVersion
	{
		bool IsBadGameVersionError(const std::string& authError)
		{
			size_t startPos = authError.find("badGameVersion");
			return startPos != std::string::npos;
		}

		/// <summary>
		/// Keys to use in Configuration::additionalParameters map to customize the plugin behavior.
		/// </summary>
		namespace ConfigurationKeys
		{
			/// <summary>
			/// Game version used by the client and sent to the server for comparison.
			/// </summary>
			constexpr const char* ClientVersion = "gameVersion.clientVersion";
		}

		class BadVersionException : public std::exception
		{
		public:
			const char* what() const noexcept override
			{
				return _message.c_str();
			}

			const std::string& expectedVersion() const
			{
				return _expectedVersion;
			}

			BadVersionException(std::string expectedVersion)
				: _expectedVersion(expectedVersion)
				, _message("Bad client version: the server expected '" + _expectedVersion + "'")
			{}

		private:
			std::string _expectedVersion;
			std::string _message;
		};

		namespace detail
		{
			class GameVersionService
			{
			public:
				GameVersionService(std::shared_ptr<Scene> scene)
				{
					scene->addRoute("gameVersion.update", [this](Packetisp_ptr packet)
					{
						Serializer serializer;
						auto version = serializer.deserializeOne<std::string>(packet->stream);

						if (_onGameVersionUpdate)
						{
							_onGameVersionUpdate(version);
						}
					});

					scene->addRoute("serverVersion.update", [this](Packetisp_ptr packet)
					{
						Serializer serializer;
						auto version = serializer.deserializeOne<std::string>(packet->stream);

						if (_onServerVersionUpdate)
						{
							_onServerVersionUpdate(version);
						}
					});
				}

				void onGameVersionUpdate(std::function<void(std::string)> callback)
				{
					_onGameVersionUpdate = callback;
				}

				void onServerVersionUpdate(std::function<void(std::string)> callback)
				{
					_onServerVersionUpdate = callback;
				}

			private:

				std::function<void(std::string)> _onGameVersionUpdate;
				std::function<void(std::string)> _onServerVersionUpdate;
			};

			class AuthEventHandler : public Users::IAuthenticationEventHandler
			{
			public:

				AuthEventHandler(std::shared_ptr<Configuration> configuration)
					: _configuration(configuration)
				{
				}

			private:

				pplx::task<void> retrieveCredentials(const Users::CredentialsContext& context) override
				{
					if (_configuration->additionalParameters.count(ConfigurationKeys::ClientVersion) == 1)
					{
						auto& clientVersion = _configuration->additionalParameters[ConfigurationKeys::ClientVersion];
						context.authParameters->parameters[ConfigurationKeys::ClientVersion] = clientVersion;
						_configuration->logger->log(LogLevel::Trace, "GameVersion", std::string("'") + ConfigurationKeys::ClientVersion + "' is set to '" + clientVersion + "'");
					}
					else
					{
						_configuration->logger->log(LogLevel::Warn, "GameVersion", std::string("Entry '") + ConfigurationKeys::ClientVersion + "' was not found in Configuration::additionalParameters. You should set this value to the game client's version if server - side version checking is enabled.");
					}

					return pplx::task_from_result();
				}

				void onLoginFailed(Users::LoginFailureContext& context) override
				{
					size_t startPos = context.errorMessage.find("badGameVersion");
					if (startPos == std::string::npos)
					{
						return;
					}

					std::string serverVersion;
					startPos = context.errorMessage.find("?", startPos);
					if (startPos != std::string::npos)
					{
						startPos = context.errorMessage.find("serverVersion=", startPos);
						if (startPos != std::string::npos)
						{
							startPos += sizeof("serverVersion=") - 1;
							if (startPos < context.errorMessage.length())
							{
								size_t end = context.errorMessage.find("&", startPos);
								if (end == std::string::npos)
								{
									end = context.errorMessage.length();
								}
								serverVersion = context.errorMessage.substr(startPos, end - startPos);
							}
						}
					}

					BadVersionException ex(serverVersion);
					context.customException = std::make_exception_ptr(ex);
				}

				std::shared_ptr<Configuration> _configuration;
			};
		}

		class GameVersionPlugin;

		class GameVersionApi : public std::enable_shared_from_this<GameVersionApi>
		{
		public:
			GameVersionApi(ILogger_ptr logger)
				: _logger(logger)
			{}

			std::string getGameVersion() const
			{
				std::lock_guard<std::mutex> lg(_gameVersionMutex);

				return _currentGameVersion;
			}

			Subscription subscribeToGameVersionUpdate(std::function<void(std::string)> callback)
			{
				return _onGameVersionUpdated.subscribe(callback);
			}

			Subscription subscribeToServerVersionUpdate(std::function<void(std::string)> callback)
			{
				return _onServerVersionUpdated.subscribe(callback);
			}

		private:
			void sceneCreated(std::shared_ptr<Scene> scene)
			{
				if (auto currentScene = _scene.lock())
				{
					_logger->log(LogLevel::Warn, "GameVersionApi::sceneCreated", "GameVersion supports only a single scene. Current scene: " + currentScene->id() + ", new scene: " + scene->id() + ". Ignoring the new scene.");
					return;
				}

				_scene = scene;
				auto service = scene->dependencyResolver().resolve<detail::GameVersionService>();

				std::weak_ptr<GameVersionApi> wThat = this->shared_from_this();
				service->onGameVersionUpdate([wThat](std::string newVersion)
				{
					if (auto that = wThat.lock())
					{
						std::lock_guard<std::mutex> lg(that->_gameVersionMutex);

						that->_currentGameVersion = newVersion;
						that->_onGameVersionUpdated(newVersion);
					}
				});
				service->onServerVersionUpdate([wThat](std::string newVersion)
				{
					if (auto that = wThat.lock())
					{
						that->_onServerVersionUpdated(newVersion);
					}
				});
			}

			ILogger_ptr _logger;
			Event<std::string> _onGameVersionUpdated;
			Event<std::string> _onServerVersionUpdated;
			std::string _currentGameVersion = "unknown";
			mutable std::mutex _gameVersionMutex;
			// GameVersion is not responsible for the scene's lifetime.
			// It is a "passive" plugin, in the sense that the connection to the scene cannot be triggered through interaction with GameVersionApi, thus happens always indirectly.
			std::weak_ptr<Scene> _scene;

			friend class GameVersionPlugin;
		};

		class GameVersionPlugin : public IPlugin
		{
		public:

			static constexpr const char* PLUGIN_NAME = "GameVersion";
			static constexpr const char* PLUGIN_VERSION = "1.0.0";

			PluginDescription getDescription() override
			{
				return PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
			}

		private:

			void registerSceneDependencies(ContainerBuilder& builder, std::shared_ptr<Scene> scene) override
			{
				auto name = scene->getHostMetadata("stormancer.gameVersion");

				if (!name.empty())
				{
					builder.registerDependency<detail::GameVersionService, Scene>().singleInstance();
				}
			}

			void sceneCreated(std::shared_ptr<Scene> scene) override
			{
				auto name = scene->getHostMetadata("stormancer.gameVersion");

				if (!name.empty())
				{
					auto api = scene->dependencyResolver().resolve<GameVersionApi>();
					api->sceneCreated(scene);
				}
			}

			void registerClientDependencies(ContainerBuilder& builder) override
			{
				builder.registerDependency<GameVersionApi, ILogger>().singleInstance();
				builder.registerDependency<detail::AuthEventHandler, Configuration>().instancePerRequest().as<Users::IAuthenticationEventHandler>();
			}
		};
	}
}
