#pragma once

#include "Friends/Friends.hpp"
#include "Party/Party.hpp"
#include "Users/Users.hpp"

#include "stormancer/Configuration.h"
#include "stormancer/IPlugin.h"
#include "stormancer/IScheduler.h"
#include "stormancer/StormancerTypes.h"
#include "stormancer/Utilities/PointerUtilities.h"
#include "stormancer/Utilities/TaskUtilities.h"

#include "galaxy/GalaxyApi.h"
#include "galaxy/GalaxyExceptionHelper.h"

namespace Stormancer
{
	namespace Galaxy
	{
		static constexpr const char* platformName = "galaxy";

		namespace ConfigurationKeys
		{
			/// <summary>
			/// Should Stormancer initialize the Galaxy platform and call periodically EOS_Platform_Tick().
			/// Default is "false".
			/// Use "true" to enable, and set the ProuctName and ProductVersion.
			/// </summary>
			constexpr const char* InitPlatform = "galaxy.initPlatform";

			/// <summary>
			/// Enable the Galaxy authentication.
			/// If disabled, the Galaxy plugin will not initiate any authentications.
			/// Default is "true".
			/// Use "false" to disable.
			/// </summary>
			constexpr const char* AuthenticationEnabled = "galaxy.authentication.enabled";

			/// <summary>
			/// Galaxy client Client Id.
			/// </summary>
			constexpr const char* ClientId = "galaxy.clientId";

			/// <summary>
			/// Galaxy client Client Secret.
			/// </summary>
			constexpr const char* ClientSecret = "galaxy.clientSecret";
		};

		using UserId = std::string;

		class IGalaxyApi
		{
		public:

			static constexpr const char* METADATA_KEY = "stormancer.plugins.galaxy";

			virtual ~IGalaxyApi() = default;

			virtual void initialize() = 0;
		};

		class GalaxyPlatformUserId : public Users::PlatformUserId
		{
		public:

			std::string type() const override
			{
				return platformName;
			}

			static std::shared_ptr<GalaxyPlatformUserId> create(UserId accountId)
			{
				// No make_shared because this class constructor is private
				return std::shared_ptr<GalaxyPlatformUserId>(new GalaxyPlatformUserId(accountId));
			}

			static std::shared_ptr<GalaxyPlatformUserId> tryCast(std::shared_ptr<Users::PlatformUserId> id)
			{
				if (id != nullptr && id->type() == platformName)
				{
					return std::static_pointer_cast<GalaxyPlatformUserId>(id);
				}
				return nullptr;
			}

			UserId getUserId()
			{
				return _userId;
			}

			bool operator==(const GalaxyPlatformUserId& right)
			{
				return _userId == right._userId;
			}

			bool operator!=(const GalaxyPlatformUserId& right)
			{
				return _userId != right._userId;
			}

		private:

			GalaxyPlatformUserId(UserId userId)
				: PlatformUserId(userId)
				, _userId(userId)
			{
			}

			const UserId _userId;
		};

		class GalaxyState
		{
		public:

