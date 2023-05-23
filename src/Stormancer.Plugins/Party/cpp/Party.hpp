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
#include "Users/ClientAPI.hpp"
#include "Users/Users.hpp"

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

namespace Stormancer
{
	namespace Party
	{
		struct PartyUserDto;
		struct PartySettings;
		struct PartyInvitation;
		struct PartyCreationOptions;
		struct PartyGameFinderFailure;
		struct MembersUpdate;
		struct JoinPartyFromSystemArgs;

		enum class PartyUserStatus
		{
			NotReady = 0,
			Ready = 1
		};

		enum class PartyGameFinderStatus
		{
			SearchStopped = 0,
			SearchInProgress = 1
		};

		enum class MemberDisconnectionReason
		{
			Left = 0,
			Kicked = 1
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
			{
			}
		};

		/// <summary>
		/// Abstraction for a party identifier.
		/// </summary>
		/// <remarks>
		/// Could be a stormancer scene Id, a platform-specific session Id, and more.
		/// </remarks>
		struct PartyId
		{
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

			MSGPACK_DEFINE(type, id, platform);

			static constexpr const char* TYPE_SCENE_ID = "stormancer.sceneId";
			static constexpr const char* TYPE_PARTY_ID = "stormancer.partyId";
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
					if (type[1] == STRING_TYPE_FIELD)
					{
						partyId.type = type[1];
					}
					auto id = stringSplit(parts[2], STRING_SEP_2);
					if (id[2] == STRING_ID_FIELD)
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

				MSGPACK_DEFINE(stormancerId, platformId, username, data);
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

			MSGPACK_DEFINE(partyId, leaderUserId, friends, metadata);
		};

		struct PartyDocument
		{
			std::string id;
			std::string content;

			MSGPACK_DEFINE(id, content)
		};

		struct SearchResult
		{
			Stormancer::uint32 total;

