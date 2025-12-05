// Steam client library for Stormancer
// Copyright (C) 2025 Stormancer
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE 
// SOFTWARE.

#pragma once

#include "Friends/Friends.hpp"
#include "Party/Party.hpp"
#include "Users/Users.hpp"
#include "Users/Environment.hpp"

#include "stormancer/Configuration.h"
#include "stormancer/IPlugin.h"
#include "stormancer/IScheduler.h"
#include "stormancer/StormancerTypes.h"
#include "stormancer/Utilities/PointerUtilities.h"
#include "stormancer/Utilities/TaskUtilities.h"
#include "stormancer/cpprestsdk/cpprest/asyncrt_utils.h"
#include "stormancer/P2P/IP2PConnectivityProvider.h"
#include "stormancer/IConnectionManager.h"
#include "stormancer/DependencyInjection.h"
#include "stormancer/IPacketDispatcher.h"

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
			/// The LobbyID is available in the program launch arguments after the string "+connect_lobby".
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
			bool online;
			std::string personaName;

			MSGPACK_DEFINE(steamId, relationship, friend_since, online, personaName);
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

			virtual pplx::task<void> joinLobby(SteamIDLobby steamIDLobby, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			virtual pplx::task<void> leaveLobby(SteamIDLobby steamIDLobby, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			virtual pplx::task<Lobby> requestLobbyData(SteamIDLobby steamIDLobby, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			virtual pplx::task<std::vector<Lobby>> requestLobbyList(LobbyFilter lobbyFilter = LobbyFilter(), pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			virtual pplx::task<void> setLobbyJoinable(SteamIDLobby steamIDLobby, bool joinable, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			virtual pplx::task<void> setLobbyData(SteamIDLobby steamIDLobby, const std::string& key, const std::string& value, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			virtual pplx::task<void> setLobbyMemberData(SteamIDLobby steamIDLobby, const std::string& key, const std::string& value, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			// Steam Utils

			virtual SteamIDApp getAppId() = 0;

			virtual int getAppBuildId() = 0;
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

		class ISteamTickEventHandler
		{
		public:
			virtual void tick() = 0;
			virtual void onSteamNetworkingMessagesSessionRequestCallback(uint64 steamId) = 0;
		};

		namespace details
		{
			class SteamNetworkingConnection : public IConnection
			{
			public:
				SteamNetworkingConnection(const std::string& account,const std::string& app,const SteamNetworkingIdentity& id,const SessionId& sessionId, DependencyScope& currentScope)
					: _steamIdentity(id)
					, _sessionId(sessionId)
				{
					_dependencyResolver = currentScope.beginLifetimeScope();
				}

				void send(const StreamWriter& streamWriter, int channelUid, PacketPriority priority = PacketPriority::MEDIUM_PRIORITY, PacketReliability reliability = PacketReliability::RELIABLE_ORDERED, const TransformMetadata& transformMetadata = TransformMetadata()) override
				{
					obytestream s;
					streamWriter(s);
					size_t length = s.tellp();
					s.seekp(0);
					
					SteamNetworkingMessages()->SendMessageToUser(_steamIdentity, s.currentPtr(), length,getSendFlags(priority,reliability),1);
				}
				void setApplication(const std::string& account, const std::string& application) override
				{
					_account = account;
					_application = application;
				}

				void close(const std::string& reason) override
				{
					if (!_steamIdentity.IsInvalid())
					{
						SteamNetworkingMessages()->CloseSessionWithUser(_steamIdentity);
					}
				}

				std::string ipAddress() const override
				{
					if (_steamIdentity.IsInvalid())
					{
						return "";
					}
					SteamNetConnectionInfo_t connectionInfo;
					SteamNetConnectionRealTimeStatus_t realTimeStatus;
					SteamNetworkingMessages()->GetSessionConnectionInfo(_steamIdentity, &connectionInfo, &realTimeStatus);
				
					char  buf[connectionInfo.m_addrRemote.k_cchMaxString];
					connectionInfo.m_addrRemote.ToString(buf, connectionInfo.m_addrRemote.k_cchMaxString, true);

					return buf;
				}

				int ping() const override
				{
					if (_steamIdentity.IsInvalid())
					{
						return -1;
					}
					SteamNetConnectionInfo_t connectionInfo;
					SteamNetConnectionRealTimeStatus_t realTimeStatus;
					SteamNetworkingMessages()->GetSessionConnectionInfo(_steamIdentity, &connectionInfo, &realTimeStatus);

					return realTimeStatus.m_nPing;
				}

				std::string key() const override
				{
					char  buf[_steamIdentity.k_cchMaxGenericString];
					_steamIdentity.ToString(buf, _steamIdentity.k_cchMaxGenericString);

					return buf;
					
				}

				time_t connectionDate() const override
				{
					return _connectionDate;
				}

				const std::string& account() const override
				{
					return _account;
				}

				const std::string& application() const override
				{
					return _application;
				}

				std::string metadata(const std::string& key) const override
				{
					auto it = _metadata.find(key);
					if (it != _metadata.end())
					{
						return (*it).second;
					}
					else
					{
						return std::string();
					}
				}

				const std::unordered_map<std::string, std::string>& metadata() const override
				{
					return _metadata;
				}

				void setMetadata(const std::unordered_map<std::string, std::string>& metadata) override
				{
					_metadata = metadata;
				}

				void setMetadata(const std::string& key, const std::string& value) override
				{
					_metadata[key] = value;
				}

				
				const DependencyScope& dependencyResolver() const override
				{
					return _dependencyResolver;
				}

				/// Returns the connection state.
				ConnectionState getConnectionState() const override
				{
					return _connectionState;
				}

				rxcpp::observable<ConnectionState> getConnectionStateChangedObservable() const override
				{
					return _connectionStateObservable.get_observable();
				}

				
				const SessionId& sessionId() const override { return _sessionId; }

				/// <summary>
				/// Test-and-set whether this connection has already been used to connect to a scene.
				/// </summary>
				/// <remarks>
				/// This check is useful if you want to perform some operations only the first time the connection connects to a scene.
				/// </remarks>
				/// <returns><c>true</c> if this is the first scene that this connection is connecting to, <c>false</c> otherwise.</returns>
				bool trySetInitialSceneConnection()
				{
					bool expected = false;
					return _isConnectedToAScene.compare_exchange_strong(expected, true);
				}

			
				uint64 getTypeHash() const override
				{
					return Stormancer::getTypeHash<SteamNetworkingConnection>();
				}

			protected:

				int getSendFlags(PacketPriority priority, PacketReliability reliability)
				{
					
					int flag = 0; 
					if (priority == PacketPriority::IMMEDIATE_PRIORITY)
					{
						flag |= k_nSteamNetworkingSend_NoNagle;
					}

					if ((reliability & PacketReliability::RELIABLE) != 0)
					{
						flag |= k_nSteamNetworkingSend_Reliable;
					}
					else
					{
						flag |= k_nSteamNetworkingSend_Unreliable;
					}

					return flag;
				}
				void setSessionId(const SessionId& sessionId) override
				{
					_sessionId = sessionId;
				}

				void setConnectionState(ConnectionState connectionState) override
				{
					_connectionState = connectionState;

					auto subscriber = _connectionStateObservable.get_subscriber();
					subscriber.on_next(_connectionState);

					if (_connectionState == ConnectionState::Disconnected)
					{
						_connectionStateObservable.get_subscriber().on_completed();
					}
				}

			private:
				std::string _account;
				std::string _application;
				std::unordered_map<std::string, std::string> _metadata;
				time_t _connectionDate = std::chrono::system_clock::to_time_t(std::chrono::system_clock::now());
				DependencyScope _dependencyResolver;
				SessionId _sessionId;
				SteamNetworkingIdentity _steamIdentity;

				std::atomic_bool _isConnectedToAScene = { false };

				rxcpp::subjects::subject<ConnectionState> _connectionStateObservable;

				ConnectionState _connectionState = ConnectionState::Disconnected;
			};
			class SteamP2PConnectivityProvider : public IP2PConnectivityProvider, public ISteamTickEventHandler
			{
			public:

				SteamP2PConnectivityProvider(std::shared_ptr<IConnectionManager> connections,std::shared_ptr<IClient> client, std::shared_ptr<ILogger> logger)
					:_connections(connections)
					, _client(client)
					,_logger(logger)
				{

				}

				virtual ~SteamP2PConnectivityProvider() {}

				void processSystemMessage(const SteamNetworkingIdentity& origin, const void* buffer, const int length)
				{
					Serializer serializer;
					ibytestream stream((byte*)buffer, length);
					SessionId sessionId = serializer.deserializeOne<SessionId>(stream);

					auto c = std::make_shared<SteamNetworkingConnection>(std::string(), std::string(), origin, sessionId, _client.lock()->dependencyResolver());
					_connections->newConnection(c);
					sendSessionIdtoPeer(origin);

				}

				void processMessage(const SteamNetworkingIdentity& origin, const void* buffer, const size_t length)
				{
					auto it = _steamConnections.find(origin.GetSteamID64());
					if (it != _steamConnections.end() && it->second.connection.is_done())
					{
						auto connection = it->second.connection.get();

						
						byte* data = new byte[length];
						std::memcpy(data, buffer, length);

						Packet_ptr packet(new Packet<>(connection, data, length), [data](Packet<>* packetPtr)
							{
								delete packetPtr;
								delete[] data;
							});

						if (auto client = _client.lock())
						{
							client->dependencyResolver().resolve<IPacketDispatcher>()->dispatchPacket(packet);
						}
					}
					else
					{
						_logger->log(LogLevel::Error, "steam.networking", "Received a message for a connection not established.");
					}
				}

				void tick() override
				{
					// system messages related to the SteamNetworkingTransport are sent through the 
					SteamNetworkingMessage_t* messagePtr[8];
					auto messageReceivedCount = SteamNetworkingMessages()->ReceiveMessagesOnChannel(0, messagePtr, 16);
					while (messageReceivedCount > 0)
					{
						for (int i = 0; i < messageReceivedCount; i++)
						{
							auto msg = messagePtr[i];
							const void* buffer = msg->GetData();
							const int length = msg->GetSize();
							processSystemMessage(msg->m_identityPeer, buffer, length);
							msg->Release();
						}
						messageReceivedCount = SteamNetworkingMessages()->ReceiveMessagesOnChannel(0, messagePtr, 16);
					}

					messageReceivedCount = SteamNetworkingMessages()->ReceiveMessagesOnChannel(1, messagePtr, 16);
					while (messageReceivedCount > 0)
					{
						for (int i = 0; i < messageReceivedCount; i++)
						{
							auto msg = messagePtr[i];
							const void* buffer = msg->GetData();
							const int length = msg->GetSize();
							processMessage(msg->m_identityPeer,buffer, length);
							msg->Release();
						}
						messageReceivedCount = SteamNetworkingMessages()->ReceiveMessagesOnChannel(1, messagePtr, 16);
					}

					for (auto& kvp : _steamConnections)
					{
						if (kvp.second.connection.is_done())
						{
							try
							{
								auto c = kvp.second.connection.get();

								if (c->getConnectionState() == Stormancer::ConnectionState::Disconnected)
								{
									_steamConnections.erase(kvp.first);
									_connections->closeConnection(c, "disconnected");
								}
								
							}
							catch(std::exception&)
							{ }
						}
						else
						{
							using namespace std::chrono_literals;
							if (kvp.second.createdOn < std::chrono::system_clock::now() - 30s)
							{
								kvp.second.tce.set_exception(std::runtime_error("connectionTimeout"));
								_steamConnections.erase(kvp.first);
							}
						}
					}
					
				}
				void sendSessionIdtoPeer(const SteamNetworkingIdentity& target)
				{
					obytestream stream;


					Serializer serializer;
					serializer.serialize(stream, this->_connections->currentSessionId);
					size_t length = stream.tellp();
					stream.seekp(0);

					SteamNetworkingMessages()->SendMessageToUser(target, stream.currentPtr(), length, k_nSteamNetworkingSend_Reliable, 0);
				}
				void onSteamNetworkingMessagesSessionRequestCallback(uint64 steamID) override
				{
					SteamNetworkingIdentity identity;
					CSteamID steamId;
					steamId.SetFromUint64(steamID);
					identity.SetSteamID(steamId);
					SteamNetworkingMessages()->AcceptSessionWithUser(identity);
				}

				pplx::task<std::shared_ptr<IConnection>> openP2PConnectionAsync(const SceneAddress& sceneAddress,std::unordered_map<std::string, std::string>& metadata, pplx::cancellation_token ct) override
				{
					auto it = metadata.find("steam");
					if (it != metadata.end())
					{
					
						SteamNetworkingIdentity identity;
						CSteamID steamId;
						steamId.SetFromString(it->second.c_str(), EUniverse::k_EUniversePublic);
						identity.SetSteamID(steamId);

						
						auto id = steamId.ConvertToUint64();
						auto it = _steamConnections.find(id);
						if (it != _steamConnections.end())
						{
							return it->second.connection.then([](std::shared_ptr<SteamNetworkingConnection> c) {return std::static_pointer_cast<IConnection>(c); });
						}
						
						SteamNetworkingConnectionContainer container(id);
						_steamConnections.emplace(id, container);
						sendSessionIdtoPeer(identity);

						return container.connection.then([](std::shared_ptr<SteamNetworkingConnection> c) {return std::static_pointer_cast<IConnection>(c); });
					}
				}


			private:
				struct SteamNetworkingConnectionContainer
				{
					SteamNetworkingConnectionContainer(uint64 steamId)
						:steamId(steamId)
					{
						createdOn = std::chrono::system_clock::now();
						connection = pplx::create_task(tce);
					}
					uint64 steamId;
					std::chrono::time_point<std::chrono::system_clock> createdOn;
					pplx::task_completion_event< std::shared_ptr<SteamNetworkingConnection>> tce;
					pplx::task<std::shared_ptr<SteamNetworkingConnection>> connection;
				};
				std::shared_ptr<IConnectionManager> _connections;
				std::weak_ptr< IClient> _client;
				std::shared_ptr<ILogger> _logger;
				std::unordered_map<uint64,SteamNetworkingConnectionContainer> _steamConnections;
			};
			class SteamP2PConnectivityEventHandler : public IP2PConnectivityEventHandler
			{
			public:
				SteamP2PConnectivityEventHandler(std::weak_ptr< IP2PConnectivityProvider> provider)
					:_provider(provider)
				{

				}

				virtual ~SteamP2PConnectivityEventHandler(){}

				void onConnecting(P2POnConnectingContext& ctx) override
				{
					auto it = ctx.metadata.find("steam");
					if (it != ctx.metadata.end())
					{
						ctx.candidates[10000] = _provider;
					}
				}

			private:
				std::weak_ptr< IP2PConnectivityProvider> _provider;
				
			};

		

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
				{
				}

				const SteamID _steamID;
			};
			class SteamImpl;
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

				bool isInitialized = false;

				std::weak_ptr<SteamImpl> steamImpl;
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

			using GetLobbyOwnerArgs = JoinLobbyDto;

			struct InviteUserToLobbyArgs
			{
				SteamID steamId;
				SteamIDLobby steamLobbyId;

				MSGPACK_DEFINE(steamId, steamLobbyId)
			};


			class SteamService : public std::enable_shared_from_this<SteamService>
			{
			public:

				SteamService(std::shared_ptr<Scene> scene)
					: _rpcService(scene->dependencyResolver().resolve<RpcService>())
				{
				}

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
				{
				}

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
				{
				}

				pplx::task<Party::PartyId> accept(std::shared_ptr<Party::PartyApi> partyApi) override
				{
					return pplx::task_from_result(_partyId);
				}

				pplx::task<void> decline(std::shared_ptr<Party::PartyApi>) override
				{
					return pplx::task_from_result();
				}

				Users::UserId getSenderId() override
				{
					return Users::UserId(platformName, _senderSteamID);
				}



				Party::PartyId getPartyId()
				{
					return _partyId;
				}

			private:

				Party::PartyId _partyId;
				std::string _senderSteamID;
			};
			class SteamImpl;
			class SteamApiCallbacks
			{

			public:
				SteamApiCallbacks(SteamImpl* impl)
					:_impl(impl)
				{
				}

			private:
				STEAM_CALLBACK(SteamApiCallbacks, onLobbyDataUpdateCallback, LobbyDataUpdate_t);

				STEAM_CALLBACK(SteamApiCallbacks, onLobbyInviteCallback, LobbyInvite_t);

				STEAM_CALLBACK(SteamApiCallbacks, onGameLobbyJoinRequestedCallback, GameLobbyJoinRequested_t);


				STEAM_CALLBACK(SteamApiCallbacks, onLobbyEnterCallback, LobbyEnter_t);

				STEAM_CALLBACK(SteamApiCallbacks, onLobbyChatUpdateCallback, LobbyChatUpdate_t);
				STEAM_CALLBACK(SteamApiCallbacks, onPersonaStateChangeCallback, PersonaStateChange_t);

				STEAM_CALLBACK(SteamApiCallbacks, onSteamNetworkingMessagesSessionRequestCallback, SteamNetworkingMessagesSessionRequest_t);
				SteamImpl* _impl;

			};

			class SteamPartyProvider;

			class SteamImpl : public ClientAPI<SteamImpl, SteamService>, public SteamApi, public Friends::IFriendsEventHandler
			{
				friend class SteamPartyProvider;
				friend class SteamPlugin;
				friend SteamApiCallbacks;

			public:

#pragma region public_methods

				SteamImpl(std::shared_ptr<Users::UsersApi> usersApi, std::shared_ptr<SteamState> steamConfig, std::shared_ptr<Configuration> config, std::shared_ptr<IScheduler> scheduler, std::shared_ptr<ILogger> logger, std::shared_ptr<Party::PartyApi> partyApi, std::shared_ptr<Party::Platform::InvitationMessenger> invitationMessenger, std::vector<std::shared_ptr<ISteamTickEventHandler>> tickers)
					: ClientAPI(usersApi, "stormancer.steam")
					, _logger(logger)
					, _wSteamConfig(steamConfig)
					, _wScheduler(scheduler)
					, _wActionDispatcher(config->actionDispatcher)

					, _wUsersApi(usersApi)
					, _wPartyApi(partyApi)
					, _wInvitationMessenger(invitationMessenger)
					, _tickers(tickers)
				{
				}

				~SteamImpl()
				{
					_cts.cancel();
				}

				Subscription subscribeFriendsChanged(std::function<void(std::vector<Friends::FriendListUpdateDto>)> callback) override
				{
					getFriends().then([callback](std::vector<SteamFriend> friends)
						{
							std::vector<Friends::FriendListUpdateDto> results;
							for (auto& f : friends)
							{
								if (f.relationship == EFriendRelationship::k_EFriendRelationshipFriend || f.relationship == EFriendRelationship::k_EFriendRelationshipBlocked)
								{
									Friends::FriendListUpdateDto dto;
									Friends::Friend fr;

									fr.status["steam"] = f.online ? Friends::FriendStatus::Connected : Friends::FriendStatus::Disconnected;

									Users::UserId userId;
									userId.platform = "steam";
									userId.userId = f.steamId;

									fr.userIds.push_back(userId);

									fr.customData = "{ \"steam\":{ \"personaName\":\"" + f.personaName + "\"},\"pseudo\":\"" + f.personaName + "\"}";
									fr.tags.push_back("steam");
									if (f.relationship == EFriendRelationship::k_EFriendRelationshipBlocked)
									{
										fr.tags.push_back("friends.blocked");
									}
									dto.operation = Friends::FriendListUpdateOperationInternal::AddOrUpdate;
									dto.data = fr;
									results.push_back(dto);
								}
							}
							callback(results);
						});

					return this->friendListUpdateEvent.subscribe([callback](Friends::FriendListUpdateDto dto)
						{
							std::vector<Friends::FriendListUpdateDto> list;
							list.push_back(dto);
							callback(list);
						});
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

							VoidSteamOperationResult result;
							result.success = steamApi->inviteToLobby(args.steamId, args.steamLobbyId);

							ctx->sendValueTemplated(result);

							return pplx::task_from_result();
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

				bool isInitialized()
				{
					return _wSteamConfig.lock()->isInitialized;
				}
				std::shared_ptr<SteamApiCallbacks> _callbackRegistrations;
				void initialize() override
				{


					if (auto steamConfig = _wSteamConfig.lock())
					{
						if (isInitialized())
						{
							return;
						}

						if (steamConfig->getSteamApiInitialize())
						{
							SteamErrMsg error;
							if (SteamAPI_InitEx(&error) != ESteamAPIInitResult::k_ESteamAPIInitResult_OK)
							{
								_logger->log(LogLevel::Error, "Steam", std::string("SteamAPI_Init failed : ") + error);

								throw std::runtime_error(error);
							}
							else
							{
								_logger->log(LogLevel::Info, "Steam", "SteamAPI_Init success");
							}

						}
						_wSteamConfig.lock()->isInitialized = true;

						_callbackRegistrations = std::make_shared<SteamApiCallbacks>(this);


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
				}


				void scheduleRunSteamAPiCallbacks()
				{
					if (!_cts.get_token().is_canceled())
					{
						SteamAPI_RunCallbacks();
						for (auto& ticker : _tickers)
						{
							ticker->tick();
						}
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

				bool inviteToLobby(SteamID steamId, SteamIDLobby steamLobbyId)
				{
					auto steamMatchmaking = SteamMatchmaking();
					if (!steamMatchmaking)
					{
						throw std::runtime_error("SteamMatchmaking() returned null");
					}

					return steamMatchmaking->InviteUserToLobby(steamLobbyId, steamId);

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

				int getAppBuildId() override
				{
					auto steamApps = SteamApps();

					if (!steamApps)
					{
						return -1;
					}

					return steamApps->GetAppBuildId();
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
								steamFriend.personaName = steamFriends->GetFriendPersonaName(steamIDFriend);
								steamFriend.relationship = steamFriends->GetFriendRelationship(steamIDFriend);
								steamFriend.online = steamFriends->GetFriendPersonaState(steamIDFriend) != EPersonaState::k_EPersonaStateOffline;
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
				void onLobbyEnterCallResult(LobbyEnter_t* callback, bool failure)
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

				void onLobbyCreatedCallResult(LobbyCreated_t* callback, bool failure)
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

				void onLobbyDataUpdateCallback(LobbyDataUpdate_t* callback)
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

				void onGameLobbyJoinRequestedCallback(GameLobbyJoinRequested_t* callback)
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



				void onLobbyEnterCallback(LobbyEnter_t* callback)
				{
					onLobbyEnterCallResult(callback, false);
				}



				void onLobbyChatUpdateCallback(LobbyChatUpdate_t* /*callback*/)
				{
				}

				void onSteamNetworkingMessagesSessionRequestCallback(SteamNetworkingMessagesSessionRequest_t* ctx)
				{
					for (auto& ticker : _tickers)
					{
						auto steamId= ctx->m_identityRemote.GetSteamID().ConvertToUint64();
						ticker->onSteamNetworkingMessagesSessionRequestCallback(steamId);
					}
				}

				void onPersonaStateChangeCallback(PersonaStateChange_t* callback)
				{
					if (callback->m_nChangeFlags & k_EPersonaChangeStatus)
					{
						Friends::FriendListUpdateDto dto;
						dto.operation = Friends::FriendListUpdateOperationInternal::UpdateStatus;
						dto.data.status["steam"] = getFriendStatusFromSteam(SteamFriends()->GetFriendPersonaState(callback->m_ulSteamID));
						this->friendListUpdateEvent(dto);
					}
					else if (callback->m_nChangeFlags & k_EPersonaChangeRelationshipChanged)
					{
						Friends::FriendListUpdateDto dto;
						std::string personaName = std::string(SteamFriends()->GetFriendPersonaName(callback->m_ulSteamID));
						switch (SteamFriends()->GetFriendRelationship(callback->m_ulSteamID))
						{

						case EFriendRelationship::k_EFriendRelationshipFriend:
							dto.operation = Friends::FriendListUpdateOperationInternal::AddOrUpdate;
							dto.data.status["steam"] = getFriendStatusFromSteam(SteamFriends()->GetFriendPersonaState(callback->m_ulSteamID));
							dto.data.userIds.push_back(Users::UserId("steam", std::to_string(callback->m_ulSteamID)));
							dto.data.tags.push_back("steam");
							dto.data.customData = "{ \"steam\":{ \"personaName\":\"" + personaName + "\"},\"pseudo\":\"" + personaName + "\"}";
							this->friendListUpdateEvent(dto);
							break;
						case EFriendRelationship::k_EFriendRelationshipBlocked:
							dto.operation = Friends::FriendListUpdateOperationInternal::AddOrUpdate;
							dto.data.userIds.push_back(Users::UserId("steam", std::to_string(callback->m_ulSteamID)));
							dto.data.tags.push_back("friends.blocked");
							dto.data.tags.push_back("steam");
							dto.data.customData = "{ \"steam\":{ \"personaName\":\"" + personaName + "\"},\"pseudo\":\"" + personaName + "\"}";
							this->friendListUpdateEvent(dto);
							break;
						default:
							break;
						}
					}
				}


				void onLobbyInviteCallback(LobbyInvite_t* /*callback*/)
				{
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



				Friends::FriendStatus getFriendStatusFromSteam(EPersonaState state)
				{
					switch (state)
					{
					case EPersonaState::k_EPersonaStateAway:
					case EPersonaState::k_EPersonaStateBusy:
					case EPersonaState::k_EPersonaStateSnooze:
						return Friends::FriendStatus::Away;
					case EPersonaState::k_EPersonaStateOnline:
					case EPersonaState::k_EPersonaStateLookingToTrade:
					case EPersonaState::k_EPersonaStateLookingToPlay:
						return Friends::FriendStatus::Connected;
					default:
						return Friends::FriendStatus::Disconnected;
					}
				}

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
				std::vector<std::shared_ptr<ISteamTickEventHandler>> _tickers;


#pragma endregion

				Event<Friends::FriendListUpdateDto> friendListUpdateEvent;
			};

			void SteamApiCallbacks::onPersonaStateChangeCallback(PersonaStateChange_t* ctx)
			{
				this->_impl->onPersonaStateChangeCallback(ctx);
			}

			void SteamApiCallbacks::onSteamNetworkingMessagesSessionRequestCallback(SteamNetworkingMessagesSessionRequest_t* ctx)
			{
				this->_impl->onSteamNetworkingMessagesSessionRequestCallback(ctx);
			}

			void SteamApiCallbacks::onLobbyDataUpdateCallback(LobbyDataUpdate_t* ctx)
			{
				this->_impl->onLobbyDataUpdateCallback(ctx);
			}

			void SteamApiCallbacks::onLobbyInviteCallback(LobbyInvite_t* ctx)
			{
				this->_impl->onLobbyInviteCallback(ctx);
			}

			void SteamApiCallbacks::onLobbyEnterCallback(LobbyEnter_t* ctx)
			{
				this->_impl->onLobbyEnterCallback(ctx);
			}
			void SteamApiCallbacks::onLobbyChatUpdateCallback(LobbyChatUpdate_t* ctx)
			{
				this->_impl->onLobbyChatUpdateCallback(ctx);
			}

			void SteamApiCallbacks::onGameLobbyJoinRequestedCallback(GameLobbyJoinRequested_t* ctx)
			{
				this->_impl->onGameLobbyJoinRequestedCallback(ctx);
			}

			class SteamProjectEnvironmentEventHandler : public Stormancer::IProjectEnvironmentEventsHandler
			{
			public:
				SteamProjectEnvironmentEventHandler(std::shared_ptr<SteamApi> steamApi)
					:_steamApi(steamApi)
				{

				}
				void onGetMetadata(std::unordered_map<std::string, std::string>& metadata) override
				{
					metadata["steam.appBuildId"] = std::to_string(_steamApi->getAppBuildId());

				}

				virtual ~SteamProjectEnvironmentEventHandler() = default;
			private:
				std::shared_ptr<SteamApi> _steamApi;


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
					, _logger(logger)
					, _wUsersApi(usersApi)
					, _wSteamApi(steamApi)

					, _wPartyApi(partyApi)
					, _wActionDispatcher(actionDispatcher)
				{
				}

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




			// https://partner.steamgames.com/doc/features/auth#client_to_backend_webapi
			// https://partner.steamgames.com/doc/api/ISteamUser#GetAuthSessionTicket

			class SteamAuthenticationEventHandler : public std::enable_shared_from_this<SteamAuthenticationEventHandler>, public Users::IAuthenticationProvider
			{
			public:

#pragma region public_methods

				SteamAuthenticationEventHandler(std::shared_ptr<details::SteamState> steamConfig)
					: _steamState(steamConfig)
				{
				}

				virtual ~SteamAuthenticationEventHandler() {}

				std::string getProviderName() const override
				{
					return platformName;
				}

				pplx::task<void> retrieveCredentials(const Users::CredentialsContext& context) override
				{
					if (context.tryUseProvider(platformName))
					{
						return getSteamCredentials([context](const std::string& type, const std::string& provider, const std::string& steamTicketHex)
							{

								context.authParameters->parameters["provider"] = provider;
								context.authParameters->parameters["ticket"] = steamTicketHex;
								context.authParameters->parameters["version"] = "v1";
								context.authParameters->parameters["appId"] = std::to_string(SteamUtils()->GetAppID());
							});
					}
					else
					{
						return pplx::task_from_result();
					}
				}

				virtual pplx::task<void> renewCredentials(const Users::CredentialsRenewalContext& context) override
				{
					if (context.authProviderType == platformName)
					{
						return getSteamCredentials([context](const std::string& /*type*/, const std::string& provider, const std::string& steamTicketHex)
							{
								context.response->parameters["provider"] = provider;
								context.response->parameters["ticket"] = steamTicketHex;
								context.response->parameters["version"] = "v1";
								context.response->parameters["appId"] = std::to_string(SteamUtils()->GetAppID());

							});
					}
					else
					{
						return pplx::task_from_result();
					}
				}


				pplx::task<void> getSteamCredentials(std::function<void(const std::string& type, const std::string& provider, const std::string& steamTicketHex)> fulfillCredentialsCallback)
				{
					this->_steamState->steamImpl.lock()->initialize();
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

#pragma region private_members

				std::recursive_mutex _mutex;
				std::shared_ptr<details::SteamState> _steamState;


#pragma endregion
			};
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
				builder.registerDependency<details::SteamImpl, Users::UsersApi, details::SteamState, Configuration, IScheduler, ILogger, Party::PartyApi, Party::Platform::InvitationMessenger, ContainerBuilder::All<ISteamTickEventHandler>>().asSelf().as<SteamApi>().as<Friends::IFriendsEventHandler>().singleInstance();
				builder.registerDependency<details::SteamPartyProvider, Party::Platform::InvitationMessenger, Users::UsersApi, details::SteamImpl, ILogger, Party::PartyApi, IActionDispatcher>().as<Party::Platform::IPlatformSupportProvider>();
				builder.registerDependency<details::SteamAuthenticationEventHandler, details::SteamState >().as<Users::IAuthenticationProvider>();
				builder.registerDependency<details::SteamProjectEnvironmentEventHandler, SteamApi>().as<IProjectEnvironmentEventsHandler>();
				builder.registerDependency<details::SteamP2PConnectivityEventHandler, details::SteamP2PConnectivityProvider>().as<IP2PConnectivityEventHandler>();
				builder.registerDependency<details::SteamP2PConnectivityProvider, IConnectionManager,IClient,ILogger>().as<IP2PConnectivityProvider>().as<ISteamTickEventHandler>().singleInstance();
			}

			void clientCreated(std::shared_ptr<IClient> client)
			{
				auto steamApi = std::static_pointer_cast<details::SteamImpl>(client->dependencyResolver().resolve<SteamApi>());
				auto steamState = client->dependencyResolver().resolve<details::SteamState>();

				steamState->steamImpl = steamApi;
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
