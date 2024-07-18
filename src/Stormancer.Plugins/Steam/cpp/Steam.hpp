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

#pragma warning(disable: 4265) // Disable virtual destructor requirement warnings

// To disable including steam_api.h, define STORM_NOINCLUDE_STEAM
// To set another path to steam_api.h, define STORM_STEAM_INCLUDE
#ifndef STORM_NOINCLUDE_STEAM
#ifndef STORM_STEAM_INCLUDE
#define STORM_STEAM_INCLUDE "steam_api.h"
#endif
#include STORM_STEAM_INCLUDE
#endif

#pragma warning(default: 4265)

// https://partner.steamgames.com/doc/sdk/api

namespace Stormancer
{
	namespace Steam
	{
		static constexpr const char* platformName = "steam";

		/// <summary>
		/// Keys to use in Configuration::additionalParameters map to customize the Steam plugin behavior.
		/// </summary>
		namespace ConfigurationKeys
		{
			/// <summary>
			/// Enable Steam authentication.
			/// If disabled, the Steam plugin will not be considered for authentication.
			/// Default is "true".
			/// Use "false" to disable.
			/// </summary>
			constexpr const char* AuthenticationEnabled = "steam.authentication.enabled";

			/// <summary>
			/// Set the backend identity string for web API authentication. Must be the same as the 'steam.backendIdentity' configuration option in the server app.
			/// </summary>
			constexpr const char* SteamBackendIdentity = "steam.backendIdentity";

			/// <summary>
			/// The lobbyID the client should connect on authentication. 
			/// Automatic connection to a Steam lobby on successful authentication should occur when the game has been launched by a lobby invitation.
			/// You can get the LobbyID by searching the "+connect_lobby" parameter in the command line arguments (argv).
			/// </summary>
			constexpr const char* ConnectLobby = "steam.connectLobby";

			/// <summary>
			/// Should Stormancer initialize the Steam API library.
			/// Default is "true".
			/// Use "false" to disable.
			/// </summary>
			constexpr const char* SteamApiInitialize = "steam.steamApi.initialize";

			/// <summary>
			/// Should Stormancer run Steam Api callbacks.
			/// Default is "true".
			/// Use "false" to disable.
			/// </summary>
			constexpr const char* SteamApiRunCallbacks = "steam.steamApi.runCallbacks";
		}

		constexpr const char* PARTY_TYPE_STEAMIDLOBBY = "steamIDLobby";

		using SteamID = uint64;
		using SteamIDLobby = uint64;
		using SteamIDFriend = uint64;
		using SteamIDApp = uint64;

		struct LobbyMember
		{
			SteamID steamID;
			std::string personaname;
			std::unordered_map<std::string, std::string> data;
		};

		struct Lobby
		{
			SteamIDLobby steamIDLobby = 0;
			int numLobbyMembers = 0;
			int lobbyMemberLimit = 0;
			SteamID lobbyOwner = 0;
			std::unordered_map<SteamID, LobbyMember> lobbyMembers;
			std::unordered_map<std::string, std::string> data;
		};

		struct LobbyFilter
		{
			ELobbyDistanceFilter distanceFilter = ELobbyDistanceFilter::k_ELobbyDistanceFilterDefault;
			int slotsAvailable = 0;
			int resultCountFilter = 0;
			std::vector<std::pair<std::string, int>> nearValueFilter;
			std::unordered_map<std::string, std::pair<int, ELobbyComparison>> numericalFilter;
			std::unordered_map<std::string, std::pair<std::string, ELobbyComparison>> stringFilter;
		};

		struct PartyDataDto
		{
			std::string partyId;
			std::string leaderUserId;
			SteamID leaderSteamId = 0;

			MSGPACK_DEFINE(partyId, leaderUserId, leaderSteamId);
		};

		struct SteamFriend
		{
			std::string steamId;
			int relationship;
			uint64 friend_since = 0;

			MSGPACK_DEFINE(steamId, relationship, friend_since);
		};

		class SteamApi
		{
		public:

			static constexpr const char* METADATA_KEY = "stormancer.plugins.steam";

			virtual ~SteamApi() = default;

			virtual void initialize() = 0;

			// Stormancer Api