			std::vector<PartyDocument> hits;
			MSGPACK_DEFINE(total, hits)
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
			virtual pplx::task<void> createParty(const PartyCreationOptions& partyRequest, const std::unordered_map<std::string, std::string>& userMetadata = {}, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			/// <summary>
			/// Creates a party if the user is not connected to one.
			/// </summary>
			/// <param name="partyRequest">Party creation parameters.</param>
			/// <param name="ct">Cancellation token that cancels party creation.</param>
			/// <returns></returns>
			virtual pplx::task<void> createPartyIfNotJoined(const PartyCreationOptions& partyRequest, const std::unordered_map<std::string, std::string>& userMetadata = {}, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			/// <summary>
			/// Join an existing party using a connection token provided by the server
			/// </summary>
			/// <param name="connectionToken">Token required to connect to the party.</param>
			/// <returns>A task that completes once the party has been joined.</returns>
			virtual pplx::task<void> joinParty(const std::string& connectionToken, const std::unordered_map<std::string, std::string>& userMetadata = {}, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			/// <summary>
			/// Join a party using an abstract PartyId.
			/// </summary>
			/// <param name="partyId">Abstract PartyId.</param>
			/// <returns>A task that completes once the party has been joined.</returns>
			virtual pplx::task<void> joinParty(const PartyId& partyId, const std::vector<byte>& userData, const std::unordered_map<std::string, std::string>& userMetadata = {}, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			/// <summary>
			/// Join an existing party using its unique scene Id.
			/// </summary>
			/// <param name="sceneId">Id of the party scene.</param>
			/// <returns>A task that completes once the party has been joined.</returns>
			virtual pplx::task<void> joinPartyBySceneId(const std::string& sceneId, const std::vector<byte>& userData, const std::unordered_map<std::string, std::string>& userMetadata = {}, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			/// <summary>
			/// Join an existing party using an invitationCode.
			/// </summary>
			/// <param name="invitationCode"></param>
			/// <param name="userData">custom data associated with the party member on join.</param>
			/// <param name="ct"></param>
			/// <returns></returns>
			virtual pplx::task<void> joinPartyByInvitationCode(const std::string& invitationCode, const std::vector<byte>& userData = {}, const std::unordered_map<std::string, std::string>& userMetadata = {}, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			/// <summary>
			/// Gets a boolean indicating if the party is currently in a gamesession.
			/// </summary>
			/// <returns></returns>
			virtual bool isInGameSession() = 0;

			/// <summary>
			/// If the party is in a gamesession, gets a token to connect to it.
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

			/// <summary>
			/// Get the party scene.
			/// </summary>
			/// <returns>The party scene.</returns>
			virtual std::shared_ptr<Scene> getPartyScene() const = 0;

			/// <summary>
			/// Get the member list of the currently joined party.
			/// </summary>
			/// <remarks>
			/// It is invalid to call this method while not in a party.
			/// Call <c>isInParty()</c> to check.
			/// </remarks>
			/// <returns>A vector of structs that describe every user who is currently in the party.</returns>
			/// <exception cref="std::exception">If you are not in a party.</exception>
			virtual std::vector<PartyUserDto> getPartyMembers() const = 0;

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
			virtual PartyUserDto getLocalMember() const = 0;

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
			virtual pplx::task<void> updatePlayerStatus(PartyUserStatus playerStatus) = 0;

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
			virtual PartyId getPartyId() const = 0;

			/// <summary>
			/// Get the User Id of the party leader.
			/// </summary>
			/// <returns>The Stormancer User Id of the party leader.</returns>
			/// <exception cref="std::exception">If you are not in a party.</exception>
			virtual std::string getPartyLeaderId() const = 0;

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
			virtual pplx::task<void> updatePartySettings(PartySettings partySettings) = 0;

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
			virtual pplx::task<void> updatePlayerData(std::vector<byte> data, unsigned int localPlayerCount = 1) = 0;

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

			virtual pplx::task<void> cancelInvitationCode(pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

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
			/// <param name="forceStormancerInvite">If <c>true</c>, always send a Stormancer invitation, even if a platform-specific invitation system is available.</param>
			/// <remarks>
			/// The stormancer server determines the kind of invitation that should be sent according to the sender and the recipient's platform.
			/// Unless <paramref name="forceStormancerInvite" /> is set to <c>true</c>, stormancer will prioritize platform-specific invitation systems where possible.
			/// If your game needs cancelable invitations as a feature, you should always set <paramref name="forceStormancerInvite" /> to <c>true</c>.
			/// </remarks>
			/// <returns>A task that completes when the invitation has been sent.</returns>
			virtual pplx::task<void> sendInvitation(const std::string& recipient, bool forceStormancerInvite = false) = 0;

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
			virtual void cancelInvitation(const std::string& recipient) = 0;

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
			/// An invtitation system may have the notion of a user declining an invitation that they received. The Stormancer invitation system does.
			/// When an invitation that was sent through such a system is declined, and said system supports notifying the sender about the declination, this event will be triggered on the sender's side.
			/// </remarks>
			virtual Subscription subscribeOnSentInvitationDeclined(std::function<void(std::string)> callback) = 0;

			/// <summary>
			/// Register a callback to be run when the party leader changes the party settings.
			/// </summary>
			/// <param name="callback">Callable object taking a <c>PartySettings</c> struct as parameter.</param>
			/// <returns>A <c>Subscription</c> object to track the lifetime of the subscription.</returns>
			virtual Event<PartySettings>::Subscription subscribeOnUpdatedPartySettings(std::function<void(PartySettings)> callback) = 0;

			/// <summary>
			/// Register a callback to be run when the party member list changes.
			/// </summary>
			/// <remarks>
			/// This event is triggered for any kind of change to the list:
			/// - Member addition and removal
			/// - Member data change
			/// - Member status change
			/// - Leader change
			/// The list of <c>PartyUserDto</c> passed to the callback contains only the entries that have changed.
			/// To retrieve the updated full list of members, call <c>getPartyMembers()</c> (it is safe to call from inside the callback too).
			/// </remarks>
			/// <param name="callback">Callable object taking a vector of <c>PartyUserDto</c> structs as parameter.</param>
			/// <returns>A <c>Subscription</c> object to track the lifetime of the subscription.</returns>
			STORM_DEPRECATED("Use subscribeOnPartyMembersUpdated() instead")
				virtual Event<std::vector<PartyUserDto>>::Subscription subscribeOnUpdatedPartyMembers(std::function<void(std::vector<PartyUserDto>)> callback) = 0;

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
			virtual Subscription subscribeOnPartyMembersUpdated(std::function<void(MembersUpdate)> callback) = 0;

			/// <summary>
			/// Register a callback to be run when the local player has joined a party.
			/// </summary>
			/// <param name="callback">Callable object.</param>
			/// <returns>A <c>Subscription</c> object to track the lifetime of the subscription.</returns>
			virtual Event<>::Subscription subscribeOnJoinedParty(std::function<void()> callback) = 0;

			/// <summary>
			/// Register a callback to be run when the local player has left the party.
			/// </summary>
			/// <remarks>
			/// The callback parameter <c>MemberDisconnectionReason</c> will be set to <c>Kicked</c> if you were kicked by the party leader.
			/// In any other case, it will be set to <c>Left</c>.
			/// </remarks>
			/// <param name="callback">Callable object taking a <c>MemberDisconnectionReason</c> parameter.</param>
			/// <returns>A <c>Subscription</c> object to track the lifetime of the subscription.</returns>
			virtual Event<MemberDisconnectionReason>::Subscription subscribeOnLeftParty(std::function<void(MemberDisconnectionReason)> callback) = 0;

			/// <summary>
			/// Register a callback to be run when the local player receives an invitation to a party from a remote player.
			/// </summary>
			/// <remarks>
			/// To accept the invitation, call <c>joinParty(PartyInvitation)</c>.
			/// To retrieve the list of all pending invitations received by the local player, call <c>getPendingInvitations()</c>.
			/// </remarks>
			/// <param name="callback">Callable object taking a <c>PartyInvitation</c> parameter.</param>
			/// <returns>A <c>Subscription</c> object to track the lifetime of the subscription.</returns>
			virtual Event<PartyInvitation>::Subscription subscribeOnInvitationReceived(std::function<void(PartyInvitation)> callback) = 0;

			/// <summary>
			/// Register a callback to be run when an invitation sent to the local player was canceled by the sender.
			/// </summary>
			/// <param name="callback">Callable object taking the Id of the user who canceled the invitation.</param>
			/// <returns>A <c>Subscription</c> object to track the lifetime of the subscription.</returns>
			virtual Event<std::string>::Subscription subscribeOnInvitationCanceled(std::function<void(std::string)> callback) = 0;

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
			/// <param name="callback">Callable object taking a <c>const PartyError&</c> that holds data about the error.</param>
			/// <returns>A <c>Subscription</c> object to control the lifetime of the subscription.</returns>
			virtual Subscription subscribeOnPartyError(std::function<void(const PartyError&)> callback) = 0;

			/// <summary>
			/// Set a handler to be run when a request to join a party is made from the game's system.
			/// </summary>
			/// <remarks>
			/// Many gaming platforms support joining a platform-specific session from an out-of-game UI.
			/// When integration with the stormancer party system for such a platform is available,
			/// when this feature is used, and it is found that the session being joined is associated to a stormancer party,
			/// the game will be notified through the handler passed to this method.
			/// It can then decide whether or not the party should be joined, and maybe do some processing, like transitioning to a different UI screen.
			/// </remarks>
			/// <param name="handler">
			/// Function that will be called when the user makes a request to join a stormancer party from the system UI.
			/// It must return a task with its result set to <c>true</c> if we should proceed to join the party, or <c>false</c> if we should not join the party.
			/// </param>
			virtual void setJoinPartyFromSystemHandler(std::function<pplx::task<bool>(JoinPartyFromSystemArgs)> handler) = 0;

			virtual pplx::task<SearchResult> searchParties(const std::string& jsonQuery, Stormancer::uint32 skip, Stormancer::uint32 size, pplx::cancellation_token cancellationToken) = 0;
		};

		/// <summary>
		/// Arguments passed to the callback set by <c>setJoinPartyFromSystemHandler()</c> when a join party from system event occurs.
		/// </summary>
		struct JoinPartyFromSystemArgs
		{
			std::shared_ptr<IClient> client;
			std::shared_ptr<PartyApi> party;
			std::shared_ptr<Users::PlatformUserId> user;
			PartyId partyId;
			pplx::cancellation_token cancellationToken = pplx::cancellation_token::none();
			std::vector<byte> userData;
		};

		/// <summary>
		/// Party creation settings.
		/// </summary>
		/// <remarks>
		/// Some of these settings can be changed by the party leader after the party has been created, by calling <c>PartyApi::updatePartySettings()</c>.
		/// </remarks>
		struct PartyCreationOptions
		{
			/// <summary>
			/// Optional: Set this if you want to force the party's scene Id to a specific value.
			/// </summary>
			/// <remarks>
			/// This should be left empty, unless you have very specific needs.
			/// For instance, it could be used if you wanted to bypass stormancer's built-in platform-specific session and invitation integration.
			/// This cannot be changed after the party has been created.
			/// </remarks>
			std::string platformSessionId;

			/// <summary>
			/// Required: Name of the GameFinder that the party will use.
			/// </summary>
			/// <remarks>
			/// This GameFinder must exist and be accessible from the party on the server application.
			/// This setting can be changed after the party has been created.
			/// </remarks>
			std::string GameFinderName;

			/// <summary>
			/// Optional: Game-specific, party-wide custom data.
			/// </summary>
			/// <remarks>
			/// This is the custom data for the whole party. After the party has been created, it can be changed by the party leader using <c>PartyApi::updatePartySettings()</c>.
			/// This must not be confused with per-player custom data, which can be set using <c>PartyApi::updatePlayerData()</c>.
			/// </remarks>
			std::string CustomData;

			/// <summary>
			/// Optional: Settings for server-side extensions of the Party system.
			/// </summary>
			/// <remarks>
			/// If you are using any Party extensions that require settings at party creation time, these settings should be put in this map.
			/// These settings cannot be changed after the party has been created.
			/// </remarks>
			std::unordered_map<std::string, std::string> serverSettings;

			/// <summary>
			/// Optional: If true, only the party leader can send invitations to other players. If false, all party members can send invitations.
			/// </summary>
			/// <remarks>
			/// By default, the party leader is the player who created the party. It can be changed later by calling <c>PartyApi::promoteLeader()</c>.
			/// This setting can be changed after the party has been created.
			/// </remarks>
			bool onlyLeaderCanInvite = true;

			/// <summary>
			/// Optional: Whether the party can be joined by other players, including players who have been invited.
			/// </summary>
			/// <remarks>
			/// When this is <c>false</c>, nobody can join the party.
			/// This setting can be changed after the party has been created.
			/// </remarks>
			bool isJoinable = true;

			/// <summary>
			/// Whether the party is public or private.
			/// </summary>
			/// <remarks>
			/// A public party is always visible to other players.
			/// A private party is visible only to players who have received an invitation.
			/// On some platforms, only public parties can be advertised.
			/// </remarks>
			bool isPublic = false;

			/// <summary>
			/// Gets or sets binary member data to associate the party leader with on party join.
			/// </summary>
			std::vector<byte> userData;

			MSGPACK_DEFINE(platformSessionId, GameFinderName, CustomData, serverSettings, onlyLeaderCanInvite, isJoinable, isPublic, userData);
		};

		namespace details
		{
			class IPartyInvitationInternal
			{
			public:

				virtual ~IPartyInvitationInternal() = default;

				virtual std::string getSenderId() = 0;

				virtual std::string getSenderPlatformId() = 0;

				virtual pplx::task<void> acceptAndJoinParty(const std::vector<byte>& userData, const std::unordered_map<std::string, std::string>& userMetadata = {}, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

				virtual void decline() = 0;

				virtual bool isValid() = 0;
			};
		}

		struct PartyInvitation
		{
			PartyInvitation(std::shared_ptr<details::IPartyInvitationInternal> invite)
				: _internal(invite)
			{
			}

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
			/// Get the stormancer Id of the user who sent the invitation.
			/// </summary>
			/// <returns>The stormancer Id of the player who sent the invitation.</returns>
			std::string getSenderId() const { return _internal->getSenderId(); }

			std::string getSenderPlatformId() const { return _internal->getSenderPlatformId(); }

			/// <summary>
			/// Accept the invitation and join the corresponding party.
			/// </summary>
			/// <returns>A task that completes once the party has been joined.</returns>
			pplx::task<void> acceptAndJoinParty(const std::vector<byte>& userData = {}, const std::unordered_map<std::string, std::string>& userMetadata = {}, pplx::cancellation_token ct = pplx::cancellation_token::none()) { return _internal->acceptAndJoinParty(userData, userMetadata, ct); }

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
			bool isValid() const { return _internal->isValid(); }

		private:

			std::shared_ptr<details::IPartyInvitationInternal> _internal;
		};

		struct PartyUserDto
		{
			std::string userId;
			PartyUserStatus partyUserStatus;
			std::vector<byte> userData;
			Stormancer::SessionId sessionId;

			unsigned int localPlayerCount;

			bool isLeader = false; // Computed locally

			PartyUserDto(std::string userId) : userId(userId) {}
			PartyUserDto() = default;

			MSGPACK_DEFINE(userId, partyUserStatus, userData, sessionId, localPlayerCount);
		};

		struct PartySettings
		{
			std::string gameFinderName;
			std::string customData;
			bool onlyLeaderCanInvite = true;
			bool isJoinable = true;
			std::unordered_map<std::string, std::string> publicServerData; // Not in MSGPACK_DEFINE because cannot be set by the client

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

			MSGPACK_DEFINE(gameFinderName, customData, onlyLeaderCanInvite, isJoinable, indexedDocument, partyId);
		};

		struct PartyGameFinderFailure
		{
			std::string reason;

			MSGPACK_DEFINE(reason);
		};

		/// <summary>
		/// This event is triggered when the state of one or more party members changes.
		/// </summary>
		struct MembersUpdate
		{
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
				virtual std::string getSenderId() = 0;

				/// <summary>
				/// Get the platform-specific user Id of the sender.
				/// </summary>
				/// <returns>The platform-specific user Id of the sender.</returns>
				virtual std::string getSenderPlatformId() = 0;

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

				void notifyInvitationReceived(std::shared_ptr<IPlatformInvitation> invitation)
				{
					if (_invitationReceivedEvent.hasSubscribers())
					{
						_invitationReceivedEvent(invitation);
					}
					else
					{
						_pendingInvitation = invitation;
					}
				}

				Subscription subscribeOnInvitationReceived(std::function<void(std::shared_ptr<IPlatformInvitation>)> callback)
				{
					auto subscription = _invitationReceivedEvent.subscribe(callback);
					if (_pendingInvitation)
					{
						_invitationReceivedEvent(_pendingInvitation);
						_pendingInvitation.reset();
					}
					return subscription;
				}

			private:

				Event<std::shared_ptr<IPlatformInvitation>> _invitationReceivedEvent;
				std::shared_ptr<IPlatformInvitation> _pendingInvitation;
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
				{
				}

				/// <summary>
				/// The name of the platform. There cannot be more than one IPlatformSupportProvider implementation per platform.
				/// </summary>
				/// <returns>Name of the platform</returns>
				virtual std::string getPlatformName() = 0;

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
				/// Register additional routes on the party scene.
				/// </summary>
				/// <param name="partyScene">Scene of the party</param>
				virtual void onPartySceneInitialization(std::shared_ptr<Scene> /*partyScene*/) {}

				/// <summary>
				/// Kick a player from the platform-specific session that backs the party.
				/// </summary>
				/// <param name="playerId">Stormancer Id of the player to kick.</param>
				/// <returns>A task that should complete when the player has been kicked from the platform-specific session.</returns>
				virtual pplx::task<void> kickPlayer(const std::string& /*playerId*/)
				{
					return pplx::task_from_result();
				}

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

				// Platform-specific invitations

				/// <summary>
				/// Listen to requests to join a party made from platform-specific UI outside of the game.
				/// </summary>
				/// <remarks>For example, acepting an invitation in the platform UI, or joining a party from a friend's profile page.</remarks>
				/// <param name="callback">Callable object to be run when a request to join a party is made.</param>
				/// <returns>A subscription object to track the lifetime of the subscription.</returns>
				virtual Subscription subscribeOnJoinPartyRequestedByPlatform(std::function<void(const PlatformInvitationRequestContext&)> /*callback*/)
				{
					return Subscription{};
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
			std::vector<byte> memberData;
			std::string partySceneId;
			PartyId partyId;
			void* customContext;
			std::shared_ptr<PartyApi> partyApi;
			std::unordered_map<std::string, std::string> metadata;
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
			std::string partySceneId;
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
			{
			}

			/// <summary>
			/// This event is fired before a connection token is requested to join a party. tasks.
			/// </summary>
			/// <remarks>
			/// This gives you a chance to add additional operations as part of the JoinParty process.
			/// For instance, you could join a platform-specific online session, as an alternative to implementing this functionality in the server application.
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
			/// This event is fired when a party member has been kicked by the local member.
			/// </summary>
			/// <remarks>
			/// This will only be called if the local player has the permission to kick. Currently, only the leader can kick other players.
			/// Using this handler, you can perform additional operations, such as kicking the player from a platform-specific session.
			/// </remarks>
			/// <param name="party"></param>
			/// <param name="playerId">Stormancer Id of the player who is being kicked.</param>
			virtual void onPlayerKickedByLocalMember(std::shared_ptr<PartyApi>, std::string /*playerId*/) {}

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
			virtual void onJoinedParty(std::shared_ptr<JoinedPartyContext> /*ctx*/) {}

			/// <summary>
			/// This event is fired when the local player leaves the party.
			/// </summary>
			/// <param name="party"></param>
			/// <param name="reason">The cause of the player leaving.</param>
			virtual void onLeftParty(std::shared_ptr<LeftPartyContext> /*ctx*/) {}
		};

		namespace details
		{
			struct PartySettingsInternal
			{
				std::string gameFinderName;
				std::string customData;
				int settingsVersionNumber = 0;
				bool onlyLeaderCanInvite = true;
				bool isJoinable = true;
				std::unordered_map<std::string, std::string> publicServerData;
				std::string indexedDocument;
				std::string partyId;

				operator PartySettings() const
				{
					PartySettings settings;
					settings.gameFinderName = gameFinderName;
					settings.customData = customData;
					settings.onlyLeaderCanInvite = onlyLeaderCanInvite;
					settings.isJoinable = isJoinable;
					settings.publicServerData = publicServerData;
					settings.indexedDocument = indexedDocument;
					settings.partyId = partyId;
					return settings;
				}

				static PartySettingsInternal fromPartySettings(const PartySettings& settings)
				{
					PartySettingsInternal settingsInternal;
					settingsInternal.gameFinderName = settings.gameFinderName;
					settingsInternal.customData = settings.customData;
					settingsInternal.onlyLeaderCanInvite = settings.onlyLeaderCanInvite;
					settingsInternal.isJoinable = settings.isJoinable;
					settingsInternal.publicServerData = settings.publicServerData;
					settingsInternal.indexedDocument = settings.indexedDocument;
					settingsInternal.partyId = settings.partyId;
					return settingsInternal;
				}

				MSGPACK_DEFINE(gameFinderName, customData, settingsVersionNumber, onlyLeaderCanInvite, isJoinable, publicServerData, indexedDocument, partyId);
			};

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

			struct PartyState
			{
				PartySettingsInternal		settings;
				std::string					leaderId;
				std::vector<PartyUserDto>	members;
				int							version = 0;

				MSGPACK_DEFINE(settings, leaderId, members, version);
			};

			struct MemberStatusUpdateRequest
			{
				PartyUserStatus	desiredStatus;
				int				localSettingsVersion;

				MSGPACK_DEFINE(desiredStatus, localSettingsVersion);
			};

			struct MemberStatusUpdate
			{
				std::string		userId;
				PartyUserStatus	status;

				MSGPACK_DEFINE(userId, status);
			};

			struct BatchStatusUpdate
			{
				std::vector<MemberStatusUpdate> memberStatus;

				MSGPACK_DEFINE(memberStatus);
			};

			struct PartyUserData
			{
				std::string userId;
				std::vector<byte> userData;
				unsigned int localPlayerCount;

				MSGPACK_DEFINE(userId, userData, localPlayerCount);
			};

			struct MemberDisconnection
			{
				std::string					userId;
				MemberDisconnectionReason	reason;

				MSGPACK_DEFINE(userId, reason);
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

			class PartyService : public std::enable_shared_from_this<PartyService>
			{
			public:

				// stormancer.party => <protocol version>
				// stormancer.party.revision => <server revision>
				// Revision is server-side only. It is independent from protocol version. Revision changes when a modification is made to server code (e.g bugfix).
				// Protocol version changes when a change to the communication protocol is made.
				// Protocol versions between client and server are not obligated to match.
				static constexpr const char* METADATA_KEY = "stormancer.party";
				static constexpr const char* REVISION_METADATA_KEY = "stormancer.party.revision";
				static constexpr const char* PROTOCOL_VERSION = "2022-06-09.1";

				static constexpr const char* IS_JOINABLE_VERSION = "2019-12-13.1";
				static constexpr const char* NEW_INVITATIONS_VERSION = "2019-11-22.1";

				static int getProtocolVersionInt()
				{
					static int protocolVersionInt = parseVersion(PROTOCOL_VERSION);
					return protocolVersionInt;
				}

				PartyService(std::weak_ptr<Scene> scene)
					: _scene(scene)
					, _logger(scene.lock()->dependencyResolver().resolve<ILogger>())
					, _rpcService(_scene.lock()->dependencyResolver().resolve<RpcService>())
					, _gameFinder(scene.lock()->dependencyResolver().resolve<Stormancer::GameFinder::GameFinderApi>())
					, _dispatcher(scene.lock()->dependencyResolver().resolve<IActionDispatcher>())
					, _users(scene.lock()->dependencyResolver().resolve<Users::UsersApi>())
					, _myUserId(_users->userId())
				{
					auto serverProtocolVersion = _scene.lock()->getHostMetadata(METADATA_KEY);
					auto serverRevision = _scene.lock()->getHostMetadata(REVISION_METADATA_KEY);
					_logger->log(LogLevel::Info, "PartyService", "Protocol version: client=" + std::string(PROTOCOL_VERSION) + ", server=" + serverProtocolVersion);
					_logger->log(LogLevel::Info, "PartyService", "Server revision=" + serverRevision);
					if (!tryParseVersion(serverProtocolVersion.c_str(), _serverProtocolVersion))
					{
						// Older versions are not in the correct format
						_serverProtocolVersion = 201910231;
					}
				}

				~PartyService()
				{
					std::lock_guard<std::recursive_mutex> lg(_stateMutex);

					_gameFinderConnectionTask.then([](pplx::task<void> task)
						{
							try { task.get(); }
							catch (...) {}
						});
				}

				// This is for compatibility with server plugins older than NEW_INVITATIONS_VERSION
				struct PartySettingsCompatibility
				{
					std::string gameFinderName;
					std::string customData;

					MSGPACK_DEFINE(gameFinderName, customData);
				};

				///
				/// Sent to server the new party status
				///
				pplx::task<void> updatePartySettings(const PartySettings& newPartySettings)
				{
					std::lock_guard<std::recursive_mutex> lg(_stateMutex);

					static const int isJoinableProtocolVersion = parseVersion(IS_JOINABLE_VERSION);
					if (newPartySettings.isJoinable == false && _serverProtocolVersion < isJoinableProtocolVersion)
					{
						_logger->log(LogLevel::Warn, "PartyService::updatePartySettings", "The server does not support joinability restriction ; 'isJoinable' will have no effect. Please update your server-side Party plugin.");
					}

					// Apply settings locally immediately. If the update RPC fails, we will re-sync the party state.
					PartySettingsInternal update = PartySettingsInternal::fromPartySettings(newPartySettings);
					update.settingsVersionNumber = _state.settings.settingsVersionNumber + 1;
					applySettingsUpdate(update);

					std::weak_ptr<PartyService> wThat = this->shared_from_this();
					static const int newInvitationsProtocolVersion = parseVersion(NEW_INVITATIONS_VERSION);
					if (newPartySettings.onlyLeaderCanInvite == true && _serverProtocolVersion < newInvitationsProtocolVersion)
					{
						_logger->log(LogLevel::Warn, "PartyService::updatePartySettings", "The server does not support invitation restriction ; 'onlyLeaderCanInvite' will have no effect, and every party member will be able to send invitations. Please update your server-side Party plugin.");
						// Also, the server DTO from these older versions is not compatible with the new client DTO. Need to send a compatible DTO.
						PartySettingsCompatibility compatible;
						compatible.gameFinderName = newPartySettings.gameFinderName;
						compatible.customData = newPartySettings.customData;
						return syncStateOnError(_rpcService->rpc<void>("party.updatepartysettings", compatible));
					}
					else
					{
						return syncStateOnError(_rpcService->rpc<void>("party.updatepartysettings", newPartySettings));
					}
				}

				pplx::task<std::string> getCurrentGameSessionConnectionToken(pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					return _rpcService->rpc<std::string>("JoinGameParty.RequestReservationInCurrentGamesession", ct);
				}


				/// 
				/// Set our party status (ready/not ready).
				/// Also make sure that we are connected to the party's GameFinder before telling the server that we're ready.
				/// 
				pplx::task<void> updatePlayerStatus(const PartyUserStatus newStatus)
				{
					std::lock_guard<std::recursive_mutex> lg(_stateMutex);

					bool statusHasChanged = std::any_of(_state.members.begin(), _state.members.end(),
						[newStatus, this](const auto& member) { return member.userId == _myUserId && member.partyUserStatus != newStatus; });

					if (!statusHasChanged)
					{
						return pplx::task_from_result(pplx::task_options(_dispatcher));
					}
					if (_state.settings.gameFinderName.empty())
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error(PartyError::Str::PartyNotReady), _dispatcher, void);
					}

					BatchStatusUpdate update;
					update.memberStatus.emplace_back(MemberStatusUpdate{ _myUserId, newStatus });
					applyMemberStatusUpdate(update);

					return syncStateOnError(updatePlayerStatusWithRetries(newStatus));
				}

				/// 
				/// Update party user data all data are replecated between all connected party scene
				/// 
				pplx::task<void> updatePlayerData(std::vector<byte> data, unsigned int localPlayerCount)
				{
					PartyUserData update;
					update.userData = data;
					update.localPlayerCount = localPlayerCount;
					update.userId = _myUserId;
					applyUserDataUpdate(update);

					return syncStateOnError(_rpcService->rpc<void>("Party.UpdatePartyUserData2", data, localPlayerCount));
				}

				///
				/// Promote player to leader of the party
				/// \param playerId party userid will be promote
				pplx::task<void> promoteLeader(const std::string playerId)
				{
					std::lock_guard<std::recursive_mutex> lg(_stateMutex);

					if (_state.leaderId == _myUserId)
					{
						applyLeaderChange(playerId);

						return syncStateOnError(_rpcService->rpc<void>("party.promoteleader", playerId));
					}

					STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error(PartyError::Str::Unauthorized), _dispatcher, void);
				}

				///
				/// Remove player from party this method can be call only by party leader.
				/// \param playerToKick is the user player id to be kicked
				pplx::task<void> kickPlayer(const std::string playerId)
				{
					std::lock_guard<std::recursive_mutex> lg(_stateMutex);

					if (_state.leaderId == _myUserId)
					{
						MemberDisconnection disconnection;
						disconnection.userId = playerId;
						disconnection.reason = MemberDisconnectionReason::Kicked;
						applyMemberDisconnection(disconnection);

						return syncStateOnError(_rpcService->rpc<void>("party.kickplayer", playerId));
					}

					STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error(PartyError::Str::Unauthorized), _dispatcher, void);
				}

				pplx::task<bool> sendInvitation(const std::string& recipientId, bool forceStormancerInvite)
				{
					if (!forceStormancerInvite)
					{
						return sendInvitationInternal(recipientId, false, pplx::cancellation_token::none());
					}

					std::lock_guard<std::recursive_mutex> lg(_invitationsMutex);

					auto& request = _pendingStormancerInvitations[recipientId];

					auto currentOperation = request.pendingOperation;
					request.pendingOperation = InvitationRequest::Operation::Send;
					if (currentOperation == InvitationRequest::Operation::None)
					{
						auto token = request.cts.get_token();
						std::weak_ptr<PartyService> wThat(this->shared_from_this());
						request.task = sendInvitationInternal(recipientId, true, token)
							.then([wThat, recipientId](pplx::task<bool> task)
								{
									if (auto that = wThat.lock())
									{
										return that->onInvitationComplete(task, recipientId);
									}
									return task;
								}, _dispatcher);
					}

					return request.task;
				}

				pplx::task<void> cancelInvitation(const std::string& recipientId)
				{
					std::lock_guard<std::recursive_mutex> lg(_invitationsMutex);

					auto invitationIt = _pendingStormancerInvitations.find(recipientId);
					if (invitationIt != _pendingStormancerInvitations.end())
					{
						auto& invitation = invitationIt->second;
						invitation.pendingOperation = InvitationRequest::Operation::Cancel;
						invitation.cts.cancel();

						std::weak_ptr<PartyService> wThat = this->shared_from_this();
						auto cancellationTask = invitation.task.then([wThat, recipientId](pplx::task<bool> task)
							{
								// Consume the boolean, let the caller handle errors
								task.wait();
							});
						return cancellationTask;
					}
					return pplx::task_from_result();
				}

				std::vector<std::string> getPendingStormancerInvitations() const
				{
					std::lock_guard<std::recursive_mutex> lg(_invitationsMutex);

					std::vector<std::string> invitations;
					invitations.reserve(_pendingStormancerInvitations.size());

					for (const auto& invite : _pendingStormancerInvitations)
					{
						invitations.push_back(invite.first);
					}

					return invitations;
				}

				pplx::task<std::string> createInvitationCode(pplx::cancellation_token ct)
				{
					return _rpcService->rpc<std::string>("Party.CreateInvitationCode", ct);
				}


				pplx::task<void> cancelInvitationCode(pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					return _rpcService->rpc<void>("Party.CancelInvitationCode", ct);
				}

				///
				/// Callback member
				///
				Event<MemberDisconnectionReason> LeftParty;
				Event<> JoinedParty;
				Event<const MembersUpdate> PartyMembersUpdated;
				Event<PartySettings> UpdatedPartySettings;
				Event<std::vector<std::string>> UpdatedInviteList;
				Event<PartyGameFinderFailure> OnGameFinderFailed;

				std::vector<PartyUserDto> members() const
				{
					std::lock_guard<std::recursive_mutex> lg(_stateMutex);
					return _state.members;
				}

				PartySettings settings() const
				{
					std::lock_guard<std::recursive_mutex> lg(_stateMutex);
					return _state.settings;
				}

				std::string leaderId() const
				{
					std::lock_guard<std::recursive_mutex> lg(_stateMutex);
					return _state.leaderId;
				}

				void initialize()
				{
					std::weak_ptr<PartyService> wThat = this->shared_from_this();
					auto scene = _scene.lock();
					auto rpcService = scene->dependencyResolver().resolve<RpcService>();

					rpcService->addProcedure("party.getPartyStateResponse", [wThat](RpcRequestContext_ptr ctx)
						{
							if (auto that = wThat.lock())
							{
								return that->handlePartyStateResponse(ctx);
							}
							return pplx::task_from_result();
						});

					rpcService->addProcedure("party.settingsUpdated", [wThat](RpcRequestContext_ptr ctx)
						{
							if (auto that = wThat.lock())
							{
								return that->handleSettingsUpdateMessage(ctx);
							}
							return pplx::task_from_result();
						});

					rpcService->addProcedure("party.memberDataUpdated", [wThat](RpcRequestContext_ptr ctx)
						{
							if (auto that = wThat.lock())
							{
								return that->handleUserDataUpdateMessage(ctx);
							}
							return pplx::task_from_result();
						});

					rpcService->addProcedure("party.memberStatusUpdated", [wThat](RpcRequestContext_ptr ctx)
						{
							if (auto that = wThat.lock())
							{
								return that->handleMemberStatusUpdateMessage(ctx);
							}
							return pplx::task_from_result();
						});

					rpcService->addProcedure("party.memberConnected", [wThat](RpcRequestContext_ptr ctx)
						{
							if (auto that = wThat.lock())
							{
								return that->handleMemberConnected(ctx);
							}
							return pplx::task_from_result();
						});

					rpcService->addProcedure("party.memberDisconnected", [wThat](RpcRequestContext_ptr ctx)
						{
							if (auto that = wThat.lock())
							{
								return that->handleMemberDisconnectedMessage(ctx);
							}
							return pplx::task_from_result();
						});

					rpcService->addProcedure("party.leaderChanged", [wThat](RpcRequestContext_ptr ctx)
						{
							if (auto that = wThat.lock())
							{
								return that->handleLeaderChangedMessage(ctx);
							}
							return pplx::task_from_result();
						});

					scene->addRoute<PartyGameFinderFailure>("party.gameFinderFailed", [wThat](PartyGameFinderFailure dto)
						{
							if (auto that = wThat.lock())
							{
								return that->handleGameFinderFailureMessage(dto);
							}
						});

					scene->getConnectionStateChangedObservable().subscribe([wThat](ConnectionState state) {
						if (auto that = wThat.lock())
						{
							try
							{
								if (state == ConnectionState::Connected)
								{
									that->JoinedParty();
								}
								else if (state == ConnectionState::Disconnected)
								{
									that->_gameFinder->disconnectFromGameFinder(that->_state.settings.gameFinderName)
										.then([](pplx::task<void> t)
											{
												try {
													t.get();
												}
												catch (...) {}
											});

									MemberDisconnectionReason reason = MemberDisconnectionReason::Left;
									if (state.reason == "party.kicked")
									{
										reason = MemberDisconnectionReason::Kicked;
									}
									that->LeftParty(reason);
								}
							}
							catch (const std::exception& ex)
							{
								that->_logger->log(LogLevel::Error, "PartyService::ConnectionStateChanged", "An exception was thrown by a connection event handler", ex);
							}
						}
						});
				}

				pplx::task<void> waitForPartyReady(pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					return waitForTaskCompletionEvent(_partyStateReceived, ct);
				}

			private:

				pplx::task<void> syncStateOnError(pplx::task<void> task)
				{
					std::weak_ptr<PartyService> wThat = this->shared_from_this();
					return task.then([wThat](pplx::task<void> task)
						{
							try
							{
								task.get();
							}
							catch (...)
							{
								if (auto that = wThat.lock())
								{
									that->syncPartyState();
								}
								throw;
							}
						}, _dispatcher);
				}

				void updateGameFinder()
				{
					std::lock_guard<std::recursive_mutex> lg(_stateMutex);

					if (_currentGameFinder == _state.settings.gameFinderName)
					{
						return;
					}

					// This CTS prevents multiple game finder connection requests from queuing up.
					_gameFinderConnectionCts.cancel();
					_gameFinderConnectionCts = pplx::cancellation_token_source();

					// No need to wait for the old GF disconnection before connecting to the new GF
					_gameFinder->disconnectFromGameFinder(_currentGameFinder).then([](pplx::task<void> task)
						{
							try { task.wait(); }
							catch (...) {}
						});

					_currentGameFinder = _state.settings.gameFinderName;
					if (_currentGameFinder.empty())
					{
						return;
					}

					_logger->log(LogLevel::Trace, "PartyService", "Connecting to the party's GameFinder", _state.settings.gameFinderName);

					std::string newGameFinderName = _currentGameFinder;
					auto token = _gameFinderConnectionCts.get_token();
					std::weak_ptr<PartyService> wThat = this->shared_from_this();
					_gameFinderConnectionTask = _gameFinderConnectionTask.then([wThat, newGameFinderName, token](pplx::task<void> task)
						{
							// I want to recover from cancellation, but not from error, since error means we're leaving the party
							task.wait();

							auto that = wThat.lock();
							if (!that || token.is_canceled())
							{
								pplx::cancel_current_task();
							}

							return that->_gameFinder->connectToGameFinder(newGameFinderName);
						}, token)
						.then([wThat, newGameFinderName](pplx::task<void> task)
							{
								auto that = wThat.lock();
								try
								{
									auto status = task.wait();
									if (that && status == pplx::completed)
									{
										that->_logger->log(LogLevel::Trace, "PartyService", "Connected to the GameFinder", newGameFinderName);
									}
								}
								catch (const std::exception& ex)
								{
									if (that)
									{
										that->_logger->log(LogLevel::Error, "PartyService", "Error connecting to the GameFinder '" + newGameFinderName + "'", ex);
										if (auto scene = that->_scene.lock())
										{
											std::lock_guard<std::recursive_mutex> lg(that->_stateMutex);
											scene->disconnect().then([](pplx::task<void> t) { try { t.get(); } catch (...) {}});
											that->_scene.reset();
										}
									}
									throw;
								}
							}, token);
				}

				bool checkVersionNumber(RpcRequestContext_ptr ctx)
				{
					auto versionNumber = ctx->readObject<int>();
					if (_state.version > 0 && versionNumber == _state.version + 1)
					{
						_state.version = versionNumber;
						return true;
					}
					else
					{
						_logger->log(LogLevel::Trace, "PartyService::checkVersionNumber", "Version number mismatch ; current=" + std::to_string(_state.version) + ", received=" + std::to_string(versionNumber));
						syncPartyState();
						return false;
					}
				}

				// This returns void because we must not block on it (or else we would cause a timeout in party update RPC)
				void syncPartyState()
				{
					syncPartyStateTask().then([](pplx::task<void> task)
						{
							try { task.get(); }
							catch (...) {}
						});
				}

				pplx::task<void> getPartyStateImpl()
				{
					static const int originalGetPartyStateVersion = parseVersion("2019-08-30.1");
					if (_serverProtocolVersion == originalGetPartyStateVersion)
					{
						return _rpcService->rpc("party.getpartystate");
					}
					else
					{
						std::weak_ptr<PartyService> wThat = this->shared_from_this();
						return _rpcService->rpc<PartyState>("party.getpartystate2").then([wThat](PartyState state)
							{
								if (auto that = wThat.lock())
								{
									that->applyPartyStateResponse(state);
								}
							});
					}
				}

				pplx::task<void> syncPartyStateTaskWithRetries()
				{
					std::weak_ptr<PartyService> wThat = this->shared_from_this();
					return getPartyStateImpl().then([wThat](pplx::task<void> task)
						{
							try
							{
								task.get();
							}
							catch (const std::exception& ex)
							{
								if (auto that = wThat.lock())
								{
									that->_logger->log(LogLevel::Error, "PartyService::syncPartyStateTaskWithRetries", "An error occurred during syncPartyState, retrying", ex);
									return taskDelay(std::chrono::milliseconds(200))
										.then([wThat]
											{
												if (auto that = wThat.lock())
												{
													return that->syncPartyStateTaskWithRetries();
												}
												return pplx::task_from_result();
											});
								}
							}
							return pplx::task_from_result();
						});
				}

				pplx::task<void> syncPartyStateTask()
				{
					std::lock_guard<std::recursive_mutex> lg(_stateMutex);

					if (_stateSyncRequest.is_done())
					{
						_stateSyncRequest = syncPartyStateTaskWithRetries();
					}

					return _stateSyncRequest;
				}

				pplx::task<void> updatePlayerStatusWithRetries(const PartyUserStatus newStatus)
				{
					std::lock_guard<std::recursive_mutex> lg(_stateMutex);

					MemberStatusUpdateRequest request;
					request.desiredStatus = newStatus;
					request.localSettingsVersion = _state.settings.settingsVersionNumber;

					// If the player wants to be Ready, we must make sure they are connected to the game finder beforehand
					pplx::task<void> preliminaryTask = pplx::task_from_result();
					if (newStatus == PartyUserStatus::Ready)
					{
						preliminaryTask = _gameFinderConnectionTask;
					}

					std::weak_ptr<PartyService> wThat = this->shared_from_this();
					return preliminaryTask.then([wThat, request]
						{
							if (auto that = wThat.lock())
							{
								return that->_rpcService->rpc<void>("party.updategamefinderplayerstatus", request);
							}
							return pplx::task_from_result();
						}).then([wThat, newStatus](pplx::task<void> task)
							{
								try
								{
									task.get();
								}
								catch (const std::exception& ex)
								{
									if (auto that = wThat.lock())
									{
										if (strcmp(ex.what(), "party.settingsOutdated") == 0)
										{
											that->_logger->log(LogLevel::Debug, "PartyService::updatePlayerStatusWithRetries", "Local settings outdated ; retrying");
											return that->syncPartyStateTask()
												.then([wThat, newStatus]
													{
														if (auto that = wThat.lock())
														{
															return that->updatePlayerStatusWithRetries(newStatus);
														}
														return pplx::task_from_result();
													});
										}
										else
										{
											throw;
										}
									}
								}
								return pplx::task_from_result();
							});
				}

				pplx::task<void> handlePartyStateResponse(RpcRequestContext_ptr ctx)
				{
					std::lock_guard<std::recursive_mutex> lg(_stateMutex);

					applyPartyStateResponse(ctx->readObject<PartyState>());

					return pplx::task_from_result();
				}

				static const PartyUserDto* findMember(const std::vector<PartyUserDto>& users, const std::string& userId)
				{
					auto it = std::find_if(users.begin(), users.end(), [&userId](const PartyUserDto& dto) { return dto.userId == userId; });
					if (it != users.end())
					{
						return &(*it);
					}
					else
					{
						return nullptr;
					}
				}

				static std::unordered_map<std::string, PartyUserDto> makeMemberMap(const std::vector<PartyUserDto>& users)
				{
					std::unordered_map<std::string, PartyUserDto> map;
					map.reserve(users.size());
					std::transform(users.begin(), users.end(), std::inserter(map, map.end()), [](PartyUserDto user) { return std::make_pair(user.userId, std::move(user)); });
					return map;
				}

				void applyPartyStateResponse(PartyState state)
				{
					std::lock_guard<std::recursive_mutex> lg(_stateMutex);

					_logger->log(LogLevel::Trace, "PartyService::applyPartyStateResponse", "Received party state, version = " + std::to_string(state.version));

					// Compare the up-to-date member list with the one we currently have, and generate MemberUpdate events where appropriate
					MembersUpdate updates;
					auto previousMembers = makeMemberMap(_state.members);

					for (auto& newMember : state.members)
					{
						if (state.leaderId == newMember.userId)
						{
							newMember.isLeader = true;
						}
						if (previousMembers.count(newMember.userId) == 0)
						{
							updates.updatedMembers.emplace_back(newMember, MembersUpdate::Joined);
							continue;
						}
						const auto& oldMember = previousMembers[newMember.userId];
						MembersUpdate::MemberUpdate update;
						if (oldMember.isLeader != newMember.isLeader)
						{
							update.changes.set(newMember.isLeader ? MembersUpdate::PromotedToLeader : MembersUpdate::DemotedFromLeader);
						}
						if (oldMember.partyUserStatus != newMember.partyUserStatus)
						{
							update.changes.set(MembersUpdate::StatusUpdated);
						}
						if (oldMember.userData != newMember.userData)
						{
							update.changes.set(MembersUpdate::DataUpdated);
						}
						previousMembers.erase(newMember.userId);
						if (update.changes.any())
						{
							update.member = newMember;
							updates.updatedMembers.push_back(std::move(update));
						}
					}
					for (const auto& memberWhoLeft : previousMembers)
					{
						updates.updatedMembers.emplace_back(memberWhoLeft.second, MembersUpdate::Left);
					}

					_state = std::move(state);
					updateGameFinder();

					_partyStateReceived.set();
					this->UpdatedPartySettings(_state.settings);
					PartyMembersUpdated(updates);
				}

				void applySettingsUpdate(const PartySettingsInternal& update)
				{
					std::lock_guard<std::recursive_mutex> lg(_stateMutex);

					if (_state.settings.settingsVersionNumber != update.settingsVersionNumber)
					{
						_state.settings = update;
						updateGameFinder();
						this->UpdatedPartySettings(_state.settings);
					}
				}

				pplx::task<void> handleSettingsUpdateMessage(RpcRequestContext_ptr ctx)
				{
					std::lock_guard<std::recursive_mutex> lg(_stateMutex);

					if (checkVersionNumber(ctx))
					{
						_logger->log(LogLevel::Trace, "PartyService::handleSettingsUpdate", "Received settings update, version = " + std::to_string(_state.version));
						applySettingsUpdate(ctx->readObject<PartySettingsInternal>());
					}

					return pplx::task_from_result();
				}

				void applyUserDataUpdate(const PartyUserData& update)
				{
					std::lock_guard<std::recursive_mutex> lg(_stateMutex);

					auto member = std::find_if(_state.members.begin(), _state.members.end(), [&update](const PartyUserDto& user) { return update.userId == user.userId; });

					if (member != _state.members.end())
					{

						member->userData = update.userData;
						member->localPlayerCount = update.localPlayerCount;
						MembersUpdate updates;
						updates.updatedMembers.emplace_back(*member, MembersUpdate::DataUpdated);
						PartyMembersUpdated(updates);

					}
				}


				pplx::task<void> handleUserDataUpdateMessage(RpcRequestContext_ptr ctx)
				{
					std::lock_guard<std::recursive_mutex> lg(_stateMutex);

					if (checkVersionNumber(ctx))
					{
						_logger->log(LogLevel::Trace, "PartyService::handleUserDataUpdate", "Received user data update, version = " + std::to_string(_state.version));
						applyUserDataUpdate(ctx->readObject<PartyUserData>());
					}

					return pplx::task_from_result();
				}

				void applyMemberStatusUpdate(const BatchStatusUpdate& updates)
				{
					std::lock_guard<std::recursive_mutex> lg(_stateMutex);

					MembersUpdate membersUpdate;
					bool updated = false;
					for (const auto& update : updates.memberStatus)
					{
						auto member = std::find_if(_state.members.begin(), _state.members.end(), [&update](const PartyUserDto& user) { return update.userId == user.userId; });

						if (member != _state.members.end())
						{
							updated = updated || (member->partyUserStatus != update.status);
							member->partyUserStatus = update.status;
							membersUpdate.updatedMembers.emplace_back(*member, MembersUpdate::StatusUpdated);
						}
					}

					if (updated)
					{
						PartyMembersUpdated(membersUpdate);
					}
				}

				pplx::task<void> handleMemberStatusUpdateMessage(RpcRequestContext_ptr ctx)
				{
					std::lock_guard<std::recursive_mutex> lg(_stateMutex);

					if (checkVersionNumber(ctx))
					{
						_logger->log(LogLevel::Trace, "PartyService::handleMemberStatusUpdate", "Received member status update, version = " + std::to_string(_state.version));

						applyMemberStatusUpdate(ctx->readObject<BatchStatusUpdate>());
					}

					return pplx::task_from_result();
				}

				pplx::task<void> handleMemberConnected(RpcRequestContext_ptr ctx)
				{
					std::lock_guard<std::recursive_mutex> lg(_stateMutex);

					if (checkVersionNumber(ctx))
					{
						auto member = ctx->readObject<PartyUserDto>();
						_logger->log(LogLevel::Trace, "PartyService::handleMemberConnected", "New party member: Id=" + member.userId + ", version = " + std::to_string(_state.version));

						_state.members.push_back(member);
						MembersUpdate update;
						update.updatedMembers.emplace_back(member, MembersUpdate::Joined);
						PartyMembersUpdated(update);
					}

					return pplx::task_from_result();
				}

				void applyMemberDisconnection(const MemberDisconnection& message)
				{
					std::lock_guard<std::recursive_mutex> lg(_stateMutex);

					auto member = std::find_if(_state.members.begin(), _state.members.end(), [&message](const PartyUserDto& user) { return message.userId == user.userId; });
					if (member != _state.members.end())
					{
						MembersUpdate update;
						MembersUpdate::MemberUpdate memberUpdate(*member, MembersUpdate::Left);
						if (message.reason == MemberDisconnectionReason::Kicked)
						{
							memberUpdate.changes.set(MembersUpdate::Kicked);
						}
						update.updatedMembers.push_back(memberUpdate);
						_state.members.erase(member);
						PartyMembersUpdated(update);
					}
				}

				pplx::task<void> handleMemberDisconnectedMessage(RpcRequestContext_ptr ctx)
				{
					std::lock_guard<std::recursive_mutex> lg(_stateMutex);

					if (checkVersionNumber(ctx))
					{
						auto message = ctx->readObject<MemberDisconnection>();
						_logger->log(LogLevel::Trace, "PartyService::handleMemberDisconnected", "Member disconnected: Id=" + message.userId + ", Reason=" + std::to_string(static_cast<int>(message.reason)) + ", version = " + std::to_string(_state.version));

						applyMemberDisconnection(message);
					}

					return pplx::task_from_result();
				}

				void applyLeaderChange(const std::string& newLeaderId)
				{
					std::lock_guard<std::recursive_mutex> lg(_stateMutex);

					if (_state.leaderId != newLeaderId)
					{
						_state.leaderId = newLeaderId;
						MembersUpdate update;
						updateLeader(update);
						PartyMembersUpdated(update);
					}
				}

				pplx::task<void> handleLeaderChangedMessage(RpcRequestContext_ptr ctx)
				{
					std::lock_guard<std::recursive_mutex> lg(_stateMutex);

					if (checkVersionNumber(ctx))
					{
						auto leaderId = ctx->readObject<std::string>();
						_logger->log(LogLevel::Trace, "PartyService::handleLeaderChanged", "New leader: Id=" + leaderId + ", version = " + std::to_string(_state.version));
						applyLeaderChange(leaderId);
					}

					return pplx::task_from_result();
				}

				void updateLeader(MembersUpdate& update)
				{
					const std::string& newLeaderId = _state.leaderId;
					auto currentLeader = std::find_if(_state.members.begin(), _state.members.end(), [](const PartyUserDto& user) { return user.isLeader; });
					if (currentLeader != _state.members.end())
					{
						currentLeader->isLeader = false;
						update.updatedMembers.emplace_back(*currentLeader, MembersUpdate::DemotedFromLeader);
					}

					auto newLeader = std::find_if(_state.members.begin(), _state.members.end(), [&newLeaderId](const PartyUserDto& user) { return newLeaderId == user.userId; });
					if (newLeader != _state.members.end())
					{
						newLeader->isLeader = true;
						update.updatedMembers.emplace_back(*newLeader, MembersUpdate::PromotedToLeader);
					}
				}

				void handleGameFinderFailureMessage(const PartyGameFinderFailure& dto)
				{
					OnGameFinderFailed(dto);
				}

				pplx::task<bool> sendInvitationInternal(const std::string& recipientId, bool forceStormancerInvite, pplx::cancellation_token ct)
				{
					static const int sendInvitationVersion = parseVersion("2019-11-22.1");
					if (_serverProtocolVersion >= sendInvitationVersion)
					{
						return _rpcService->rpc<bool>("party.sendinvitation", ct, recipientId, forceStormancerInvite);
					}
					else
					{
						return _users->sendRequestToUser<void>(recipientId, "party.invite", ct, _scene.lock()->id())
							.then([] { return true; });
					}
				}

				pplx::task<bool> onInvitationComplete(pplx::task<bool> task, const std::string& recipientId)
				{
					pplx::task_status status;
					try
					{
						status = task.wait();
					}
					catch (...)
					{
						// Errors are handled by the caller
						status = pplx::not_complete;
					}

					{
						std::lock_guard<std::recursive_mutex> lg(_invitationsMutex);

						auto& invite = _pendingStormancerInvitations[recipientId];
						if (status != pplx::canceled || invite.pendingOperation == InvitationRequest::Operation::Cancel)
						{
							_pendingStormancerInvitations.erase(recipientId);
							UpdatedInviteList(getPendingStormancerInvitations());
							return task;
						}
						else
						{
							// Another sendInvitation() to the same recipient has been issued after a cancelInvitation()
							invite.cts = pplx::cancellation_token_source();
							std::weak_ptr<PartyService> wThat = this->shared_from_this();
							invite.task = sendInvitationInternal(recipientId, true, invite.cts.get_token())
								.then([wThat, recipientId](pplx::task<bool> task)
									{
										if (auto that = wThat.lock())
										{
											return that->onInvitationComplete(task, recipientId);
										}
										return task;
									}, _dispatcher);
							return invite.task;
						}
					}
				}

				PartyState _state;
				std::string _currentGameFinder;
				std::weak_ptr<Scene> _scene;
				std::shared_ptr<ILogger> _logger;
				std::shared_ptr<RpcService> _rpcService;
				std::shared_ptr<Stormancer::GameFinder::GameFinderApi> _gameFinder;
				std::shared_ptr<IActionDispatcher> _dispatcher;
				std::shared_ptr<Users::UsersApi> _users;

				std::string _myUserId;
				// Synchronize async state update, as well as getters.
				// This is "coarse grain" synchronization, but the simplicity gains vs. multiple mutexes win against the possible performance loss imo.
				mutable std::recursive_mutex _stateMutex;
				// Prevent having multiple game finder connection tasks at the same time (could happen if multiple settings updates are received in a short amount of time)
				pplx::task<void> _gameFinderConnectionTask = pplx::task_from_result();
				pplx::cancellation_token_source _gameFinderConnectionCts;
				// Used to signal to client code when the party is ready
				pplx::task_completion_event<void> _partyStateReceived;
				pplx::task<void> _stateSyncRequest = pplx::task_from_result();
				int _serverProtocolVersion = 0;

				std::unordered_map<std::string, InvitationRequest> _pendingStormancerInvitations;
				mutable std::recursive_mutex _invitationsMutex;
			};

			class PartyContainer
			{
				friend class Party_Impl;

			public:

				PartyContainer(
					std::shared_ptr<Scene> scene,
					Event<MemberDisconnectionReason>::Subscription LeftPartySubscription,
					Event<std::vector<PartyUserDto>>::Subscription UpdatedPartyMembersSubscription,
					Event<PartySettings>::Subscription UpdatedPartySettingsSubscription,
					Subscription UpdatedInvitationListSubscription,
					Subscription GameFinderFailedSubscription
				)
					: _partyScene(scene)
					, _partyService(scene->dependencyResolver().resolve<PartyService>())
					, LeftPartySubscription(LeftPartySubscription)
					, UpdatedPartyMembersSubscription(UpdatedPartyMembersSubscription)
					, UpdatedPartySettingsSubscription(UpdatedPartySettingsSubscription)
					, UpdatedInvitationListSubscription(UpdatedInvitationListSubscription)
					, GameFinderFailedSubscription(GameFinderFailedSubscription)
				{
				}

				PartySettings settings() const
				{
					return _partyService->settings();
				}

				std::vector<PartyUserDto> members() const
				{
					return _partyService->members();
				}

				bool isLeader() const
				{
					return (_partyService->leaderId() == _partyScene->dependencyResolver().resolve<Stormancer::Users::UsersApi>()->userId());
				}

				std::string leaderId() const
				{
					return _partyService->leaderId();
				}

				std::shared_ptr<Scene> getScene() const
				{
					return _partyScene;
				}

				std::string getSceneId() const
				{
					return _partyScene->id();
				}

				std::shared_ptr<PartyService> partyService() const
				{
					return _partyService;
				}

				PartyId getPartyId() const
				{
					PartyId partyId;
					if (!settings().partyId.empty())
					{
						partyId.id = settings().partyId;
						partyId.type = PartyId::TYPE_PARTY_ID;
					}
					else
					{
						partyId.id = getSceneId();
						partyId.type = PartyId::TYPE_SCENE_ID;
					}
					return partyId;
				}

			private:

				std::shared_ptr<Scene> _partyScene;
				std::shared_ptr<PartyService> _partyService;

				Event<MemberDisconnectionReason>::Subscription LeftPartySubscription;
				Event<std::vector<PartyUserDto>>::Subscription UpdatedPartyMembersSubscription;
				Event<PartySettings>::Subscription UpdatedPartySettingsSubscription;
				Subscription UpdatedInvitationListSubscription;
				Subscription GameFinderFailedSubscription;
			};

			class PartyManagementService : public std::enable_shared_from_this<PartyManagementService>
			{
			public:

				static constexpr const char* METADATA_KEY = "stormancer.partymanagement";
				static constexpr const char* PROTOCOL_VERSION = "2020-05-20.1";

				static constexpr const char* IS_JOINABLE_VERSION = "2019-12-13.1";

				PartyManagementService(std::shared_ptr<Scene> scene)
					: _scene(scene)
					, _logger(scene->dependencyResolver().resolve<ILogger>())
				{
					auto serverVersion = scene->getHostMetadata(METADATA_KEY);
					_logger->log(LogLevel::Info, "PartyManagementService", "Protocol version: client=" + std::string(PROTOCOL_VERSION) + ", server=" + serverVersion);
					if (!tryParseVersion(serverVersion.c_str(), _serverProtocolVersion))
					{
						_logger->log(LogLevel::Warn, "PartyManagementService", "Could not parse server protocol version");
						_serverProtocolVersion = 0;
					}
				}

				pplx::task<std::string> createParty(const PartyCreationOptions& partyRequestDto, pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					static const int isJoinableProtocolVersion = parseVersion(IS_JOINABLE_VERSION);
					if (partyRequestDto.isJoinable == false && _serverProtocolVersion < isJoinableProtocolVersion)
					{
						_logger->log(LogLevel::Warn, "PartyManagementService::createParty", "The server does not support joinability restriction ; 'isJoinable' will have no effect. Please update your server-side Party plugin.");
					}

					auto rpc = _scene.lock()->dependencyResolver().resolve<RpcService>();
					return rpc->rpc<std::string, PartyCreationOptions>("partymanagement.createsession", ct, partyRequestDto);
				}

				pplx::task<std::string> getConnectionTokenFromInvitationCode(const std::string& invitationCode, const std::vector<byte>& userData, pplx::cancellation_token ct)
				{
					auto rpc = _scene.lock()->dependencyResolver().resolve<RpcService>();
					return rpc->rpc<std::string>("PartyManagement.CreateConnectionTokenFromInvitationCode", ct, invitationCode, userData);
				}

				pplx::task<std::string> getConnectionTokenFromPartyId(const std::string& partyId, const std::vector<byte>& userData, pplx::cancellation_token ct)
				{
					auto rpc = _scene.lock()->dependencyResolver().resolve<RpcService>();
					return rpc->rpc<std::string>("PartyManagement.CreateConnectionTokenFromPartyId", ct, partyId, userData);
				}

				pplx::task<SearchResult> searchParties(const std::string& jsonQuery, Stormancer::uint32 skip, Stormancer::uint32 size, pplx::cancellation_token cancellationToken)
				{
					auto rpc = _scene.lock()->dependencyResolver().resolve<RpcService>();
					return rpc->rpc<SearchResult>("PartyManagement.SearchParties", cancellationToken, jsonQuery, skip, size);
				}
			private:

				std::weak_ptr<Scene> _scene;
				ILogger_ptr _logger;
				int _serverProtocolVersion = 0;
			};

			// Disable deprecation warnings on implementations of deprecated methods
			STORM_WARNINGS_PUSH;
			STORM_MSVC_WARNING(disable: 4996);
			STORM_CLANG_DIAGNOSTIC("clang diagnostic ignored \"-Wdeprecated-declarations\"")
				class Party_Impl : public ClientAPI<Party_Impl, details::PartyManagementService>, public PartyApi
			{
			public:

				Party_Impl(
					std::weak_ptr<Stormancer::Users::UsersApi> users,
					std::weak_ptr<ILogger> logger,
					std::shared_ptr<IActionDispatcher> dispatcher,
					std::shared_ptr<GameFinder::GameFinderApi> gameFinder,
					std::shared_ptr<IClient> client
				)
					: ClientAPI<Party_Impl, details::PartyManagementService>(users, "stormancer.plugins.partyManagement")
					, _logger(logger)
					, _dispatcher(dispatcher)
					, _gameFinder(gameFinder)
					, _scope(client->dependencyResolver().beginLifetimeScope("party"))
					, _wClient(client) // _wClient is a weak_ptr so no cycle here
				{
				}

				const DependencyScope& dependencyScope() const override
				{
					return _scope;
				}

				pplx::task<void> createParty(const PartyCreationOptions& partySettings, const std::unordered_map<std::string, std::string>& userMetadata = {}, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					if (_party)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error(PartyError::Str::AlreadyInParty), _dispatcher, void);
					}

					auto wThat = STORM_WEAK_FROM_THIS();

					auto partyTask = getPartyManagementService(ct)
						.then([partySettings, ct](std::shared_ptr<PartyManagementService> partyManagement)
							{
								return partyManagement->createParty(partySettings, ct);
							})
						.then([wThat, partySettings, userMetadata, ct](std::string sceneToken)
							{
								auto that = wThat.lock();
								if (!that)
								{
									throw ObjectDeletedException("PartyApi");
								}

								PartyId partyId;
								partyId.type = PartyId::TYPE_CONNECTION_TOKEN;
								partyId.id = sceneToken;
								//user data already setup in the sceneToken.
								return that->joinPartyInternal(partyId, {}, userMetadata, ct);
							});

							setPartySafe(std::make_shared<pplx::task<std::shared_ptr<PartyContainer>>>(partyTask));

							return partyTask
								.then([wThat](pplx::task<std::shared_ptr<PartyContainer>> task)
									{
										triggerPartyJoinedEvents(wThat, task);
									});
				}

				pplx::task<void> createPartyIfNotJoined(const PartyCreationOptions& partyRequest, const std::unordered_map<std::string, std::string>& userMetadata = {}, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					auto wThat = STORM_WEAK_FROM_THIS();
					return pplx::task_from_result(this->isInParty())
						.then([wThat, partyRequest, userMetadata, ct](bool isInParty)
							{
								auto that = wThat.lock();
								if (!that)
								{
									throw ObjectDeletedException("PartyApi");
								}
								if (isInParty)
								{
									return pplx::task_from_result();
								}
								else
								{
									return that->createParty(partyRequest, userMetadata, ct);
								}
							});
				}

				pplx::task<void> joinParty(const std::string& token, const std::unordered_map<std::string, std::string>& userMetadata = {}, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					PartyId partyId;
					partyId.type = PartyId::TYPE_CONNECTION_TOKEN;
					partyId.id = token;
					//userdata included in token.
					return joinParty(partyId, {}, userMetadata, ct);
				}

				pplx::task<void> joinPartyByInvitationCode(const std::string& invitationCode, const std::vector<byte>& userData = {}, const std::unordered_map<std::string, std::string>& userMetadata = {}, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					auto wThat = STORM_WEAK_FROM_THIS();
					return getConnectionTokenFromInvitationCode(invitationCode, userData, ct)
						.then([userMetadata, ct, wThat](std::string connectionToken)
							{
								if (ct.is_canceled())
								{
									pplx::cancel_current_task();
								}

								auto that = wThat.lock();
								if (that == nullptr)
								{
									throw ObjectDeletedException("PartyApi");
								}

								return that->joinParty(connectionToken, userMetadata, ct);
							});
				}

				pplx::task<void> joinParty(const PartyId& partyId, const std::vector<byte>& userData, const std::unordered_map<std::string, std::string>& userMetadata = {}, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					std::lock_guard<std::recursive_mutex> lg(_partyMutex);

					if (_party)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error(PartyError::Str::AlreadyInParty), _dispatcher, void);
					}

					auto wThat = STORM_WEAK_FROM_THIS();

					auto partyTask = joinPartyInternal(partyId, userData, userMetadata, ct);

					setPartySafe(std::make_shared<pplx::task<std::shared_ptr<PartyContainer>>>(partyTask));

					return partyTask
						.then([wThat](pplx::task<std::shared_ptr<PartyContainer>> task)
							{
								triggerPartyJoinedEvents(wThat, task);
							});
				}

				pplx::task<std::shared_ptr<PartyContainer>> joinPartyInternal(const PartyId& partyId, const std::vector<byte>& userData, const std::unordered_map<std::string, std::string>& userMetadata = {}, pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					auto wThat = STORM_WEAK_FROM_THIS();

					return _leavePartyTask
						.then([wThat, partyId, userData, userMetadata, ct, logger = _logger]()
							{
								return withRetries<std::shared_ptr<PartyContainer>>([wThat, partyId, userData, userMetadata](pplx::cancellation_token ct)
									{
										if (auto that = wThat.lock())
										{
											return that->obtainConnectionToken(partyId, userData, ct)
												.then([wThat, partyId, userMetadata, ct](std::string connectionToken)
													{
														if (auto that = wThat.lock())
														{
															return that->getPartySceneByToken(connectionToken, partyId, userMetadata, ct);
														}
														throw std::runtime_error(PartyError::Str::StormancerClientDestroyed);
													});
										}
										throw std::runtime_error(PartyError::Str::StormancerClientDestroyed);
									}, 1000ms, 2, [logger](const std::exception& ex)
									{
										logger->log(LogLevel::Error, "Party", "Join party failed", ex);
										if (std::string(ex.what()).find("party.joinDenied") == 0)
										{
											return false;
										}
										return true;
									}, pplx::get_ambient_scheduler(), ct);
							})
						.then([wThat](pplx::task<std::shared_ptr<PartyContainer>> task)
							{
								try
								{

									return pplx::task_from_result(task.get());
								}
								catch (std::exception& ex)
								{
									if (auto that = wThat.lock())
									{
										if (that->isInParty())
										{
											return that->leaveParty().then([ex]()
												{
													return pplx::task_from_exception<std::shared_ptr<PartyContainer>>(ex);
												});
										}
									}
									throw;
								}
							}, _dispatcher);
				}

				pplx::task<std::string> obtainConnectionToken(const PartyId& partyId, const std::vector<byte>& userData, pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					if (partyId.type == PartyId::TYPE_CONNECTION_TOKEN)
					{
						return pplx::task_from_result(partyId.id);
					}

					pplx::task<PartyId> partyIdTask;
					if (partyId.type == PartyId::TYPE_PARTY_ID)
					{
						partyIdTask = pplx::task_from_result(partyId);
					}
					else if (partyId.type == PartyId::TYPE_SCENE_ID) // TODO : deprecated, we should get a connexion token from a partyId only
					{
						partyIdTask = pplx::task_from_result(partyId);
					}
					else
					{
						auto provider = getProviderForPlatform(partyId.platform);
						if (provider == nullptr)
						{
							STORM_RETURN_TASK_FROM_EXCEPTION(std::runtime_error(PartyError::Str::UnsupportedPlatform), std::string);
						}
						partyIdTask = provider->getPartyId(partyId, ct);
					}

					auto wThat = STORM_WEAK_FROM_THIS();
					return partyIdTask
						.then([wThat, ct](PartyId partyId)
							{
								if (auto that = wThat.lock())
								{
									return that->getPartyManagementService(ct)
										.then([partyId](std::shared_ptr<Stormancer::Party::details::PartyManagementService> service)
											{
												return std::make_tuple(service, partyId.id);
											});
								}
								else
								{
									throw ObjectDeletedException("PartyApi");
								}
							})
						.then([userData, ct](std::tuple< std::shared_ptr<Stormancer::Party::details::PartyManagementService>, std::string> tuple)
							{
								return std::get<0>(tuple)->getConnectionTokenFromPartyId(std::get<1>(tuple), userData, ct);
							});
				}

				std::shared_ptr<Platform::IPlatformSupportProvider> getProviderForPlatform(const std::string& platformName)
				{
					auto providers = platformProviders();
					auto it = std::find_if(providers.begin(), providers.end(),
						[&platformName](std::shared_ptr<Platform::IPlatformSupportProvider> provider) { return provider->getPlatformName() == platformName; });
					if (it != providers.end())
					{
						return *it;
					}
					return nullptr;
				}

				static void triggerPartyJoinedEvents(std::weak_ptr<Party_Impl> partyWeak, pplx::task<std::shared_ptr<PartyContainer>> joinPartyTask)
				{
					auto party = partyWeak.lock();
					if (!party)
					{
						return;
					}

					try
					{
						joinPartyTask.get();

						party->raiseJoinedParty();

						auto members = party->getPartyMembers();
						MembersUpdate initialUpdate;
						initialUpdate.partyApi = party;
						initialUpdate.updatedMembers.reserve(members.size());
						for (auto& member : members)
						{
							initialUpdate.updatedMembers.emplace_back(std::move(member), MembersUpdate::Joined);
						}
						party->raisePartyMembersUpdated(initialUpdate);

						party->raisePartySettingsUpdated(party->getPartySettings());
					}
					catch (const std::exception& ex)
					{
						party->setPartySafe(nullptr);
						party->_onPartyError(PartyError(PartyError::Api::JoinParty, ex.what()));
						throw;
					}
				}

				pplx::task<void> leaveParty(pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					std::lock_guard<std::recursive_mutex> lg(_partyMutex);
					if (!_party)
					{
						return pplx::task_from_result(pplx::task_options(_dispatcher));
					}

					auto party = *_party;
					_party = nullptr;
					auto logger = _logger;
					party.then([ct, logger](std::shared_ptr<PartyContainer> partyContainer)
						{
							return partyContainer->getScene()->disconnect(ct)
								.then([logger, partyContainer](pplx::task<void> task)
									{
										// Need to keep partyContainer alive so that onLeaving/onLeft are triggered
										try
										{
											task.wait();
										}
										catch (const std::exception& ex)
										{
											logger->log(LogLevel::Debug, "PartyApi::leaveParty", "An error occurred while leaving the party", ex);
										}
										catch (...) {}
									});
						});

					setGameFinderStatus(PartyGameFinderStatus::SearchStopped);

					_leavePartyTask = pplx::create_task(_leavePartyTce, _dispatcher);
					return _leavePartyTask;
				}

				/// <summary>
				/// Gets a boolean indicating if the party is currently in a gamesession.
				/// </summary>
				/// <returns></returns>
				bool isInGameSession() override
				{
					auto party = tryGetParty();
					if (party != nullptr)
					{
						const auto& serverData = party->settings().publicServerData;
						auto it = serverData.find("stormancer.partyStatus");
						return it != serverData.end() && it->second == "gamesession";
					}
					else
					{
						return false;
					}
				}

				/// <summary>
				/// If the party is in a gamesession, gets a token to connect to it.
				/// </summary>
				/// <param name="ct"></param>
				/// <returns></returns>
				pplx::task<std::string> getCurrentGameSessionConnectionToken(pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					auto party = tryGetParty();
					if (!party)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error(PartyError::Str::NotInParty), _dispatcher, std::string);
					}
					return party->partyService()->getCurrentGameSessionConnectionToken(ct);
				}

				bool isInParty() const noexcept override
				{
					return tryGetParty() != nullptr;
				}

				std::shared_ptr<Scene> getPartyScene() const override
				{
					auto partyContainer = tryGetParty();

					if (!partyContainer)
					{
						return nullptr;
					}

					return partyContainer->getScene();
				}

				std::vector<PartyUserDto> getPartyMembers() const override
				{
					auto party = tryGetParty();
					if (!party)
					{
						throw std::runtime_error(PartyError::Str::NotInParty);
					}

					return party->members();
				}

				PartyUserDto getLocalMember() const override
				{
					auto party = tryGetParty();
					if (!party)
					{
						throw std::runtime_error(PartyError::Str::NotInParty);
					}

					auto users = _wUsers.lock();
					if (!users)
					{
						throw ObjectDeletedException("UsersApi");
					}

					auto myId = users->userId();
					auto members = party->members();
					auto it = std::find_if(members.begin(), members.end(), [&myId](const PartyUserDto& user)
						{
							return user.userId == myId;
						});

					if (it != members.end())
					{
						return *it;
					}
					assert(false); // Bug!
					throw std::runtime_error(PartyError::Str::NotInParty);
				}

				PartySettings getPartySettings() const override
				{
					auto party = tryGetParty();
					if (!party)
					{
						throw std::runtime_error(PartyError::Str::NotInParty);
					}

					return party->settings();
				}

				PartyId getPartyId() const override
				{
					auto party = tryGetParty();
					if (!party)
					{
						throw std::runtime_error(PartyError::Str::NotInParty);
					}

					return party->getPartyId();
				}

				std::string getPartyLeaderId() const override
				{
					auto party = tryGetParty();
					if (!party)
					{
						throw std::runtime_error(PartyError::Str::NotInParty);
					}

					return party->leaderId();
				}

				bool isLeader() const override
				{
					auto party = tryGetParty();
					if (!party)
					{
						throw std::runtime_error(PartyError::Str::NotInParty);
					}

					return party->isLeader();
				}

				std::vector<std::string> getSentPendingInvitations() override
				{
					auto party = tryGetParty();
					if (!party)
					{
						return std::vector<std::string>();
					}

					return party->partyService()->getPendingStormancerInvitations();
				}

				pplx::task<std::string> createInvitationCode(pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					auto party = tryGetParty();
					if (!party)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error(PartyError::Str::NotInParty), _dispatcher, std::string);
					}
					return party->partyService()->createInvitationCode(ct);
				}

