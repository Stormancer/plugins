// MIT License
//
// Copyright (c) 2019 Stormancer
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

#pragma once

#include "gamefinder/GameFinder.hpp"
#include "users/ClientAPI.hpp"
#include "users/Users.hpp"
#include "users/desiredState.hpp"

#include "stormancer/IClient.h"
#include "stormancer/Event.h"
#include "stormancer/msgpack_define.h"
#include "stormancer/Scene.h"
#include "stormancer/StormancerTypes.h"
#include "stormancer/Tasks.h"
#include "stormancer/Utilities/Macros.h"
#include "stormancer/Utilities/PointerUtilities.h"
#include "stormancer/Utilities/StringUtilities.h"
#include "stormancer/Utilities/TaskUtilities.h"
#include "stormancer/cpprestsdk/cpprest/json.h"

#include <bitset>
#include <stdio.h>
#include <string>
#include <unordered_map>

#if !defined(STORM_PLUGIN_IMPL)
#define STORM_PLUGIN_IMPL 0
#endif


namespace Stormancer
{
	namespace Party
	{
		namespace PartyConstants
		{
			static constexpr const char* METADATA_KEY = "stormancer.party";
		}

		struct PartyUserDto;
		struct PartySettings;
		struct PartyInvitation;
		struct PartyGameFinderFailure;
		struct MembersUpdate;

		class States
		{
		public:
			STORMANCER_API static const StateId& InParty();
		};

		enum class PartyUserStatus
		{
			NotReady = 0,
			Ready = 1
		};
		enum class PartyUserConnectionStatus
		{
			Disconnected = 0,
			Reservation = 1,
			Connected = 2
		};

		enum class PartyGameFinderStatus
		{
			SearchStopped = 0,
			SearchInProgress = 1
		};

		enum class MemberDisconnectionReason
		{
			Left = 0,
			Kicked = 1,
			Joining = 2,
			NotApplicable = 3,
			Unknown = 4
		};
		enum class PartyConnectionStateId
		{
			Disconnected,
			Disconnecting,
			ConnectingToScene,
			Synchronizing,
			Connected
		};

		/// <summary>
		/// Event data provided when the party connection state changes.
		/// </summary>
		struct PartyConnectionStateChangedArgs
		{
			PartyConnectionStateId previousState;
			PartyConnectionStateId newState;
			MemberDisconnectionReason disconnectionReason;
		};

		/// <summary>
		/// Errors of the party system.
		/// </summary>
		/// <remarks>
		/// An instance of this class represents a specific error.
		/// This class also contains static helpers to parse error strings.
		/// </remarks>
		struct PartyError
		{
			/// <summary>
			/// Represents well-known causes of error.
			/// </summary>
			enum Value
			{
				UnspecifiedError,
				InvalidInvitation, /* You tried to perform an operation on an invitation that is no longer valid. */
				AlreadyInParty, /* You tried to join a party while already being in a party. Call leaveParty() before joining the other party. */
				NotInParty, /* An operation that requires you to be in a party could not be performed because you are not in a party. */
				PartyNotReady, /* The party cannot enter the GameFinder yet because no GameFinder has been set in the party settings. */
				Unauthorized, /* A party operation failed because you do not have the required privileges. */
				StormancerClientDestroyed, /* An operation could not complete because the Stormancer client has been destroyed. */
				UnsupportedPlatform /* An operation could not be performed because of missing platform-specific support. */
			};

			/// <summary>
			/// Represents the different methods of PartyApi that can emit a PartyError object.
			/// </summary>
			enum class Api
			{
				JoinParty
			};

			struct Str
			{
				static constexpr const char* InvalidInvitation = "party.invalidInvitation";
				static constexpr const char* AlreadyInParty = "party.alreadyInParty";
				static constexpr const char* AlreadyInSameParty = "party.alreadyInSameParty";
				static constexpr const char* NotInParty = "party.notInParty";
				static constexpr const char* PartyNotReady = "party.partyNotReady";
				static constexpr const char* Unauthorized = "unauthorized";
				static constexpr const char* StormancerClientDestroyed = "party.clientDestroyed";
				static constexpr const char* UnsupportedPlatform = "party.unsupportedPlatform";

				Str() = delete;
			};

			static Value fromString(const char* error)
			{
				if (std::strcmp(error, Str::AlreadyInParty) == 0) { return AlreadyInParty; }

				if (std::strcmp(error, Str::InvalidInvitation) == 0) { return InvalidInvitation; }

				if (std::strcmp(error, Str::NotInParty) == 0) { return NotInParty; }

				if (std::strcmp(error, Str::PartyNotReady) == 0) { return PartyNotReady; }

				if (std::strcmp(error, Str::Unauthorized) == 0) { return Unauthorized; }

				if (std::strcmp(error, Str::StormancerClientDestroyed) == 0) { return StormancerClientDestroyed; }

				if (std::strcmp(error, Str::UnsupportedPlatform) == 0) { return UnsupportedPlatform; }

				return UnspecifiedError;
			}

			/// <summary>
			/// The API call that failed
			/// </summary>
			Api apiCalled;

			/// <summary>
			/// The reason for the failure
			/// </summary>
			std::string error;

			/// <summary>
			/// Get the error code for this particular <c>error</c>.
			/// </summary>
			/// <remarks>If the error has no particular code associated to it, this method will return <c>UnspecifiedError</c>.
			/// <returns>The error code (member of the <c>PartyError::Value</c> enum) corresponding to the <c>error</c> member.</returns>
			Value getErrorCode() const
			{
				return fromString(error.c_str());
			}

			/// <summary>
			/// Construct a PartyError, specifying the API (PartyApi method) that failed, and the error string.
			/// </summary>
			/// <param name="api">The PartyApi method that failed</param>
			/// <param name="error">The error string. This is a const char* because this value often comes from <c>exception.what()</c>. We avoid creating a temporary string.</param>
			PartyError(Api api, const char* error)
				: apiCalled(api)
				, error(error)
			{}
		};

		struct LocalPlayerInfos
		{
			std::string stormancerUserId;
			std::string platform;
			std::string pseudo;
			std::string platformId;

			std::string customData;

			int localPlayerIndex;

			STRM_MSGPACK_DEFINE(platform, stormancerUserId, pseudo, platformId, customData, localPlayerIndex)
		};

		struct PartyUserDto
		{
			std::string userId;
			PartyUserStatus partyUserStatus;
			std::vector<byte> userData;
			Stormancer::SessionId sessionId;

			std::vector<LocalPlayerInfos> localPlayers;

			PartyUserConnectionStatus connectionStatus;

			bool isLeader = false; // Computed locally

			PartyUserDto(std::string userId) : userId(userId) {}
			PartyUserDto() = default;

			STRM_MSGPACK_DEFINE(userId, partyUserStatus, userData, sessionId, localPlayers, connectionStatus);
		};


		/// <summary>
		/// Abstraction for a party identifier.
		/// </summary>
		/// <remarks>
		/// Could be a stormancer scene Id, a platform-specific session Id, and more.
		/// </remarks>
		struct PartyId
		{
			PartyId() {}
			PartyId(const char* type, const std::string& id)
				: type(type)
				, id(id)
			{

			}
			/// <summary>
			/// Platform-specific type of the PartyId.
			/// </summary>
			std::string type;

			/// <summary>
			/// Identifier for a party.
			/// </summary>
			std::string id;

			/// <summary>
			/// Platform of this PartyId. Can be empty if type is scene Id or connection token.
			/// </summary>
			std::string platform;

			bool isValid() const
			{
				return !type.empty();
			}