			virtual pplx::task<std::unordered_map<std::string, PartyDataDto>> decodePartyDataBearerTokens(const std::unordered_map<std::string, std::string>& partyDataBearerToken, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			virtual pplx::task<std::unordered_map<SteamID, std::string>> queryUserIds(const std::vector<SteamID>& steamIDs, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			virtual pplx::task<bool> inLobby(SteamIDLobby steamIDLobby, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			virtual pplx::task<bool> isOwner(SteamIDLobby steamIDLobby, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			virtual pplx::task<std::vector<SteamFriend>> getFriends(int friendsFlag = k_EFriendFlagImmediate, uint32 maxFriendsCount = UINT32_MAX, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			// Steam Api

			virtual SteamID getSteamID() = 0;
			virtual SteamID getLobbyLeader(SteamIDLobby lobbyId) = 0;
			virtual pplx::task<SteamIDLobby> createLobby(ELobbyType lobbyType = ELobbyType::k_ELobbyTypeFriendsOnly, int maxMembers = 5, bool joinable = true, const std::unordered_map<std::string, std::string> metadata = std::unordered_map<std::string, std::string>(), pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			virtual void inviteUserToLobby(SteamID userID, SteamIDLobby steamIDLobby) = 0;
			virtual pplx::task<void> joinLobby(SteamIDLobby steamIDLobby, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			virtual pplx::task<void> leaveLobby(SteamIDLobby steamIDLobby, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			virtual pplx::task<Lobby> requestLobbyData(SteamIDLobby steamIDLobby, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			virtual pplx::task<std::vector<Lobby>> requestLobbyList(LobbyFilter lobbyFilter = LobbyFilter(), pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			virtual pplx::task<void> setLobbyJoinable(SteamIDLobby steamIDLobby, bool joinable, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			virtual pplx::task<void> setLobbyData(SteamIDLobby steamIDLobby, const std::string& key, const std::string& value, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			virtual pplx::task<void> setLobbyMemberData(SteamIDLobby steamIDLobby, const std::string& key, const std::string& value, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			// Steam Utils

			virtual SteamIDApp getAppId() = 0;
		};

		std::string convertEResultToString(EResult result)
		{
			switch (result)
			{
			case k_EResultOK:
				return "OK";
			case k_EResultFail:
				return "Fail";
			case k_EResultTimeout:
				return "Timeout";
			case k_EResultLimitExceeded:
				return "LimitExceeded";
			case k_EResultAccessDenied:
				return "AccessDenied";
			case k_EResultNoConnection:
				return "NoConnection";
			default:
				return "Unknow";
			}
		}

		namespace details
		{
			class SteamPlatformUserId : public Users::PlatformUserId
			{
			public:

				std::string type() const override
				{
					return platformName;
				}

				static std::shared_ptr<SteamPlatformUserId> create(SteamID steamID)
				{
					// No make_shared because this class constructor is private
					return std::shared_ptr<SteamPlatformUserId>(new SteamPlatformUserId(steamID));
				}

				static std::shared_ptr<SteamPlatformUserId> tryCast(std::shared_ptr<Users::PlatformUserId> id)
				{
					if (id != nullptr && id->type() == platformName)
					{
						return std::static_pointer_cast<SteamPlatformUserId>(id);
					}
					return nullptr;
				}

				SteamID getSteamID()
				{
					return _steamID;
				}

				bool operator==(const SteamPlatformUserId& right)
				{
					return _steamID == right._steamID;
				}

				bool operator!=(const SteamPlatformUserId& right)
				{
					return _steamID != right._steamID;
				}

			private:

				SteamPlatformUserId(SteamID steamID)
					: PlatformUserId(std::to_string(steamID))
					, _steamID(steamID)
				{}

				const SteamID _steamID;
			};

			class SteamState
			{
			public:

				SteamState(std::shared_ptr<Configuration> config, std::shared_ptr<ILogger> logger)
				{
					_authenticationEnabled = config->additionalParameters.find(ConfigurationKeys::AuthenticationEnabled) != config->additionalParameters.end() ? (config->additionalParameters.at(ConfigurationKeys::AuthenticationEnabled) != "false") : true;
					_connectLobby = config->additionalParameters.find(ConfigurationKeys::ConnectLobby) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::ConnectLobby) : "";
					_steamApiInitialize = config->additionalParameters.find(ConfigurationKeys::SteamApiInitialize) != config->additionalParameters.end() ? (config->additionalParameters.at(ConfigurationKeys::SteamApiInitialize) != "false") : true;
					_steamApiRunCallbacks = config->additionalParameters.find(ConfigurationKeys::SteamApiRunCallbacks) != config->additionalParameters.end() ? (config->additionalParameters.at(ConfigurationKeys::SteamApiRunCallbacks) != "false") : true;
					_backendIdentity = config->additionalParameters.find(ConfigurationKeys::SteamBackendIdentity) != config->additionalParameters.end() ? config->additionalParameters.at(ConfigurationKeys::SteamBackendIdentity) : "";
					if (_connectLobby.empty() && config->processLaunchArguments.size() >= 2)
					{
						for (auto argi = 0; argi < config->processLaunchArguments.size(); argi++)
						{
							if (config->processLaunchArguments[argi] == "+connect_lobby" && config->processLaunchArguments.size() > (argi + 1))
							{
								std::string steamIDLobby = config->processLaunchArguments[argi + 1];

								logger->log(LogLevel::Info, "Steam", "Extracting `+connect_lobby` arg from processLaunchArguments", steamIDLobby);

								_connectLobby = steamIDLobby;
							}
						}
					}
				}

				bool getAuthenticationEnabled() const
				{
					return _authenticationEnabled;
				}

				std::string getBackendIdentity() const
				{
					return _backendIdentity;
				}

				std::string getConnectLobby() const
				{
					return _connectLobby;
				}

				bool getSteamApiInitialize() const
				{
					return _steamApiInitialize;
				}

				bool getSteamApiRunCallbacks() const
				{
					return _steamApiRunCallbacks;
				}

				void resetConnectLobby()
				{
					_connectLobby = "";
				}

			private:

				bool _authenticationEnabled = true;
				std::string _connectLobby;
				bool _steamApiInitialize = true;
				bool _steamApiRunCallbacks = true;
				std::string _backendIdentity;
			};

			struct GetAuthSessionTokenForWebApiContext
			{
				GetAuthSessionTokenForWebApiContext(HAuthTicket ticketId)
					: ticketId(ticketId)
				{
					//cCallResult.Set(ticketId, this, &details::GetAuthSessionTokenForWebApiContext::onResultReceived);

				}

				HAuthTicket ticketId;
				pplx::task_completion_event<std::string> tce;
				STEAM_CALLBACK(GetAuthSessionTokenForWebApiContext, onResultReceived, GetTicketForWebApiResponse_t);
				//CCallResult<details::GetAuthSessionTokenForWebApiContext, GetTicketForWebApiResponse_t> cCallResult;
			};

			void GetAuthSessionTokenForWebApiContext::onResultReceived(GetTicketForWebApiResponse_t* response)
			{
				if (ticketId == response->m_hAuthTicket)
				{
					if (response->m_eResult != EResult::k_EResultOK)
					{
						tce.set_exception(std::runtime_error("Failed to obtain Steam web API ticket : " + convertEResultToString(response->m_eResult)));
						return;
					}

					std::stringstream ss;
					ss << std::uppercase << std::hex << std::setfill('0');
					for (int i = 0; i < response->m_cubTicket; i++)
					{
						ss << std::setw(2) << static_cast<unsigned>(response->m_rgubTicket[i]);
					}
					auto steamTicketHex = ss.str();
					tce.set(steamTicketHex);
				}

			}

			std::string to_string(ELobbyType lobbyType)
			{
				switch (lobbyType)
				{
				case k_ELobbyTypePrivate:
					return "private";
				case k_ELobbyTypeFriendsOnly:
					return "friendsOnly";
				case k_ELobbyTypePublic:
					return "public";
				case k_ELobbyTypeInvisible:
					return "invisible";
				case k_ELobbyTypePrivateUnique:
					return "privateUnique";
				default:
					return std::to_string((int)lobbyType);
				}
			}


			struct CreateLobbyDto
			{
				ELobbyType lobbyType = ELobbyType::k_ELobbyTypePrivate;
				int maxMembers = 0;
				bool joinable = false;
				std::unordered_map<std::string, std::string> metadata;




				MSGPACK_DEFINE(lobbyType, maxMembers, joinable, metadata)
			};
			struct CreateLobbyResult
			{
				bool success;
				std::string errorId;
				std::string errorDetails;
				unsigned long long steamLobbyId;

				MSGPACK_DEFINE(success, errorId, errorDetails, steamLobbyId)
			};

			struct VoidSteamOperationResult
			{
				bool success;
				std::string errorId;
				std::string errorDetails;
				MSGPACK_DEFINE(success, errorId, errorDetails)
			};

			struct GetSteamFriendsOperationResult
			{
				bool success;
				std::string errorId;
				std::string errorDetails;
				std::vector<SteamFriend> friends;

				MSGPACK_DEFINE(success, errorId, errorDetails, friends)

			};

			struct GetLobbyOwnerResult
			{
				bool success;
				std::string errorId;
				std::string errorDetails;
				SteamID owner;
				MSGPACK_DEFINE(success, errorId, errorDetails, owner)
			};

			struct JoinLobbyDto
			{
				SteamIDLobby steamIDLobby;

				MSGPACK_DEFINE(steamIDLobby)
			};
			struct UpdateLobbyJoinableArgs
			{
				SteamIDLobby steamIDLobby;
				bool joinable;
				MSGPACK_DEFINE(steamIDLobby, joinable)
			};

			struct InviteUserToLobbyArgs
			{
				SteamID userId;
				SteamIDLobby lobbyId;
				MSGPACK_DEFINE(userId,lobbyId)
			};
			using GetLobbyOwnerArgs = JoinLobbyDto;



			class SteamService : public std::enable_shared_from_this<SteamService>
			{
			public:

				SteamService(std::shared_ptr<Scene> scene)
					: _rpcService(scene->dependencyResolver().resolve<RpcService>())
				{}

				pplx::task<std::unordered_map<std::string, PartyDataDto>> decodePartyDataBearerTokens(const std::unordered_map<std::string, std::string>& partyDataBearerTokens, pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					return _rpcService->rpc<std::unordered_map<std::string, PartyDataDto>>("Steam.DecodePartyDataBearerTokens", ct, partyDataBearerTokens);
				}

				pplx::task<std::unordered_map<SteamID, std::string>> queryUserIds(const std::vector<SteamID>& steamIDs, pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					return _rpcService->rpc<std::unordered_map<SteamID, std::string>>("Steam.QueryUserIds", ct, steamIDs);
				}

			private:

				std::shared_ptr<RpcService> _rpcService;
			};

			class SteamPartyService : public std::enable_shared_from_this<SteamPartyService>
			{
			public:

				SteamPartyService(std::shared_ptr<Scene> scene)
					: _rpcService(scene->dependencyResolver().resolve<RpcService>())
				{}

				pplx::task<std::string> createPartyDataBearerToken(pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					return _rpcService->rpc<std::string>("SteamParty.CreatePartyDataBearerToken", ct);
				}



			private:

				std::shared_ptr<RpcService> _rpcService;
			};

			class SteamPartyInvitation : public Party::Platform::IPlatformInvitation
			{
			public:

				SteamPartyInvitation(const Party::PartyId& partyId, const std::string& senderSteamID = "")
					: _partyId(partyId)
					, _senderSteamID(senderSteamID)
				{}

				pplx::task<Party::PartyId> accept(std::shared_ptr<Party::PartyApi> partyApi) override
				{
					return pplx::task_from_result(_partyId);
				}

				pplx::task<void> decline(std::shared_ptr<Party::PartyApi>) override
				{
					return pplx::task_from_result();
				}

				std::string getSenderId() override
				{
					return _senderSteamID;
				}

				std::string getSenderPlatformId() override
				{
					return platformName;
				}

				Party::PartyId getPartyId()
				{
					return _partyId;
				}

			private:

				Party::PartyId _partyId;
				std::string _senderSteamID;
			};

			class SteamPartyProvider;

			class SteamImpl : public ClientAPI<SteamImpl, SteamService>, public SteamApi
			{
				friend class SteamPartyProvider;
				friend class SteamPlugin;

			public:

#pragma region public_methods

				SteamImpl(std::shared_ptr<Users::UsersApi> usersApi, std::shared_ptr<SteamState> steamConfig, std::shared_ptr<Configuration> config, std::shared_ptr<IScheduler> scheduler, std::shared_ptr<ILogger> logger, std::shared_ptr<Party::PartyApi> partyApi, std::shared_ptr<Party::Platform::InvitationMessenger> invitationMessenger)
					: ClientAPI(usersApi, "stormancer.steam")
					, _wSteamConfig(steamConfig)
					, _wScheduler(scheduler)
					, _wActionDispatcher(config->actionDispatcher)
					, _logger(logger)
					, _wUsersApi(usersApi)
					, _wPartyApi(partyApi)
					, _wInvitationMessenger(invitationMessenger)
				{}

				~SteamImpl()
				{
					_cts.cancel();
				}

				void initializePartyScene(std::shared_ptr<Scene> scene)
				{
					auto wSteamImpl = STORM_WEAK_FROM_THIS();
					auto rpc = scene->dependencyResolver().resolve<RpcService>();
					rpc->addProcedure("Steam.CreateLobby", [wSteamImpl](RpcRequestContext_ptr ctx)
						{
							auto steamApi = wSteamImpl.lock();
							if (!steamApi)
							{
								STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("SteamApi"), void);
							}

							auto args = ctx->readObject<CreateLobbyDto>();

							return steamApi->onCreateLobbyAsync(args, ctx->cancellationToken())
								.then([ctx](CreateLobbyResult result)
									{
										ctx->sendValueTemplated(result);
									});
						});

					rpc->addProcedure("Steam.JoinLobby", [wSteamImpl](RpcRequestContext_ptr ctx)
						{
							auto steamApi = wSteamImpl.lock();
							if (!steamApi)
							{
								STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("SteamApi"), void);
							}

							auto args = ctx->readObject<JoinLobbyDto>();

							return steamApi->onJoinLobbyAsync(args, ctx->cancellationToken())
								.then([ctx](VoidSteamOperationResult result)
									{
										ctx->sendValueTemplated(result);
									});
						});

					rpc->addProcedure("Steam.UpdateLobbyJoinable", [wSteamImpl](RpcRequestContext_ptr ctx)
						{
							auto steamApi = wSteamImpl.lock();
							if (!steamApi)
							{
								STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("SteamApi"), void);
							}

							auto args = ctx->readObject<UpdateLobbyJoinableArgs>();

							return steamApi->setLobbyJoinable(args.steamIDLobby, args.joinable, ctx->cancellationToken())
								.then([ctx](pplx::task<void> t)
									{
										VoidSteamOperationResult result;
										try
										{
											t.get();
											result.success = true;
											ctx->sendValueTemplated(result);
										}
										catch (std::exception& ex)
										{
											result.success = false;
											result.errorDetails = ex.what();
											ctx->sendValueTemplated(result);
										}


									});
						});

					rpc->addProcedure("Steam.GetLobbyOwner", [wSteamImpl](RpcRequestContext_ptr ctx)
						{
							auto steamApi = wSteamImpl.lock();
							if (!steamApi)
							{
								STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("SteamApi"), void);
							}

							auto args = ctx->readObject<GetLobbyOwnerArgs>();
							auto leader = steamApi->getLobbyLeader(args.steamIDLobby);
							GetLobbyOwnerResult result;
							result.success = true;
							result.owner = leader;
							ctx->sendValueTemplated(result);

							return pplx::task_from_result();
						});

					rpc->addProcedure("Steam.Invite", [wSteamImpl](RpcRequestContext_ptr ctx)
					{
						auto steamApi = wSteamImpl.lock();
						if (!steamApi)
						{
							STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("SteamApi"), void);
						}

						auto args = ctx->readObject<InviteUserToLobbyArgs>();

						steamApi->inviteUserToLobby(args.userId, args.lobbyId);
					});



				}

				void initializeFriendsScene(std::shared_ptr<Scene> scene)
				{
					auto wSteamImpl = STORM_WEAK_FROM_THIS();
					auto rpc = scene->dependencyResolver().resolve<RpcService>();

					rpc->addProcedure("Steam.GetFriends", [wSteamImpl](RpcRequestContext_ptr ctx)
						{
							auto steamApi = wSteamImpl.lock();
							if (!steamApi)
							{
								STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("SteamApi"), void);
							}

							uint32 maxFriendsCount = ctx->readObject<uint32>();

							return steamApi->getFriends(k_EFriendFlagImmediate, maxFriendsCount, ctx->cancellationToken())
								.then([ctx](pplx::task<std::vector<SteamFriend>> task)
									{
										GetSteamFriendsOperationResult result;
										try
										{
											auto friends = task.get();
											result.friends = friends;
											result.success = true;
										}
										catch (std::exception& ex)
										{
											result.success = false;
											result.errorId = "steamError";
											result.errorDetails = ex.what();
										}
										ctx->sendValueTemplated(result);
									});


						});
				}

				void initialize() override
				{
					if (auto steamConfig = _wSteamConfig.lock())
					{
						if (steamConfig->getSteamApiInitialize())
						{
							if (!SteamAPI_Init())
							{
								_logger->log(LogLevel::Error, "Steam", "SteamAPI_Init failed");
								return;
							}
							else
							{
								_logger->log(LogLevel::Info, "Steam", "SteamAPI_Init success");
							}

						}

						if (steamConfig->getSteamApiRunCallbacks())
						{
							scheduleRunSteamAPiCallbacks();
						}

						auto connectLobbyArgument = steamConfig->getConnectLobby();

						if (!connectLobbyArgument.empty())
						{
							if (auto invitationMessenger = _wInvitationMessenger.lock())
							{
								_logger->log(LogLevel::Info, "Steam", "Steam process launch argument found: '+connect_lobby'", connectLobbyArgument);

								SteamIDLobby steamIDLobby = std::stoull(connectLobbyArgument);

								Party::PartyId partyId;
								partyId.id = std::to_string(steamIDLobby);
								partyId.type = PARTY_TYPE_STEAMIDLOBBY;
								partyId.platform = platformName;

								auto steamPartyInvitation = std::make_shared<SteamPartyInvitation>(partyId);
								invitationMessenger->notifyInvitationReceived(steamPartyInvitation);
							}
						}
					}

					auto usersApi = _wUsersApi.lock();
					if (!usersApi)
					{
						_logger->log(LogLevel::Error, "Steam", "UsersApi deleted");
						return;
					}

					auto wSteamImpl = STORM_WEAK_FROM_THIS();

					usersApi->setOperationHandler("Steam.GetFriends", [wSteamApi = wSteamImpl, wUsersApi = _wUsersApi, logger = _logger](Stormancer::Users::OperationCtx& ctx)
						{
							auto steamApi = wSteamApi.lock();
							if (!steamApi)
							{
								STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("SteamApi"), void);
							}

							uint32 maxFriendsCount = ctx.request->readObject<uint32>();

							return steamApi->getFriends(k_EFriendFlagImmediate, maxFriendsCount, ctx.request->cancellationToken())
								.then([ctx](std::vector<SteamFriend> friends)
									{
										ctx.request->sendValueTemplated(friends);
									});
						});

					usersApi->setOperationHandler("Steam.CreateLobby", [wSteamImpl, wUsersApi = _wUsersApi, logger = _logger](Stormancer::Users::OperationCtx& ctx)
						{
							auto steamImpl = wSteamImpl.lock();
							if (!steamImpl)
							{
								STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("SteamApi"), void);
							}

							auto createLobbyDto = ctx.request->readObject<CreateLobbyDto>();

							// Create lobby
							return steamImpl->createLobby(createLobbyDto.lobbyType, createLobbyDto.maxMembers, createLobbyDto.joinable, createLobbyDto.metadata, ctx.request->cancellationToken())
								.then([wSteamImpl, wUsersApi, ctx](SteamIDLobby steamIDLobby)
									{
										auto steamImpl = wSteamImpl.lock();
										if (!steamImpl)
										{
											STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("SteamApi"), void);
										}

										auto usersApi = wUsersApi.lock();
										if (!usersApi)
										{
											STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("UsersApi"), void);
										}

										{
											std::lock_guard<std::recursive_mutex> lg(steamImpl->_mutex);

											// Keep steamIDLobby to leave on party leave
											steamImpl->_partySteamIDLobby = steamIDLobby;
										}

										auto myUserId = usersApi->userId();

										return steamImpl->setLobbyMemberData(steamIDLobby, "stormancer.userId", myUserId, ctx.request->cancellationToken())
											.then([steamIDLobby, ctx]()
												{
													// Send back steamIDLobby to server
													ctx.request->sendValue([steamIDLobby](obytestream& stream)
														{
															Serializer serializer;
															serializer.serialize(stream, steamIDLobby);
														});
												});
									});
						});

					usersApi->setOperationHandler("Steam.JoinLobby", [wSteamImpl, wUsersApi = _wUsersApi, logger = _logger](Stormancer::Users::OperationCtx& ctx)
						{
							auto steamImpl = wSteamImpl.lock();
							if (!steamImpl)
							{
								STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("SteamApi"), void);
							}

							auto joinLobbyDto = ctx.request->readObject<JoinLobbyDto>();
							auto steamIDLobby = joinLobbyDto.steamIDLobby;

							std::lock_guard<std::recursive_mutex> lg(steamImpl->_mutex);

							// Keep steamIDLobby to leave on party leave
							steamImpl->_partySteamIDLobby = steamIDLobby;

							return steamImpl->inLobby(steamIDLobby, ctx.request->cancellationToken())
								.then([steamIDLobby, wSteamImpl, ctx](bool inLobby)
									{
										if (inLobby)
										{
											// We already are in the lobby, do nothing
											return pplx::task_from_result();
										}
										else
										{
											// Join lobby
											auto steamImpl = wSteamImpl.lock();
											if (!steamImpl)
											{
												STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("SteamApi"), void);
											}

											return steamImpl->joinLobby(steamIDLobby, ctx.request->cancellationToken());
										}
									})
								.then([wSteamImpl, wUsersApi, steamIDLobby, ctx]()
									{
										auto steamImpl = wSteamImpl.lock();
										if (!steamImpl)
										{
											STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("SteamApi"), void);
										}

										auto usersApi = wUsersApi.lock();
										if (!usersApi)
										{
											STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("UsersApi"), void);
										}

										auto myUserId = usersApi->userId();
										return steamImpl->setLobbyMemberData(steamIDLobby, "stormancer.userId", myUserId, ctx.request->cancellationToken());
									});
						});
				}