			GalaxyState(std::shared_ptr<Configuration> config, std::shared_ptr<ILogger> logger)
				: _logger(logger)
			{
				std::lock_guard<std::recursive_mutex> lg(_mutex);

				_initPlatform = config->additionalParameters.find(ConfigurationKeys::InitPlatform) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::InitPlatform) != "false" : true;
				_authenticationEnabled = config->additionalParameters.find(ConfigurationKeys::AuthenticationEnabled) != config->additionalParameters.end() ? (config->additionalParameters.at(ConfigurationKeys::AuthenticationEnabled) != "false") : true;
				_clientId = config->additionalParameters.find(ConfigurationKeys::ClientId) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::ClientId) : "";
				_clientSecret = config->additionalParameters.find(ConfigurationKeys::ClientSecret) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::ClientSecret) : "";
			}

			virtual ~GalaxyState()
			{
				std::lock_guard<std::recursive_mutex> lg(_mutex);
			}

			bool getInitPlatform() const
			{
				std::lock_guard<std::recursive_mutex> lg(_mutex);

				return _initPlatform;
			}

			bool getAuthenticationEnabled() const
			{
				std::lock_guard<std::recursive_mutex> lg(_mutex);

				return _authenticationEnabled;
			}

			std::string getClientId() const
			{
				std::lock_guard<std::recursive_mutex> lg(_mutex);

				return _clientId;
			}

			std::string getClientSecret() const
			{
				std::lock_guard<std::recursive_mutex> lg(_mutex);

				return _clientSecret;
			}

			void setStormancerInitializedPlatform(bool stormancerInitializedPlatform)
			{
				_stormancerInitializedPlatform = stormancerInitializedPlatform;
			}

			bool getStormancerInitializedPlatform() const
			{
				return _stormancerInitializedPlatform;
			}

		private:

			mutable std::recursive_mutex _mutex;
			bool _initPlatform = true;
			bool _stormancerInitializedPlatform = false;
			bool _authenticationEnabled = true;
			std::string _clientId;
			std::string _clientSecret;
			std::shared_ptr<ILogger> _logger;
		};

		class GalaxyTicker : public std::enable_shared_from_this<GalaxyTicker>
		{
		public:

			GalaxyTicker(std::shared_ptr<Configuration> config, std::shared_ptr<GalaxyState> galaxyState, std::shared_ptr<ILogger> logger)
				: _wActionDispatcher(config->actionDispatcher)
				, _logger(logger)
			{
			}

			void start()
			{
				_stopTicker = false;

				if (_stoppedTicker)
				{
					_stoppedTicker = false;

					if (auto actionDispatcher = _wActionDispatcher.lock())
					{
						auto wGalaxyTicker = STORM_WEAK_FROM_THIS();
						actionDispatcher->post([wGalaxyTicker]()
						{
							if (auto galaxyTicker = wGalaxyTicker.lock())
							{
								galaxyTicker->scheduleTick();
							}
						});
					}
				}
			}

			void stop()
			{
				_stopTicker = true;
			}

		private:

			void scheduleTick()
			{
				galaxy::api::ProcessData();

				if (auto actionDispatcher = _wActionDispatcher.lock())
				{
					auto wGalaxyTicker = STORM_WEAK_FROM_THIS();
					actionDispatcher->post([wGalaxyTicker]()
					{
						if (auto galaxyTicker = wGalaxyTicker.lock())
						{
							if (galaxyTicker->_stopTicker)
							{
								galaxyTicker->_stoppedTicker = true;
							}
							else
							{
								galaxyTicker->scheduleTick();
							}
						}
					});
				}
				else
				{
					_stoppedTicker = true;
				}
			}

			std::weak_ptr<IActionDispatcher> _wActionDispatcher;
			std::shared_ptr<ILogger> _logger;

			bool _stopTicker = false;
			bool _stoppedTicker = true;
		};

		class GalaxyService : public std::enable_shared_from_this<GalaxyService>
		{
		public:

			GalaxyService(std::shared_ptr<Scene> scene)
				: _rpcService(scene->dependencyResolver().resolve<RpcService>())
			{
			}

		private:

			std::shared_ptr<RpcService> _rpcService;
		};

		class GalaxyApi : public ClientAPI<GalaxyApi, GalaxyService>, public IGalaxyApi
		{
		public:

#pragma region public_methods

			GalaxyApi(std::shared_ptr<Users::UsersApi> usersApi, std::shared_ptr<GalaxyState> galaxyState, std::shared_ptr<Configuration> config, std::shared_ptr<IScheduler> scheduler, std::shared_ptr<ILogger> logger, std::shared_ptr<Party::PartyApi> partyApi)
				: ClientAPI(usersApi, "stormancer.galaxy")
				, _galaxyState(galaxyState)
				, _wScheduler(scheduler)
				, _wActionDispatcher(config->actionDispatcher)
				, _logger(logger)
				, _wUsersApi(usersApi)
				, _wPartyApi(partyApi)
			{
			}

			~GalaxyApi() = default;

			void initialize() override
			{
				if (_galaxyState->getInitPlatform())
				{
					std::string clientId = _galaxyState->getClientId();
					std::string clientSecret = _galaxyState->getClientSecret();
					galaxy::api::InitOptions options(clientId.c_str(), clientSecret.c_str());
					try
					{
						galaxy::api::Init(options);
					}
					catch (const galaxy::api::IError& error)
					{
						_logger->log(LogLevel::Error, error.GetName(), error.GetMsg(), std::to_string((int32_t)error.GetType()));
					}
					_galaxyState->setStormancerInitializedPlatform(true);
				}
			}

		private:

			std::shared_ptr<ILogger> _logger;
			std::shared_ptr<GalaxyState> _galaxyState;
			std::weak_ptr<IScheduler> _wScheduler;
			std::weak_ptr<IActionDispatcher> _wActionDispatcher;
			std::weak_ptr<Users::UsersApi> _wUsersApi;
			std::weak_ptr<Party::PartyApi> _wPartyApi;

		};

		class GalaxyAuthenticationEventHandler : public std::enable_shared_from_this<GalaxyAuthenticationEventHandler>, public Users::IAuthenticationEventHandler, public galaxy::api::IEncryptedAppTicketListener, public galaxy::api::IAuthListener
		{
		public:

			GalaxyAuthenticationEventHandler(std::shared_ptr<GalaxyState> galaxyState, std::shared_ptr<Configuration> config, std::shared_ptr<ILogger> logger)
				: _galaxyState(galaxyState)
				, _wActionDispatcher(config->actionDispatcher)
				, _logger(logger)
			{
			}

			pplx::task<void> retrieveCredentials(const Users::CredentialsContext& context) override
			{
				return getGalaxyCredentials([context](std::string type, std::string provider, std::string ticket)
				{
					context.authParameters->type = type;
					context.authParameters->parameters["provider"] = provider;
					context.authParameters->parameters["ticket"] = ticket;
				});
			}

			pplx::task<void> renewCredentials(const Users::CredentialsRenewalContext& context) override
			{
				return getGalaxyCredentials([context](std::string type, std::string provider, std::string ticket)
				{
					context.response->parameters["provider"] = provider;
					context.response->parameters["ticket"] = ticket;
				});
			}

			pplx::task<void> getGalaxyCredentials(std::function<void(std::string type, std::string provider, std::string accessToken)> fulfillCredentialsCallback)
			{
				if (!_galaxyState->getAuthenticationEnabled())
				{
					return pplx::task_from_result();
				}

				std::lock_guard<std::recursive_mutex> lg(_mutex);

				_authTce = std::make_shared<pplx::task_completion_event<void>>();
				_ticketTce = std::make_shared<pplx::task_completion_event<std::string>>();

				auto user = galaxy::api::User();
				if (user == nullptr)
				{
					throw std::runtime_error("Galaxy User is not available");
				}

				user->SignInGalaxy(true, this);

				return pplx::create_task(*_authTce)
					.then([fulfillCredentialsCallback, wGalaxyAuthEvtHandler = STORM_WEAK_FROM_THIS()]()
				{
					auto galaxyAuthEvtHandler = LockOrThrow(wGalaxyAuthEvtHandler, "GalaxyAuthenticationEventHandler");

					auto user = galaxy::api::User();
					if (user == nullptr)
					{
						throw std::runtime_error("Galaxy User is not available");
					}

					bool signedIn = user->SignedIn();
					bool loggedIn = user->IsLoggedOn();

					if (!signedIn)
					{
						throw std::runtime_error("Galaxy User is not logged in or doesn't have a license of the game");
					}
					else if (!loggedIn)
					{
						throw std::runtime_error("Galaxy User logged in and offline");
					}

					user->RequestEncryptedAppTicket(nullptr, 0, &*galaxyAuthEvtHandler);

					std::lock_guard<std::recursive_mutex> lg(galaxyAuthEvtHandler->_mutex);

					return pplx::create_task(*galaxyAuthEvtHandler->_ticketTce);
				})
					.then([fulfillCredentialsCallback](std::string ticket)
				{
					fulfillCredentialsCallback(platformName, platformName, ticket);
				});
			}

		private:

			void OnAuthSuccess() override
			{
				std::lock_guard<std::recursive_mutex> lg(_mutex);

				_authTce->set();
			}

			void OnAuthFailure(galaxy::api::IAuthListener::FailureReason failureReason) override
			{
				std::lock_guard<std::recursive_mutex> lg(_mutex);

				_authTce->set_exception(std::runtime_error("Galaxy auth failed : failureReason = " + std::to_string((int32_t)failureReason)));
			}

			void OnAuthLost() override
			{
				std::lock_guard<std::recursive_mutex> lg(_mutex);

				_ticketTce->set_exception(std::runtime_error("Galaxy auth lost"));
			}

			void OnEncryptedAppTicketRetrieveSuccess() override
			{
				auto user = galaxy::api::User();
				if (user == nullptr)
				{
					throw std::runtime_error("Galaxy User not available");
				}

				const uint32_t encryptedAppTicketSizeMax = 1024;
				char encryptedAppTicketData[encryptedAppTicketSizeMax] = { 0 };
				uint32_t encryptedAppTicketSize = 0;
				user->GetEncryptedAppTicket(encryptedAppTicketData, encryptedAppTicketSizeMax, encryptedAppTicketSize);

				std::lock_guard<std::recursive_mutex> lg(_mutex);

				_ticketTce->set(std::string(encryptedAppTicketData, encryptedAppTicketSize));
			}

			void OnEncryptedAppTicketRetrieveFailure(galaxy::api::IEncryptedAppTicketListener::FailureReason failureReason) override
			{
				std::lock_guard<std::recursive_mutex> lg(_mutex);

				_ticketTce->set_exception(std::runtime_error("Galaxy ticket retrieve failed : failureReason = " + std::to_string((int32_t)failureReason)));
			}

			std::recursive_mutex _mutex;
			std::shared_ptr<GalaxyState> _galaxyState;
			std::shared_ptr<ILogger> _logger;
			std::weak_ptr<IActionDispatcher> _wActionDispatcher;
			std::shared_ptr<pplx::task_completion_event<void>> _authTce; // shared_ptr used as an optional
			std::shared_ptr<pplx::task_completion_event<std::string>> _ticketTce; // shared_ptr used as an optional
		};

		class GalaxyPlugin : public IPlugin
		{
		public:

			static constexpr const char* PLUGIN_NAME = "Galaxy";
			static constexpr const char* PLUGIN_VERSION = "1.0.0";

			PluginDescription getDescription() override
			{
				return PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
			}

		private:

			void registerClientDependencies(ContainerBuilder& builder) override
			{
				builder.registerDependency<GalaxyState, Configuration, ILogger>().singleInstance();
				builder.registerDependency<GalaxyApi, Users::UsersApi, GalaxyState, Configuration, IScheduler, ILogger, Party::PartyApi>().asSelf().as<IGalaxyApi>();
				builder.registerDependency<GalaxyAuthenticationEventHandler, GalaxyState, Configuration, ILogger>().as<Users::IAuthenticationEventHandler>();
			}

			void clientCreated(std::shared_ptr<IClient> client) override
			{
				auto galaxyApi = client->dependencyResolver().resolve<IGalaxyApi>();
				galaxyApi->initialize();

				auto galaxyState = client->dependencyResolver().resolve<GalaxyState>();
				auto config = client->dependencyResolver().resolve<Configuration>();
				auto logger = client->dependencyResolver().resolve<ILogger>();
				_galaxyTicker = std::make_shared<GalaxyTicker>(config, galaxyState, logger);
				_galaxyTicker->start();
			}

			void clientDisconnecting(std::shared_ptr<IClient> client) override
			{
				auto galaxyState = client->dependencyResolver().resolve<GalaxyState>();
				if (galaxyState->getStormancerInitializedPlatform())
				{
					galaxy::api::Shutdown();
				}
				_galaxyTicker->stop();
			}

			void registerSceneDependencies(ContainerBuilder& builder, std::shared_ptr<Scene> scene) override
			{
				if (scene->getHostMetadata(IGalaxyApi::METADATA_KEY).length() > 0)
				{
					builder.registerDependency<GalaxyService, Scene>();
				}
			}

			std::shared_ptr<GalaxyTicker> _galaxyTicker;
		};
	};
};