			STRM_MSGPACK_DEFINE(type, id, platform);

			static constexpr const char* TYPE_PARTY_ID = "stormancer.partyId";
			static constexpr const char* TYPE_SCENE_ID = "stormancer.sceneId";
			static constexpr const char* TYPE_OFFLINE = "stormancer.offline";
			static constexpr const char* TYPE_INVITATIONCODE = "stormancer.invitationCode";
			static constexpr const char* TYPE_CONNECTION_TOKEN = "stormancer.connectionToken";

			static constexpr const char* STRING_PLATFORM_FIELD = "platform";
			static constexpr const char* STRING_TYPE_FIELD = "type";
			static constexpr const char* STRING_ID_FIELD = "id";
			static constexpr const char* STRING_SEP_1 = ", ";
			static constexpr const char* STRING_SEP_2 = ": ";

			std::string toJson() const
			{
				auto jsonObject = web::json::value::object();
				jsonObject[utility::conversions::to_string_t(STRING_ID_FIELD)] = web::json::value(utility::conversions::to_string_t(id));
				jsonObject[utility::conversions::to_string_t(STRING_TYPE_FIELD)] = web::json::value(utility::conversions::to_string_t(type));
				jsonObject[utility::conversions::to_string_t(STRING_PLATFORM_FIELD)] = web::json::value(utility::conversions::to_string_t(platform));
				return utility::conversions::to_utf8string(jsonObject.serialize());
			}

			static PartyId fromJson(const std::string& jsonString)
			{
				PartyId partyId;
				auto jsonValue = web::json::value::parse(utility::conversions::to_string_t(jsonString));
				if (jsonValue.is_object())
				{
					auto jsonObject = jsonValue.as_object();
					auto idIt = jsonObject.find(utility::conversions::to_string_t(STRING_ID_FIELD));
					if (idIt != jsonObject.end() && idIt->second.is_string())
					{
						partyId.id = utility::conversions::to_utf8string(idIt->second.as_string());
					}
					auto typeIt = jsonObject.find(utility::conversions::to_string_t(STRING_TYPE_FIELD));
					if (typeIt != jsonObject.end() && typeIt->second.is_string())
					{
						partyId.type = utility::conversions::to_utf8string(typeIt->second.as_string());
					}
					auto platformIt = jsonObject.find(utility::conversions::to_string_t(STRING_PLATFORM_FIELD));
					if (platformIt != jsonObject.end() && platformIt->second.is_string())
					{
						partyId.type = utility::conversions::to_utf8string(platformIt->second.as_string());
					}
				}
				return partyId;
			}

			std::string toString() const
			{
				std::stringstream ss;
				ss << STRING_PLATFORM_FIELD << STRING_SEP_2 << platform
					<< STRING_SEP_1
					<< STRING_TYPE_FIELD << STRING_SEP_2 << type
					<< STRING_SEP_1
					<< STRING_ID_FIELD << STRING_SEP_2 << id;
				return ss.str();
			}

			static PartyId fromString(const std::string& partyIdStr)
			{
				PartyId partyId;
				auto parts = stringSplit(partyIdStr, STRING_SEP_1);
				if (parts.size() == 3)
				{
					auto platform = stringSplit(parts[0], STRING_SEP_2);
					if (platform[0] == STRING_PLATFORM_FIELD)
					{
						partyId.platform = platform[1];
					}
					auto type = stringSplit(parts[1], STRING_SEP_2);
					if (type[0] == STRING_TYPE_FIELD)
					{
						partyId.type = type[1];
					}
					auto id = stringSplit(parts[2], STRING_SEP_2);
					if (id[0] == STRING_ID_FIELD)
					{
						partyId.id = id[1];
					}
				}
				return partyId;
			}

			bool operator==(const PartyId& right)
			{
				return !((*this) != right);
			}

			bool operator!=(const PartyId& right)
			{
				return (id != right.id || type != right.type || (!platform.empty() && !right.platform.empty() && platform != right.platform));
			}
		};

		/// <summary>
		/// Contains information about a party that the current user can join.
		/// </summary>
		struct AdvertisedParty
		{
			/// <summary>
			/// A friend of the current user.
			/// </summary>
			struct Friend
			{
				/// <summary>
				/// Stormancer user Id of the friend. May be empty.
				/// </summary>
				std::string stormancerId;

				/// <summary>
				/// Platform-specific user Id of the friend. May be empty.
				/// </summary>
				std::string platformId;

				/// <summary>
				/// Username of the friend. May be empty.
				/// </summary>
				std::string username;

				/// <summary>
				/// Additional data for this friend.
				/// </summary>
				std::unordered_map<std::string, std::string> data;

				STRM_MSGPACK_DEFINE(stormancerId, platformId, username, data);
			};

			/// <summary>
			/// Abstract party Id, possibly platform-specific.
			/// </summary>
			PartyId partyId;

			/// <summary>
			/// Stormancer user Id of the party leader. May be empty.
			/// </summary>
			std::string leaderUserId;

			/// <summary>
			/// List of friends who are in the party.
			/// </summary>
			std::vector<Friend> friends;

			/// <summary>
			/// Additional metadata for the party.
			/// </summary>
			std::unordered_map<std::string, std::string> metadata;

			STRM_MSGPACK_DEFINE(partyId, leaderUserId, friends, metadata);
		};

		/// <summary>
		/// Party document for search purpose
		/// </summary>
		struct PartyDocument
		{
			std::string id;
			std::string content;

			STRM_MSGPACK_DEFINE(id, content)
		};

		struct SearchResult
		{
			Stormancer::uint32 total;

			std::vector<PartyDocument> hits;
			STRM_MSGPACK_DEFINE(total, hits)
		};



		class PartyApi
		{
		public:

			virtual ~PartyApi() = default;