				void scheduleRunSteamAPiCallbacks()
				{
					if (!_cts.get_token().is_canceled())
					{
						SteamAPI_RunCallbacks();

						if (auto actionDispatcher = _wActionDispatcher.lock())
						{
							auto wSteamImpl = STORM_WEAK_FROM_THIS();
							actionDispatcher->post([wSteamImpl]()
								{
									if (auto steamImpl = wSteamImpl.lock())
									{
										steamImpl->scheduleRunSteamAPiCallbacks();
									}
								});
						}
					}
				}

				SteamID getSteamID() override
				{
					auto steamUser = SteamUser();

					auto steamID = steamUser->GetSteamID();

					return steamID.ConvertToUint64();
				}

				SteamID getLobbyLeader(SteamIDLobby lobbyId) override
				{

					auto steamMatchmaking = SteamMatchmaking();
					if (!steamMatchmaking)
					{
						throw std::runtime_error("SteamMatchmaking() returned null");
					}

					return steamMatchmaking->GetLobbyOwner(lobbyId).ConvertToUint64();
				}

				pplx::task<SteamIDLobby> createLobby(ELobbyType lobbyType = ELobbyType::k_ELobbyTypeFriendsOnly, int maxMembers = 5, bool joinable = true, const std::unordered_map<std::string, std::string> metadata = std::unordered_map<std::string, std::string>(), pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{

					std::string log;
					log += "joinable=" + std::to_string(joinable);
					log += ",maxMembers=" + std::to_string(maxMembers);
					log += ",lobbyType=" + to_string(lobbyType);
					log += ",metadata={";

					for (auto& kvp : metadata)
					{
						log += kvp.first + "=" + kvp.second + ",";
					}
					log += "}";

					_logger->log(LogLevel::Info, "steam", "Creating steam lobby.", log);
					auto actionDispatcher = _wActionDispatcher.lock();
					auto taskOptions = actionDispatcher ? pplx::task_options(actionDispatcher) : pplx::task_options();

					if (maxMembers < 1 || maxMembers > 250)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error("maxMembers must be between 1 and 250"), taskOptions, SteamIDLobby);
					}

