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
#include "stormancer/cpprestsdk/cpprest/asyncrt_utils.h"
#include "stormancer/cpprestsdk/cpprest/json.h"
#include "stormancer/cpprestsdk/cpprest/http_client.h"

#include "eos_auth.h"
#include "eos_auth_types.h"
#include "eos_init.h"
#include "eos_sdk.h"
#include "eos_types.h"

// https://dev.epicgames.com/docs/services/en-US/index.html

namespace Stormancer
{
	namespace Epic
	{
		static constexpr const char* platformName = "epic";

		/// <summary>
		/// Keys to use in Configuration::additionalParameters map to customize the Epic plugin behavior.
		/// </summary>
		namespace ConfigurationKeys
		{
			/// <summary>
			/// Enable the Epic authentication.
			/// If disabled, the Epic plugin will not initiate any authentications.
			/// Default is "true".
			/// Use "false" to disable.
			/// </summary>
			constexpr const char* AuthenticationEnabled = "epic.authentication.enabled";

			/// <summary>
			/// Login mode.
			/// Available options :
			/// - "AccountPortal"
			/// - "DevAuth"
			/// </summary>
			constexpr const char* LoginMode = "epic.authentication.loginMode";

			constexpr const char* DevAuthHost = "epic.authentication.devAuth.host";

			constexpr const char* DevAuthCredentialsName = "epic.authentication.devAuth.credentialsName";

			constexpr const char* ProductId = "epic.productId";

			constexpr const char* SandboxId = "epic.sandboxId";

			constexpr const char* DeploymentId = "epic.deploymentId";

			constexpr const char* ClientId = "epic.clientId";

			constexpr const char* ClientSecret = "epic.clientSecret";
		}

		constexpr const char* PARTY_TYPE_EPICGAMESIDLOBBY = "epicIDLobby";

		using AccountId = std::string;
		using AppId = std::string;

		class IEpicApi
		{
		public:

			static constexpr const char* METADATA_KEY = "stormancer.plugins.epic";

			virtual ~IEpicApi() = default;

			virtual void initialize() = 0;

			virtual void setPlatformHandle(EOS_HPlatform platformHandle) = 0;

			virtual void setPlatformHandleOwned(bool owned) = 0;

			virtual EOS_HPlatform getPlatformHandle() = 0;

			virtual EOS_HPlatform getOrCreatePlatformHandle() = 0;
		};

		namespace details
		{
			class EpicPlatformUserId : public Users::PlatformUserId
			{
			public:

				std::string type() const override
				{
					return platformName;
				}

				static std::shared_ptr<EpicPlatformUserId> create(AccountId accountId)
				{
					// No make_shared because this class constructor is private
					return std::shared_ptr<EpicPlatformUserId>(new EpicPlatformUserId(accountId));
				}

				static std::shared_ptr<EpicPlatformUserId> tryCast(std::shared_ptr<Users::PlatformUserId> id)
				{
					if (id != nullptr && id->type() == platformName)
					{
						return std::static_pointer_cast<EpicPlatformUserId>(id);
					}
					return nullptr;
				}

				static std::string toString(EOS_EpicAccountId accountId)
				{
					static char bufData[EOS_EPICACCOUNTID_MAX_LENGTH + 1] = { 0 };
					int32_t bufSize = sizeof(bufData);
					EOS_EResult result = EOS_EpicAccountId_ToString(accountId, bufData, &bufSize);
					if (result != EOS_EResult::EOS_Success)
					{
						throw std::runtime_error("EpicAccountId conversion to string failed (Error " + std::to_string((int32_t)result) + ")");
					}
					return std::string(bufData, bufSize);
				}

				static EOS_EpicAccountId toEpicAccountId(const std::string& accountIdStr)
				{
					if (accountIdStr.size() != EOS_EPICACCOUNTID_MAX_LENGTH)
					{
						throw std::runtime_error("EpicAccountId conversion from string failed (Size=" + std::to_string(accountIdStr.size()) + ")");
					}
					return EOS_EpicAccountId_FromString(accountIdStr.c_str());
				}

				AccountId getAccountId()
				{
					return _accountId;
				}

				bool operator==(const EpicPlatformUserId& right)
				{
					return _accountId == right._accountId;
				}

				bool operator!=(const EpicPlatformUserId& right)
				{
					return _accountId != right._accountId;
				}

			private:

				EpicPlatformUserId(AccountId accountId)
					: PlatformUserId(accountId)
					, _accountId(accountId)
				{
				}

				const AccountId _accountId;
			};

			class EpicConfiguration
			{
			public:

