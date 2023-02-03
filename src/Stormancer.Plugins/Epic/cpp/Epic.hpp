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

#include "eos_auth.h"
#include "eos_auth_types.h"
#include "eos_init.h"
#include "eos_logging.h"
#include "eos_sdk.h"
#include "eos_types.h"
#include "eos_custominvites.h"

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
			/// Should Stormancer initialize the Epic platform and call periodically EOS_Platform_Tick().
			/// Default is "false".
			/// Use "true" to enable, and set the ProuctName and ProductVersion.
			/// </summary>
			constexpr const char* InitPlatform = "epic.initPlatform";

			/// <summary>
			/// Epic product name.
			/// Mandatory if Stormancer initializes the Epic platform.
			/// </summary>
			constexpr const char* ProductName = "epic.productName";

			/// <summary>
			/// Epic product version.
			/// Mandatory if Stormancer initializes the Epic platform.
			/// </summary>
			constexpr const char* ProductVersion = "epic.productVersion";

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
			/// - "ExchangeCode"
			/// - "DevAuth"
			/// </summary>
			constexpr const char* LoginMode = "epic.authentication.loginMode";

			/// <summary>
			/// Dev Auth Host.
			/// The dev auth binary is in the Epic SDK package (/SDK/Tools/EOS_DevAuthTool-win32-x64-X.Y.Z.zip).
			/// </summary>
			constexpr const char* DevAuthHost = "epic.authentication.devAuth.host";

			/// <summary>
			/// Dev Auth name.
			/// The Name you entered in the Dev Auth tool.
			/// </summary>
			constexpr const char* DevAuthCredentialsName = "epic.authentication.devAuth.credentialsName";

			/// <summary>
			/// Exchange code.
			/// The exchange code to authenticate the user.
			/// </summary>
			constexpr const char* ExchangeCode = "epic.authentication.exchangeCode";

			/// <summary>
			/// Epic Product Id.
			/// </summary>
			constexpr const char* ProductId = "epic.productId";

			/// <summary>
			/// Epic Sandbox Id.
			/// </summary>
			constexpr const char* SandboxId = "epic.sandboxId";

			/// <summary>
			/// Epic Deployment Id.
			/// </summary>
			constexpr const char* DeploymentId = "epic.deploymentId";

			/// <summary>
			/// Epic client Client Id.
			/// </summary>
			constexpr const char* ClientId = "epic.clientId";

			/// <summary>
			/// Epic client Client Secret.
			/// </summary>
			constexpr const char* ClientSecret = "epic.clientSecret";

			/// <summary>
			/// Epic diagnostics (enable logs etc...).
			/// Default is "false".
			/// Use "true" to enable.
			/// </summary>
			constexpr const char* Diagnostics = "epic.diagnostics";
		}

		using AccountId = std::string;

		class IEpicApi
		{
		public:

			static constexpr const char* METADATA_KEY = "stormancer.plugins.epic";

			virtual ~IEpicApi() = default;

			virtual void initialize() = 0;

			virtual void setPlatformHandle(EOS_HPlatform platformHandle) = 0;

			virtual EOS_HPlatform getPlatformHandle() = 0;

			virtual EOS_EpicAccountId getEpicAccountId() = 0;
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
					return std::string(bufData, bufSize - 1);
				}

				static EOS_EpicAccountId toEpicAccountId(const AccountId& accountId)
				{
					if (accountId.size() != EOS_EPICACCOUNTID_MAX_LENGTH)
					{
						throw std::runtime_error("EpicAccountId conversion from string failed (Size=" + std::to_string(accountId.size()) + ")");
					}
					return EOS_EpicAccountId_FromString(accountId.c_str());
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

			class EpicState
			{
			public:

				EpicState(std::shared_ptr<Configuration> config, std::shared_ptr<ILogger> logger)
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					_authenticationEnabled = config->additionalParameters.find(ConfigurationKeys::AuthenticationEnabled) != config->additionalParameters.end() ? (config->additionalParameters.at(ConfigurationKeys::AuthenticationEnabled) != "false") : true;
					_loginMode = config->additionalParameters.find(ConfigurationKeys::LoginMode) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::LoginMode) : "";
					_devAuthHost = config->additionalParameters.find(ConfigurationKeys::DevAuthHost) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::DevAuthHost) : "";
					_devAuthCredentialsName = config->additionalParameters.find(ConfigurationKeys::DevAuthCredentialsName) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::DevAuthCredentialsName) : "";
					_exchangeCode = config->additionalParameters.find(ConfigurationKeys::ExchangeCode) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::ExchangeCode) : "";
					_productId = config->additionalParameters.find(ConfigurationKeys::ProductId) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::ProductId) : "";
					_sandboxId = config->additionalParameters.find(ConfigurationKeys::SandboxId) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::SandboxId) : "";
					_deploymentId = config->additionalParameters.find(ConfigurationKeys::DeploymentId) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::DeploymentId) : "";
					_clientId = config->additionalParameters.find(ConfigurationKeys::ClientId) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::ClientId) : "";
					_clientSecret = config->additionalParameters.find(ConfigurationKeys::ClientSecret) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::ClientSecret) : "";
					_initPlatform = config->additionalParameters.find(ConfigurationKeys::InitPlatform) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::InitPlatform) != "false" : true;
					_productName = config->additionalParameters.find(ConfigurationKeys::ProductName) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::ProductName) : "";
					_productVersion = config->additionalParameters.find(ConfigurationKeys::ProductVersion) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::ProductVersion) : "";
					_diagnostics = config->additionalParameters.find(ConfigurationKeys::Diagnostics) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::Diagnostics) != "false" : false;

					if (_loginMode.empty() && _exchangeCode.empty() && config->processLaunchArguments.size() > 1)
					{
						bool authTypeExchangecode = false;
						bool exchangeCodeRetrieved = false;
						std::string exchangeCode;
						for (auto& arg : config->processLaunchArguments)
						{
							if (!authTypeExchangecode && arg == "-AUTH_TYPE=exchangecode")
							{
								authTypeExchangecode = true;
							}
							
							if (!exchangeCodeRetrieved && arg.compare(0, 15, "-AUTH_PASSWORD=<password>", 15) == 0)
							{
								exchangeCode = arg.substr(15, arg.size() - 15);
								exchangeCodeRetrieved = true;
							}

							if (authTypeExchangecode && exchangeCodeRetrieved)
							{
								_loginMode = "ExchangeCode";
								_exchangeCode = exchangeCode;
								break;
							}
						}
					}
				}

				virtual ~EpicState()
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					clear();
				}

				bool getInitPlatform() const
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					return _initPlatform;
				}

				std::string getProductName() const
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					return _productName;
				}

				std::string getProductVersion() const
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					return _productVersion;
				}

				bool getAuthenticationEnabled() const
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					return _authenticationEnabled;
				}

				std::string getLoginMode() const
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					return _loginMode;
				}

				std::string getDevAuthHost() const
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					return _devAuthHost;
				}

				std::string getDevAuthCredentialsName() const
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					return _devAuthCredentialsName;
				}

				std::string getExchangeCode() const
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					return _exchangeCode;
				}

				std::string getProductId() const
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					return _productId;
				}

				std::string getSandboxId() const
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					return _sandboxId;
				}

				std::string getDeploymentId() const
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					return _deploymentId;
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

				bool getDiagnostics() const
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					return _diagnostics;
				}

				void setPlatformHandle(EOS_HPlatform platformHandle)
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					if (platformHandle != _platformHandle)
					{
						clear();
					}

					_platformHandle = platformHandle;
				}

				EOS_HPlatform getPlatformHandle() const
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					return _platformHandle;
				}

				void setPlatformHandleOwned(bool owned)
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					_platformHandleOwned = owned;
				}

				void setEpicAccountId(EOS_EpicAccountId epicAccountId)
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					_epicAccountId = epicAccountId;
				}

				EOS_EpicAccountId getEpicAccountId()
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					return _epicAccountId;
				}

				void setEpicProductUserId(EOS_ProductUserId productUserId)
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					_productUserId = productUserId;
				}

				EOS_ProductUserId getProductUserId()
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					return _productUserId;
				}

			private:

				void clear()
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					if (_platformHandleOwned)
					{
						_platformHandleOwned = false;
						EOS_Platform_Release(_platformHandle);
					}

					_platformHandle = nullptr;
				}

				mutable std::recursive_mutex _mutex;
				bool _initPlatform = true;
				std::string _productName;
				std::string _productVersion;
				bool _authenticationEnabled = true;
				std::string _loginMode;
				std::string _devAuthHost;
				std::string _devAuthCredentialsName;
				std::string _exchangeCode;
				std::string _productId;
				std::string _sandboxId;
				std::string _deploymentId;
				std::string _clientId;
				std::string _clientSecret;
				bool _platformHandleOwned = false;
				bool _diagnostics = false;
				EOS_HPlatform _platformHandle = nullptr;
				std::shared_ptr<ILogger> _logger;
				EOS_EpicAccountId _epicAccountId;
				EOS_ProductUserId _productUserId;
			};

			class EpicTicker : public std::enable_shared_from_this<EpicTicker>
			{
			public:

				EpicTicker(std::shared_ptr<Configuration> config, std::shared_ptr<EpicState> epicState, std::shared_ptr<ILogger> logger)
					: _wActionDispatcher(config->actionDispatcher)
				{
					_platformHandle = epicState->getPlatformHandle();
					if (_platformHandle == nullptr)
					{
						logger->log(LogLevel::Warn, "EpicTicker", "Epic platform handle is null");
					}
				}

				void start()
				{
					_stopTicker = false;

					if (_stoppedTicker)
					{
						_stoppedTicker = false;

						if (auto actionDispatcher = _wActionDispatcher.lock())
						{
							auto wEpicTicker = STORM_WEAK_FROM_THIS();
							actionDispatcher->post([wEpicTicker]()
							{
								if (auto epicTicker = wEpicTicker.lock())
								{
									epicTicker->scheduleTick();
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
					if (! _stopTicker && _platformHandle)
					{
						EOS_Platform_Tick(_platformHandle);
					}
					else
					{
						_stoppedTicker = true;
						return;
					}

					if (auto actionDispatcher = _wActionDispatcher.lock())
					{
						auto wEpicTicker = STORM_WEAK_FROM_THIS();
						actionDispatcher->post([wEpicTicker]()
						{
							if (auto epicTicker = wEpicTicker.lock())
							{
								if (epicTicker->_stopTicker)
								{
									epicTicker->_stoppedTicker = true;
								}
								else
								{
									epicTicker->scheduleTick();
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

				EOS_HPlatform _platformHandle = nullptr;

				bool _stopTicker = false;
				bool _stoppedTicker = true;
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

				EpicPartyInvitation(const std::string& senderId, const std::string& partySceneId)
					: _senderId(senderId)
					, _partySceneId(partySceneId)
				{
				}

				pplx::task<Party::PartyId> accept(std::shared_ptr<Party::PartyApi> partyApi) override
				{
					return partyApi->joinPartyBySceneId(_partySceneId, {})
						.then([partySceneId = _partySceneId]()
					{
						Party::PartyId partyId;
						partyId.platform = "epic";
						partyId.type = Party::PartyId::TYPE_SCENE_ID;
						partyId.id = partySceneId;
						return partyId;
					});
				}

				pplx::task<void> decline(std::shared_ptr<Party::PartyApi>) override
				{
					return pplx::task_from_result();
				}

				std::string getSenderId() override
				{
					return _senderId;
				}

				std::string getSenderPlatformId() override
				{
					return "Epic";
				}

				std::string getPartySceneId()
				{
					return _partySceneId;
				}

			private:

				std::string _senderId;
				std::string _partySceneId;
			};

			class EpicPartyProvider;

			class EpicApi : public ClientAPI<EpicApi, EpicService>, public IEpicApi
			{
				friend class EpicPartyProvider;

			public:

#pragma region public_methods

				EpicApi(std::shared_ptr<Users::UsersApi> usersApi, std::shared_ptr<EpicState> epicState, std::shared_ptr<Configuration> config, std::shared_ptr<IScheduler> scheduler, std::shared_ptr<ILogger> logger, std::shared_ptr<Party::PartyApi> partyApi)
					: ClientAPI(usersApi, "stormancer.epic")
					, _epicState(epicState)
					, _wScheduler(scheduler)
					, _wActionDispatcher(config->actionDispatcher)
					, _logger(logger)
					, _wUsersApi(usersApi)
					, _wPartyApi(partyApi)
				{
				}

				~EpicApi()
				{
				}

				void initialize() override
				{
					if (_epicState->getInitPlatform())
					{
						std::string productName = _epicState->getProductName();
						if (productName.empty())
						{
							_logger->log(LogLevel::Warn, "Epic", "Epic product name is empty");
						}

						std::string productVersion = _epicState->getProductVersion();
						if (productVersion.empty())
						{
							_logger->log(LogLevel::Warn, "Epic", "Epic product version is empty");
						}

						// Init EOS SDK
						EOS_InitializeOptions SDKOptions = {};
						SDKOptions.ApiVersion = EOS_INITIALIZE_API_LATEST;
						SDKOptions.AllocateMemoryFunction = nullptr;
						SDKOptions.ReallocateMemoryFunction = nullptr;
						SDKOptions.ReleaseMemoryFunction = nullptr;
						SDKOptions.ProductName = productName.c_str();
						SDKOptions.ProductVersion = productVersion.c_str();
						SDKOptions.Reserved = nullptr;
						SDKOptions.SystemInitializeOptions = nullptr;
						SDKOptions.OverrideThreadAffinity = nullptr;

						EOS_EResult InitResult = EOS_Initialize(&SDKOptions);
						if (InitResult != EOS_EResult::EOS_Success)
						{
							_logger->log(LogLevel::Error, "Epic", "EOS_Initialize failed", "Result=" + std::to_string((int)InitResult));
						}

						if (_epicState->getPlatformHandle() == nullptr)
						{
							EOS_Platform_Options platformOptions = {};
							platformOptions.ApiVersion = EOS_PLATFORM_OPTIONS_API_LATEST;
							platformOptions.bIsServer = false;
							std::string productId = _epicState->getProductId();
							platformOptions.ProductId = productId.c_str();
							std::string sandboxId = _epicState->getSandboxId();
							platformOptions.SandboxId = sandboxId.c_str();
							std::string deploymentId = _epicState->getDeploymentId();
							platformOptions.DeploymentId = deploymentId.c_str();
							std::string clientId = _epicState->getClientId();
							platformOptions.ClientCredentials.ClientId = clientId.c_str();
							std::string clientSecret = _epicState->getClientSecret();
							platformOptions.ClientCredentials.ClientSecret = clientSecret.c_str();
							EOS_HPlatform platformHandle = EOS_Platform_Create(&platformOptions);
							_epicState->setPlatformHandle(platformHandle);
							_epicState->setPlatformHandleOwned(true);
						}
					}
				}

				void setPlatformHandle(EOS_HPlatform platformHandle)
				{
					_epicState->setPlatformHandle(platformHandle);
				}

				EOS_HPlatform getPlatformHandle()
				{
					return _epicState->getPlatformHandle();
				}

				EOS_EpicAccountId getEpicAccountId()
				{
					return _epicState->getEpicAccountId();
				}

#pragma endregion

			private:

#pragma region private_methods

#pragma endregion

#pragma region private_members

				std::shared_ptr<ILogger> _logger;
				std::shared_ptr<EpicState> _epicState;
				std::weak_ptr<IScheduler> _wScheduler;
				std::weak_ptr<IActionDispatcher> _wActionDispatcher;
				std::weak_ptr<Users::UsersApi> _wUsersApi;
				std::weak_ptr<Party::PartyApi> _wPartyApi;

#pragma endregion
			};

			class EpicEventsManager
			{
			public:

				EpicEventsManager(std::shared_ptr<IClient> client, std::shared_ptr<EpicState> epicState, std::shared_ptr<ILogger> logger)
					: _wClient(client)
					, _epicState(epicState)
					, _logger(logger)
				{
					if (_epicState->getDiagnostics())
					{
						EOS_EResult SetLogCallbackResult = EOS_Logging_SetCallback(&EOSSDKLoggingCallback);
						if (SetLogCallbackResult != EOS_EResult::EOS_Success)
						{
							_logger->log(LogLevel::Warn, "EpicApi.initialize", "Set Logging Callback Failed!", std::to_string((int)SetLogCallbackResult));
						}
						else
						{
							_logger->log(LogLevel::Trace, "EpicApi.initialize", "Logging Callback Set");
							EOS_Logging_SetLogLevel(EOS_ELogCategory::EOS_LC_ALL_CATEGORIES, EOS_ELogLevel::EOS_LOG_Verbose);
						}
					}

					// Custom invitates
					EOS_HPlatform platformHandle = _epicState->getPlatformHandle();
					if (!platformHandle)
					{
						_logger->log(LogLevel::Error, "EpicApi.initialize", "Epic platform handle is null");
					}
					else
					{
						EOS_HCustomInvites customInvitesHandle = EOS_Platform_GetCustomInvitesInterface(platformHandle);
						assert(customInvitesHandle != nullptr);

						void* wClientPtr = new std::weak_ptr<IClient>(_wClient);

						// subscribe to notifications for when we receive a custom invite
						EOS_CustomInvites_AddNotifyCustomInviteReceivedOptions AddNotifyInviteReceivedOptions = {};
						AddNotifyInviteReceivedOptions.ApiVersion = EOS_CUSTOMINVITES_ADDNOTIFYCUSTOMINVITERECEIVED_API_LATEST;
						EOS_CustomInvites_AddNotifyCustomInviteReceived(customInvitesHandle, &AddNotifyInviteReceivedOptions, wClientPtr, onNotifyCustomInviteReceived);

						// subscribe to notifications for when we accept a custom invite externally (e.g. via the Overlay)
						EOS_CustomInvites_AddNotifyCustomInviteAcceptedOptions AddNotifyInviteAcceptedOptions = {};
						AddNotifyInviteAcceptedOptions.ApiVersion = EOS_CUSTOMINVITES_ADDNOTIFYCUSTOMINVITEACCEPTED_API_LATEST;
						EOS_CustomInvites_AddNotifyCustomInviteAccepted(customInvitesHandle, &AddNotifyInviteAcceptedOptions, wClientPtr, onNotifyCustomInviteAccepted);

						// subscribe to notifications for when we reject a custom invite externally (e.g. via the Overlay)
						EOS_CustomInvites_AddNotifyCustomInviteRejectedOptions AddNotifyInviteRejectedOptions = {};
						AddNotifyInviteRejectedOptions.ApiVersion = EOS_CUSTOMINVITES_ADDNOTIFYCUSTOMINVITEREJECTED_API_LATEST;
						EOS_CustomInvites_AddNotifyCustomInviteRejected(customInvitesHandle, &AddNotifyInviteRejectedOptions, wClientPtr, onNotifyCustomInviteRejected);
					}
				}

				static void EOS_CALL EOSSDKLoggingCallback(const EOS_LogMessage* InMsg)
				{
					if (InMsg->Level != EOS_ELogLevel::EOS_LOG_Off)
					{
						printf("[EOS SDK] %s: %s\n", InMsg->Category, InMsg->Message);
					}
				}

				static void EOS_CALL onNotifyCustomInviteReceived(const EOS_CustomInvites_OnCustomInviteReceivedCallbackInfo* data)
				{
					if (data != nullptr && data->ClientData != nullptr)
					{
						auto wClient = (std::weak_ptr<IClient>*)data->ClientData;
						auto client = wClient->lock();
						if (client)
						{
							auto logger = client->dependencyResolver().resolve<ILogger>();
							logger->log(LogLevel::Trace, "onNotifyCustomInviteReceived", "CustomInvites (OnNotifyCustomInviteReceived): invite received.");
							//FGame::Get().GetCustomInvites()->HandleCustomInviteReceived(data->Payload, data->CustomInviteId, data->TargetUserId);
						}
					}
					else
					{
						printf("[EOS SDK] %s: %s\n", "onNotifyCustomInviteReceived", "CustomInvites (OnNotifyCustomInviteReceived): EOS_CustomInvites_OnCustomInviteReceivedCallbackInfo is null");
					}
				}

				static void EOS_CALL onNotifyCustomInviteAccepted(const EOS_CustomInvites_OnCustomInviteAcceptedCallbackInfo* data)
				{
					if (data != nullptr && data->ClientData != nullptr)
					{
						auto wClient = (std::weak_ptr<IClient>*)data->ClientData;
						auto client = wClient->lock();
						if (client)
						{
							auto logger = client->dependencyResolver().resolve<ILogger>();
							logger->log(LogLevel::Trace, "onNotifyCustomInviteAccepted", "CustomInvites (OnNotifyCustomInviteAccepted): invite accepted from Overlay.");
							auto invitationMessenger = client->dependencyResolver().resolve<Party::Platform::InvitationMessenger>();
							char senderId[EOS_PRODUCTUSERID_MAX_LENGTH];
							int32_t senderIdBufferLength = EOS_PRODUCTUSERID_MAX_LENGTH;
							EOS_ProductUserId_ToString(data->TargetUserId, senderId, &senderIdBufferLength);
							auto epicPartyInvitation = std::make_shared<EpicPartyInvitation>(senderId, data->Payload);
							invitationMessenger->notifyInvitationReceived(epicPartyInvitation);
							//FGame::Get().GetCustomInvites()->HandleCustomInviteAccepted(Data->Payload, Data->CustomInviteId, Data->TargetUserId);
						}
					}
					else
					{
						printf("[EOS SDK] %s: %s\n", "onNotifyCustomInviteAccepted", "CustomInvites (OnNotifyCustomInviteAccepted): EOS_CustomInvites_OnCustomInviteAcceptedCallbackInfo is null");
					}
				}

				static void EOS_CALL onNotifyCustomInviteRejected(const EOS_CustomInvites_CustomInviteRejectedCallbackInfo* data)
				{
					if (data != nullptr && data->ClientData != nullptr)
					{
						auto wClient = (std::weak_ptr<IClient>*)data->ClientData;
						auto client = wClient->lock();
						if (client)
						{
							auto logger = client->dependencyResolver().resolve<ILogger>();
							logger->log(LogLevel::Trace, "onNotifyCustomInviteRejected", "CustomInvites (OnNotifyCustomInviteRejected): invite rejected from Overlay.");
							//FGame::Get().GetCustomInvites()->HandleCustomInviteRejected(Data->Payload, Data->CustomInviteId, Data->TargetUserId);
						}
					}
					else
					{
						printf("[EOS SDK] %s: %s\n", "onNotifyCustomInviteRejected", "CustomInvites (OnNotifyCustomInviteRejected): EOS_CustomInvites_CustomInviteRejectedCallbackInfo is null");
					}
				}

			private:

				std::weak_ptr<IClient> _wClient;
				std::shared_ptr<ILogger> _logger;
				std::shared_ptr<EpicState> _epicState;
			};

			class EpicPartyProvider : public Party::Platform::IPlatformSupportProvider
			{
			public:

#pragma region public_methods

				EpicPartyProvider(
					std::shared_ptr<Party::Platform::InvitationMessenger> messenger,
					std::shared_ptr<Users::UsersApi> usersApi,
					std::shared_ptr<EpicState> epicState,
					std::shared_ptr<details::EpicApi> epicApi,
					std::shared_ptr<ILogger> logger,
					std::shared_ptr<Party::PartyApi> partyApi,
					std::shared_ptr<IActionDispatcher> actionDispatcher
				)
					: IPlatformSupportProvider(messenger)
					, _wUsersApi(usersApi)
					, _epicState(epicState)
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

				bool tryShowSystemInvitationUI(std::shared_ptr<Party::PartyApi> partyApi) override
				{
					_logger->log(LogLevel::Error, "Epic", "tryShowSystemInvitationUI not implemented");
					return false;
				}

#pragma endregion

#pragma region private_members

			private:

				std::recursive_mutex _mutex;
				std::shared_ptr<ILogger> _logger;
				std::shared_ptr<EpicState> _epicState;
				std::weak_ptr<Users::UsersApi> _wUsersApi;
				std::weak_ptr<details::EpicApi> _wEpicApi;
				std::weak_ptr<Party::PartyApi> _wPartyApi;
				std::weak_ptr<IActionDispatcher> _wActionDispatcher;

#pragma endregion
			};
			
			class EpicPartyEventHandler : public Party::IPartyEventHandler
			{
			public:

				EpicPartyEventHandler(std::shared_ptr<ILogger> logger, std::shared_ptr<EpicState> epicState)
					: _logger(logger)
					, _epicState(epicState)
				{
				}

				~EpicPartyEventHandler()
				{
				}

				void onJoinedParty(std::shared_ptr<Party::PartyApi>, std::string partySceneId) override
				{
					// We need to set the custom invite payload to allow users to invite their friends though the Epic Games UI (clic on a friend -> invite)

					if (partySceneId.empty())
					{
						_logger->log(LogLevel::Error, "Steam", "Party scene id is invalid");
						return;
					}

					if (partySceneId.size() > EOS_CUSTOMINVITES_MAX_PAYLOAD_LENGTH)
					{
						_logger->log(LogLevel::Error, "Steam", "Party scene id too long to be sent in a EOS_CustomInvite");
						return;
					}

					EOS_HPlatform platformHandle = _epicState->getPlatformHandle();
					if (!platformHandle)
					{
						throw std::runtime_error("Epic platform handle is null");
					}

					EOS_HCustomInvites customInvitesHandle = EOS_Platform_GetCustomInvitesInterface(platformHandle);
					assert(customInvitesHandle != nullptr);

					EOS_ProductUserId productUserId = _epicState->getProductUserId();

					EOS_CustomInvites_SetCustomInviteOptions setCustomInviteOptions;
					setCustomInviteOptions.ApiVersion = EOS_CUSTOMINVITES_SETCUSTOMINVITE_API_LATEST;
					setCustomInviteOptions.LocalUserId = productUserId;
					setCustomInviteOptions.Payload = partySceneId.c_str();
					EOS_CustomInvites_SetCustomInvite(customInvitesHandle, &setCustomInviteOptions);
					// Whenever a Custom Invite Payload has been set, the Social Overlay will allow the local player to use the "Invite" button to send an invite with the currently set Custom Invite Payload to their friends.

					_logger->log(LogLevel::Info, "Epic", "Custom invite payload has been set");
					return;
				}

				pplx::task<void> onLeavingParty(std::shared_ptr<Party::PartyApi>, std::string)
				{
					// We reset the custom invite payload

					EOS_HPlatform platformHandle = _epicState->getPlatformHandle();
					if (!platformHandle)
					{
						throw std::runtime_error("Epic platform handle is null");
					}

					EOS_HCustomInvites customInvitesHandle = EOS_Platform_GetCustomInvitesInterface(platformHandle);
					assert(customInvitesHandle != nullptr);

					EOS_ProductUserId productUserId = _epicState->getProductUserId();

					EOS_CustomInvites_SetCustomInviteOptions setCustomInviteOptions;
					setCustomInviteOptions.ApiVersion = EOS_CUSTOMINVITES_SETCUSTOMINVITE_API_LATEST;
					setCustomInviteOptions.LocalUserId = productUserId;
					setCustomInviteOptions.Payload = "";
					EOS_CustomInvites_SetCustomInvite(customInvitesHandle, &setCustomInviteOptions);
					return pplx::task_from_result();
				}

			private:

				std::shared_ptr<ILogger> _logger;
				std::shared_ptr<EpicState> _epicState;
			};
		}

		// https://dev.epicgames.com/docs/services/en-US/WebAPIRef/AuthWebAPI/index.html

		class EpicAuthenticationEventHandler : public std::enable_shared_from_this<EpicAuthenticationEventHandler>, public Users::IAuthenticationEventHandler
		{
		public:

#pragma region public_methods

			EpicAuthenticationEventHandler(std::shared_ptr<details::EpicState> epicState, std::shared_ptr<ILogger> logger)
				: _epicState(epicState)
				, _logger(logger)
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

			pplx::task<void> renewCredentials(const Users::CredentialsRenewalContext& context) override
			{
				return getEpicCredentials([context](std::string type, std::string provider, std::string accessToken)
				{
					context.response->parameters["provider"] = provider;
					context.response->parameters["accessToken"] = accessToken;
				});
			}

			pplx::task<void> getEpicCredentials(std::function<void(std::string type, std::string provider, std::string accessToken)> fulfillCredentialsCallback)
			{
				if (!_epicState->getAuthenticationEnabled())
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

				EOS_HPlatform platformHandle = _epicState->getPlatformHandle();
				if (!platformHandle)
				{
					throw std::runtime_error("Epic platform handle is null");
				}

				EOS_HAuth authHandle = EOS_Platform_GetAuthInterface(platformHandle);
				assert(authHandle != nullptr);

				EOS_Auth_Credentials credentials = {};
				credentials.ApiVersion = EOS_AUTH_CREDENTIALS_API_LATEST;

				EOS_Auth_LoginOptions loginOptions;
				memset(&loginOptions, 0, sizeof(loginOptions));
				loginOptions.ApiVersion = EOS_AUTH_LOGIN_API_LATEST;

				auto loginMode = _epicState->getLoginMode();

				std::string id, token;

				if (loginMode == "DevAuth") // Dev auth (DevAuth)
				{
					id = _epicState->getDevAuthHost();
					token = _epicState->getDevAuthCredentialsName();

					if (id.empty() || token.empty())
					{
						STORM_RETURN_TASK_FROM_EXCEPTION(std::runtime_error("Missing host or credentials name for DevAuth login mode"), void);
					}

					credentials.Id = id.c_str();
					credentials.Token = token.c_str();
					credentials.Type = EOS_ELoginCredentialType::EOS_LCT_Developer;
				}
				else // Default regular auth (ExchangeCode)
				{
					token = _epicState->getExchangeCode();

					if (token.empty())
					{
						STORM_RETURN_TASK_FROM_EXCEPTION(std::runtime_error("Missing exchange code for ExchangeCode login mode"), void);
					}

					credentials.Token = token.c_str();
					credentials.Type = EOS_ELoginCredentialType::EOS_LCT_ExchangeCode;
				}

				loginOptions.ScopeFlags = EOS_EAuthScopeFlags::EOS_AS_BasicProfile | EOS_EAuthScopeFlags::EOS_AS_FriendsList | EOS_EAuthScopeFlags::EOS_AS_Presence;

				loginOptions.Credentials = &credentials;

				auto wEpicAuth = new std::weak_ptr<EpicAuthenticationEventHandler>(STORM_WEAK_FROM_THIS());

				EOS_Auth_Login(authHandle, &loginOptions, wEpicAuth, loginCompleteCallbackFn);

				auto wEpicAuthEvtHandler = STORM_WEAK_FROM_THIS();

				return pplx::create_task(*_authTce)
					.then([fulfillCredentialsCallback, authHandle, wEpicAuthEvtHandler](std::string accountIdStr)
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

					auto epicAuthEvtHandler = wEpicAuthEvtHandler.lock();
					if (!epicAuthEvtHandler)
					{
						throw ObjectDeletedException("EpicAuthenticationEventHandler");
					}

					epicAuthEvtHandler->_epicState->setEpicAccountId(accountId);

					fulfillCredentialsCallback(platformName, platformName, accessToken);
				});
			}

			pplx::task<void> OnLoggingOut() override
			{
				_epicState->setEpicAccountId(nullptr);
				return pplx::task_from_result();
			}

#pragma endregion

		private:

#pragma region private_methods

			void loginCompleteCallback(const EOS_Auth_LoginCallbackInfo* data)
			{
				if (data->ResultCode != EOS_EResult::EOS_Success)
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					_authTce->set_exception(std::runtime_error("Epic login failed : EOS_EResult = " + std::to_string((int32_t)data->ResultCode)));
					
					return;
				}

				std::string accountIdStr = details::EpicPlatformUserId::toString(data->LocalUserId);

				_logger->log(LogLevel::Trace, "EOS SDK", "Login Complete", "User ID: " + accountIdStr);

				auto platformHandle = _epicState->getPlatformHandle();

				if (platformHandle == nullptr)
				{
					throw std::runtime_error("Epic platform handle not found");
				}

				EOS_HAuth authHandle = EOS_Platform_GetAuthInterface(platformHandle);

				if (authHandle == nullptr)
				{
					throw std::runtime_error("Epic auth handle not found");
				}

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

			static void EOS_CALL loginCompleteCallbackFn(const EOS_Auth_LoginCallbackInfo* data)
			{
				assert(data != NULL && data->ClientData != NULL);

				auto wEpicAuthPtr = static_cast<std::weak_ptr<EpicAuthenticationEventHandler>*>(data->ClientData);
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
			std::shared_ptr<details::EpicState> _epicState;
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
				builder.registerDependency<details::EpicState, Configuration, ILogger>().singleInstance();
				builder.registerDependency<details::EpicTicker, Configuration, details::EpicState, ILogger>().asSelf().singleInstance();
				builder.registerDependency<details::EpicEventsManager, IClient, details::EpicState, ILogger>().asSelf().singleInstance();
				builder.registerDependency<details::EpicApi, Users::UsersApi, details::EpicState, Configuration, IScheduler, ILogger, Party::PartyApi>().asSelf().as<IEpicApi>();
				builder.registerDependency<details::EpicPartyProvider, Party::Platform::InvitationMessenger, Users::UsersApi, details::EpicState, details::EpicApi, ILogger, Party::PartyApi, IActionDispatcher>().as<Party::Platform::IPlatformSupportProvider>();
				builder.registerDependency<details::EpicPartyEventHandler, ILogger, details::EpicState>().as<Party::IPartyEventHandler>();
				builder.registerDependency<EpicAuthenticationEventHandler, details::EpicState, ILogger>().as<Users::IAuthenticationEventHandler>();
			}

			void clientCreated(std::shared_ptr<IClient> client) override
			{
				auto epicApi = client->dependencyResolver().resolve<IEpicApi>();
				epicApi->initialize();

				auto epicEventsManager = client->dependencyResolver().resolve<details::EpicEventsManager>();

				auto epicState = client->dependencyResolver().resolve<details::EpicState>();
				if (epicState->getInitPlatform())
				{
					auto epicTicker = client->dependencyResolver().resolve<details::EpicTicker>();
					epicTicker->start();
				}
			}

			void clientDisconnecting(std::shared_ptr<IClient> client) override
			{
				auto epicState = client->dependencyResolver().resolve<details::EpicState>();
				if (epicState->getInitPlatform())
				{
					auto epicTicker = client->dependencyResolver().resolve<details::EpicTicker>();
					epicTicker->stop();
				}
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