			/// <summary>
			/// Create and join a new party.
			/// </summary>
			/// <remarks>
			/// If the local player is currently in a party, the operation fails.
			/// The local player will be the leader of the newly created party.
			/// </remarks>
			/// <param name="partyRequest">Party creation parameters</param>
			/// <returns>A task that completes when the party has been created and joined.</returns>
			virtual pplx::task<void> createParty(pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			/// <summary>
			/// Join a party using an abstract PartyId.
			/// </summary>
			/// <param name="partyId">Abstract PartyId.</param>
			/// <returns>A task that completes once the party has been joined.</returns>
			virtual pplx::task<void> joinParty(const PartyId& partyId, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			/// <summary>
			/// Gets the current party connection state.
			/// </summary>
			/// <returns></returns>
			virtual PartyConnectionStateId connectionState() const = 0;

			/// <summary>
			/// Gets a boolean indicating if the party is currently in a game session.
			/// </summary>
			/// <returns></returns>
			virtual bool isInGameSession() const = 0;

			/// <summary>
			/// Returns a value indicating whether 
			/// </summary>
			/// <returns></returns>
			virtual bool hasPendingPlatformInvitation() const = 0;

			/// <summary>
			/// If the party is in a game session, gets a token to connect to it.
			/// </summary>
			/// <param name="ct"></param>
			/// <returns></returns>
			virtual pplx::task<std::string> getCurrentGameSessionConnectionToken(pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			/// <summary>
			/// Leave the party
			/// </summary>
			/// <returns>A task that completes with the operation.</returns>
			virtual pplx::task<void> leaveParty(pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			/// <summary>
			/// Check if you are currently in a party.
			/// </summary>
			/// <returns>
			/// <c>true</c> if you are in a party, <c>false</c> otherwise.
			/// Note that if you are in the process of joining or creating a party, but are not finished yet, this method will also return <c>false</c>.
			/// </returns>
			virtual bool isInParty() const noexcept = 0;

			virtual bool isOffline() const = 0;

			bool isOnline() const
			{
				return !isOffline();
			}

			/// <summary>
			/// Get the party scene.
			/// </summary>
			/// <returns>The party scene.</returns>
			virtual bool tryGetPartyScene(std::shared_ptr<Scene>& scene) const = 0;

			/// <summary>
			/// Get the member list of the currently joined party.
			/// </summary>
			/// <remarks>
			/// It is invalid to call this method while not in a party.
			/// Call <c>isInParty()</c> to check.
			/// </remarks>
			/// <returns>A vector of structs that describe every user who is currently in the party.</returns>
			/// <exception cref="std::exception">If you are not in a party.</exception>
			virtual const std::vector<PartyUserDto> getPartyMembers() const = 0;

			/// <summary>
			/// Get the local member's party data.
			/// </summary>
			/// <remarks>
			/// This method is a shortcut for calling <c>getPartyMembers()</c> and iterating over the list to find the local member.
			/// </remarks>
			/// <remarks>
			/// It is invalid to call this method while not in a party.
			/// Call <c>isInParty()</c> to check.
			/// </remarks>
			/// <returns>The struct containing the local player's party data.</returns>
			/// <exception cref="std::exception">If you are not in a party.</exception>
			virtual const PartyUserDto getLocalMember() const = 0;

			virtual bool tryGetLocalMember(PartyUserDto& localMember) const = 0;

			/// <summary>
			/// Set the local player's status (ready/not ready).
			/// </summary>
			/// <remarks>
			/// By default, a GameFinder request (matchmaking group queuing) is automatically started when all players in the party are ready.
			/// This behavior can be controlled server-side. See the Party documentation for details.
			/// </remarks>
			/// <param name="playerStatus">Ready or not ready</param>
			/// <returns>A task that completes when the update has been sent.</returns>
			/// <exception cref="std::exception">If you are not in a party.</exception>
			virtual pplx::task<void> updatePlayerStatus(PartyUserStatus playerStatus, pplx::cancellation_token cancellationToken = pplx::cancellation_token::none()) = 0;

			/// <summary>
			/// Get the settings of the current party.
			/// </summary>
			/// <returns>The settings of the current party, if the current user is currently in a party.</returns>
			/// <exception cref="std::exception">If you are not in a party.</exception>
			virtual PartySettings getPartySettings() const = 0;

			/// <summary>
			/// Get the partyId of the current party.
			/// </summary>
			/// <returns>The partyId of the current party, if the current user is currently in a party.</returns>
			/// <exception cref="std::exception">If you are not in a party.</exception>
			virtual const PartyId getPartyId() const = 0;

			/// <summary>
			/// Get the User Id of the party leader.
			/// </summary>
			/// <returns>The Stormancer User Id of the party leader.</returns>
			/// <exception cref="std::exception">If you are not in a party.</exception>
			virtual const Users::StormancerUserId getPartyLeaderId() const = 0;

			/// <summary>
			/// Update the party settings
			/// </summary>
			/// <remarks>
			/// Party settings can only be set by the party leader.
			/// Party settings are automatically replicated to other players. The current value is available
			/// in the current party object. Subscribe to the onUpdatedPartySettings event to listen to update events.
			/// </remarks>
			/// <param name="partySettings">New settings</param>
			/// <returns>A task that completes when the settings have been updated and replicated to other players.</returns>
			/// <exception cref="std::exception">If you are not in a party.</exception>
			virtual pplx::task<void> updatePartySettings(const PartySettings& partySettings, pplx::cancellation_token cancellationToken = pplx::cancellation_token::none()) = 0;
			virtual pplx::task<void> updatePartySettings(std::function<void(PartySettings&)> updater, pplx::cancellation_token cancellationToken = pplx::cancellation_token::none()) = 0;
			/// <summary>
			/// Update the data associated with the local player
			/// </summary>
			/// <remarks>
			/// player data are automatically replicated to other players. The current value is available
			/// in the current party members list. Subscribe to the OnUpdatedPartyMembers event to listen to update events.
			/// </remarks>
			/// <param name="data">New player data</param>
			/// <param name="localPlayers">The local players </param>
			/// <returns>A task that completes when the data has been updated and replicated to other players.</returns>
			/// <exception cref="std::exception">If you are not in a party.</exception>
			virtual pplx::task<void> updatePlayerData(std::vector<byte> data, std::vector<LocalPlayerInfos> localPlayers, pplx::cancellation_token cancellationToken = pplx::cancellation_token::none()) = 0;

			/// <summary>
			/// Update the data associated with the local player
			/// </summary>
			/// <remarks>
			/// player data are automatically replicated to other players. The current value is available
			/// in the current party members list. Subscribe to the OnUpdatedPartyMembers event to listen to update events.
			/// </remarks>
			/// <param name="data">New player data</param>
			/// <returns>A task that completes when the data has been updated and replicated to other players.</returns>
			/// <exception cref="std::exception">If you are not in a party.</exception>
			pplx::task<void> updatePlayerData(std::vector<byte> data, pplx::cancellation_token cancellationToken = pplx::cancellation_token::none())
			{
				return updatePlayerData(data, this->getLocalMember().localPlayers, cancellationToken);
			}

			/// <summary>
			/// Check if the local user is the leader of the party.
			/// </summary>
			/// <returns><c>true</c> if the local user is the leader, <c>false</c> otherwise.</returns>
			/// <exception cref="std::exception">If you are not in a party.</exception>
			virtual bool isLeader() const = 0;

			/// <summary>
			/// Promote the specified user as leader
			/// </summary>
			/// <remarks>
			/// The caller must be the leader of the party
			/// The new leader must be in the party
			/// </remarks>
			/// <param name="userId">The id of the player to promote</param>
			/// <returns>A task that completes when the underlying RPC (remote procedure call) has returned.</returns>
			/// <exception cref="std::exception">If you are not in a party.</exception>
			virtual pplx::task<void> promoteLeader(std::string userId) = 0;

			/// <summary>
			/// Kick the specified user from the party
			/// </summary>
			/// <remarks>
			/// The caller must be the leader of the party
			/// If the user has already left the party, the operation succeeds.
			/// </remarks>
			/// <param name="userId">The id of the player to kick</param>
			/// <returns>A task that completes when the underlying RPC (remote procedure call) has returned.</returns>
			/// <exception cref="std::exception">If you are not in a party.</exception>
			virtual pplx::task<void> kickPlayer(std::string userId) = 0;

			/// <summary>
			/// Creates an invitation code that can be used by users to join the party.
			/// </summary>
			virtual pplx::task<std::string> createInvitationCode(pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			/// <summary>
			/// Cancels the existing invitation code.
			/// </summary>
			/// <param name="ct"></param>
			/// <returns></returns>
			virtual pplx::task<void> cancelInvitationCode(pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			/// <summary>
			/// Tries getting the current invitation code.
			/// </summary>
			/// <remarks>
			/// The invitation code is stored only if the party is configured with ShareInvitationCode() during creation.
			/// </remarks>
			/// <param name="code"></param>
			/// <returns></returns>
			virtual bool tryGetInvitationCode(std::string& code) = 0;

			/// <summary>
			/// Get pending party invitations for the player.
			/// </summary>
			/// <remarks>
			/// Call <c>subscribeOnInvitationReceived()</c> in order to be notified when an invitation is received.
			/// </remarks>
			/// <returns>A vector of invitations that have been received and have not yet been accepted.</returns>
			virtual std::vector<PartyInvitation> getPendingInvitations() = 0;

			/// <summary>
			/// Get the list of invitations the player has sent for the current party.
			/// </summary>
			/// <remarks>
			/// This list will only contain invitations that support cancellation.
			/// Invitations that are backed by a system which doesn't support cancellation, like most platform-specific invitation systems, will not appear in the list.
			/// If your game needs cancelable invitations as a feature, you should always set <c>forceStormancerInvite</c> to <c>true</c> when calling <c>sendInvitation()</c>.
			/// </remarks>
			/// <returns>A vector of user ids to whom invitations have been sent but not yet accepted or declined.</returns>
			virtual std::vector<std::string> getSentPendingInvitations() = 0;

			/// <summary>
			/// Check whether the local player can send invitations with <c>sendInvitation()</c>.
			/// </summary>
			/// <returns><c>true</c> if the local player is in a party, and is authorized to send invitations, <c>false</c> otherwise.</returns>
			/// <remarks>
			/// By default, invitations can only be sent by the leader of the party.
			/// This restriction can be lifted by setting <c>PartyCreationOptions::onlyLeaderCanInvite</c> to <c>false</c> when creating a party,
			/// or later on by changing the party settings with <c>updatePartySettings()</c>.
			/// </remarks>
			virtual bool canSendInvitations() const = 0;

			/// <summary>
			/// Send an invitation to another player.
			/// </summary>
			/// <param name="recipient">Stormancer Id of the player to be invited.</param>
			/// <param name="preferPlatformInvite">If true, prefer sending the invitation through the platform, if false use only the internal invitation system.</param>
			/// <remarks>
			/// The Stormancer server determines the kind of invitation that should be sent according to the sender and the recipient's platform.
			/// Unless <paramref name="forceStormancerInvite" /> is set to <c>true</c>, Stormancer will prioritize platform-specific invitation systems where possible.
			/// If your game needs cancelable invitations as a feature, you should always set <paramref name="forceStormancerInvite" /> to <c>true</c>.
			/// </remarks>
			/// <returns>A task that completes when the invitation has been sent.</returns>
			virtual pplx::task<void> sendInvitation(const ::Stormancer::Users::UserId& recipient, bool preferPlatformInvite = false) = 0;

			/// <summary>
			/// Show the system UI to send invitations to the current party, if the current platform supports it.
			/// </summary>
			/// <returns><c>true</c> if we were able to show the UI, <c>false</c> otherwise.</returns>
			virtual bool showSystemInvitationUI() = 0;

			/// <summary>
			/// Cancel an invitation that was previously sent.
			/// </summary>
			/// <param name="recipient">Stormancer Id of the player who was previously invited through <c>sendInvitation()</c>.</param>
			/// <remarks>
			/// This call will only have an effect if the invitation is backed by a system which supports canceling an invitation, such as Stormancer invitations,
			/// and the invitation has not yet been accepted or declined by the recipient.
			/// In all other circumstances, it will have no effect.
			/// </remarks>
			virtual pplx::task<void> cancelInvitation(const std::string& recipient) = 0;

			/// <summary>
			/// Get advertised parties.
			/// </summary>
			/// <returns>A list of advertised parties.</returns>
			virtual pplx::task<std::vector<AdvertisedParty>> getAdvertisedParties(pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			/// <summary>
			/// Get the PartyApi's DependencyScope.
			/// </summary>
			/// <returns>The DependencyScope of the PartyApi instance.</returns>
			virtual const DependencyScope& dependencyScope() const = 0;

			/// <summary>
			/// Register a callback to be notified when the list of sent invitations changes.
			/// </summary>
			/// <param name="callback">Callable object taking a vector if strings as parameter.</param>
			/// <returns>A <c>Subscription</c> object to track the lifetime of the subscription.</returns>
			/// <remarks>
			/// The vector of strings passed to <paramref name="callback" /> is the list of stormancer Ids to which you have sent an invitation to the current party.
			/// Only invitations that are cancelable, and have not yet been accepted or declined by their recipient, will appear in the list.
			/// </remarks>
			virtual Subscription subscribeOnSentInvitationsListUpdated(std::function<void(std::vector<std::string>)> callback) = 0;

			/// <summary>
			/// Register a callback to be notified when an invitation that you previously sent has been declined by its recipient.
			/// </summary>
			/// <param name="callback">Callable object taking the stormancer Id of the recipient of the ivitation as parameter.</param>
			/// <returns>A <c>Subscription</c> object to track the lifetime of the subscription.</returns>
			/// <remarks>
			/// An invitation system may have the notion of a user declining an invitation that they received. The Stormancer invitation system does.
			/// When an invitation that was sent through such a system is declined, and said system supports notifying the sender about the declination, this event will be triggered on the sender's side.
			/// </remarks>
			virtual Subscription subscribeOnSentInvitationDeclined(std::function<void(Stormancer::Users::UserId)> callback) = 0;

			/// <summary>
			/// Register a callback to be run when the party leader changes the party settings.
			/// </summary>
			/// <param name="callback">Callable object taking a <c>PartySettings</c> struct as parameter.</param>
			/// <returns>A <c>Subscription</c> object to track the lifetime of the subscription.</returns>
			virtual Event<PartySettings>::Subscription subscribeOnUpdatedPartySettings(std::function<void(PartySettings)> callback) = 0;

			/// <summary>
			/// Register a callback to be run when there is a change to any party member.
			/// </summary>
			/// <remarks>
			/// This event is triggered for any kind of change to the party members:
			/// - Member joining, leaving or being kicked
			/// - Member data change
			/// - Member status change
			/// - Leader change
			/// A single event can contain multiple kinds of changes for multiple party members.
			/// The <c>MembersUpdate</c> object passed to the callback contains the details of every change.
			/// To retrieve the updated full list of members, call <c>getPartyMembers()</c> (it is safe to call from inside the callback too).
			/// </remarks>
			/// <param name="callback">Callable object taking a <c>MembersUpdate</c> struct as parameter.</param>
			/// <returns>A <c>Subscription</c> object to track the lifetime of the subscription.</returns>
			[[nodiscard("Store the subscription to not unsubscribe immediately from the event.")]] virtual Subscription subscribeOnPartyMembersUpdated(std::function<void(MembersUpdate)> callback) = 0;



			[[nodiscard("Store the subscription to not unsubscribe immediately from the event.")]] virtual Event<PartyConnectionStateChangedArgs>::Subscription subscribeOnPartyConnectionStateChanged(std::function<void(PartyConnectionStateChangedArgs)> callback, bool fireInitialState) = 0;

			/// <summary>
			/// Register a callback to be run when the local player receives an invitation to a party from a remote player.
			/// </summary>
			/// <remarks>
			/// To accept the invitation, call <c>joinParty(PartyInvitation)</c>.
			/// To retrieve the list of all pending invitations received by the local player, call <c>getPendingInvitations()</c>.
			/// </remarks>
			/// <param name="callback">Callable object taking a <c>PartyInvitation</c> parameter.</param>
			/// <returns>A <c>Subscription</c> object to track the lifetime of the subscription.</returns>
			[[nodiscard("Store the subscription to not unsubscribe immediately from the event.")]] virtual Event<PartyInvitation>::Subscription subscribeOnInvitationReceived(std::function<void(PartyInvitation)> callback) = 0;

			/// <summary>
			/// Register a callback to be run when an invitation sent to the local player was canceled by the sender.
			/// </summary>
			/// <param name="callback">Callable object taking the Id of the user who canceled the invitation.</param>
			/// <returns>A <c>Subscription</c> object to track the lifetime of the subscription.</returns>
			[[nodiscard("Store the subscription to not unsubscribe immediately from the event.")]] virtual Event<std::string>::Subscription subscribeOnInvitationCanceled(std::function<void(std::string)> callback) = 0;

			/// <summary>
			/// Register a callback to be run when the status of the GameFinder for this party is updated.
			/// </summary>
			/// <remarks>
			/// Monitoring the status of the GameFinder can be useful to provide visual feedback to the player.
			/// </remarks>
			/// <param name="callback">Callable object taking a <c>GameFinderStatus</c>.</param>
			/// <returns>A <c>Subscription</c> object to track the lifetime of the subscription.</returns>
			virtual Subscription subscribeOnGameFinderStatusUpdate(std::function<void(PartyGameFinderStatus)> callback) = 0;

			/// <summary>
			/// Register a callback to be run when a game session has been found for this party.
			/// </summary>
			/// <remarks>
			/// This event happens as a result of a successful GameFinder request. Call <c>subscribeOnGameFinderStatusUpdate()</c> to monitor the state of the request.
			/// </remarks>
			/// <param name="callback">Callable object taking a <c>GameFinder::GameFinderResponse</c> containing the information needed to join the game session.</param>
			/// <returns>A <c>Subscription</c> object to track the lifetime of the subscription.</returns>
			virtual Subscription subscribeOnGameFound(std::function<void(GameFinder::GameFoundEvent)> callback) = 0;

			/// <summary>
			/// Register a callback to be run when an error occurs while looking for a game session. 
			/// </summary>
			/// <remarks>
			/// This event is triggered when an ongoing GameFinder request for this party fails for any reason.
			/// GameFinder failure conditions are fully customizable on the server side ; please see the GameFinder documentation for details.
			/// </remarks>
			/// <param name="callback">Callable object taking a <c>PartyGameFinderFailure</c> containing details about the failure.</param>
			/// <returns>A <c>Subscription</c> object to track the lifetime of the subscription.</returns>
			virtual Subscription subscribeOnGameFinderFailure(std::function<void(PartyGameFinderFailure)> callback) = 0;

			/// <summary>
			/// Register a callback to be run when an error occurs in the party system.
			/// </summary>
			/// <param name="callback">Callable object taking a const PartyError& that holds data about the error.</param>
			/// <returns>A Subscription object used to control the lifetime of the subscription.</returns>
			virtual Subscription subscribeOnPartyError(std::function<void(PartyError)> callback) = 0;


			/// <summary>
			/// Search parties
			/// </summary>
			/// <param name="jsonQuery">
			/// 
			/// "bool":{
			/// 	"must": [
			///		{
			///			"match":
			/// 		{
			/// 			"field":"numplayers",
			/// 			"value" : 2
			/// 		}
			///		},
			/// 	{
			///			"match":{
			/// 			"field":"gamemode.map",
			/// 			"value" : "level3-a"
			///			}
			/// 	}],
			///     "must_not":[
			///     ],
			///		"should":[
			///     ],
			///     minimum_should_match:1
			/// }
			/// 
			/// </param>
			/// <param name="skip"></param>
			/// <param name="size"></param>
			/// <param name="cancellationToken"></param>
			/// <returns></returns>
			virtual pplx::task<SearchResult> searchParties(const std::string& jsonQuery, Stormancer::uint32 skip, Stormancer::uint32 size, pplx::cancellation_token cancellationToken) = 0;
		};





		namespace details
		{
			class IPartyInvitationInternal
			{
			public:

				virtual ~IPartyInvitationInternal() = default;

				virtual Users::UserId getSenderId() = 0;
				virtual PartyId getPartyId() = 0;

				virtual pplx::task<void> acceptAndJoinParty(pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

				virtual void decline() = 0;

				virtual bool isValid() = 0;
			};
		}

		struct PartyInvitation
		{
			PartyInvitation(std::shared_ptr<details::IPartyInvitationInternal> invite)
				: _internal(invite)
			{}

			PartyInvitation()
			{}

#ifdef __clang__
			// Avoid clang warnings with implicit default constructors. Note: the same solution cannot be applied with MSVC (and isn't needed).
			STORM_WARNINGS_PUSH;
			STORM_CLANG_DIAGNOSTIC("clang diagnostic ignored \"-Wdeprecated-declarations\"")
				PartyInvitation(const PartyInvitation& other) = default;
			PartyInvitation(PartyInvitation&& other) = default;
			PartyInvitation& operator=(const PartyInvitation& other) = default;
			STORM_WARNINGS_POP;
#endif

			/// <summary>
			/// Get the Stormancer Id of the user who sent the invitation.
			/// </summary>
			/// <returns>The Stormancer Id of the player who sent the invitation.</returns>
			Users::UserId getSenderId() const { return _internal->getSenderId(); }

			PartyId getPartyId() const { return _internal->getPartyId(); }
			/// <summary>
			/// Accept the invitation and join the corresponding party.
			/// </summary>
			/// <returns>A task that completes once the party has been joined.</returns>
			pplx::task<void> acceptAndJoinParty(pplx::cancellation_token ct = pplx::cancellation_token::none()) const { return _internal->acceptAndJoinParty(ct); }

			/// <summary>
			/// Decline the invitation.
			/// </summary>
			/// <remarks>
			/// This will remove the invitation from the list obtained via <c>PartyApi::getPendingInvitations()</c>,
			/// and, if the underlying invitation system supports it, send a declination message.
			/// </remarks>
			void decline() { _internal->decline(); }

			/// <summary>
			/// Check whether this invitation is still valid.
			/// </summary>
			/// <remarks>
			/// An invitation becomes invalid once it has been accepted or denied.
			/// </remarks>
			/// <returns><c>true</c> if the invitation is valid, <c>false</c> otherwise.</returns>
			bool isValid() const { return _internal && _internal->isValid(); }

		private:

			std::shared_ptr<details::IPartyInvitationInternal> _internal;
		};



		struct PartySettings
		{
			std::string gameFinderName;
			std::vector<byte> customData;

			template<typename T>
			T getCustomDataAs()
			{
				Serializer serializer;
				size_t read;
				return serializer.deserializeOne<T>(customData, read);
			}

			template<typename T>
			void setCustomDataAs(const T& value)
			{
				Serializer serializer;
				BufferWriter writer;
				serializer.serialize(writer, value);
				auto span = writer.content();
				customData.resize(span.size());
				std::memcpy(customData.data(), span.data(), customData.size());
			}

			bool onlyLeaderCanInvite = true;
			bool isJoinable = true;
			std::unordered_map<std::string, std::string> publicServerData; // Not in STRM_MSGPACK_DEFINE because cannot be set by the client

			/// <summary>
			/// Json document used to search the party.
			/// </summary>
			/// <remarks>
			/// Must be a valid json object.
			/// The party is not searchable if set to empty or an invalid json object.
			/// The content of the document are indexed using the field paths as keys, with '.' as separator.
			/// 
			/// For example, the following document:
			/// {
			/// 	"maxPlayers":3,
			/// 	"gamemode":{
			/// 		"map":"level3-a",
			/// 		"extraFooEnabled":true
			/// 	}
			/// }
			/// 
			/// will be indexed with the following keys:
			/// - "numplayers": 3 (numeric)
			/// - "gamemode.map":"level3-a" (string)
			/// - "gamemode.extraFooEnabled":true (bool)
			/// 
			/// To enable search without filtering, set indexedDocument to an empty json object '{}'.
			/// </remarks>
			std::string indexedDocument;
			std::string partyId;

			STRM_MSGPACK_DEFINE(gameFinderName, customData, onlyLeaderCanInvite, isJoinable, indexedDocument, partyId);
		};

		struct PartyGameFinderFailure
		{
			std::string reason;

			STRM_MSGPACK_DEFINE(reason);
		};

		/// <summary>
		/// This event is triggered when the state of one or more party members changes.
		/// </summary>
		struct MembersUpdate
		{
			MembersUpdate(std::shared_ptr<PartyApi> partyApi)
				: partyApi(partyApi)
			{}

			/// <summary>
			/// The possible kinds of changes that can affect a party member.
			/// </summary>
			enum Kind
			{
				Joined, /* This member just joined the party */
				Left, /* This member just left the party */
				Kicked, /* This member was kicked from the party. The 'Left' bit will be set too in this case. */
				StatusUpdated, /* member.partyUserStatus has changed. */
				DataUpdated, /* member.userData has changed. */
				PromotedToLeader, /* member is the new party leader */
				DemotedFromLeader, /* member is no longer the party leader */
				NUM_KINDS
			};

			struct MemberUpdate
			{
				/// <summary>
				/// The kind of changes that affect <c>member</c>.
				/// </summary>
				/// <remarks>
				/// Multiple kinds of changes can happen at the same time for the same member.
				/// When a certain kind of change is present, the corresponding <c>Kind</c> bit will be set.
				/// </remarks>
				/// <example>
				/// Checking if this member's data has changed:
				/// <code>
				/// if (changes[MembersUpdate::DataUpdated])
				///		// member.userData has changed
				/// </code>
				/// </example>
				std::bitset<NUM_KINDS> changes;

				/// <summary>
				/// The member whose state has changed.
				/// </summary>
				PartyUserDto member;

				MemberUpdate() = default;

				MemberUpdate(PartyUserDto member, Kind updateKind)
					: member(std::move(member))
				{
					changes.set(updateKind);
				}
			};

			/// <summary>
			/// Convenience pointer to the PartyApi.
			/// </summary>
			/// <remarks>
			/// Calling <c>getPartyMembers()</c> from inside this event will yield the updated member list.
			/// </remarks>
			std::shared_ptr<PartyApi> partyApi;
			/// <summary>
			/// This list of member updates which have occurred.
			/// </summary>
			std::vector<MemberUpdate> updatedMembers;
		};

		namespace Platform
		{
			struct PlatformInvitationRequestContext
			{
				/// If the error string is empty, the party api will try to join the filled partyId.
				/// <remarks>
				/// Maybe you will need to set error with `Party::PartyError::Str::InvalidInvitation`.
				/// </remarks>
				std::string error;

				/// Party Id to join.
				PartyId partyId;

				/// Invited user.
				std::shared_ptr<Users::PlatformUserId> invitedUser;

				/// Cancellation token.
				pplx::cancellation_token cancellationToken = pplx::cancellation_token::none();
			};

			/// <summary>
			/// Interface for a platform-specific invitation to a party.
			/// </summary>
			class IPlatformInvitation
			{
			public:

				virtual ~IPlatformInvitation() = default;

				/// <summary>
				/// This method is called when the user accepts the invitation.
				/// </summary>
				/// <remarks>
				/// Inside this method, you must do the operations required by your platform to accept the invitation, if any.
				/// You must also provide a PartyId for the party to be joined.
				/// </remarks>
				/// <param name="party">PartyApi instance</param>
				/// <returns>
				/// A task which result is a PartyId to the party.
				/// </returns>
				virtual pplx::task<PartyId> accept(std::shared_ptr<PartyApi> party) = 0;

				virtual PartyId getPartyId() = 0;
				/// <summary>
				/// This method is called when the user declines the invitation.
				/// </summary>
				/// <remarks>
				/// If your platform has support for declining an invitation, you must do the necessary operations there.
				/// Otherwise, you should return <c>pplx::task_from_result()</c>.
				/// </remarks>
				/// <param name="party">PartyApi instance</param>
				virtual pplx::task<void> decline(std::shared_ptr<PartyApi> party) = 0;

				/// <summary>
				/// Get the stormancer user Id of the sender.
				/// </summary>
				/// <remarks>
				/// You must provide a way to retrieve the stormancer user Id of the user who sent the invitation.
				/// </remarks>
				/// <returns>The stormancer user Id of the player who sent the invitation.</returns>
				virtual Users::UserId getSenderId() = 0;



				// Called by PartyApi 
				Subscription subscribeOnInvitationCanceled(std::function<void()> callback)
				{
					return _invitationCanceledEvent.subscribe(callback);
				}

			protected:

				/// <summary>
				/// Notify the party system that this invitation was canceled by its sender.
				/// </summary>
				/// <remarks>This is relevant for invitation systems that support invitation canceling.</remarks>
				void notifyInvitationCanceled()
				{
					_invitationCanceledEvent();
				}

			private:

				Event<> _invitationCanceledEvent;
			};

			/// <summary>
			/// This class transmits platform-specific invitation events from the platform support providers to the PartyApi.
			/// </summary>
			/// <remarks>
			/// It allows decoupling PartyApi and IPlatformSupportProvider to avoid cyclic dependency issues.
			/// </remarks>
			class InvitationMessenger
			{
			public:

				InvitationMessenger(std::shared_ptr<ILogger> logger)
					: _logger(logger)
				{}

				void notifyInvitationReceived(std::shared_ptr<IPlatformInvitation> invitation)
				{
					if (_invitationReceivedEvent.hasSubscribers())
					{
						_logger->log(LogLevel::Trace, "InvitationMessenger", "Fire event invitation received");
						_invitationReceivedEvent(invitation);
					}
					else
					{
						_logger->log(LogLevel::Trace, "InvitationMessenger", "Store invitation as pending invitation");
						_pendingInvitation = invitation;
					}
				}

				Subscription subscribeOnInvitationReceived(std::function<void(std::shared_ptr<IPlatformInvitation>)> callback)
				{
					auto subscription = _invitationReceivedEvent.subscribe(callback);
					if (_pendingInvitation)
					{
						_logger->log(LogLevel::Trace, "InvitationMessenger", "Fire event invitation received (pending invitation)");
						_invitationReceivedEvent(_pendingInvitation);
						_pendingInvitation.reset();
					}
					return subscription;
				}

			private:

				Event<std::shared_ptr<IPlatformInvitation>> _invitationReceivedEvent;
				std::shared_ptr<IPlatformInvitation> _pendingInvitation;
				std::shared_ptr<ILogger> _logger;
			};

			/// <summary>
			/// Platform-specific extensibility points for the party system.
			/// </summary>
			class IPlatformSupportProvider
			{
			public:

				virtual ~IPlatformSupportProvider() = default;

				IPlatformSupportProvider(std::shared_ptr<InvitationMessenger> messenger)
					: _invitationMessenger(messenger)
				{}

				/// <summary>
				/// The name of the platform. There cannot be more than one IPlatformSupportProvider implementation per platform.
				/// </summary>
				/// <returns>Name of the platform</returns>
				virtual const std::string& getPlatformName() const = 0;

				// Platform-specific session

				/// <summary>
				/// Retrieve the stormancer PartyId for a platform-specific PartyId.
				/// </summary>
				/// <param name="platformPartyId">Platform-specific Id for the party for this platform.</param>
				/// <returns>The stormancer scene Id for the party. If the function can't find the sceneId, it returns an empty string.</returns>
				virtual pplx::task<PartyId> getPartyId(const PartyId&, pplx::cancellation_token = pplx::cancellation_token::none())
				{
					STORM_RETURN_TASK_FROM_EXCEPTION(std::runtime_error("Unsupported"), PartyId);
				}

				/// <summary>
				/// Create or join a platform-specific session for the party.
				/// </summary>
				/// <remarks>
				/// This method is called when creating or joining a party.
				/// </remarks>
				/// <param name="partySceneId">Scene Id of the party being joined</param>
				/// <returns>A task that should complete when the platform-specific session has been joined.</returns>
				virtual pplx::task<void> createOrJoinSessionForParty(const std::string& /*partySceneId*/)
				{
					return pplx::task_from_result();
				}

				/// <summary>
				/// Leave a platform-specific session that backs a party.
				/// </summary>
				/// <param name="partySceneId">Scene Id of the party that we are leaving</param>
				/// <returns>A task that should complete when the platform-specific session has been left.</returns>
				virtual pplx::task<void> leaveSessionForParty(const std::string& /*partySceneId*/)
				{
					return pplx::task_from_result();
				}

				/// <summary>
				/// Enables customization of the party scene.
				/// </summary>
				/// <param name="partyScene">Scene of the party</param>
				virtual void onPartySceneInitialization(std::shared_ptr<Scene> /*partyScene*/) {}

				/// <summary>
				/// Update the platform-specific session settings according to the party settings.
				/// </summary>
				/// <remarks>
				/// Implement this method if you need to keep settings in sync between the party and your platform-specific session.
				/// </remarks>
				/// <param name="settings">Party settings</param>
				/// <returns>A task that should complete when the platform-specific session settings have been updated, if needed.</returns>
				virtual pplx::task<void> updateSessionSettings(const PartySettings& /*settings*/)
				{
					return pplx::task_from_result();
				}

				/// <summary>
				/// Update the platform-specific session members according to their counterparts in the party.
				/// </summary>
				/// <remarks>
				/// Implement this method if you need to keep member data in sync between the party and your platform-specific session.
				/// </remarks>
				/// <param name="update">Object describing the changes that have occurred for party members.</param>
				/// <returns>A task that should complete when the platform-specific session members have been updated, if needed.</returns>
				virtual pplx::task<void> updateSessionMembers(const MembersUpdate&)
				{
					return pplx::task_from_result();
				}

				// Advertised parties

				/// <summary>
				/// Get a list of parties advertised by this platform.
				/// </summary>
				/// <remarks>
				/// For instance, these can be parties joined by friends of the current user.
				/// </remarks>
				/// <returns>List of parties advertised on this platform</returns>
				virtual pplx::task<std::vector<AdvertisedParty>> getAdvertisedParties(pplx::cancellation_token = pplx::cancellation_token::none())
				{
					return pplx::task_from_result(std::vector<AdvertisedParty>{});
				}



				/// <summary>
				/// Show a platform-specific UI to send invitations to the current party
				/// </summary>
				/// <param name="party"></param>
				/// <returns><c>true</c> if the invitation UI could be shown, <c>false</c> otherwise.</returns>
				virtual bool tryShowSystemInvitationUI(std::shared_ptr<PartyApi> /*partyApi*/)
				{
					return false;
				}

			protected:

				/// <summary>
				/// Call this method when the user receives an invitation on this platform.
				/// </summary>
				/// <remarks>
				/// This method notifies the party system that an invitation has been received.
				/// </remarks>
				void notifyInvitationReceived(std::shared_ptr<IPlatformInvitation> invitation)
				{
					_invitationMessenger->notifyInvitationReceived(invitation);
				}

			private:

				std::shared_ptr<InvitationMessenger> _invitationMessenger;
			};
		}

		/// <summary>
		/// This context is used by <see cref="Stormancer::IPartyEventHandler::OnJoiningParty(std::shared_ptr&lt;JoiningPartyContext&gt;)" />.
		/// It contains data used to connect to the party scene.
		/// </summary>
		struct JoiningPartyContext
		{
			JoiningPartyContext(const PartyId& partyId, pplx::cancellation_token cancellationToken)
				:partyId(partyId)
				, cancellationToken(cancellationToken)
			{}
			PartyUserDto localUser;
			PartySettings settings;

			std::string partySceneId;
			PartyId partyId;
			std::shared_ptr<PartyApi> partyApi;
			std::unordered_map<std::string, std::string> metadata;
			pplx::cancellation_token cancellationToken;
			bool isValid = true;

			void setError(const std::string& errorMsg)
			{
				this->error = errorMsg;
				isValid = false;
			}
			/// <summary>
			/// gets or sets the validation error if isValid = false;
			/// </summary>
			std::string error;

		};

		struct JoinedPartyContext
		{
			std::string partySceneId;
			PartyId partyId;
			std::shared_ptr<PartyApi> partyApi;
		};

		struct LeavingPartyContext
		{
			std::string partySceneId;
			PartyId partyId;
			std::shared_ptr<PartyApi> partyApi;
		};

		struct LeftPartyContext
		{
			PartyId partyId;
			std::shared_ptr<PartyApi> partyApi;
			MemberDisconnectionReason reason;
		};

		class IPartyEventHandler
		{
		public:

			virtual ~IPartyEventHandler() = default;

			/// <summary>
			/// This event is fired during the initialization of a party scene that is being joined.
			/// </summary>
			/// <remarks>
			/// This event enables you to add handlers for custom routes and server-to-client RPCs.
			/// </remarks>
			/// <param name="partyScene">Scene of the party you are currently joining.</param>
			virtual void onPartySceneInitialization(std::shared_ptr<Scene> /*partyScene*/)
			{}

			/// <summary>
			/// This event is fired before the client joins a party.
			/// </summary>
			/// <remarks>
			/// Use this event to customize the data sent to the party when joining, or to validate the joining process.
			/// </remarks>
			/// <param name="party">The general Party API</param>
			/// <param name="partySceneId">Id of the party's scene</param>
			/// <returns>
			/// A task that should complete when your custom operation is done.
			/// If this task is faulted or canceled, the user will be disconnected from the party immediately.
			/// </returns>
			virtual pplx::task<void> onJoiningParty(std::shared_ptr<JoiningPartyContext> /*ctx*/)
			{
				return pplx::task_from_result();
			}

			/// <summary>
			/// This event is fired upon leaving the Party you were previously in.
			/// </summary>
			/// <remarks>
			/// This gives you a chance to perform additional operations when you are leaving a party.
			/// For instance, if you joined a platform-specific online session in <c>onJoiningParty()</c>,
			/// you probably want to leave this session in <c>onLeavingParty()</c>.
			/// </remarks>
			/// <param name="party">The general Party API</param>
			/// <param name="partySceneId">Id of the party's scene</param>
			/// <returns>
			/// A task that should complete when your custom operation is done.
			/// </returns>
			virtual pplx::task<void> onLeavingParty(std::shared_ptr<LeavingPartyContext> /*ctx*/)
			{
				return pplx::task_from_result();
			}


			/// <summary>
			/// This event is fired when a change happens to one or more party members, and when members join or leave the party.
			/// </summary>
			/// <param name="update">Structure containing details about the updated members.</param>
			virtual void onPartyMembersUpdated(const MembersUpdate& /*update*/) {}

			/// <summary>
			/// The event is fired when the party settings change.
			/// </summary>
			/// <param name="party"></param>
			/// <param name="settings">The updated party settings.</param>
			virtual void onPartySettingsUpdated(std::shared_ptr<PartyApi>, const PartySettings&) {}

			/// <summary>
			/// This event is fired when the local player joins a party.
			/// </summary>
			/// <param name="party"></param>
			virtual pplx::task<void> onJoinedParty(std::shared_ptr<JoinedPartyContext> /*ctx*/) { return pplx::task_from_result(); }

			/// <summary>
			/// This event is fired when the local player leaves the party.
			/// </summary>
			/// <param name="party"></param>
			/// <param name="reason">The cause of the player leaving.</param>
			virtual pplx::task<void> onLeftParty(std::shared_ptr<LeftPartyContext> /*ctx*/) { return pplx::task_from_result(); }
		};

		namespace details
		{
			struct InvitationRequest
			{
				enum class Operation
				{
					None,
					Send,
					Cancel
				};

				// The operation that is currently pending for this invitation. This member helps handling the case where send/cancel/send are called repeatedly.
				Operation pendingOperation = Operation::None;

				// The invitation's task. The result is true when the user accepts, false when they refuse. For platform-specific invitations, it is always true.
				pplx::task<bool> task;

				// Used to cancel the invitation when calling cancelInvitation()
				pplx::cancellation_token_source cts;
			};




			struct MemberStatusUpdateRequest
			{
				PartyUserStatus	desiredStatus;
				int				localSettingsVersion;

				STRM_MSGPACK_DEFINE(desiredStatus, localSettingsVersion);
			};

			struct MemberStatusUpdate
			{
				std::string		userId;
				PartyUserStatus	status;

				STRM_MSGPACK_DEFINE(userId, status);
			};

			struct BatchStatusUpdate
			{
				std::vector<MemberStatusUpdate> memberStatus;

				STRM_MSGPACK_DEFINE(memberStatus);
			};

			struct PartyUserData
			{
				std::string userId;
				std::vector<byte> userData;
				std::vector<LocalPlayerInfos> localPlayers;

				STRM_MSGPACK_DEFINE(userId, userData, localPlayers);
			};

			struct MemberDisconnection
			{
				std::string					userId;
				MemberDisconnectionReason	reason;

				STRM_MSGPACK_DEFINE(userId, reason);
			};

			inline bool tryParseVersion(const char* version, int& outVersionNumber)
			{
				int year = 0, month = 0, day = 0, revision = 0;

#if defined(_WINDOWS_) || defined(__ORBIS__)
				int numMatches = sscanf_s(version, "%4d-%2d-%2d.%d", &year, &month, &day, &revision);
#else
				int numMatches = sscanf(version, "%4d-%2d-%2d.%d", &year, &month, &day, &revision);
#endif
				if (numMatches != 4 ||
					year < 2019 || month < 1 || month > 12 || day < 1 || day > 31 || revision < 1)
				{
					return false;
				}
				// Make a decimal number out of the version string
				outVersionNumber = revision + (day * 10) + (month * 1000) + (year * 100000);
				return true;
			}

			inline int parseVersion(const char* version)
			{
				int versionInt = 0;
				if (!tryParseVersion(version, versionInt))
				{
					throw std::runtime_error("Could not parse version");
				}
				return versionInt;
			}

		}

		class InPartyDesiredState :public DesiredState
		{
		public:
			InPartyDesiredState(std::shared_ptr<IClient> client)
				:_client(client)
				, _party(client->dependencyResolver().resolve<PartyApi>())
				, _users(client->dependencyResolver().resolve<Stormancer::Users::UsersApi>())
				, _logger(client->dependencyResolver().resolve<ILogger>())
			{


			}

			bool withCode = false;

			void initialize() override
			{
				auto party = LockOrThrow(_party);
				auto users = LockOrThrow(_users);


				_partyConnectionStateChangedSubscription = party->subscribeOnPartyConnectionStateChanged([this](PartyConnectionStateChangedArgs args)
					{
						if (args.newState == PartyConnectionStateId::Disconnected && args.disconnectionReason != MemberDisconnectionReason::Joining)
						{
							connectToParty();
						}
					}, true);
				_userConnectionStateChangedSubscription = users->connectionStateChanged.subscribe(
					[this](Users::GameConnectionState args)
					{
						if (args.state == Users::GameConnectionState::Authenticated)
						{
							connectToParty();
						}
					});

			}


			~InPartyDesiredState() override
			{
				cts.cancel();
			}

			const StateId& id() const override
			{
				return States::InParty();
			}
			virtual bool isStateReached() const override
			{
				auto party = _party.lock();
				if (!party)
				{
					return false;
				}

				return party->isInParty();
			}
		private:
			bool _connecting = false;
			std::weak_ptr<InPartyDesiredState> weak_ptr()
			{
				return std::static_pointer_cast<InPartyDesiredState>(this->shared_from_this());
			}

			void connectToParty()
			{

				std::lock_guard<std::mutex> lg(_syncRoot);
				if (_connecting)
				{
					return;
				}
				_connecting = true;
				auto party = _party.lock();
				if (!party)
				{
					return;
				}
				if (!party->isOffline())
				{
					return;
				}
				withRetries<void>([wParty = _party, withCode = withCode,
					wThat = weak_ptr()](pplx::cancellation_token ct)
					{
						auto party = LockOrThrow(wParty);

						return party->createParty(ct)
							.then([party, withCode, wThat]()
								{
									if (withCode)
									{
										return party->createInvitationCode().then(
											[wThat](std::string)
											{
												if (auto that = wThat.lock())
												{
													that->notifyStateReached();
												}
											}
										);
									}
									else
									{
										return pplx::task_from_result();
									}
								});


					}, 1000ms, 0, [logger = _logger](auto& ex) {
						logger->log(LogLevel::Error, "desiredState.party", "Failed to create party.", ex);
						return true; }, pplx::get_ambient_scheduler(), cts.get_token())
					.then(
						[wThat = weak_ptr()](pplx::task<void> t)
						{
							try
							{
								t.get();
							}
							catch (...)
							{

							}

							if (auto that = wThat.lock())
							{
								that->_connecting = false;
							}
						});
			}
			std::mutex _syncRoot;
			std::weak_ptr< IClient> _client;
			std::weak_ptr<PartyApi> _party;
			std::weak_ptr<Stormancer::Users::UsersApi> _users;
			std::shared_ptr<ILogger> _logger;
			pplx::cancellation_token_source cts;
			Stormancer::Subscription _partyConnectionStateChangedSubscription;
			Stormancer::Subscription _userConnectionStateChangedSubscription;
		};
	}
} // namespace Stormancer
namespace std
{
	template<>
	struct std::formatter<Stormancer::Party::PartyId>
	{
		constexpr auto parse(std::format_parse_context& ctx)
		{
			return ctx.begin(); // No custom format specifiers
		}

		auto format(const Stormancer::Party::PartyId& p, std::format_context& ctx) const
		{
			return std::format_to(ctx.out(), "{}", p.toString());
		}
	};

	template<>
	struct std::formatter<Stormancer::Party::PartyInvitation>
	{
		constexpr auto parse(std::format_parse_context& ctx)
		{
			return ctx.begin(); // No custom format specifiers
		}

		auto format(const Stormancer::Party::PartyInvitation& p, std::format_context& ctx) const
		{
			return std::format_to(ctx.out(), "{}:{}", p.getSenderId().toString(), p.getPartyId().toString());
		}
	};
} // namespace std
STRM_MSGPACK_ADD_ENUM(Stormancer::Party::PartyUserStatus);
STRM_MSGPACK_ADD_ENUM(Stormancer::Party::MemberDisconnectionReason);
STRM_MSGPACK_ADD_ENUM(Stormancer::Party::PartyUserConnectionStatus);

#if STORM_PLUGIN_IMPL == 1


#endif