					auto steamMatchmaking = SteamMatchmaking();
					if (!steamMatchmaking)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error("SteamMatchmaking() returned null"), taskOptions, SteamIDLobby);
					}

					_logger->log(LogLevel::Info, "Steam", "Creating lobby");

					std::lock_guard<std::recursive_mutex> lg(_mutex);

					// Cancel
					if (_lobbyCreatedTce)
					{
						_lobbyCreatedCallResult.Cancel();
						_lobbyCreatedTce->set_exception(pplx::task_canceled());
					}

					// Prepare
					_lobbyCreatedTce = std::make_shared<pplx::task_completion_event<SteamIDLobby>>();

					// Timeout
					timeout(10s, ct)
						.register_callback([tce = _lobbyCreatedTce]()
							{
								tce->set_exception(pplx::task_canceled());
							});

					// Call SteamAPI and register call result
					SteamAPICall_t hSteamAPICall = steamMatchmaking->CreateLobby(lobbyType, maxMembers);
					_lobbyCreatedCallResult.Set(hSteamAPICall, this, &SteamImpl::onLobbyCreatedCallResult);

					return pplx::create_task(*_lobbyCreatedTce, taskOptions)
						.then([steamMatchmaking, joinable, metadata, wSteamApi = STORM_WEAK_FROM_THIS(), logger = _logger, ct](SteamIDLobby steamIDLobby)
							{
								auto steamApi = wSteamApi.lock();

								auto task = pplx::task_from_result();
								logger->log(LogLevel::Info, "steam", "Steam lobby created", std::to_string(steamIDLobby));
								if (!joinable)
								{
									steamApi->setLobbyJoinable(steamIDLobby, joinable, ct)
										.then([logger](pplx::task<void> task)
											{
												try
												{
													return task.get();
												}
												catch (const std::exception& ex)
												{
													logger->log(LogLevel::Warn, "Steam", "setLobbyJoinable failed", ex);
												}
											});
								}

								if (metadata.size() > 0)
								{
									for (auto& md : metadata)
									{
										steamApi->setLobbyData(steamIDLobby, md.first, md.second, ct)
											.then([logger](pplx::task<void> task)
												{
													try
													{
														return task.get();
													}
													catch (const std::exception& ex)
													{
														logger->log(LogLevel::Warn, "Steam", "setLobbyData failed, metadata ignored", ex);
													}
												});
									}
								}

								return steamIDLobby;
							});
				}

				pplx::task<void> joinLobby(SteamIDLobby steamIDLobby, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					auto actionDispatcher = _wActionDispatcher.lock();
					auto taskOptions = actionDispatcher ? pplx::task_options(actionDispatcher) : pplx::task_options();

					auto steamMatchmaking = SteamMatchmaking();
					if (!steamMatchmaking)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error("SteamMatchmaking() returned null"), actionDispatcher, void);
					}

					_logger->log(LogLevel::Info, "Steam", "Join lobby", std::to_string(steamIDLobby));

					std::lock_guard<std::recursive_mutex> lg(_mutex);

					// Cancel
					auto it = _lobbyEnterEventData.find(steamIDLobby);
					if (it != _lobbyEnterEventData.end())
					{
						it->second.callResult.Cancel();
						it->second.tce.set_exception(pplx::task_canceled());
						_lobbyEnterEventData.erase(it);
					}

					// Prepare
					auto& lobbyEnterEventData = _lobbyEnterEventData[steamIDLobby];

					// Timeout
					timeout(10s, ct)
						.register_callback([tce = lobbyEnterEventData.tce]()
							{
								tce.set_exception(pplx::task_canceled());
							});

					// Call SteamAPI and register call result
					SteamAPICall_t hSteamAPICall = steamMatchmaking->JoinLobby(CSteamID(steamIDLobby));
					lobbyEnterEventData.callResult.Set(hSteamAPICall, this, &SteamImpl::onLobbyEnterCallResult);

					return pplx::create_task(lobbyEnterEventData.tce, taskOptions);
				}

				void inviteUserToLobby(SteamID userID, SteamIDLobby steamIDLobby) override
				{
					steamMatchmaking->inviteUserToLobby(steamIDLobby, userID);

				}

				pplx::task<void> leaveLobby(SteamIDLobby steamIDLobby, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					auto actionDispatcher = _wActionDispatcher.lock();
					auto taskOptions = actionDispatcher ? pplx::task_options(actionDispatcher) : pplx::task_options();

					auto steamMatchmaking = SteamMatchmaking();
					if (!steamMatchmaking)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error("SteamMatchmaking() returned null"), actionDispatcher, void);
					}

					if (ct.is_cancelable() && ct.is_canceled())
					{
						STORM_RETURN_TASK_CANCELED_OPT(actionDispatcher, void);
					}

					_logger->log(LogLevel::Info, "Steam", "Leaving lobby", std::to_string(steamIDLobby));

					steamMatchmaking->LeaveLobby(CSteamID(steamIDLobby));

					_logger->log(LogLevel::Trace, "Steam", "Lobby left", std::to_string(steamIDLobby));

					return pplx::task_from_result(taskOptions);
				}

				pplx::task<std::vector<Lobby>> requestLobbyList(LobbyFilter lobbyFilter = LobbyFilter(), pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					auto actionDispatcher = _wActionDispatcher.lock();
					auto taskOptions = actionDispatcher ? pplx::task_options(actionDispatcher) : pplx::task_options();

					auto steamMatchmaking = SteamMatchmaking();
					if (!steamMatchmaking)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error("SteamMatchmaking() returned null"), taskOptions, std::vector<Lobby>);
					}

					_logger->log(LogLevel::Info, "Steam", "requestLobbyList");

					if (lobbyFilter.distanceFilter != ELobbyDistanceFilter::k_ELobbyDistanceFilterDefault)
					{
						steamMatchmaking->AddRequestLobbyListDistanceFilter(lobbyFilter.distanceFilter);
					}

					if (lobbyFilter.slotsAvailable > 0)
					{
						steamMatchmaking->AddRequestLobbyListFilterSlotsAvailable(lobbyFilter.slotsAvailable);
					}

					if (lobbyFilter.resultCountFilter > 0)
					{
						steamMatchmaking->AddRequestLobbyListResultCountFilter(lobbyFilter.distanceFilter);
					}

					for (auto& nearValueFilter : lobbyFilter.nearValueFilter)
					{
						steamMatchmaking->AddRequestLobbyListNearValueFilter(nearValueFilter.first.c_str(), nearValueFilter.second);
					}

					for (auto& numericalFilter : lobbyFilter.numericalFilter)
					{
						steamMatchmaking->AddRequestLobbyListNumericalFilter(numericalFilter.first.c_str(), numericalFilter.second.first, numericalFilter.second.second);
					}

					for (auto& stringFilter : lobbyFilter.stringFilter)
					{
						steamMatchmaking->AddRequestLobbyListStringFilter(stringFilter.first.c_str(), stringFilter.second.first.c_str(), stringFilter.second.second);
					}

					std::lock_guard<std::recursive_mutex> lg(_mutex);

					// Cancel
					if (_requestLobbyListTce)
					{
						_requestLobbyListCallResult.Cancel();
						_requestLobbyListTce->set_exception(pplx::task_canceled());
					}

					// Prepare
					_requestLobbyListTce = std::make_shared<pplx::task_completion_event<std::vector<Lobby>>>();

					// Timeout
					timeout(10s, ct)
						.register_callback([tce = _requestLobbyListTce]()
							{
								tce->set_exception(pplx::task_canceled());
							});

					// Call SteamAPI and register call result
					SteamAPICall_t hSteamAPICall = steamMatchmaking->RequestLobbyList();
					_requestLobbyListCallResult.Set(hSteamAPICall, this, &SteamImpl::onRequestLobbyListCallResult);

					return pplx::create_task(*_requestLobbyListTce, taskOptions);
				}

				pplx::task<void> setLobbyJoinable(SteamIDLobby steamIDLobby, bool joinable, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					auto actionDispatcher = _wActionDispatcher.lock();
					auto taskOptions = actionDispatcher ? pplx::task_options(actionDispatcher) : pplx::task_options();

					auto steamMatchmaking = SteamMatchmaking();
					if (!steamMatchmaking)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error("SteamMatchmaking() returned null"), taskOptions, void);
					}

					if (ct.is_cancelable() && ct.is_canceled())
					{
						STORM_RETURN_TASK_CANCELED_OPT(taskOptions, void);
					}

					auto res = steamMatchmaking->SetLobbyJoinable(CSteamID(steamIDLobby), joinable);

					if (!res)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error("Steam::SetLobbyJoinable Api call failed : Player doesn't own the lobby"), taskOptions, void);
					}

					return pplx::task_from_result(taskOptions);
				}

				pplx::task<void> setLobbyData(SteamIDLobby steamIDLobby, const std::string& key, const std::string& value, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					auto actionDispatcher = _wActionDispatcher.lock();
					auto taskOptions = actionDispatcher ? pplx::task_options(actionDispatcher) : pplx::task_options();

					auto steamMatchmaking = SteamMatchmaking();
					if (!steamMatchmaking)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error("SteamMatchmaking() returned null"), taskOptions, void);
					}

					if (key.size() > k_nMaxLobbyKeyLength)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::invalid_argument("Steam.SetLobbyData failed: key size too long."), taskOptions, void);
					}

					if (value.size() > k_cubChatMetadataMax)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::invalid_argument("Steam.SetLobbyData failed: value size too long."), taskOptions, void);
					}

					if (ct.is_cancelable() && ct.is_canceled())
					{
						STORM_RETURN_TASK_CANCELED_OPT(taskOptions, void);
					}

					bool res = steamMatchmaking->SetLobbyData(CSteamID(steamIDLobby), key.c_str(), value.c_str());

					if (!res)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error("steamMatchmaking::SetLobbyData call returned failed."), taskOptions, void);
					}

					return pplx::task_from_result(taskOptions);
				}

				pplx::task<void> setLobbyMemberData(SteamIDLobby steamIDLobby, const std::string& key, const std::string& value, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					auto actionDispatcher = _wActionDispatcher.lock();
					auto taskOptions = actionDispatcher ? pplx::task_options(actionDispatcher) : pplx::task_options();

					auto steamMatchmaking = SteamMatchmaking();
					if (!steamMatchmaking)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error("SteamMatchmaking() returned null"), taskOptions, void);
					}

					if (key.size() > k_nMaxLobbyKeyLength)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::invalid_argument("Steam.SetLobbyData failed: key size too long."), taskOptions, void);
					}

					if (value.size() > k_cubChatMetadataMax)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::invalid_argument("Steam.SetLobbyData failed: value size too long."), taskOptions, void);
					}

					if (ct.is_cancelable() && ct.is_canceled())
					{
						STORM_RETURN_TASK_CANCELED_OPT(taskOptions, void);
					}

					steamMatchmaking->SetLobbyMemberData(CSteamID(steamIDLobby), key.c_str(), value.c_str());

					return pplx::task_from_result(taskOptions);
				}

				pplx::task<Lobby> requestLobbyData(SteamIDLobby steamIDLobby, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					auto actionDispatcher = _wActionDispatcher.lock();
					auto taskOptions = actionDispatcher ? pplx::task_options(actionDispatcher) : pplx::task_options();

					auto steamMatchmaking = SteamMatchmaking();
					if (!steamMatchmaking)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error("SteamMatchmaking() returned null"), taskOptions, Lobby);
					}

					pplx::task_completion_event<Lobby> requestLobbyDataTce;

					auto res = steamMatchmaking->RequestLobbyData(CSteamID(steamIDLobby));

					if (res)
					{
						std::lock_guard<std::recursive_mutex> lg(_mutex);

						_requestLobbyDataTces[steamIDLobby] = requestLobbyDataTce;

						timeout(10s, ct)
							.register_callback([steamIDLobby, wSteamImpl = STORM_WEAK_FROM_THIS()]()
								{
									if (auto steamImpl = wSteamImpl.lock())
									{
										std::lock_guard<std::recursive_mutex> lg(steamImpl->_mutex);

										auto it = steamImpl->_requestLobbyDataTces.find(steamIDLobby);
										if (it != steamImpl->_requestLobbyDataTces.end())
										{
											it->second.set_exception(pplx::task_canceled());
											steamImpl->_requestLobbyDataTces.erase(it);
										}
									}
								});
					}
					else
					{
						requestLobbyDataTce.set_exception(std::runtime_error("Steam request lobby data failed : Not connected to Steam."));
					}

					return pplx::create_task(requestLobbyDataTce, taskOptions);
				}

				SteamIDApp getAppId() override
				{
					auto steamUtils = SteamUtils();

					if (!steamUtils)
					{
						return 0;
					}

					return steamUtils->GetAppID();
				}

				pplx::task<std::unordered_map<SteamID, std::string>> queryUserIds(const std::vector<SteamID>& steamIDs, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					return getService([](auto, auto, auto) {}, [](auto, auto) {}, ct)
						.then([steamIDs, ct](std::shared_ptr<SteamService> service)
							{
								return service->queryUserIds(steamIDs, ct);
							});
				}

				pplx::task<std::unordered_map<std::string, PartyDataDto>> decodePartyDataBearerTokens(const std::unordered_map<std::string, std::string>& partyDataBearerTokens, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					return getService([](auto, auto, auto) {}, [](auto, auto) {}, ct)
						.then([partyDataBearerTokens, ct](std::shared_ptr<SteamService> service)
							{
								return service->decodePartyDataBearerTokens(partyDataBearerTokens, ct);
							});
				}

				pplx::task<bool> inLobby(SteamIDLobby steamIDLobby, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					return requestLobbyData(steamIDLobby, ct)
						.then([steamIDLobby](Lobby lobby)
							{
								auto steamUser = SteamUser();
								if (!steamUser)
								{
									return false;
								}

								SteamID steamID = steamUser->GetSteamID().ConvertToUint64();

								for (auto& kvp : lobby.lobbyMembers)
								{
									if (kvp.first == steamID)
									{
										return true;
									}
								}

								return false;
							});
				}

				pplx::task<bool> isOwner(SteamIDLobby steamIDLobby, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					return requestLobbyData(steamIDLobby, ct)
						.then([](Lobby lobby)
							{
								auto steamUser = SteamUser();
								if (!steamUser)
								{
									return false;
								}

								auto steamID = steamUser->GetSteamID();

								return (lobby.lobbyOwner != 0 && steamID == lobby.lobbyOwner);
							});
				}

				pplx::task<std::vector<SteamFriend>> getFriends(int friendsFlag = k_EFriendFlagImmediate, uint32 maxFriendsCount = UINT32_MAX, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					auto task = pplx::create_task([friendsFlag, maxFriendsCount, logger = _logger]()
						{
							auto steamFriends = SteamFriends();
							if (!steamFriends)
							{
								logger->log(LogLevel::Warn, "Steam.GetFriends", "SteamFriends() returned nullptr");
								return std::vector<SteamFriend>();
							}

							int cFriends = steamFriends->GetFriendCount(friendsFlag);

							std::vector<SteamFriend> friendsList;

							for (int i = 0; i < cFriends && (uint32)i < maxFriendsCount; i++)
							{
								CSteamID steamIDFriend = steamFriends->GetFriendByIndex(i, k_EFriendFlagImmediate);


								SteamFriend steamFriend;
								steamFriend.steamId = std::to_string(steamIDFriend.ConvertToUint64());
								steamFriend.relationship = steamFriends->GetFriendRelationship(steamIDFriend);
								friendsList.push_back(steamFriend);

							}

							return friendsList;
						});

					auto actionDispatcher = _wActionDispatcher.lock();
					auto taskOptions = actionDispatcher ? task_options(actionDispatcher, ct) : pplx::task_options(ct);
					return waitForTask<std::vector<SteamFriend>>(task, taskOptions);
				}