				EpicConfiguration(std::shared_ptr<Configuration> config)
				{
					_authenticationEnabled = config->additionalParameters.find(ConfigurationKeys::AuthenticationEnabled) != config->additionalParameters.end() ? (config->additionalParameters.at(ConfigurationKeys::AuthenticationEnabled) != "false") : true;
					_loginMode = config->additionalParameters.find(ConfigurationKeys::LoginMode) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::LoginMode) : "";
					_devAuthHost = config->additionalParameters.find(ConfigurationKeys::DevAuthHost) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::DevAuthHost) : "";
					_devAuthCredentialsName = config->additionalParameters.find(ConfigurationKeys::DevAuthCredentialsName) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::DevAuthCredentialsName) : "";
					_productId = config->additionalParameters.find(ConfigurationKeys::ProductId) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::ProductId) : "";
					_sandboxId = config->additionalParameters.find(ConfigurationKeys::SandboxId) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::SandboxId) : "";
					_deploymentId = config->additionalParameters.find(ConfigurationKeys::DeploymentId) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::DeploymentId) : "";
					_clientId = config->additionalParameters.find(ConfigurationKeys::ClientId) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::ClientId) : "";
					_clientSecret = config->additionalParameters.find(ConfigurationKeys::ClientSecret) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::ClientSecret) : "";
				}

				bool getAuthenticationEnabled() const
				{
					return _authenticationEnabled;
				}

				std::string getLoginMode() const
				{
					return _loginMode;
				}

				std::string getDevAuthHost() const
				{
					return _devAuthHost;
				}

				std::string getDevAuthCredentialsName() const
				{
					return _devAuthCredentialsName;
				}

				std::string getProductId() const
				{
					return _productId;
				}

				std::string getSandboxId() const
				{
					return _sandboxId;
				}

				std::string getDeploymentId() const
				{
					return _deploymentId;
				}

				std::string getClientId() const
				{
					return _clientId;
				}

				std::string getClientSecret() const
				{
					return _clientSecret;
				}

			private:

				bool _authenticationEnabled = true;
				std::string _loginMode;
				std::string _devAuthHost;
				std::string _devAuthCredentialsName;
				std::string _productId;
				std::string _sandboxId;
				std::string _deploymentId;
				std::string _clientId;
				std::string _clientSecret;
			};

			class EpicService : public std::enable_shared_from_this<EpicService>
			{
			public:

				EpicService(std::shared_ptr<Scene> scene)
					: _rpcService(scene->dependencyResolver().resolve<RpcService>())
				{
				}

			private:

				std::shared_ptr<RpcService> _rpcService;
			};

			class EpicPartyService : public std::enable_shared_from_this<EpicPartyService>
			{
			public:

				EpicPartyService(std::shared_ptr<Scene> scene)
					: _rpcService(scene->dependencyResolver().resolve<RpcService>())
				{
				}

			private:

				std::shared_ptr<RpcService> _rpcService;
			};

			class EpicPartyInvitation : public Party::Platform::IPlatformInvitation
			{
			public:

				EpicPartyInvitation(const std::string& senderId)
					: _senderId(senderId)
				{
				}

				pplx::task<Party::PartyId> accept(std::shared_ptr<Party::PartyApi>) override
				{
					throw std::runtime_error("Not implemented");
				}

				pplx::task<void> decline(std::shared_ptr<Party::PartyApi>) override
				{
					throw std::runtime_error("Not implemented");
				}

				std::string getSenderId() override
				{
					return _senderId;
				}

				std::string getSenderPlatformId() override
				{
					throw std::runtime_error("Not implemented");
				}

				std::string getSenderUsername() override
				{
					throw std::runtime_error("Not implemented");
				}

			private:

				std::string _senderId;
			};

			class EpicPartyProvider;

			class EpicApi : public ClientAPI<EpicApi, EpicService>, public IEpicApi
			{
				friend class EpicPartyProvider;

			public:

#pragma region public_methods

				EpicApi(std::shared_ptr<Users::UsersApi> usersApi, std::shared_ptr<EpicConfiguration> epicConfig, std::shared_ptr<Configuration> config, std::shared_ptr<IScheduler> scheduler, std::shared_ptr<ILogger> logger, std::shared_ptr<Party::PartyApi> partyApi)
					: ClientAPI(usersApi, "stormancer.epic")
					, _epicConfig(epicConfig)
					, _wScheduler(scheduler)
					, _wActionDispatcher(config->actionDispatcher)
					, _logger(logger)
					, _wUsersApi(usersApi)
					, _wPartyApi(partyApi)
				{
				}

				~EpicApi()
				{
					if (_platformHandleOwned)
					{
						_platformHandleOwned = false;
						EOS_Platform_Release(_platformHandle);
						_platformHandle = nullptr;
					}
				}

				void initialize() override
				{
				}

				void setPlatformHandle(EOS_HPlatform platformHandle)
				{
					_platformHandle = platformHandle;
				}

				void setPlatformHandleOwned(bool owned)
				{
					_platformHandleOwned = owned;
				}

				EOS_HPlatform getPlatformHandle()
				{
					return _platformHandle;
				}

				EOS_HPlatform getOrCreatePlatformHandle()
				{
					if (_platformHandle == nullptr)
					{
						EOS_Platform_Options PlatformOptions = {};
						PlatformOptions.ApiVersion = EOS_PLATFORM_OPTIONS_API_LATEST;
						PlatformOptions.bIsServer = false;
//						static constexpr char EncryptionKey[] = "1111111111111111111111111111111111111111111111111111111111111111";
//						PlatformOptions.EncryptionKey = EncryptionKey;
//						PlatformOptions.OverrideCountryCode = nullptr;
//						PlatformOptions.OverrideLocaleCode = nullptr;
//						PlatformOptions.Flags = EOS_PF_WINDOWS_ENABLE_OVERLAY_D3D9 | EOS_PF_WINDOWS_ENABLE_OVERLAY_D3D10 | EOS_PF_WINDOWS_ENABLE_OVERLAY_OPENGL; // Enable overlay support for D3D9/10 and OpenGL. This sample uses D3D11 or SDL.
//#ifdef _WIN32
//						static char Buffer[1024] = { 0 };
//						if (Buffer[0] == 0)
//						{
//							GetTempPathA(sizeof(Buffer), Buffer);
//						}
//						PlatformOptions.CacheDirectory = Buffer;
//#elif defined(__APPLE__)
//						PlatformOptions.CacheDirectory = "/private/var/tmp";
//#else
//						PlatformOptions.CacheDirectory = "/var/tmp";
//#endif
//						EOS_Platform_RTCOptions RtcOptions = { 0 };
//						PlatformOptions.RTCOptions = &RtcOptions;
						PlatformOptions.ProductId = _epicConfig->getProductId().c_str();
						PlatformOptions.SandboxId = _epicConfig->getSandboxId().c_str();
						PlatformOptions.DeploymentId = _epicConfig->getDeploymentId().c_str();
						PlatformOptions.ClientCredentials.ClientId = _epicConfig->getClientId().c_str();
						PlatformOptions.ClientCredentials.ClientSecret = _epicConfig->getClientSecret().c_str();
						PlatformOptions.Reserved = NULL;
						_platformHandle = EOS_Platform_Create(&PlatformOptions);
						_platformHandleOwned = true;
					}

					return _platformHandle;
				}

#pragma endregion

			private:

#pragma region private_methods

#pragma endregion

#pragma region private_members

				std::shared_ptr<ILogger> _logger;
				std::shared_ptr<EpicConfiguration> _epicConfig;
				std::weak_ptr<IScheduler> _wScheduler;
				std::weak_ptr<IActionDispatcher> _wActionDispatcher;
				std::weak_ptr<Users::UsersApi> _wUsersApi;
				std::weak_ptr<Party::PartyApi> _wPartyApi;
				bool _platformHandleOwned = false;
				EOS_HPlatform _platformHandle = nullptr;

#pragma endregion
			};

			class EpicPartyProvider : public Party::Platform::IPlatformSupportProvider
			{
			public:

#pragma region public_methods

				EpicPartyProvider(
					std::shared_ptr<Party::Platform::InvitationMessenger> messenger,
					std::shared_ptr<Users::UsersApi> usersApi,
					std::shared_ptr<details::EpicApi> epicApi,
					std::shared_ptr<ILogger> logger,
					std::shared_ptr<Party::PartyApi> partyApi,
					std::shared_ptr<IActionDispatcher> actionDispatcher
				)
					: IPlatformSupportProvider(messenger)
					, _wUsersApi(usersApi)
					, _wEpicApi(epicApi)
					, _logger(logger)
					, _wPartyApi(partyApi)
					, _wActionDispatcher(actionDispatcher)
				{
				}

				std::string getPlatformName() override
				{
					return platformName;
				}

#pragma endregion

#pragma region private_members

			private:

				std::recursive_mutex _mutex;
				std::shared_ptr<ILogger> _logger;
				std::weak_ptr<Users::UsersApi> _wUsersApi;
				std::weak_ptr<details::EpicApi> _wEpicApi;
				std::weak_ptr<Party::PartyApi> _wPartyApi;
				std::weak_ptr<IActionDispatcher> _wActionDispatcher;

#pragma endregion
			};
		}

		// https://dev.epicgames.com/docs/services/en-US/WebAPIRef/AuthWebAPI/index.html

		class EpicAuthenticationEventHandler : public std::enable_shared_from_this<EpicAuthenticationEventHandler>, public Users::IAuthenticationEventHandler
		{
		public:

#pragma region public_methods

			EpicAuthenticationEventHandler(std::shared_ptr<details::EpicConfiguration> epicConfig, std::shared_ptr<IEpicApi> epicApi, std::shared_ptr<ILogger> logger)
				: _epicConfiguration(epicConfig)
				, _epicApi(epicApi)
			{
			}

			pplx::task<void> retrieveCredentials(const Users::CredentialsContext& context) override
			{
				return getEpicCredentials([context](std::string type, std::string provider, std::string accessToken)
				{
					context.authParameters->type = type;
					context.authParameters->parameters["provider"] = provider;
					context.authParameters->parameters["accessToken"] = accessToken;
				});
			}

			virtual pplx::task<void> renewCredentials(const Users::CredentialsRenewalContext& context) override
			{
				return getEpicCredentials([context](std::string type, std::string provider, std::string accessToken)
				{
					context.response->parameters["provider"] = provider;
					context.response->parameters["accessToken"] = accessToken;
				});
			}

			pplx::task<void> getEpicCredentials(std::function<void(std::string type, std::string provider, std::string accessToken)> fulfillCredentialsCallback)
			{
				if (!_epicConfiguration->getAuthenticationEnabled())
				{
					return pplx::task_from_result();
				}

				std::lock_guard<std::recursive_mutex> lg(_mutex);

				if (_authTce)
				{
					_authTce->set_exception(pplx::task_canceled());
				}

				_authTce = std::make_shared<pplx::task_completion_event<std::string>>();

				timeout(10s)
					.register_callback([tce = _authTce]()
				{
					tce->set_exception(pplx::task_canceled());
				});

				EOS_HPlatform platformHandle = _epicApi->getOrCreatePlatformHandle();

				if (platformHandle == nullptr)
				{
					throw std::runtime_error("Epic platform handle not set or created");
				}

				EOS_HAuth authHandle = EOS_Platform_GetAuthInterface(platformHandle);
				assert(authHandle != nullptr);

				EOS_Auth_Credentials credentials = {};
				credentials.ApiVersion = EOS_AUTH_CREDENTIALS_API_LATEST;

				EOS_Auth_LoginOptions loginOptions;
				memset(&loginOptions, 0, sizeof(loginOptions));
				loginOptions.ApiVersion = EOS_AUTH_LOGIN_API_LATEST;

				std::string firstParam, secondParam;

				firstParam = _epicConfiguration->getDevAuthHost();
				secondParam = _epicConfiguration->getDevAuthCredentialsName();

				if (!firstParam.empty() && !secondParam.empty())
				{
					// Dev auth (DevAuth)
					credentials.Type = EOS_ELoginCredentialType::EOS_LCT_Developer;
				}
				else
				{
					// Regular auth (AccountPortal)
					credentials.Type = EOS_ELoginCredentialType::EOS_LCT_AccountPortal;
				}

				loginOptions.ScopeFlags = EOS_EAuthScopeFlags::EOS_AS_BasicProfile | EOS_EAuthScopeFlags::EOS_AS_FriendsList | EOS_EAuthScopeFlags::EOS_AS_Presence;

				loginOptions.Credentials = &credentials;

				auto wEpicAuth = new std::weak_ptr<EpicAuthenticationEventHandler>(STORM_WEAK_FROM_THIS());

				EOS_Auth_Login(authHandle, &loginOptions, &wEpicAuth, loginCompleteCallbackFn);

				return pplx::create_task(*_authTce)
					.then([fulfillCredentialsCallback, authHandle](std::string accountIdStr)
				{
					assert(authHandle != nullptr);

					EOS_Auth_CopyUserAuthTokenOptions authTokenOptions = { 0 };
					authTokenOptions.ApiVersion = EOS_AUTH_COPYUSERAUTHTOKEN_API_LATEST;

					EOS_EpicAccountId accountId = details::EpicPlatformUserId::toEpicAccountId(accountIdStr);

					EOS_Auth_Token* authToken = nullptr;

					EOS_EResult result = EOS_Auth_CopyUserAuthToken(authHandle, &authTokenOptions, accountId, &authToken);

					if (result != EOS_EResult::EOS_Success)
					{
						throw std::runtime_error("EOS_Auth_CopyUserAuthToken failed with result " + std::to_string((int32_t)result));
					}

					std::string accessToken(authToken->AccessToken);

					fulfillCredentialsCallback(platformName, platformName, accessToken);
				});
			}

#pragma endregion

		private:

#pragma region private_methods

			void loginCompleteCallback(const EOS_Auth_LoginCallbackInfo* data)
			{
				std::string accountIdStr = details::EpicPlatformUserId::toString(data->LocalUserId);

				_logger->log(LogLevel::Trace, "EOS SDK", "Login Complete", "User ID: " + accountIdStr);

				auto platformHandle = _epicApi->getPlatformHandle();

				if (platformHandle == nullptr)
				{
					throw std::runtime_error("Epic platform handle not found");
				}

				EOS_HAuth authHandle = EOS_Platform_GetAuthInterface(platformHandle);

				if (authHandle == nullptr)
				{
					throw std::runtime_error("Epic auth handle not found");
				}

				if (data->ResultCode == EOS_EResult::EOS_Success)
				{
					const int32_t AccountsCount = EOS_Auth_GetLoggedInAccountsCount(authHandle);
					for (int32_t AccountIdx = 0; AccountIdx < AccountsCount; ++AccountIdx)
					{
						auto accountId2 = EOS_Auth_GetLoggedInAccountByIndex(authHandle, AccountIdx);

						EOS_ELoginStatus LoginStatus;
						LoginStatus = EOS_Auth_GetLoginStatus(authHandle, accountId2);

						std::string accountId2Str = details::EpicPlatformUserId::toString(accountId2);

						_logger->log(LogLevel::Trace, "EOS SDK", "AccountId=" + accountId2Str + "; Status=" + std::to_string((int32_t)LoginStatus));
					}

					std::lock_guard<std::recursive_mutex> lg(_mutex);

					_authTce->set(accountIdStr);
				}
				else
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					_authTce->set_exception(std::runtime_error("Epic login failed : Result = " + std::to_string((int32_t)data->ResultCode)));
				}
			}

			static void EOS_CALL loginCompleteCallbackFn(const EOS_Auth_LoginCallbackInfo* data)
			{
				assert(data != NULL);

				auto wEpicAuthPtr = (std::weak_ptr<EpicAuthenticationEventHandler>*)data->ClientData;
				auto wEpicAuth = *wEpicAuthPtr;

				if (auto epicAuth = wEpicAuth.lock())
				{
					epicAuth->loginCompleteCallback(data);
				}

				delete wEpicAuthPtr;
			}