				pplx::task<std::string> getConnectionTokenFromInvitationCode(const std::string& invitationCode, const std::vector<byte>& userData, pplx::cancellation_token ct)
				{
					return getPartyManagementService(ct)
						.then([invitationCode, userData, ct](std::shared_ptr<PartyManagementService> service)
							{
								return service->getConnectionTokenFromInvitationCode(invitationCode, userData, ct);
							});
				}

				pplx::task<std::string> getConnectionTokenFromPartyId(const std::string& partyId, const std::vector<byte>& userData, pplx::cancellation_token ct)
				{
					return getPartyManagementService(ct)
						.then([partyId, userData, ct](std::shared_ptr<PartyManagementService> service)
							{
								return service->getConnectionTokenFromPartyId(partyId, userData, ct);
							});
				}

				pplx::task<SearchResult> searchParties(const std::string& jsonQuery, Stormancer::uint32 skip, Stormancer::uint32 size, pplx::cancellation_token cancellationToken) override
				{
					return getPartyManagementService(cancellationToken)
						.then([jsonQuery, skip, size, cancellationToken](std::shared_ptr<PartyManagementService> service)
							{
								return service->searchParties(jsonQuery, skip, size, cancellationToken);
							});
				}

				pplx::task<void> cancelInvitationCode(pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					auto party = tryGetParty();
					if (!party)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error(PartyError::Str::NotInParty), _dispatcher, void);
					}
					if (party->isLeader())
					{
						return party->partyService()->cancelInvitationCode(ct);
					}
					else
					{
						return pplx::task_from_exception<void>(std::runtime_error("unauthorized"));
					}
				}

				// Not const because of mutex lock
				std::vector<PartyInvitation> getPendingInvitations() override
				{
					std::vector<PartyInvitation> pendingInvitations;
					{
						std::lock_guard<std::recursive_mutex> lg(_invitationsMutex);

						pendingInvitations.reserve(_invitationsNew.size());
						for (auto invitation : _invitationsNew)
						{
							pendingInvitations.emplace_back(invitation);
						}
					}
					return pendingInvitations;
				}

				pplx::task<void> updatePlayerStatus(PartyUserStatus playerStatus) override
				{
					auto party = tryGetParty();
					if (!party)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error(PartyError::Str::NotInParty), _dispatcher, void);
					}

					return party->partyService()->updatePlayerStatus(playerStatus);
				}

				pplx::task<void> updatePartySettings(PartySettings partySettingsDto) override
				{
					auto party = tryGetParty();
					if (!party)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error(PartyError::Str::NotInParty), _dispatcher, void);
					}

					if (partySettingsDto.customData == "")
					{
						partySettingsDto.customData = "{}";
					}

					return party->partyService()->updatePartySettings(partySettingsDto);
				}

				pplx::task<void> updatePlayerData(std::vector<byte> data, unsigned int localPlayerCount) override
				{
					auto party = tryGetParty();
					if (!party)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error(PartyError::Str::NotInParty), _dispatcher, void);
					}

					return party->partyService()->updatePlayerData(data, localPlayerCount);
				}

				pplx::task<void> promoteLeader(std::string userId) override
				{
					auto party = tryGetParty();
					if (!party)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error(PartyError::Str::NotInParty), _dispatcher, void);
					}

					return party->partyService()->promoteLeader(userId);
				}

				pplx::task<void> kickPlayer(std::string userId) override
				{
					auto party = tryGetParty();
					if (!party)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error(PartyError::Str::NotInParty), _dispatcher, void);
					}

					std::weak_ptr<Party_Impl> wThat = this->shared_from_this();
					return party->partyService()->kickPlayer(userId)
						.then([userId, wThat]
							{
								auto handlersTask = pplx::task_from_result();
								if (auto that = wThat.lock())
								{
									auto logger = that->_logger;
									for (auto provider : that->platformProviders())
									{
										handlersTask = handlersTask.then([that, provider, userId, logger]
											{
												return provider->kickPlayer(userId)
													.then([logger, provider, userId](pplx::task<void> task)
														{
															try
															{
																task.get();
															}
															catch (const std::exception& ex)
															{
																logger->log(LogLevel::Error, "PartyApi::kickPlayer", "An error occurred while kicking player " + userId + " from session on platform " + provider->getPlatformName(), ex);
															}
														});
											}, that->_dispatcher);
									}
								}
								return handlersTask;
							})
						.then([wThat, userId]
							{
								if (auto that = wThat.lock())
								{
									auto eventHandlers = that->getEventHandlers();
									for (auto handler : eventHandlers)
									{
										try
										{
											handler->onPlayerKickedByLocalMember(that, userId);
										}
										catch (const std::exception& ex)
										{
											that->_logger->log(LogLevel::Error, "Party_Impl::kickPlayer", "An exception was thrown by an onPlayerKickedByLocalMember event handler", ex);
										}
									}
								}
							}, _dispatcher);
				}

				bool canSendInvitations() const override
				{
					auto party = tryGetParty();
					if (!party)
					{
						return false;
					}

					return party->isLeader() || !party->settings().onlyLeaderCanInvite;
				}

				pplx::task<void> sendInvitation(const std::string& recipient, bool forceStormancerInvitation) override
				{
					auto party = tryGetParty();
					if (!party)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error(PartyError::Str::NotInParty), _dispatcher, void);
					}

					std::weak_ptr<Party_Impl> wThat = this->shared_from_this();
					auto logger = _logger;
					std::weak_ptr<PartyContainer> wParty = party;
					party->partyService()->sendInvitation(recipient, forceStormancerInvitation)
						.then([wParty, wThat, logger, recipient](pplx::task<bool> task)
							{
								auto that = wThat.lock();
								auto party = wParty.lock();
								try
								{
									auto status = task.wait();
									if (that && status == pplx::completed)
									{
										bool accepted = task.get();
										if (!accepted)
										{
											that->_onSentInvitationDeclined(recipient);
										}
									}
								}
								catch (const std::exception& ex)
								{
									logger->log(LogLevel::Error, "PartyApi::sendInvitation", "Could not send an invitation to " + recipient, ex);
								}
							}, _dispatcher);
					// TODO Use an observable RPC to tell when the invite has been sent as well as when it has been accepted or declined.
					return pplx::task_from_result();
				}

				void cancelInvitation(const std::string& recipient) override
				{
					auto party = tryGetParty();
					if (!party)
					{
						return;
					}

					auto logger = _logger;
					party->partyService()->cancelInvitation(recipient).then([logger, recipient](pplx::task<void> task)
						{
							try
							{
								task.wait();
							}
							catch (const std::exception& ex)
							{
								logger->log(LogLevel::Error, "PartyApi::cancelInvitation", "Error while canceling invitation to " + recipient, ex);
							}
						});
				}

				bool showSystemInvitationUI() override
				{
					std::lock_guard<std::recursive_mutex> lock(_partyMutex);

					if (!isInParty())
					{
						return false;
					}

					for (auto& provider : platformProviders())
					{
						if (provider->tryShowSystemInvitationUI(this->shared_from_this()))
						{
							return true;
						}
					}

					return false;
				}

				pplx::task<std::vector<AdvertisedParty>> getAdvertisedParties(pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					std::vector<pplx::task<std::vector<AdvertisedParty>>> tasks;

					auto cts = ct.is_cancelable() ? pplx::cancellation_token_source::create_linked_source(ct) : pplx::cancellation_token_source();

					for (auto& partyAdvertiser : platformProviders())
					{
						auto task = partyAdvertiser->getAdvertisedParties(cts.get_token());

						tasks.push_back(task);

						task.then([cts, logger = _logger](pplx::task<std::vector<AdvertisedParty>> task)
							{
								try
								{
									task.get();
								}
								catch (const std::exception& ex)
								{
									cts.cancel();
									logger->log(LogLevel::Error, "Party", "An IPartyAdvertiser failed", ex);
								}
							});
					}

					return pplx::when_all(tasks.begin(), tasks.end(), _dispatcher);
				}

				Subscription subscribeOnSentInvitationsListUpdated(std::function<void(std::vector<std::string>)> callback) override
				{
					return _onSentInvitationsUpdated.subscribe(callback);
				}

				Subscription subscribeOnSentInvitationDeclined(std::function<void(std::string)> callback) override
				{
					return _onSentInvitationDeclined.subscribe(callback);
				}

				Event<PartySettings>::Subscription subscribeOnUpdatedPartySettings(std::function<void(PartySettings)> callback) override
				{
					return _onUpdatedPartySettings.subscribe(callback);
				}

				Event<std::vector<PartyUserDto>>::Subscription subscribeOnUpdatedPartyMembers(std::function<void(std::vector<PartyUserDto>)> callback) override
				{
					return _onUpdatedPartyMembers.subscribe(callback);
				}

				Subscription subscribeOnPartyMembersUpdated(std::function<void(MembersUpdate)> callback) override
				{
					return _onPartyMembersUpdated.subscribe(callback);
				}

				Event<>::Subscription subscribeOnJoinedParty(std::function<void()> callback) override
				{
					return _onJoinedParty.subscribe(callback);
				}

				Event<MemberDisconnectionReason>::Subscription subscribeOnLeftParty(std::function<void(MemberDisconnectionReason)> callback) override
				{
					return _onLeftParty.subscribe(callback);
				}

				Event<PartyInvitation>::Subscription subscribeOnInvitationReceived(std::function<void(PartyInvitation)> callback) override
				{
					// Initialize platform providers so that they can listen to platform invitations
					platformProviders();

					auto subscription = _invitationReceivedEvent.subscribe(callback);
					if (_pendingInvitation)
					{
						_invitationReceivedEvent(*_pendingInvitation);
						_pendingInvitation.reset();
					}
					return subscription;
				}

				Event<std::string>::Subscription subscribeOnInvitationCanceled(std::function<void(std::string)> callback) override
				{
					return _onInvitationCanceled.subscribe(callback);
				}

				Subscription subscribeOnGameFinderStatusUpdate(std::function<void(PartyGameFinderStatus)> callback) override
				{
					return _onGameFinderStatusUpdate.subscribe(callback);
				}

				Subscription subscribeOnGameFound(std::function<void(GameFinder::GameFoundEvent)> callback) override
				{
					return _onGameFound.subscribe(callback);
				}

				Subscription subscribeOnGameFinderFailure(std::function<void(PartyGameFinderFailure)> callback) override
				{
					return _onGameFinderFailure.subscribe(callback);
				}

				Subscription subscribeOnPartyError(std::function<void(const PartyError&)> callback) override
				{
					return _onPartyError.subscribe(std::move(callback));
				}

				void setGameFinderStatus(PartyGameFinderStatus status)
				{
					std::lock_guard<std::recursive_mutex> lg(_partyMutex);

					if (status != _gameFinderStatus)
					{
						_gameFinderStatus = status;
						_onGameFinderStatusUpdate(status);
					}
				}

				void initialize()
				{
					auto wThat = STORM_WEAK_FROM_THIS();
					_subscriptions.push_back(_gameFinder->subscribeGameFinderStateChanged([wThat](GameFinder::GameFinderStatusChangedEvent evt)
						{
							if (auto that = wThat.lock())
							{
								auto party = that->tryGetParty();
								if (party && party->settings().gameFinderName == evt.gameFinder)
								{
									switch (evt.status)
									{
									case GameFinder::GameFinderStatus::Searching:
										that->setGameFinderStatus(PartyGameFinderStatus::SearchInProgress);
										break;
									default:
										that->setGameFinderStatus(PartyGameFinderStatus::SearchStopped);
										break;
									}
								}
							}
						}));
					_subscriptions.push_back(_gameFinder->subscribeGameFound([wThat](GameFinder::GameFoundEvent evt)
						{
							if (auto that = wThat.lock())
							{
								auto party = that->tryGetParty();
								if (party && party->settings().gameFinderName == evt.gameFinder)
								{
									that->_onGameFound(evt);
								}
							}
						}));
					auto messenger = _scope.resolve<Platform::InvitationMessenger>();
					_subscriptions.push_back(messenger->subscribeOnInvitationReceived([wThat](std::shared_ptr<Platform::IPlatformInvitation> invite)
						{
							if (auto that = wThat.lock())
							{
								that->onInvitationReceived(invite);
							}
						}));
				}

			private:

				void onJoinPartyRequestedByPlatform(const Platform::PlatformInvitationRequestContext& ctx)
				{
					if (!ctx.error.empty())
					{
						_onPartyError(PartyError(PartyError::Api::JoinParty, ctx.error.c_str()));
						return;
					}

					_logger->log(LogLevel::Trace, "PartyApi::onJoinpartyRequestedByPlatform", "Received a platform join party request", ctx.partyId.toString());

					std::lock_guard<std::recursive_mutex> lock(_partyMutex);

					auto that = this->shared_from_this();
					JoinPartyFromSystemArgs args;
					args.party = that;
					args.client = _wClient.lock();
					args.user = ctx.invitedUser;
					args.partyId = ctx.partyId;
					args.cancellationToken = ctx.cancellationToken;
					auto ct = ctx.cancellationToken;
					std::vector<byte> userData = args.userData;
					_joinPartyFromSystemHandler(args)
						.then([partyId = ctx.partyId, that, ct, userData, ctx](bool accept)
							{
								if (accept)
								{
									pplx::task<void> task = pplx::task_from_result();

									if (that->isInParty())
									{
										auto partyContainer = that->tryGetParty();
										if (partyContainer != nullptr && partyContainer->getPartyId() != partyId)
										{
											task = that->leaveParty();
										}
									}

									return task.then([partyId, that, ct, userData, ctx]()
										{
											return that->joinParty(partyId, userData, std::unordered_map<std::string, std::string>{ { "invitedUser", ctx.invitedUser->userId } }, ct);
										});
								}
								else
								{
									return pplx::task_from_result();
								}
							})
						.then([that](pplx::task<void> task)
							{
								try
								{
									task.get();
								}
								catch (const std::exception& ex)
								{
									that->_logger->log(LogLevel::Error, "PartyApi::onJoinpartyRequestedByPlatform", "Could not join party", ex);
								}
							});
				}

				std::vector<std::shared_ptr<IPartyEventHandler>> getEventHandlers()
				{
					try
					{
						return _scope.resolveAll<IPartyEventHandler>();
					}
					catch (...)
					{
						// The scope can be invalid when the client is being destroyed.
						return std::vector<std::shared_ptr<IPartyEventHandler>>{};
					}
				}

				void raisePartyMembersUpdated(const MembersUpdate& update)
				{
					_onUpdatedPartyMembers(getPartyMembers());
					_onPartyMembersUpdated(update);
					auto eventHandlers = getEventHandlers();
					for (auto handler : eventHandlers)
					{
						try
						{
							handler->onPartyMembersUpdated(update);
						}
						catch (const std::exception& ex)
						{
							_logger->log(LogLevel::Error, "Party_Impl::raisePartyMembersUpdated", "An exception was thrown by an onPartyMembersUpdated handler", ex);
						}
					}

					auto logger = _logger;
					for (auto provider : platformProviders())
					{
						// Keep this task as member to prevent rapid settings updates from overlapping
						_platformPartyMembersUpdateTask = _platformPartyMembersUpdateTask.then([provider, update]
							{
								return provider->updateSessionMembers(update);
							}, _dispatcher)
							.then([logger, provider](pplx::task<void> task)
								{
									try
									{
										task.get();
									}
									catch (const std::exception& ex)
									{
										logger->log(LogLevel::Error, "Party_Impl::raisePartyMembersUpdated", "An error occurred while updating platform-specific session members for platform " + provider->getPlatformName(), ex);
									}
								});
					}
				}

				void raisePartySettingsUpdated(const PartySettings& settings)
				{
					_onUpdatedPartySettings(settings);
					auto eventHandlers = getEventHandlers();
					for (auto handler : eventHandlers)
					{
						try
						{
							handler->onPartySettingsUpdated(this->shared_from_this(), settings);
						}
						catch (const std::exception& ex)
						{
							_logger->log(LogLevel::Error, "Party_Impl::raisePartySettingsUpdated", "An exception was thrown by an onPartySettingsUpdated handler", ex);
						}
					}

					auto logger = _logger;
					for (auto provider : platformProviders())
					{
						// Keep this task as member to prevent rapid settings updates from overlapping
						_platformPartySettingsUpdateTask = _platformPartySettingsUpdateTask.then([provider, settings]
							{
								return provider->updateSessionSettings(settings);
							}, _dispatcher)
							.then([logger, provider](pplx::task<void> task)
								{
									try
									{
										task.get();
									}
									catch (const std::exception& ex)
									{
										logger->log(LogLevel::Error, "Party_Impl::raisePartySettingsUpdated", "An error occurred while updating platform-specific session settings for platform " + provider->getPlatformName(), ex);
									}
								});
					}
				}

				void raiseJoinedParty()
				{
					_onJoinedParty();
					auto eventHandlers = getEventHandlers();
					for (auto handler : eventHandlers)
					{
						try
						{
							auto scene = getPartyScene();
							std::string partySceneId = (scene ? scene->id() : "");
							auto ctx = std::make_shared<JoinedPartyContext>();
							ctx->partyId = getPartyId();
							ctx->partySceneId = partySceneId;
							ctx->partyApi = this->shared_from_this();
							handler->onJoinedParty(ctx);
						}
						catch (const std::exception& ex)
						{
							_logger->log(LogLevel::Error, "Party_Impl::raiseJoinedParty", "An exception was thrown by an onJoinedParty handler", ex);
						}
					}
				}

				void raiseLeftParty(MemberDisconnectionReason reason)
				{
					_onLeftParty(reason);
					auto eventHandlers = getEventHandlers();
					for (auto handler : eventHandlers)
					{
						try
						{
							auto scene = getPartyScene();
							std::string partySceneId = scene ? scene->id() : "";
							auto ctx = std::make_shared<LeftPartyContext>();
							ctx->partyId = getPartyId();
							ctx->partySceneId = partySceneId;
							ctx->partyApi = this->shared_from_this();
							ctx->reason = reason;
							handler->onLeftParty(ctx);
						}
						catch (const std::exception& ex)
						{
							_logger->log(LogLevel::Error, "Party_Impl::raiseLeftParty", "An exception was thrown by an onLeftParty handler", ex);
						}
					}
				}

				class InvitationInternal : public IPartyInvitationInternal, public std::enable_shared_from_this<InvitationInternal>
				{
				public:

					InvitationInternal(std::shared_ptr<Platform::IPlatformInvitation> impl, std::shared_ptr<Party_Impl> party)
						: _impl(impl)
						, _party(party)
						, _senderId(impl->getSenderId())
					{
					}

					InvitationInternal(const InvitationInternal&) = delete;

					InvitationInternal& operator=(const InvitationInternal&) = delete;

					void initialize()
					{
						std::weak_ptr<InvitationInternal> wThat(this->shared_from_this());
						_cancellationSubscription = _impl->subscribeOnInvitationCanceled([wThat]
							{
								if (auto that = wThat.lock())
								{
									if (auto party = that->_party.lock())
									{
										// While we are in this cancellation event, the user could be calling one of the other methods, hence the mutex lock on top of each method.
										// I want that when the IsValid() call has returned true in these methods, the rest of the method can execute
										// with the certitude that the invitation will not be removed from the list while it is executing.
										// We could have one mutex per invitation instead, but mutexes are a limited resource, esp. on consoles, so we probably shouldn't
										std::lock_guard<std::recursive_mutex> lg(party->_invitationsMutex);
										that->_isValid = false;
										party->removeInvitation(*that);
										party->_logger->log(LogLevel::Trace, "InvitationInternal", "Invitation from " + that->_senderId + " was canceled");
										party->_onInvitationCanceled(that->_senderId);
									}
								}
							});
					}

					std::string getSenderId() override
					{
						if (!_impl)
						{
							throw std::runtime_error(PartyError::Str::InvalidInvitation);
						}

						return _impl->getSenderId();
					}

					std::string getSenderPlatformId() override
					{
						if (!_impl)
						{
							throw std::runtime_error(PartyError::Str::InvalidInvitation);
						}

						return _impl->getSenderPlatformId();
					}

					pplx::task<void> acceptAndJoinParty(const std::vector<byte>& userData, const std::unordered_map<std::string, std::string>& userMetadata = {}, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
					{
						auto party = _party.lock();
						if (!party)
						{
							STORM_RETURN_TASK_FROM_EXCEPTION(std::runtime_error(PartyError::Str::InvalidInvitation), void);
						}

						std::lock_guard<std::recursive_mutex> invitesLock(party->_invitationsMutex);
						std::lock_guard<std::recursive_mutex> partyLock(party->_partyMutex);
						if (!isValid())
						{
							STORM_RETURN_TASK_FROM_EXCEPTION(std::runtime_error(PartyError::Str::InvalidInvitation), void);
						}

						if (party->isInParty())
						{
							STORM_RETURN_TASK_FROM_EXCEPTION(std::runtime_error(PartyError::Str::AlreadyInParty), void);
						}

						party->removeInvitation(*this);

						_isValid = false;
						auto partyTask = _impl->accept(party)
							.then([party, userMetadata, userData, ct](PartyId partyId)
								{
									return party->joinPartyInternal(partyId, userData, userMetadata, ct);
								});
						party->setPartySafe(std::make_shared<pplx::task<std::shared_ptr<PartyContainer>>>(partyTask));
						auto wParty = _party;
						return partyTask
							.then([wParty](pplx::task<std::shared_ptr<PartyContainer>> task)
								{
									triggerPartyJoinedEvents(wParty, task);
								});
					}

					void decline() override
					{
						auto party = _party.lock();
						if (!party)
						{
							return;
						}

						std::lock_guard<std::recursive_mutex> lg(party->_invitationsMutex);
						if (!isValid())
						{
							return;
						}

						party->removeInvitation(*this);

						_isValid = false;
						auto logger = party->_logger;
						_impl->decline(party).then([logger](pplx::task<void> task)
							{
								try
								{
									task.wait();
								}
								catch (const std::exception& ex)
								{
									logger->log(LogLevel::Error, "InvitationInternal::decline", "An error occurred while declining an invitation", ex);
								}
							});
					}

					bool isValid() override
					{
						return _impl && _isValid && !_party.expired();
					}

				private:

					std::shared_ptr<Platform::IPlatformInvitation> _impl;
					std::weak_ptr<Party_Impl> _party;
					std::string _senderId;
					Subscription _cancellationSubscription;
					bool _isValid = true;
				};

				// Events
				Event<PartySettings> _onUpdatedPartySettings;
				Event<std::vector<PartyUserDto>> _onUpdatedPartyMembers;
				Event<MembersUpdate> _onPartyMembersUpdated;
				Event<> _onJoinedParty;
				Event<MemberDisconnectionReason> _onLeftParty;
				Event<PartyInvitation> _invitationReceivedEvent;
				std::shared_ptr<PartyInvitation> _pendingInvitation;
				Event<std::string> _onInvitationCanceled;
				Event<std::vector<std::string>> _onSentInvitationsUpdated;
				Event<std::string> _onSentInvitationDeclined;
				Event<PartyGameFinderStatus> _onGameFinderStatusUpdate;
				Event<GameFinder::GameFoundEvent> _onGameFound;
				Event<PartyGameFinderFailure> _onGameFinderFailure;
				Event<PartyError> _onPartyError;

				std::shared_ptr<PartyContainer> tryGetParty() const noexcept
				{
					std::lock_guard<std::recursive_mutex> lg(_partyMutex);

					if (_party && _party->is_done())
					{
						// The task could be faulted. In that case, we consider that we are not in the party.
						try
						{
							return _party->get();
						}
						catch (...) {}
					}
					return nullptr;
				}

				void setPartySafe(std::shared_ptr<pplx::task<std::shared_ptr<PartyContainer>>> party)
				{
					std::lock_guard<std::recursive_mutex> lg(_partyMutex);

					_party = party;
				}

				void runSceneInitEventHandlers(std::shared_ptr<Scene> scene)
				{
					for (const auto& provider : platformProviders())
					{
						provider->onPartySceneInitialization(scene);
					}

					auto eventHandlers = getEventHandlers();
					for (const auto& handler : eventHandlers)
					{
						handler->onPartySceneInitialization(scene);
					}
				}

				pplx::task<std::shared_ptr<PartyContainer>> getPartySceneByToken(const std::string& token, const PartyId& partyId, const std::unordered_map<std::string, std::string>& userMetadata = {}, pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					auto users = _wUsers.lock();
					if (!users)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("UsersApi"), std::shared_ptr<PartyContainer>);
					}

					auto joiningPartyContext = std::make_shared<JoiningPartyContext>();
					joiningPartyContext->metadata = userMetadata;
					joiningPartyContext->partyId = partyId;
					joiningPartyContext->partySceneId = (partyId.type == PartyId::TYPE_SCENE_ID || partyId.type == PartyId::TYPE_PARTY_ID ? partyId.id : "");

					auto wThat = STORM_WEAK_FROM_THIS();

					return runEventHandlers(getEventHandlers(), [joiningPartyContext](std::shared_ptr<IPartyEventHandler> eventHandler)
						{
							return eventHandler->onJoiningParty(joiningPartyContext);
						}, [logger = _logger](const std::exception& ex)
						{
							logger->log(LogLevel::Error, "Party_Impl.getPartySceneByToken", "Party onJoiningParty event handler failed", ex.what());
							throw;
						})
						.then([users, token, wThat, ct]()
							{
								return users->connectToPrivateSceneByToken(token, [wThat](std::shared_ptr<Scene> scene)
									{
										if (auto that = wThat.lock())
										{
											that->runSceneInitEventHandlers(scene);
										}
									}, ct);
							})
							.then([ct, wThat](std::shared_ptr<Scene> scene)
								{
									auto that = wThat.lock();
									if (!that)
									{
										throw ObjectDeletedException("PartyApi");
									}
									return that->initPartyFromScene(scene, ct);
								})
								.then([wThat, userMetadata](std::shared_ptr<PartyContainer> container)
									{
										auto that = wThat.lock();
										if (!that)
										{
											throw ObjectDeletedException("PartyApi");
										}

										pplx::task<void> handlersTask = pplx::task_from_result();
										for (auto provider : that->platformProviders())
										{
											handlersTask = handlersTask.then([wThat, provider, container]
												{
													if (auto that = wThat.lock())
													{
														return provider->createOrJoinSessionForParty(container->getSceneId());
													}
													throw ObjectDeletedException("PartyApi");
												}, that->_dispatcher);
										}

										auto eventHandlers = that->getEventHandlers();


										return handlersTask.then([container](pplx::task<void> task)
											{
												try
												{
													task.get();
													return container;
												}
												catch (...)
												{
													// Keep container alive so that OnLeftParty gets triggered for event handlers
													container->getScene()->disconnect()
														.then([container](pplx::task<void> t)
															{
																try
																{
																	t.wait();
																}
																catch (...)
																{
																}
															});
													throw;
												}
											});
									});
				}

				pplx::task<std::shared_ptr<PartyManagementService>> getPartyManagementService(pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					return this->getService([](auto, auto, auto) {}, [](auto, auto) {}, ct);
				}

				pplx::task<void> runLeavingPartyHandlers(std::string partySceneId)
				{
					pplx::task<void> handlersTask = pplx::task_from_result();
					auto logger = _logger;
					auto partyApi = this->shared_from_this();
					for (auto provider : platformProviders())
					{
						handlersTask = handlersTask.then([partySceneId, provider]
							{
								return provider->leaveSessionForParty(partySceneId);
							}, _dispatcher)
							.then([logger, provider](pplx::task<void> task)
								{
									// As these handlers could do important cleanup (e.g leaving a session), it is important that we run all of them even if some fail
									// This is why I handle errors for each of them
									try
									{
										task.wait();
									}
									catch (const std::exception& ex)
									{
										logger->log(LogLevel::Error, "Party_Impl::runLeavingPartyEventHandlers", "An exception was thrown by leaveSessionForParty() for platform " + provider->getPlatformName(), ex);
									}
								});
					}

					auto eventHandlers = getEventHandlers();
					for (auto handler : eventHandlers)
					{
						// Capture a shared_ptr because the handlers could do important cleanup and need access to PartyApi
						handlersTask = handlersTask.then([partyApi, partySceneId, handler]
							{
								auto ctx = std::make_shared<LeavingPartyContext>();
								ctx->partyId = partyApi->getPartyId();
								ctx->partySceneId = partySceneId;
								ctx->partyApi = partyApi;
								return handler->onLeavingParty(ctx);
							}, _dispatcher)
							.then([logger](pplx::task<void> task)
								{
									try
									{
										task.wait();
									}
									catch (const std::exception& ex)
									{
										logger->log(LogLevel::Error, "Party_Impl::runLeavingPartyEventHandlers", "An exception was thrown by an onLeavingParty() handler", ex);
									}
								});
					}

					return handlersTask;
				}

				pplx::task<std::shared_ptr<PartyContainer>> initPartyFromScene(std::shared_ptr<Scene> scene, pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					auto wPartyManagement = STORM_WEAK_FROM_THIS();
					std::shared_ptr<PartyService> partyService;
					try
					{
						partyService = scene->dependencyResolver().resolve<PartyService>();
					}
					catch (const DependencyResolutionException&)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION(std::runtime_error(("The scene " + scene->id() + " does not contain a PartyService").c_str()), std::shared_ptr<PartyContainer>);
					}

					auto sceneId = scene->id();

					auto party = std::make_shared<PartyContainer>(
						scene,
						partyService->LeftParty.subscribe([wPartyManagement, sceneId](MemberDisconnectionReason reason)
							{
								if (auto partyManagement = wPartyManagement.lock())
								{
									auto handlersTask = partyManagement->runLeavingPartyHandlers(sceneId);
									// Wait for the handlers to be done before effectively completing the _leavePartyTce.
									// This is important for handlers which manage party-related state such as platform-specific game sessions (e.g steam plugin).
									handlersTask.then([wPartyManagement, reason] // Exceptions have already been handled for this task
										{
											if (auto partyManagement = wPartyManagement.lock())
											{
												if (partyManagement->isInParty())
												{
													partyManagement->setPartySafe(nullptr);
												}
												partyManagement->raiseLeftParty(reason);
												partyManagement->_leavePartyTce.set();
												partyManagement->_leavePartyTce = pplx::task_completion_event<void>();
											}
										}, partyManagement->_dispatcher);
								}
							}),
						partyService->PartyMembersUpdated.subscribe([wPartyManagement](MembersUpdate update)
							{
								if (auto partyManagement = wPartyManagement.lock())
								{
									if (partyManagement->isInParty())
									{
										update.partyApi = partyManagement;
										partyManagement->raisePartyMembersUpdated(update);
									}
								}
							}),
						partyService->UpdatedPartySettings.subscribe([wPartyManagement](PartySettings settings)
							{
								if (auto partyManagement = wPartyManagement.lock())
								{
									if (partyManagement->isInParty())
									{
										partyManagement->raisePartySettingsUpdated(settings);
									}
								}
							}),
						partyService->UpdatedInviteList.subscribe([wPartyManagement](std::vector<std::string> invitations)
							{
								if (auto partyManagement = wPartyManagement.lock())
								{
									if (partyManagement->isInParty())
									{
										partyManagement->_onSentInvitationsUpdated(invitations);
									}
								}
							}),
						partyService->OnGameFinderFailed.subscribe([wPartyManagement](PartyGameFinderFailure dto)
							{
								if (auto partyManagement = wPartyManagement.lock())
								{
									if (partyManagement->isInParty())
									{
										partyManagement->_onGameFinderFailure(dto);
									}
								}
							})
					);

					return partyService->waitForPartyReady(ct)
						.then([party]
							{
								return party;
							});
				}

				void onInvitationReceived(std::shared_ptr<Platform::IPlatformInvitation> invite)
				{
					auto inviteInternal = std::make_shared<InvitationInternal>(invite, this->shared_from_this());
					inviteInternal->initialize();

					{
						std::lock_guard<std::recursive_mutex> lg(_invitationsMutex);
						_invitationsNew.push_back(inviteInternal);
					}

					auto that = this->shared_from_this();

					if (_invitationReceivedEvent.hasSubscribers())
					{
						_invitationReceivedEvent(PartyInvitation(inviteInternal));
					}
					else
					{
						_pendingInvitation = std::make_shared<PartyInvitation>(inviteInternal);
					}
				}

				void removeInvitation(const InvitationInternal& invite)
				{
					std::lock_guard<std::recursive_mutex> lg(_invitationsMutex);

					auto it = std::find_if(_invitationsNew.begin(), _invitationsNew.end(), [&invite](const std::shared_ptr<InvitationInternal>& other)
						{
							return &invite == other.get();
						});
					assert(it != _invitationsNew.end());
					_invitationsNew.erase(it);
				}

				pplx::task<void> joinPartyBySceneId(const std::string& sceneId, const std::vector<byte>& userData, const std::unordered_map<std::string, std::string>& userMetadata = {}, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					PartyId partyId;
					partyId.type = PartyId::TYPE_SCENE_ID;
					partyId.id = sceneId;
					return joinParty(partyId, userData, userMetadata, ct);
				}

				std::vector<std::shared_ptr<Platform::IPlatformSupportProvider>> platformProviders() const
				{
					// Retrieve handlers from the client's scope to avoid instantiating them in the partyApi's scope,
					// which could cause cyclic references if providers hold a shared_ptr to PartyApi.
					auto client = _wClient.lock();
					if (client)
					{
						return client->dependencyResolver().resolveAll<Platform::IPlatformSupportProvider>();
					}
					else
					{
						throw std::runtime_error(PartyError::Str::StormancerClientDestroyed);
					}
				}

				void setJoinPartyFromSystemHandler(std::function<pplx::task<bool>(JoinPartyFromSystemArgs)> handler) override
				{
					std::lock_guard<std::recursive_mutex> lock(_partyMutex);

					bool previouslyEmpty = !static_cast<bool>(_joinPartyFromSystemHandler);
					_joinPartyFromSystemHandler = handler;

					// The game has "unsubscribed", so do we
					if (!_joinPartyFromSystemHandler)
					{
						_joinPartyFromSystemSubs.clear();
						return;
					}

					// Subscribe to events the first time this API is called (or if the handler was previously unset)
					if (previouslyEmpty)
					{
						auto providers = platformProviders();
						for (const auto& provider : providers)
						{
							auto wThat = STORM_WEAK_FROM_THIS();
							_joinPartyFromSystemSubs.push_back(provider->subscribeOnJoinPartyRequestedByPlatform([wThat](const Platform::PlatformInvitationRequestContext& ctx)
								{
									if (auto that = wThat.lock())
									{
										that->_dispatcher->post([that, ctx]
											{
												that->onJoinPartyRequestedByPlatform(ctx);
											});
									}
								}));
						}
					}
				}

				std::shared_ptr<ILogger> _logger;
				// This mutex mainly protects the _party member
				mutable std::recursive_mutex _partyMutex;
				std::shared_ptr<pplx::task<std::shared_ptr<PartyContainer>>> _party;
				// This mutex protects the invitations vector, and each individual invitation's API.
				// Recursive mutex needed because the user can call getPendingInvitations() while in a callback where the mutex is already held
				std::recursive_mutex _invitationsMutex;
				std::shared_ptr<IActionDispatcher> _dispatcher;
				std::shared_ptr<Stormancer::GameFinder::GameFinderApi> _gameFinder;
				DependencyScope _scope;
				// Things Party_Impl is subscribed to, that outlive the party scene (e.g GameFinder events)
				std::vector<Subscription> _subscriptions;
				pplx::task<void> _leavePartyTask = pplx::task_from_result();
				std::vector<std::shared_ptr<InvitationInternal>> _invitationsNew;
				PartyGameFinderStatus _gameFinderStatus = PartyGameFinderStatus::SearchStopped;
				// When doing a manual leaveParty(), this will ensure the resulting task completes only when every OnLeavingParty event handler has run.
				pplx::task_completion_event<void> _leavePartyTce;
				// Prevent platform-specific settings updates from overlapping
				pplx::task<void> _platformPartySettingsUpdateTask = pplx::task_from_result();
				pplx::task<void> _platformPartyMembersUpdateTask = pplx::task_from_result();
				std::function<pplx::task<bool>(JoinPartyFromSystemArgs)> _joinPartyFromSystemHandler;
				std::weak_ptr<IClient> _wClient;
				// These subscriptions are separated from the main one because when want to be able to unsub when the user does.
				std::vector<Subscription> _joinPartyFromSystemSubs;
			};
			STORM_WARNINGS_POP;

			class StormancerInvitationProvider : public Platform::IPlatformSupportProvider, public std::enable_shared_from_this<StormancerInvitationProvider>
			{
			public:

				StormancerInvitationProvider(std::shared_ptr<Platform::InvitationMessenger> messenger, std::shared_ptr<Users::UsersApi> users, ILogger_ptr logger)
					: Platform::IPlatformSupportProvider(messenger)
					, _users(users)
					, _logger(logger)
				{
				}

				void initialize()
				{
					std::weak_ptr<StormancerInvitationProvider> wThat = this->shared_from_this();
					_users->setOperationHandler("party.invite", [wThat](Users::OperationCtx& ctx)
						{
							if (auto that = wThat.lock())
							{
								return that->invitationHandler(ctx);
							}
							ctx.request->sendValueTemplated(false);
							return pplx::task_from_result();
						});
				}

				std::string getPlatformName() override { return "stormancer"; }

				pplx::task<PartyId> getPartyId(const PartyId&, pplx::cancellation_token = pplx::cancellation_token::none()) override { assert(false); throw std::runtime_error("stormancer platform support has no PartyId"); }

				pplx::task<void> createOrJoinSessionForParty(const std::string&) override { return pplx::task_from_result(); }

				pplx::task<void> leaveSessionForParty(const std::string&) override { return pplx::task_from_result(); }

				pplx::task<void> kickPlayer(const std::string&) override { return pplx::task_from_result(); }

				pplx::task<void> updateSessionSettings(const PartySettings&) override { return pplx::task_from_result(); }

				pplx::task<void> updateSessionMembers(const MembersUpdate&) override { return pplx::task_from_result(); }

				pplx::task<std::vector<AdvertisedParty>> getAdvertisedParties(pplx::cancellation_token = pplx::cancellation_token::none()) override
				{
					return pplx::task_from_result(std::vector<AdvertisedParty>());
				};

				Subscription subscribeOnJoinPartyRequestedByPlatform(std::function<void(const Platform::PlatformInvitationRequestContext&)>) override { return Subscription{}; }

				bool tryShowSystemInvitationUI(std::shared_ptr<PartyApi>) override { return false; }

			private:

				class StormancerInvitation : public Platform::IPlatformInvitation
				{
				public:

					StormancerInvitation(std::string senderId, std::string sceneId, pplx::task_completion_event<bool> tce, pplx::cancellation_token ct)
						: senderId(std::move(senderId))
						, sceneId(std::move(sceneId))
						, requestTce(tce)
						, requestCt(ct)
					{
						// This cannot be in the initialization list because of a compiler bug which denies access to the protected notifyInvitationCanceled()
						ctRegistration = ct.register_callback([this] { notifyInvitationCanceled(); });
					}

					~StormancerInvitation()
					{
						requestCt.deregister_callback(ctRegistration);
					}

				private:

					pplx::task<PartyId> accept(std::shared_ptr<PartyApi>) override
					{
						requestTce.set(true);
						PartyId partyId;
						partyId.type = PartyId::TYPE_SCENE_ID;
						partyId.id = sceneId;
						return pplx::task_from_result(partyId);
					}

					pplx::task<void> decline(std::shared_ptr<PartyApi>) override
					{
						requestTce.set(false);
						return pplx::task_from_result();
					}

					std::string getSenderId() override
					{
						return senderId;
					}

					std::string getSenderPlatformId() override
					{
						return senderId;
					}

					std::string senderId;
					std::string sceneId;
					pplx::task_completion_event<bool> requestTce;
					pplx::cancellation_token requestCt;
					pplx::cancellation_token_registration ctRegistration;
				};

				pplx::task<void> invitationHandler(Users::OperationCtx& ctx)
				{
					Serializer serializer;
					auto senderId = ctx.originId;
					auto sceneId = serializer.deserializeOne<std::string>(ctx.request->inputStream());
					_logger->log(LogLevel::Trace, "StormancerInvitationProvider::invitationHandler", "Received an invitation: sender=" + senderId + " ; sceneId=" + sceneId);

					pplx::task_completion_event<bool> inviteResponseTce;
					auto invitation = std::make_shared<StormancerInvitation>(senderId, sceneId, inviteResponseTce, ctx.request->cancellationToken());
					notifyInvitationReceived(invitation);

					auto logger = _logger;
					return pplx::create_task(inviteResponseTce)
						.then([ctx, logger](bool response)
							{
								logger->log(LogLevel::Trace, "StormancerInvitationProvider::invitationHandler", "Sending invitation response to user " + ctx.originId, std::to_string(response));
								ctx.request->sendValueTemplated(response);
							});
				}

				std::shared_ptr<Users::UsersApi> _users;
				ILogger_ptr _logger;
			};
		}

		class PartyPlugin : public IPlugin
		{
		public:

			/// <summary>
			/// Plugin-wide revision, to increment every time there is a meaningful change (e.g bugfix...)
			/// </summary>
			/// <remarks>
			/// Unlike protocol versions, its only purpose is to help debugging.
			/// </remarks>
			static constexpr const char* PLUGIN_NAME = "Party";
			static constexpr const char* PLUGIN_REVISION = "2020-08-21.1";
			static constexpr const char* PLUGIN_METADATA_KEY = "stormancer.party.plugin";

			PluginDescription getDescription() override
			{
				return PluginDescription(PLUGIN_NAME, PLUGIN_REVISION);
			}

		private:

			void registerSceneDependencies(ContainerBuilder& builder, std::shared_ptr<Scene> scene) override
			{
				auto version = scene->getHostMetadata(details::PartyService::METADATA_KEY);
				if (!version.empty())
				{
					builder.registerDependency<details::PartyService, Scene>().singleInstance();
				}

				version = scene->getHostMetadata(details::PartyManagementService::METADATA_KEY);
				if (!version.empty())
				{
					builder.registerDependency<details::PartyManagementService, Scene>().singleInstance();
				}
			}

			void sceneCreated(std::shared_ptr<Scene> scene) override
			{
				if (!scene->getHostMetadata(details::PartyService::METADATA_KEY).empty())
				{
					scene->dependencyResolver().resolve<details::PartyService>()->initialize();
				}
			}

			void registerClientDependencies(ContainerBuilder& builder) override
			{
				builder.registerDependency<PartyApi>([](const DependencyScope& dr) {
					auto partyImpl = std::make_shared<details::Party_Impl>(
						dr.resolve<Stormancer::Users::UsersApi>(),
						dr.resolve<ILogger>(),
						dr.resolve<IActionDispatcher>(),
						dr.resolve<GameFinder::GameFinderApi>(),
						dr.resolve<IClient>()
					);
					// initialize() needs weak_from_this(), so it can't be called from Party_Impl's constructor
					partyImpl->initialize();
					return partyImpl;
					}).singleInstance();

					builder.registerDependency<Platform::InvitationMessenger>().singleInstance();

					builder.registerDependency<details::StormancerInvitationProvider>([](const DependencyScope& dr)
						{
							auto provider = std::make_shared<details::StormancerInvitationProvider>(dr.resolve<Platform::InvitationMessenger>(), dr.resolve<Users::UsersApi>(), dr.resolve<ILogger>());
							provider->initialize();
							return provider;
						}).as<Platform::IPlatformSupportProvider>().singleInstance();
			}

			void clientCreated(std::shared_ptr<IClient> client) override
			{
				client->setMetadata(details::PartyService::METADATA_KEY, details::PartyService::PROTOCOL_VERSION);
				client->setMetadata(details::PartyManagementService::METADATA_KEY, details::PartyManagementService::PROTOCOL_VERSION);
				client->setMetadata(PLUGIN_METADATA_KEY, PLUGIN_REVISION);

				auto logger = client->dependencyResolver().resolve<ILogger>();
				logger->log(LogLevel::Info, "PartyPlugin", "Registered Party plugin, revision", PLUGIN_REVISION);
			}
		};
	}
}

MSGPACK_ADD_ENUM(Stormancer::Party::PartyUserStatus);
MSGPACK_ADD_ENUM(Stormancer::Party::MemberDisconnectionReason);