#pragma endregion

			private:

#pragma region private_methods


				pplx::task<VoidSteamOperationResult> onJoinLobbyAsync(JoinLobbyDto& joinLobbyDto, pplx::cancellation_token cancellationToken)
				{
					auto wSteamImpl = STORM_WEAK_FROM_THIS();
					std::weak_ptr<Stormancer::Users::UsersApi> wUsersApi = _wUsersApi;

					auto steamIDLobby = joinLobbyDto.steamIDLobby;

					std::lock_guard<std::recursive_mutex> lg(_mutex);

					// Keep steamIDLobby to leave on party leave
					_partySteamIDLobby = steamIDLobby;

					return inLobby(steamIDLobby, cancellationToken)
						.then([steamIDLobby, wSteamImpl, cancellationToken](bool inLobby)
							{
								if (inLobby)
								{
									// We already are in the lobby, do nothing
									return pplx::task_from_result();
								}
								else
								{
									// Join lobby
									auto steamImpl = wSteamImpl.lock();
									if (!steamImpl)
									{
										STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("SteamApi"), void);
									}

									return steamImpl->joinLobby(steamIDLobby, cancellationToken);
								}
							})
						.then([wSteamImpl, wUsersApi, steamIDLobby, cancellationToken]()
							{
								auto steamImpl = wSteamImpl.lock();
								if (!steamImpl)
								{
									STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("SteamApi"), void);
								}

								auto usersApi = wUsersApi.lock();
								if (!usersApi)
								{
									STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("UsersApi"), void);
								}

								auto myUserId = usersApi->userId();
								return steamImpl->setLobbyMemberData(steamIDLobby, "stormancer.userId", myUserId, cancellationToken);
							})
						.then([](pplx::task<void> t)
							{
								VoidSteamOperationResult result;
								try
								{
									t.get();
									result.success = true;
								}
								catch (std::exception& ex)
								{
									result.success = false;
									result.errorDetails = ex.what();
									result.errorId = ex.what();
								}
								return result;
							});

				}

				pplx::task<CreateLobbyResult> onCreateLobbyAsync(CreateLobbyDto& createLobbyDto, pplx::cancellation_token cancellationToken)
				{

					// Create lobby
					auto wSteamImpl = STORM_WEAK_FROM_THIS();
					return createLobby(createLobbyDto.lobbyType, createLobbyDto.maxMembers, createLobbyDto.joinable, createLobbyDto.metadata, cancellationToken)
						.then([wSteamImpl, wUsersApi = _wUsersApi, logger = _logger, cancellationToken](SteamIDLobby steamIDLobby)
							{

								auto steamImpl = wSteamImpl.lock();
								if (!steamImpl)
								{
									STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("SteamApi"), CreateLobbyResult);
								}

								auto usersApi = wUsersApi.lock();
								if (!usersApi)
								{
									STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("UsersApi"), CreateLobbyResult);
								}

								{
									std::lock_guard<std::recursive_mutex> lg(steamImpl->_mutex);

									// Keep steamIDLobby to leave on party leave
									steamImpl->_partySteamIDLobby = steamIDLobby;
								}

								auto myUserId = usersApi->userId();

								return steamImpl->setLobbyMemberData(steamIDLobby, "stormancer.userId", myUserId, cancellationToken)
									.then([steamIDLobby]()
										{
											// Send back steamIDLobby to server
											CreateLobbyResult result;
											result.success = true;
											result.steamLobbyId = steamIDLobby;
											return result;
										});
							})
						.then([](pplx::task<CreateLobbyResult> t)
							{
								try
								{
									return t.get();
								}
								catch (std::exception& ex)
								{
									CreateLobbyResult result;
									result.success = false;
									result.errorDetails = ex.what();
									result.errorId = "steamLobbyCreationFailed";
									return result;
								}
							});
				}



				void onRequestLobbyListCallResult(LobbyMatchList_t* callback, bool failure);
				CCallResult<SteamImpl, LobbyMatchList_t> _requestLobbyListCallResult;

				STEAM_CALLBACK(SteamImpl, onLobbyDataUpdateCallback, LobbyDataUpdate_t);

				STEAM_CALLBACK(SteamImpl, onLobbyInviteCallback, LobbyInvite_t);

				STEAM_CALLBACK(SteamImpl, onGameLobbyJoinRequestedCallback, GameLobbyJoinRequested_t);

				void onLobbyEnterCallResult(LobbyEnter_t* callback, bool failure);
				STEAM_CALLBACK(SteamImpl, onLobbyEnterCallback, LobbyEnter_t);

				STEAM_CALLBACK(SteamImpl, onLobbyChatUpdateCallback, LobbyChatUpdate_t);

				void onLobbyCreatedCallResult(LobbyCreated_t* callback, bool failure);
				CCallResult<SteamImpl, LobbyCreated_t> _lobbyCreatedCallResult;

				void fillLobbyData(Lobby& lobby, ISteamMatchmaking* steamMatchmaking)
				{
					CSteamID steamIDLobby(lobby.steamIDLobby);

					auto numLobbyMembers = steamMatchmaking->GetNumLobbyMembers(steamIDLobby);
					lobby.numLobbyMembers = numLobbyMembers;

					auto lobbyMemberLimit = steamMatchmaking->GetLobbyMemberLimit(steamIDLobby);
					lobby.lobbyMemberLimit = lobbyMemberLimit;

					auto lobbyOwner = steamMatchmaking->GetLobbyOwner(steamIDLobby);
					lobby.lobbyOwner = lobbyOwner.ConvertToUint64();

					for (int member = 0; member < numLobbyMembers; member++)
					{
						auto lobbyMemberSteamId = steamMatchmaking->GetLobbyMemberByIndex(steamIDLobby, member);

						if (lobbyMemberSteamId.IsValid())
						{
							LobbyMember lobbyMember;
							lobbyMember.steamID = lobbyMemberSteamId.ConvertToUint64();

							auto value = steamMatchmaking->GetLobbyMemberData(steamIDLobby, lobbyMemberSteamId, "stormancer.userId");
							if (value && strlen(value) > 0)
							{
								lobbyMember.data["stormancer.userId"] = std::string(value);
							}

							lobby.lobbyMembers.emplace(lobbyMember.steamID, lobbyMember);
						}
						else
						{
							break; // We can't get lobby member informations, we skip this step
						}
					}

					auto lobbyDataCount = steamMatchmaking->GetLobbyDataCount(steamIDLobby);
					char key[k_nMaxLobbyKeyLength];
					char value[k_cubChatMetadataMax];
					for (int lobbyData = 0; lobbyData < lobbyDataCount; lobbyData++)
					{
						if (steamMatchmaking->GetLobbyDataByIndex(steamIDLobby, lobbyData, key, k_nMaxLobbyKeyLength, value, k_cubChatMetadataMax))
						{
							lobby.data.emplace(key, value);
						}
					}
				}



				std::string convertEChatRoomEnterResponseToString(uint32 chatRoomEnterResponse)
				{
					switch (chatRoomEnterResponse)
					{
					case k_EChatRoomEnterResponseBanned:
						return "Banned";
					case k_EChatRoomEnterResponseClanDisabled:
						return "ClanDisabled";
					case k_EChatRoomEnterResponseCommunityBan:
						return "CommunityBan";
					case k_EChatRoomEnterResponseDoesntExist:
						return "DoesntExist";
					case k_EChatRoomEnterResponseError:
						return "Error";
					case k_EChatRoomEnterResponseLimited:
						return "Limited";
					case k_EChatRoomEnterResponseMemberBlockedYou:
						return "BlockedYou";
					case k_EChatRoomEnterResponseNotAllowed:
						return "NotAllowed";
					case k_EChatRoomEnterResponseRatelimitExceeded:
						return "RatelimitExceeded";
					case k_EChatRoomEnterResponseYouBlockedMember:
						return "YouBlockedMember";
					case k_EChatRoomEnterResponseFull:
						return "Full";
					case k_EChatRoomEnterResponseSuccess:
						return "Success";
					default:
						return "Unknow-" + std::to_string(chatRoomEnterResponse);
					}
				}