#pragma endregion

#pragma region private_members

			std::recursive_mutex _mutex;
			std::shared_ptr<details::EpicConfiguration> _epicConfiguration;
			std::shared_ptr<IEpicApi> _epicApi;
			std::shared_ptr<ILogger> _logger;
			std::shared_ptr<pplx::task_completion_event<std::string>> _authTce; // shared_ptr used as an optional

#pragma endregion
		};

		class EpicPlugin : public IPlugin
		{
		public:

			static constexpr const char* PLUGIN_NAME = "Epic";
			static constexpr const char* PLUGIN_VERSION = "1.0.0";

			PluginDescription getDescription() override
			{
				return PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
			}

		private:

			void registerClientDependencies(ContainerBuilder& builder) override
			{
				builder.registerDependency<details::EpicConfiguration, Configuration>().singleInstance();
				builder.registerDependency<details::EpicApi, Users::UsersApi, details::EpicConfiguration, Configuration, IScheduler, ILogger, Party::PartyApi>().asSelf().as<IEpicApi>().singleInstance();
				builder.registerDependency<details::EpicPartyProvider, Party::Platform::InvitationMessenger, Users::UsersApi, details::EpicApi, ILogger, Party::PartyApi, IActionDispatcher>().as<Party::Platform::IPlatformSupportProvider>();
				builder.registerDependency<EpicAuthenticationEventHandler, details::EpicConfiguration, IEpicApi, ILogger>().as<Users::IAuthenticationEventHandler>();
			}

			void clientCreated(std::shared_ptr<IClient> client)
			{
				auto epicApi = client->dependencyResolver().resolve<IEpicApi>();
				epicApi->initialize();
			}

			void registerSceneDependencies(ContainerBuilder& builder, std::shared_ptr<Scene> scene) override
			{
				if (scene->getHostMetadata(IEpicApi::METADATA_KEY).length() > 0)
				{
					builder.registerDependency<details::EpicService, Scene>();
				}

				if (scene->getHostMetadata(Party::details::PartyService::METADATA_KEY).length() > 0)
				{
					builder.registerDependency<details::EpicPartyService, Scene>();
				}
			}
		};
	}
}