#pragma endregion

#pragma region private_members

				struct LobbyEnterEventData
				{
					pplx::task_completion_event<void> tce;
					CCallResult<SteamImpl, LobbyEnter_t> callResult;
				};

				pplx::cancellation_token_source _cts;
				std::recursive_mutex _mutex;
				SteamIDLobby _partySteamIDLobby = 0;
				Subscription _gameConnectionStateSub;
				std::unordered_map<SteamIDLobby, pplx::task_completion_event<Lobby>> _requestLobbyDataTces;
				std::shared_ptr<pplx::task_completion_event<std::vector<Lobby>>> _requestLobbyListTce; // shared_ptr is used as an optional
				std::unordered_map<SteamIDLobby, LobbyEnterEventData> _lobbyEnterEventData;
				std::shared_ptr<pplx::task_completion_event<SteamIDLobby>> _lobbyCreatedTce; // shared_ptr is used as an optional

				std::shared_ptr<ILogger> _logger;
				std::weak_ptr<SteamState> _wSteamConfig;
				std::weak_ptr<IScheduler> _wScheduler;
				std::weak_ptr<IActionDispatcher> _wActionDispatcher;
				std::weak_ptr<Users::UsersApi> _wUsersApi;
				std::weak_ptr<Party::PartyApi> _wPartyApi;
				std::weak_ptr<Party::Platform::InvitationMessenger> _wInvitationMessenger;

#pragma endregion
			};

			inline void SteamImpl::onRequestLobbyListCallResult(LobbyMatchList_t* callback, bool failure)
			{
				_logger->log(LogLevel::Trace, "Steam", "Lobby list requested", "failure=" + std::to_string(failure) + " m_nLobbiesMatching=" + std::to_string(callback->m_nLobbiesMatching));

				std::lock_guard<std::recursive_mutex> lg(_mutex);

				if (failure)
				{
					_requestLobbyListTce->set_exception(std::runtime_error("Steam request lobby list failed"));
					return;
				}

				auto steamMatchmaking = SteamMatchmaking();
				if (!steamMatchmaking)
				{
					_requestLobbyListTce->set_exception(std::runtime_error("SteamMatchmaking() returned null"));
					return;
				}

				std::vector<Lobby> lobbies;

				for (uint32 iLobby = 0; iLobby < callback->m_nLobbiesMatching; iLobby++)
				{
					Lobby lobby;

					try
					{
						auto steamIDLobby = steamMatchmaking->GetLobbyByIndex(iLobby);
						lobby.steamIDLobby = steamIDLobby.ConvertToUint64();
						fillLobbyData(lobby, steamMatchmaking);
					}
					catch (const std::exception& ex)
					{
						_requestLobbyListTce->set_exception(ex);
					}

					lobbies.push_back(lobby);
				}

				_requestLobbyListTce->set(lobbies);
			}

			inline void SteamImpl::onLobbyDataUpdateCallback(LobbyDataUpdate_t* callback)
			{

				if (!callback || !CSteamID(callback->m_ulSteamIDLobby).IsValid() || !CSteamID(callback->m_ulSteamIDMember).IsValid())
				{
					return;
				}

				// We only watch lobby changes for requestLobbyData calls (not user changes)
				if (callback->m_ulSteamIDLobby == callback->m_ulSteamIDMember) // The lobby itself changed
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					auto it = _requestLobbyDataTces.find(callback->m_ulSteamIDLobby);
					if (it != _requestLobbyDataTces.end())
					{
						auto requestLobbyDataTce = it->second;
						_requestLobbyDataTces.erase(it);

						if (!callback->m_bSuccess)
						{
							_logger->log(LogLevel::Error, "Steam", std::string() + "Update lobby data failed", "");

							requestLobbyDataTce.set_exception(std::runtime_error("Steam request lobby data failed (success == false)"));
						}

						auto steamMatchmaking = SteamMatchmaking();
						if (!steamMatchmaking)
						{
							requestLobbyDataTce.set_exception(std::runtime_error("SteamMatchmaking() returned null"));
							return;
						}

						Lobby lobby;

						try
						{
							lobby.steamIDLobby = callback->m_ulSteamIDLobby;
							fillLobbyData(lobby, steamMatchmaking);
						}
						catch (const std::exception& ex)
						{
							_logger->log(LogLevel::Error, "Steam", std::string() + "Fill lobby data failed", ex.what());

							requestLobbyDataTce.set_exception(ex);
							return;
						}
						_logger->log(LogLevel::Info, "Steam", std::string() + "Lobby data updated", " islobby=" + std::to_string(callback->m_ulSteamIDLobby == callback->m_ulSteamIDMember) + " lobby=" + std::to_string(callback->m_ulSteamIDLobby) + " member=" + std::to_string(callback->m_ulSteamIDMember));

						requestLobbyDataTce.set(lobby);
					}
					else
					{
					}
				}
				else // Lobby member changed
				{
				}
			}

			inline void SteamImpl::onGameLobbyJoinRequestedCallback(GameLobbyJoinRequested_t* callback)
			{
				if (!callback->m_steamIDLobby.IsValid())
				{
					_logger->log(LogLevel::Warn, "Steam", "onGameLobbyJoinRequestedCallback skipped", "SteamIDLobby invalid");
					return;
				}

				SteamIDLobby steamIDLobby = callback->m_steamIDLobby.ConvertToUint64();

				_logger->log(LogLevel::Trace, "Steam", "Game lobby join requested", std::to_string(steamIDLobby));

				SteamID senderId = callback->m_steamIDFriend.ConvertToUint64();

				Party::PartyId partyId;
				partyId.id = std::to_string(steamIDLobby);
				partyId.type = PARTY_TYPE_STEAMIDLOBBY;
				partyId.platform = platformName;

				auto invitationMessenger = _wInvitationMessenger.lock();
				if (!invitationMessenger)
				{
					_logger->log(LogLevel::Warn, "Steam", "onGameLobbyJoinRequestedCallback skipped", "Invitation messenger deleted");
					return;
				}

				auto steamPartyInvitation = std::make_shared<SteamPartyInvitation>(partyId, std::to_string(senderId));
				invitationMessenger->notifyInvitationReceived(steamPartyInvitation);
			}

			inline void SteamImpl::onLobbyCreatedCallResult(LobbyCreated_t* callback, bool failure)
			{
				std::lock_guard<std::recursive_mutex> lg(_mutex);



				if (failure || callback->m_eResult != EResult::k_EResultOK)
				{
					_logger->log(LogLevel::Info, "Steam", "Lobby creation failed", convertEResultToString(callback->m_eResult));

					_lobbyCreatedTce->set_exception(std::runtime_error("Create lobby failed (" + convertEResultToString(callback->m_eResult) + ")"));
					return;
				}
				_logger->log(LogLevel::Info, "Steam", "Lobby created", std::to_string(callback->m_ulSteamIDLobby));
				_lobbyCreatedTce->set(callback->m_ulSteamIDLobby);
			}

			inline void SteamImpl::onLobbyEnterCallback(LobbyEnter_t* callback)
			{
				onLobbyEnterCallResult(callback, false);
			}

			inline void SteamImpl::onLobbyEnterCallResult(LobbyEnter_t* callback, bool failure)
			{

				std::lock_guard<std::recursive_mutex> lg(_mutex);

				auto it = _lobbyEnterEventData.find(callback->m_ulSteamIDLobby);
				if (it != _lobbyEnterEventData.end())
				{
					if (failure || callback->m_EChatRoomEnterResponse != k_EChatRoomEnterResponseSuccess)
					{
						_logger->log(LogLevel::Info, "Steam", "Failed to join steam lobby " + std::to_string(callback->m_ulSteamIDLobby), convertEChatRoomEnterResponseToString(callback->m_EChatRoomEnterResponse));

						it->second.tce.set_exception(std::runtime_error("steam.joinLobbyFailed(" + convertEChatRoomEnterResponseToString(callback->m_EChatRoomEnterResponse) + ")"));
						return;
					}
					else
					{
						_logger->log(LogLevel::Info, "Steam", "Joined steam lobby", std::to_string(callback->m_ulSteamIDLobby));

					}

					it->second.tce.set();
				}
			}

			inline void SteamImpl::onLobbyChatUpdateCallback(LobbyChatUpdate_t* /*callback*/)
			{}

			inline void SteamImpl::onLobbyInviteCallback(LobbyInvite_t* /*callback*/)
			{}

			class SteamPartyProvider : public Party::Platform::IPlatformSupportProvider
			{
			public:

#pragma region public_methods

				SteamPartyProvider(
					std::shared_ptr<Party::Platform::InvitationMessenger> messenger,
					std::shared_ptr<Users::UsersApi> usersApi,
					std::shared_ptr<details::SteamImpl> steamApi,
					std::shared_ptr<ILogger> logger,
					std::shared_ptr<Party::PartyApi> partyApi,
					std::shared_ptr<IActionDispatcher> actionDispatcher
				)
					: IPlatformSupportProvider(messenger)
					, _wUsersApi(usersApi)
					, _wSteamApi(steamApi)
					, _logger(logger)
					, _wPartyApi(partyApi)
					, _wActionDispatcher(actionDispatcher)
				{}

				std::string getPlatformName() override
				{
					return platformName;
				}

				pplx::task<Party::PartyId> getPartyId(const Party::PartyId& partyId, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					if (partyId.type != PARTY_TYPE_STEAMIDLOBBY)
					{
						assert(false);
						STORM_RETURN_TASK_FROM_EXCEPTION(std::runtime_error("Unknown PartyId type"), Party::PartyId);
					}

					_logger->log(LogLevel::Trace, "SteamPartyProvider::getPartyId", "Retrieve partyId from Steam lobby metadata", partyId.id);

					std::lock_guard<std::recursive_mutex> lg(_mutex);

					auto steamApi = _wSteamApi.lock();

					// Get lobby data
					return steamApi->requestLobbyData(std::stoull(partyId.id), ct)
						.then([wSteamApi = _wSteamApi, ct, logger = _logger](Lobby lobby)
							{
								auto steamApi = wSteamApi.lock();
								if (!steamApi)
								{
									throw ObjectDeletedException("SteamApi");
								}

								auto it = lobby.data.find("partyDataToken");
								if (it == lobby.data.end())
								{
									throw std::runtime_error("partyDataToken not found in Steam lobby data");
								}

								// If the "partyDataToken" metadata is found in the Steam lobby, we can join the associated party.
								// And if the party is joined, the server will ask to join the Steam lobby.
								auto partyDataToken = it->second;

								return steamApi->decodePartyDataBearerTokens(std::unordered_map<std::string, std::string>{ { std::to_string(lobby.steamIDLobby), partyDataToken } }, ct)
									.then([wSteamApi, logger](std::unordered_map<std::string, PartyDataDto> dtos)
										{
											if (dtos.size() != 1)
											{
												throw std::runtime_error("decodePartyDataBearerTokens returned invalid result size");
											}

											auto& partyDataDto = dtos.begin()->second;

											if (partyDataDto.partyId.size() == 0)
											{
												throw std::runtime_error("Invalid partyId");
											}

											logger->log(LogLevel::Trace, "SteamPartyProvider::getPartyId", "PartyId obtained", partyDataDto.partyId);

											Party::PartyId partyId;
											partyId.id = partyDataDto.partyId;
											partyId.type = Party::PartyId::TYPE_PARTY_ID;

											return partyId;
										});
							});
				}

				pplx::task<void> createOrJoinSessionForParty(const std::string& /*partySceneId*/) override
				{
					return pplx::task_from_result();
				}

				pplx::task<void> leaveSessionForParty(const std::string& /*partySceneId*/) override
				{
					auto steamApi = _wSteamApi.lock();

					if (!steamApi)
					{
						auto actionDispatcher = _wActionDispatcher.lock();
						auto taskOptions = actionDispatcher ? pplx::task_options(actionDispatcher) : pplx::task_options();
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(ObjectDeletedException("SteamApi"), taskOptions, void);
					}

					std::lock_guard<std::recursive_mutex> lg(steamApi->_mutex);

					auto partySteamIDLobby = steamApi->_partySteamIDLobby;

					if (partySteamIDLobby == 0)
					{
						return pplx::task_from_result();
					}

					steamApi->_partySteamIDLobby = 0;

					return steamApi->leaveLobby(partySteamIDLobby);
				}

				pplx::task<void> kickPlayer(const std::string&) override
				{
					return pplx::task_from_result();
				}

				pplx::task<void> updateSessionMembers(const Party::MembersUpdate& update) override
				{
					auto steamApi = _wSteamApi.lock();

					auto actionDispatcher = _wActionDispatcher.lock();
					auto taskOptions = actionDispatcher ? pplx::task_options(actionDispatcher) : pplx::task_options();

					if (!steamApi)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(ObjectDeletedException("SteamApi"), taskOptions, void);
					}

					std::lock_guard<std::recursive_mutex> lg(steamApi->_mutex);

					if (steamApi->_partySteamIDLobby == 0)
					{
						return pplx::task_from_result();
					}

					auto usersApi = _wUsersApi.lock();
					if (!usersApi)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(ObjectDeletedException("UsersApi"), taskOptions, void);
					}

					auto ct = timeout(10s);

					auto myUserId = usersApi->userId();

					std::vector<pplx::task<void>> updates;
					for (auto& updatedMember : update.updatedMembers)
					{
						if (updatedMember.changes.test(Party::MembersUpdate::PromotedToLeader))
						{
							auto updateTask = steamApi->isOwner(steamApi->_partySteamIDLobby, ct)
								.then([update, wSteamApi = _wSteamApi, logger = _logger](bool isOwner)
									{
										if (!isOwner)
										{
											throw std::runtime_error("Not lobby owner");
										}

										auto scene = update.partyApi->getPartyScene();

										if (!scene)
										{
											throw std::runtime_error("Party scene is null");
										}

										auto steamPartyService = scene->dependencyResolver().resolve<SteamPartyService>();

										return steamPartyService->createPartyDataBearerToken(timeout(10s))
											.then([wSteamApi, logger](std::string dataBearerToken)
												{
													auto _steamApi = wSteamApi.lock();
													if (!_steamApi)
													{
														STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("SteamApi"), void);
													}

													return _steamApi->setLobbyData(_steamApi->_partySteamIDLobby, "partyDataToken", dataBearerToken, timeout(10s));
												})
											.then([logger](pplx::task<void> task)
												{
													try
													{
														return task.get();
													}
													catch (const std::exception& ex)
													{
														logger->log(LogLevel::Error, "Steam", ex.what());
													}
												});
									});
							updates.push_back(updateTask);
						}
					}

					return pplx::when_all(updates.begin(), updates.end());
				}

				pplx::task<void> updateSessionSettings(const Party::PartySettings& settings) override
				{
					auto actionDispatcher = _wActionDispatcher.lock();
					auto taskOptions = actionDispatcher ? pplx::task_options(actionDispatcher) : pplx::task_options();

					auto partyApi = _wPartyApi.lock();
					auto steamApi = _wSteamApi.lock();
					auto steamMatchmaking = SteamMatchmaking();
					if (partyApi && partyApi->isLeader() && steamApi && steamMatchmaking)
					{
						auto steamIDLobbyIt = settings.publicServerData.find("SteamIDLobby");
						if (steamIDLobbyIt != settings.publicServerData.end())
						{
							auto steamIDLobby = std::stoull(steamIDLobbyIt->second);
							if (steamIDLobby != 0)
							{
								_logger->log(LogLevel::Debug, "Steam", std::string() + "Setting lobby " + (settings.isJoinable ? "" : "not ") + "joinable", std::to_string(steamIDLobby));

								return steamApi->setLobbyJoinable(steamIDLobby, settings.isJoinable)
									.then([settings, steamIDLobby, logger = _logger]
										{
											logger->log(LogLevel::Debug, "Steam", std::string() + "Lobby " + (settings.isJoinable ? "" : "not ") + "joinable set", std::to_string(steamIDLobby));
										});
							}
						}
					}
					return pplx::task_from_result(taskOptions);
				}

				pplx::task<std::vector<Party::AdvertisedParty>> getAdvertisedParties(pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					auto actionDispatcher = _wActionDispatcher.lock();
					auto taskOptions = actionDispatcher ? pplx::task_options(actionDispatcher) : pplx::task_options();

					auto steamFriends = SteamFriends();
					if (!steamFriends)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error("SteamFriends() returned nullptr"), taskOptions, std::vector<Party::AdvertisedParty>);
					}

					auto steamApi = _wSteamApi.lock();
					if (!steamApi)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(ObjectDeletedException("SteamApi"), taskOptions, std::vector<Party::AdvertisedParty>);
					}

					int cFriends = steamFriends->GetFriendCount(k_EFriendFlagImmediate);
					SteamIDApp appId = steamApi->getAppId();

					auto mapSteamIDLobbyToFriend = std::make_shared<std::unordered_map<SteamIDLobby, SteamIDFriend>>();
					std::vector<pplx::task<Steam::Lobby>> lobbyTasks;

					for (int i = 0; i < cFriends; i++)
					{
						FriendGameInfo_t friendGameInfo;
						CSteamID steamIDFriend = steamFriends->GetFriendByIndex(i, k_EFriendFlagImmediate);
						if (
							steamFriends->GetFriendGamePlayed(steamIDFriend, &friendGameInfo)
							&& friendGameInfo.m_gameID.IsValid()
							&& friendGameInfo.m_gameID.ToUint64() == appId
							&& friendGameInfo.m_steamIDLobby.IsValid()
							)
						{
							auto steamIDLobby = friendGameInfo.m_steamIDLobby.ConvertToUint64();

							(*mapSteamIDLobbyToFriend)[steamIDLobby] = steamIDFriend.ConvertToUint64();

							auto task = steamApi->requestLobbyData(steamIDLobby, ct);
							lobbyTasks.push_back(task);
							task.then([logger = _logger](pplx::task<Steam::Lobby> task)
								{
									try
									{
										task.get();
									}
									catch (const std::exception& ex)
									{
										logger->log(LogLevel::Error, "Steam", "Request lobby data failed", ex);
									}
								});
						}
					}

					auto advertisedParties = std::make_shared<std::vector<Party::AdvertisedParty>>();

					return pplx::when_all(lobbyTasks.begin(), lobbyTasks.end())
						.then([steamApi, mapSteamIDLobbyToFriend, advertisedParties, ct, logger = _logger](std::vector<Steam::Lobby> lobbies)
							{
								std::unordered_map<std::string, std::string> partyDataTokens;

								for (auto& lobby : lobbies)
								{
									auto it = lobby.data.find("partyDataToken");
									if (it != lobby.data.end())
									{
										auto it2 = mapSteamIDLobbyToFriend->find(lobby.steamIDLobby);
										if (it2 != mapSteamIDLobbyToFriend->end())
										{
											auto partyDataToken = it->second;
											partyDataTokens[std::to_string(lobby.steamIDLobby)] = partyDataToken;

											auto& steamIDFriend = it2->second;
											Party::AdvertisedParty advertisedParty;
											advertisedParty.metadata["steam.steamIDFriend"] = std::to_string(steamIDFriend);
											advertisedParty.metadata["steam.steamIDLobby"] = std::to_string(lobby.steamIDLobby);
											advertisedParty.metadata["steam.lobbyOwner"] = std::to_string(lobby.lobbyOwner);
											advertisedParty.metadata["steam.lobbyMemberLimit"] = std::to_string(lobby.lobbyMemberLimit);
											advertisedParty.metadata["steam.numLobbyMembers"] = std::to_string(lobby.numLobbyMembers);
											for (auto& kvp : lobby.data)
											{
												advertisedParty.metadata["steam.lobbyData." + kvp.first] = kvp.second;
											}
											advertisedParties->push_back(advertisedParty);
										}
									}
								}

								auto task = partyDataTokens.size() > 0
									? steamApi->decodePartyDataBearerTokens(partyDataTokens, ct)
									: pplx::task_from_result(std::unordered_map<std::string, PartyDataDto>());

								return task;
							})
						.then([steamApi, advertisedParties, ct, logger = _logger](std::unordered_map<std::string, PartyDataDto> dtos)
							{
								std::vector<SteamID> steamIDs;

								for (auto& advertisedParty : *advertisedParties)
								{
									auto it = dtos.find(advertisedParty.metadata["steam.steamIDLobby"]);
									if (it != dtos.end())
									{
										auto& dto = it->second;
										advertisedParty.partyId.id = dto.partyId;
										advertisedParty.partyId.type = Party::PartyId::TYPE_PARTY_ID;
										advertisedParty.leaderUserId = dto.leaderUserId;

										steamIDs.push_back(std::stoull(advertisedParty.metadata["steam.steamIDFriend"]));
									}
								}

								return steamApi->queryUserIds(steamIDs, ct);
							})
						.then([advertisedParties](std::unordered_map<SteamID, std::string> mapSteamIdToUserId)
							{
								for (auto& advertisedParty : *advertisedParties)
								{
									auto it = mapSteamIdToUserId.find(std::stoull(advertisedParty.metadata["steam.steamIDFriend"]));
									if (it != mapSteamIdToUserId.end())
									{
										auto& friendId = it->second;
										advertisedParty.metadata["stormancer.friendId"] = friendId;
									}
								}

								return *advertisedParties;
							});
				}

				bool tryShowSystemInvitationUI(std::shared_ptr<Party::PartyApi> partyApi) override
				{
					std::lock_guard<std::recursive_mutex> lg(_mutex);

					auto steamFriends = SteamFriends();
					if (!steamFriends)
					{
						_logger->log(LogLevel::Error, "Steam", "SteamFriends() returned nullptr");
						return false;
					}

					if (!partyApi->isInParty())
					{
						_logger->log(LogLevel::Error, "Steam", "Not in a party");
						return false;
					}

					auto steamApi = _wSteamApi.lock();

					if (!steamApi)
					{
						_logger->log(LogLevel::Error, "Steam", "SteamApi deleted");
						return false;
					}

					auto partySteamIDLobby = steamApi->_partySteamIDLobby;

					if (partySteamIDLobby == 0)
					{
						_logger->log(LogLevel::Error, "Steam", "Not in a lobby");
						return false;
					}

					steamFriends->ActivateGameOverlayInviteDialog(CSteamID(partySteamIDLobby));

					return true;
				}

#pragma endregion

#pragma region private_members

			private:

				std::recursive_mutex _mutex;
				std::shared_ptr<ILogger> _logger;
				std::weak_ptr<Users::UsersApi> _wUsersApi;
				std::weak_ptr<details::SteamImpl> _wSteamApi;
				std::weak_ptr<Party::PartyApi> _wPartyApi;
				std::weak_ptr<IActionDispatcher> _wActionDispatcher;

#pragma endregion
			};
		}



		// https://partner.steamgames.com/doc/features/auth#client_to_backend_webapi
		// https://partner.steamgames.com/doc/api/ISteamUser#GetAuthSessionTicket

		class SteamAuthenticationEventHandler : public std::enable_shared_from_this<SteamAuthenticationEventHandler>, public Users::IAuthenticationEventHandler
		{
		public:

#pragma region public_methods

			SteamAuthenticationEventHandler(std::shared_ptr<details::SteamState> steamConfig)
				: _steamState(steamConfig)
			{}

			pplx::task<void> retrieveCredentials(const Users::CredentialsContext& context) override
			{
				return getSteamCredentials([context](const std::string& type, const std::string& provider, const std::string& steamTicketHex)
					{
						context.authParameters->type = type;
						context.authParameters->parameters["provider"] = provider;
						context.authParameters->parameters["ticket"] = steamTicketHex;
						context.authParameters->parameters["version"] = "v1";
						context.authParameters->parameters["appId"] = std::to_string(SteamUtils()->GetAppID());
					});
			}

			virtual pplx::task<void> renewCredentials(const Users::CredentialsRenewalContext& context) override
			{
				return getSteamCredentials([context](const std::string& /*type*/, const std::string& provider, const std::string& steamTicketHex)
					{
						context.response->parameters["provider"] = provider;
						context.response->parameters["ticket"] = steamTicketHex;
						context.response->parameters["version"] = "v1";
						context.response->parameters["appId"] = std::to_string(SteamUtils()->GetAppID());

					});
			}

			pplx::task<void> getSteamCredentials(std::function<void(const std::string& type, const std::string& provider, const std::string& steamTicketHex)> fulfillCredentialsCallback)
			{
				if (!_steamState->getAuthenticationEnabled())
				{
					return pplx::task_from_result();
				}

				/*if (!SteamAPI_IsSteamRunning())
				{
					throw std::runtime_error("Steam is not running");
				}*/

				std::lock_guard<std::recursive_mutex> lg(_mutex);




				std::string steamTicketHex;

				std::shared_ptr<std::vector<byte>> steamTicket;

				auto steamUser = SteamUser();
				if (!steamUser)
				{
					return pplx::task_from_exception<void>(Stormancer::ObjectDeletedException("ISteamUser null"));
				}

				if (_steamState->getBackendIdentity().empty())
				{
					return pplx::task_from_exception<void>(std::runtime_error("config->additionalParameters[\"steam.backendIdentity\"] must be set to a non empty value."));
				}

				auto hAuthTicket = steamUser->GetAuthTicketForWebApi(_steamState->getBackendIdentity().c_str());

				auto ctx = std::make_shared<details::GetAuthSessionTokenForWebApiContext>(hAuthTicket);


				if (hAuthTicket == k_HAuthTicketInvalid)
				{
					throw std::runtime_error("Steam : invalid user authentication ticket");
				}





				return pplx::create_task(ctx->tce)
					.then([fulfillCredentialsCallback, ctx](std::string steamTicketHex)
						{
							fulfillCredentialsCallback(platformName, platformName, steamTicketHex);
						});
			}

#pragma endregion

		private:

#pragma region private_methods

			STEAM_CALLBACK(SteamAuthenticationEventHandler, onAuthSessionTicket, GetAuthSessionTicketResponse_t);

#pragma endregion

#pragma region private_members

			std::recursive_mutex _mutex;
			std::shared_ptr<details::SteamState> _steamState;
			std::shared_ptr<pplx::task_completion_event<void>> _authTce; // shared_ptr used as an optional

#pragma endregion
		};

		inline void SteamAuthenticationEventHandler::onAuthSessionTicket(GetAuthSessionTicketResponse_t* callback)
		{
			std::lock_guard<std::recursive_mutex> lg(_mutex);

			if (callback->m_eResult != EResult::k_EResultOK)
			{
				_authTce->set_exception(std::runtime_error("Steam GetAuthSessionTicket failed : EResult = " + std::to_string((int)callback->m_eResult)));
			}

			if (callback->m_hAuthTicket == k_HAuthTicketInvalid)
			{
				_authTce->set_exception(std::runtime_error("Steam GetAuthSessionTicket failed : Invalid user authentication ticket"));
			}

			_authTce->set();
		}

		class SteamPlugin : public IPlugin
		{
		public:

			static constexpr const char* PLUGIN_NAME = "Steam";
			static constexpr const char* PLUGIN_VERSION = "1.0.0";

			PluginDescription getDescription() override
			{
				return PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
			}

		private:

			void registerClientDependencies(ContainerBuilder& builder) override
			{
				builder.registerDependency<details::SteamState, Configuration, ILogger>().singleInstance();
				builder.registerDependency<details::SteamImpl, Users::UsersApi, details::SteamState, Configuration, IScheduler, ILogger, Party::PartyApi, Party::Platform::InvitationMessenger>().asSelf().as<SteamApi>().singleInstance();
				builder.registerDependency<details::SteamPartyProvider, Party::Platform::InvitationMessenger, Users::UsersApi, details::SteamImpl, ILogger, Party::PartyApi, IActionDispatcher>().as<Party::Platform::IPlatformSupportProvider>();
				builder.registerDependency<SteamAuthenticationEventHandler, details::SteamState>().as<Users::IAuthenticationEventHandler>();
			}

			void clientCreated(std::shared_ptr<IClient> client)
			{
				auto steamApi = client->dependencyResolver().resolve<SteamApi>();
				steamApi->initialize();
			}

			void registerSceneDependencies(ContainerBuilder& builder, std::shared_ptr<Scene> scene) override
			{
				if (scene->getHostMetadata(SteamApi::METADATA_KEY).length() > 0)
				{
					builder.registerDependency<details::SteamService, Scene>();
				}

				if (scene->getHostMetadata(Party::details::PartyService::METADATA_KEY).length() > 0)
				{
					builder.registerDependency<details::SteamPartyService, Scene>();
				}
			}

			void sceneCreated(std::shared_ptr<Scene> scene)
			{
				if (scene->getHostMetadata(Party::details::PartyService::METADATA_KEY).length() > 0)
				{
					auto service = scene->dependencyResolver().resolve<details::SteamImpl>();
					service->initializePartyScene(scene);
				}
				if (scene->getHostMetadata(Friends::FriendsPlugin::METADATA_KEY).length() > 0)
				{
					auto service = scene->dependencyResolver().resolve<details::SteamImpl>();
					service->initializeFriendsScene(scene);
				}
			}
		};
	}
}

MSGPACK_ADD_ENUM(ELobbyType);
