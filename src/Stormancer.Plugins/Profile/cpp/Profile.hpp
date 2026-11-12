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

#include "stormancer/Tasks.h"
#include <string>
#include <unordered_map>
#include <stormancer/msgpack_define.h>


#if !defined(STORM_PLUGIN_IMPL)
#define STORM_PLUGIN_IMPL 0
#endif

namespace Stormancer
{
	namespace Profile
	{
		struct UserProfilePart
		{
			std::string userId;
			std::string lastPlatform;
			std::string pseudo;
			std::string avatar;
			std::unordered_map <std::string, std::string> identities;
			/// <summary>
			/// Platform specific information associated with the player
			/// </summary>
			/// <remarks>
			/// For instance if the player is on Steam. For privacy reasons, this info might depend if the player is on the same platform as the calling player.
			/// { 
			///		"steam":{ "steamid": "xxxx" }
			/// }
			/// </remarks>
			std::unordered_map < std::string, std::unordered_map < std::string, std::string>> platforms;

			/// <summary>
			/// Tries getting a platform id from the platforms object.
			/// </summary>
			/// <param name="platform"></param>
			/// <param name="path"></param>
			/// <param name="idOut"></param>
			/// <remarks>
			/// For instance, to get the steam id on a steam player:
			/// 
			/// `tryGetPlatformId("steam","steamid",output)`
			/// </remarks>
			/// <returns></returns>
			bool tryGetPlatformId(const std::string& platform, const std::string& path, std::string& idOut)
			{
				auto platformIt = platforms.find(platform);

				if (platformIt == platforms.end())
				{
					return false;
				}
				auto& platformData = platformIt->second;
				auto idIt = platformData.find(path);
				if (idIt == platformData.end())
				{
					return false;
				}
				idOut = idIt->second;
				return true;
			}

			template <typename Packer> void msgpack_pack(Packer& pk) const {
				msgpack::type::make_define_map("userId", userId, "lastPlatform", lastPlatform, "pseudo", pseudo, "platforms", platforms, "avatar", avatar, "identities", identities).msgpack_pack(pk);
			}
			void msgpack_unpack(msgpack::object const& o) {
				msgpack::type::make_define_map("userId", userId, "lastPlatform", lastPlatform, "pseudo", pseudo, "platforms", platforms, "avatar", avatar, "identities", identities).msgpack_unpack(o);
			}
			template <typename STRM_MSGPACK_OBJECT> void msgpack_object(STRM_MSGPACK_OBJECT* o, msgpack::zone& z) const {
				msgpack::type::make_define_map("userId", userId, "lastPlatform", lastPlatform, "pseudo", pseudo, "platforms", platforms, "avatar", avatar, "identities", identities).msgpack_object(o, z);
			}
		};
		struct Profile
		{
			std::unordered_map<std::string, std::vector<byte>> data;

			template<typename TPart>
			bool tryGetPartAs(std::string partId, TPart& part)
			{
				Serializer serializer;
				auto it = data.find(partId);
				if (it != data.end())
				{
					serializer.deserialize(std::span<byte>(it->second), part);
					return true;
				}
				else
				{
					return false;
				}
			}
		};

		/// <summary>
		/// Event handler providing extensibility to the profiles plugin.
		/// </summary>
		class IProfileEventHandler
		{
		public:

			virtual ~IProfileEventHandler() = default;

			virtual void onGetProfiles(std::unordered_map<std::string, Profile>&)
			{}

			virtual void onGetProfiles(std::unordered_map<SessionId, Profile>&)
			{}

			virtual void onGetLocalPlayerProfile(Profile& profile, const std::unordered_map<std::string, std::string>& displayOptions)
			{}
		};


		class ProfileApi
		{
		public:

			virtual ~ProfileApi() = default;

			/// <summary>
			/// Gets profiles for a list of users.
			/// </summary>
			/// <remarks>For performance reasons, it is advised to call the method with many user ids, instead of calling the method a lot of times with a single user id.</remarks>
			/// <param name="userIds"></param>
			/// <param name="displayOptions"></param>
			/// <returns></returns>
			virtual pplx::task<std::unordered_map<std::string, Profile>> getProfiles(const std::vector<std::string>& userIds, const std::unordered_map<std::string, std::string>& displayOptions = defaultDisplayOptions(), pplx::cancellation_token cancellationToken = pplx::cancellation_token::none()) = 0;


			/// <summary>
			/// Get the local player profile synchronously
			/// </summary>
			/// <param name="displayOptions"></param>
			/// <returns></returns>
			virtual Profile getLocalPlayerProfile(const std::unordered_map<std::string, std::string>& displayOptions = defaultDisplayOptions()) = 0;

			/// <summary>
			/// Gets profiles for a list of users.
			/// </summary>
			/// <remarks>For performance reasons, it is advised to call the method with many user ids, instead of calling the method a lot of times with a single user id.</remarks>
			/// <param name="sessionIds"></param>
			/// <param name="displayOptions"></param>
			/// <returns></returns>
			virtual pplx::task<std::unordered_map<SessionId, Profile>> getProfiles(const std::vector<SessionId>& sessionIds, const std::unordered_map<std::string, std::string>& displayOptions = defaultDisplayOptions(), pplx::cancellation_token cancellationToken = pplx::cancellation_token::none()) = 0;

			/// <summary>
			/// Gets the user's profile.
			/// </summary>
			/// <param name="userId"></param>
			/// <param name="displayOptions">
			/// A map of options allowing the server to filter the data sent back to the client.
			/// 
			/// The options available depend on the part builders queried on the server. 
			/// By default, parts added using the CustomProfilePart attribute are queries by adding a key with the same part id in the display options.
			/// </param>
			/// <returns></returns>
			virtual pplx::task<Profile> getProfile(const std::string& userId, const std::unordered_map<std::string, std::string>& displayOptions = defaultDisplayOptions(), pplx::cancellation_token cancellationToken = pplx::cancellation_token::none()) = 0;

			/// <summary>
			/// Updates the pseudo stored in the user document.
			/// </summary>
			/// <param name="newPseudonym">The new pseudonym to use.</param>
			/// <remarks>
			/// The actual player pseudonym may be different from the string provided as argument. For instance, mia may become mia#4323 for unicity.
			/// Pseudo generation can be customized on the server.
			/// </remarks>
			/// <returns>A task containing the updated pseudonym.</returns>
			virtual pplx::task<std::string> updateUserHandle(const std::string& newPseudonym, pplx::cancellation_token cancellationToken = pplx::cancellation_token::none()) = 0;

			/// <summary>
			/// Queries user profiles.
			/// </summary>
			/// <param name="pseudoPrefix"></param>
			/// <param name="skip"></param>
			/// <param name="take"></param>
			/// <param name="displayOptions">
			/// A map of options allowing the server to filter the data sent back to the client.
			/// 
			/// The options available depend on the part builders queried on the server. 
			/// By default, parts added using the CustomProfilePart attribute are queries by adding a key with the same part id in the display options.
			/// </param>
			/// <returns></returns>
			virtual pplx::task<std::unordered_map<std::string, Profile>> queryProfiles(const std::string& pseudoPrefix, const int& skip, const int& take, const std::unordered_map<std::string, std::string>& displayOptions = defaultDisplayOptions(), pplx::cancellation_token cancellationToken = pplx::cancellation_token::none()) = 0;

			static const std::unordered_map<std::string, std::string>& defaultDisplayOptions()
			{
				static const std::unordered_map<std::string, std::string> options{ { "user", "summary" } };
				return options;
			}

			/// <summary>
			/// Updates a custom profile part associated with the user.
			/// </summary>
			/// <param name="partId">Id of the part to update.</param>
			/// <param name="profilePartWriter">Function used to write the profile update message.</param>
			/// <param name="version">Version of the part protocol. Version is made available to server side custom part handlers to enable data migration capabilities.</param>
			/// <remarks></remarks>
			/// <returns></returns>
			virtual pplx::task<void> updateCustomProfilePart(const std::string& partId, const std::function<void(BufferWriter&)>& profilePartWriter, const std::string& version, pplx::cancellation_token cancellationToken = pplx::cancellation_token::none()) = 0;

			/// <summary>
			/// Deletes a custom profile part associated with the user.
			/// </summary>
			/// <remarks>The operation is validated on the server and might be refused.</remarks>
			/// <param name="partId"></param>
			/// <returns></returns>
			virtual pplx::task<void> deleteProfilePart(const std::string& partId, pplx::cancellation_token cancellationToken = pplx::cancellation_token::none()) = 0;

			struct MaskProfanityContext
			{
				std::string text;
			};

			virtual void setMaskProfanityHandler(std::function<void(MaskProfanityContext&)> handler) = 0;

			virtual std::function<void(MaskProfanityContext&)> getMaskProfanityHandler() const = 0;
		};
	}
}

#if STORM_PLUGIN_IMPL == 1
#include "stormancer/IPlugin.h"
#include "stormancer/msgpack_define.h"
#include "stormancer/Logger/ILogger.h"
#include "users/Users.hpp"
#include "users/ClientAPI.hpp"
namespace Stormancer
{
	namespace Profile
	{
		namespace details
		{
			struct ProfileDto
			{
				std::unordered_map<std::string, std::vector<byte>> data;
				STRM_MSGPACK_DEFINE(data);
			};
		}



		namespace details
		{
			struct ProfilesResult
			{
				std::unordered_map<std::string, ProfileDto> profiles;
			};

			struct ProfileDtoSessionId
			{
				std::unordered_map<SessionId, std::shared_ptr<std::string>> data;
				STRM_MSGPACK_DEFINE(data);
			};

			struct ProfilesResultSessionId
			{
				std::unordered_map<SessionId, ProfileDto> profiles;
			};

			class ProfileService
			{
			public:

				ProfileService(std::shared_ptr<Scene> scene, std::shared_ptr<IClient> client)
					: _wScene(scene)
					, _wClient(client)
					, _rpcService(scene->dependencyResolver().resolve<RpcService>())
					, _serializer(scene->dependencyResolver().resolve<Serializer>())
					, _logger(scene->dependencyResolver().resolve<ILogger>())
				{}

				~ProfileService()
				{}

				pplx::task<ProfilesResult> getProfiles(const std::vector<std::string>& userIds, const std::unordered_map<std::string, std::string>& displayOptions, pplx::cancellation_token cancellationToken)
				{
					return _rpcService->rpc<std::unordered_map<std::string, ProfileDto>>("Profile.GetProfiles", cancellationToken, userIds, displayOptions)
						.then([wClient = _wClient](std::unordered_map<std::string, ProfileDto> result)
							{
								auto client = wClient.lock();
								if (!client)
								{
									throw ObjectDeletedException("IClient");
								}



								ProfilesResult r;
								r.profiles = result;
								return r;
							});
				}

				pplx::task<ProfilesResultSessionId> getProfiles(const std::vector<SessionId>& sessionIds, const std::unordered_map<std::string, std::string>& displayOptions, pplx::cancellation_token cancellationToken)
				{
					return _rpcService->rpc<std::unordered_map<SessionId, ProfileDto>>("Profile.GetProfilesBySessionIds", cancellationToken, sessionIds, displayOptions)
						.then([wClient = _wClient](std::unordered_map<SessionId, ProfileDto> result)
							{
								auto client = wClient.lock();
								if (!client)
								{
									throw ObjectDeletedException("IClient");
								}


								ProfilesResultSessionId r;
								r.profiles = result;
								return r;
							});
				}

				pplx::task<ProfileDto> getProfile(const std::string& userId, const std::unordered_map<std::string, std::string>& displayOptions, pplx::cancellation_token cancellationToken)
				{
					return getProfiles(std::vector<std::string> { userId }, displayOptions, cancellationToken)
						.then([userId](ProfilesResult profiles)
							{
								if (profiles.profiles.size() == 1)
								{
									return profiles.profiles[userId];
								}
								else
								{
									throw std::runtime_error("No profile");
								}
							});
				}

				pplx::task<std::string> updateUserHandle(const std::string& newHandle, pplx::cancellation_token cancellationToken)
				{
					return _rpcService->rpc<std::string, std::string>("Profile.UpdateUserHandle", cancellationToken, newHandle);
				}

				pplx::task<ProfilesResult> queryProfiles(const std::string& pseudoPrefix, const int& skip, const int& take, const std::unordered_map<std::string, std::string>& displayOptions, pplx::cancellation_token cancellationToken)
				{
					return _rpcService->rpc<std::unordered_map<std::string, ProfileDto>>("Profile.QueryProfiles", cancellationToken, pseudoPrefix, skip, take, displayOptions)
						.then([](std::unordered_map<std::string, ProfileDto> result)
							{
								ProfilesResult r;
								r.profiles = result;
								return r;
							});
				}

				pplx::task<void> updateCustomProfilePart(const std::string& partId, const std::function<void(BufferWriter&)>& profilePartWriter, const std::string& version, pplx::cancellation_token cancellationToken)
				{
					auto serializer = _serializer;
					return _rpcService->rpc("Profile.UpdateCustomProfilePart", cancellationToken, [serializer, partId, profilePartWriter, version](Stormancer::BufferWriter& s) {
						serializer->serialize(s, partId);
						serializer->serialize(s, version);
						profilePartWriter(s);
						});
				}

				pplx::task<void> deleteProfilePart(const std::string& partId)
				{
					return _rpcService->rpc("Profile.DeleteCustomProfilePart", partId);
				}

			private:

				std::weak_ptr<Scene> _wScene;
				std::weak_ptr<IClient> _wClient;
				std::shared_ptr<RpcService> _rpcService;
				std::shared_ptr<Serializer> _serializer;
				std::shared_ptr<Stormancer::ILogger> _logger;
				std::string _logCategory = "Profile";
			};

			class Profiles_Impl : public ClientAPI<Profiles_Impl, ProfileService>, public ProfileApi
			{
			public:

				Profiles_Impl(std::weak_ptr<Users::UsersApi> users, std::weak_ptr<IClient> client, std::shared_ptr<IActionDispatcher> dispatcher)
					: ClientAPI(users, "stormancer.profile")
					, _client(client)
					, _dispatcher(dispatcher)
				{}

				pplx::task<std::unordered_map<std::string, Profile>> getProfiles(const std::vector<std::string>& userIds, const std::unordered_map<std::string, std::string>& displayOptions, pplx::cancellation_token cancellationToken) override
				{
					if (userIds.empty())
					{
						return pplx::task_from_result<std::unordered_map<std::string, Profile>>({});
					}
					return getProfileService(cancellationToken)
						.then([userIds, displayOptions, cancellationToken](std::shared_ptr<ProfileService> gr)
							{
								return gr->getProfiles(userIds, displayOptions, cancellationToken);
							})
						.then([wClient = _client](ProfilesResult profiles)
							{
								std::unordered_map<std::string, Profile> result;
								for (auto& dto : profiles.profiles)
								{
									Profile p;
									p.data = dto.second.data;
									result.emplace(dto.first, p);
								}
								if (auto client = wClient.lock())
								{
									auto profileEventHandlers = client->dependencyResolver().resolveAll<IProfileEventHandler>();
									for (auto& profileEventHandler : profileEventHandlers)
									{
										profileEventHandler->onGetProfiles(result);
									}
								}
								return result;
							}, _dispatcher);
				}

				pplx::task<std::unordered_map<SessionId, Profile>> getProfiles(const std::vector<SessionId>& sessionIds, const std::unordered_map<std::string, std::string>& displayOptions, pplx::cancellation_token cancellationToken) override
				{
					if (sessionIds.empty())
					{
						return pplx::task_from_result<std::unordered_map<SessionId, Profile>>({});
					}
					return getProfileService(cancellationToken)
						.then([sessionIds, displayOptions, cancellationToken](std::shared_ptr<ProfileService> gr)
							{
								return gr->getProfiles(sessionIds, displayOptions, cancellationToken);
							})
						.then([wClient = _client](ProfilesResultSessionId profiles)
							{
								std::unordered_map<SessionId, Profile> result;
								for (auto& dto : profiles.profiles)
								{
									Profile p;
									p.data = dto.second.data;
									result.emplace(dto.first, p);
								}
								if (auto client = wClient.lock())
								{
									auto profileEventHandlers = client->dependencyResolver().resolveAll<IProfileEventHandler>();
									for (auto& profileEventHandler : profileEventHandlers)
									{
										profileEventHandler->onGetProfiles(result);
									}
								}
								return result;
							}, _dispatcher);
				}

				pplx::task<Profile> getProfile(const std::string& userId, const std::unordered_map<std::string, std::string>& displayOptions, pplx::cancellation_token cancellationToken = pplx::cancellation_token::none()) override
				{

					return getProfileService(cancellationToken)
						.then([userId, displayOptions, cancellationToken](std::shared_ptr<ProfileService> gr)
							{
								return gr->getProfile(userId, displayOptions, cancellationToken);
							})
						.then([](ProfileDto profile)
							{
								Profile p;
								p.data = profile.data;
								return p;
							}, _dispatcher);
				}

				pplx::task<std::string> updateUserHandle(const std::string& userIds, pplx::cancellation_token cancellationToken) override
				{
					std::weak_ptr<Users::UsersApi> wUsers = this->_wUsers;
					return getProfileService(cancellationToken)
						.then([userIds, cancellationToken](std::shared_ptr<ProfileService> gr) {
						return gr->updateUserHandle(userIds, cancellationToken);
							})
						.then([wUsers](pplx::task<std::string> t) {
						auto users = wUsers.lock();
						if (!users)
						{
							throw ::Stormancer::ObjectDeletedException("users destroyed.");
						}
						auto pseudo = t.get();
						users->setPseudo(pseudo);
						return pseudo;
							}, _dispatcher);
				}

				pplx::task<std::unordered_map<std::string, Profile>> queryProfiles(const std::string& pseudoPrefix, const int& skip, const int& take, const std::unordered_map<std::string, std::string>& displayOptions, pplx::cancellation_token cancellationToken) override
				{
					return getProfileService(cancellationToken)
						.then([pseudoPrefix, skip, take, displayOptions, cancellationToken](std::shared_ptr<ProfileService> gr) {return gr->queryProfiles(pseudoPrefix, skip, take, displayOptions, cancellationToken); })
						.then([](ProfilesResult profiles) {
						std::unordered_map<std::string, Profile> result;
						for (auto& dto : profiles.profiles)
						{
							Profile p;
							p.data = dto.second.data;
							result.emplace(dto.first, p);
						}
						return result;
							}, _dispatcher);
				}

				pplx::task<void> updateCustomProfilePart(const std::string& partId, const std::function<void(BufferWriter&)>& profilePartWriter, const std::string& version = "1.0.0", pplx::cancellation_token cancellationToken = pplx::cancellation_token::none()) override
				{
					return getProfileService(cancellationToken)
						.then([partId, profilePartWriter, version, cancellationToken](std::shared_ptr<ProfileService> gr) {return gr->updateCustomProfilePart(partId, profilePartWriter, version, cancellationToken); }, _dispatcher);

				}

				pplx::task<void> deleteProfilePart(const std::string& partId, pplx::cancellation_token cancellationToken) override
				{
					return getProfileService(cancellationToken)
						.then([partId](std::shared_ptr<ProfileService> gr) {return gr->deleteProfilePart(partId); }, _dispatcher);
				}

				void setMaskProfanityHandler(std::function<void(MaskProfanityContext&)> handler) override
				{
					_maskProfanityHandler = handler;
				}

				std::function<void(MaskProfanityContext&)> getMaskProfanityHandler() const override
				{
					return _maskProfanityHandler;
				}

				Profile getLocalPlayerProfile(const std::unordered_map<std::string, std::string>& displayOptions = defaultDisplayOptions())
				{

					if (auto client = _client.lock())
					{
						Profile result;

						auto profileEventHandlers = client->dependencyResolver().resolveAll<IProfileEventHandler>();
						for (auto& h : profileEventHandlers)
						{
							h->onGetLocalPlayerProfile(result, displayOptions);
						}

						if (displayOptions.contains("user") && result.data.find("user") == result.data.end())
						{
							UserProfilePart part;
							auto users = LockOrThrow(_wUsers, "scene.partyManagement");
							part.pseudo = users->username();

							Serializer serializer;
							BufferWriter buffer;
							serializer.serialize(buffer, part);
							std::vector<byte> content;
							content.resize(buffer.content().size());
							std::memcpy(content.data(), buffer.content().data(), buffer.content().size());
							result.data.emplace("user", content);
						}
						return result;
					}
					else
					{
						throw Stormancer::ObjectDeletedException("client");
					}
				}

			private:

				pplx::task<std::shared_ptr<ProfileService>> getProfileService(pplx::cancellation_token cancellationToken)
				{
					return this->getService([](auto, auto, auto) {}, [](auto, auto) {}, cancellationToken);
				}

				std::function<void(MaskProfanityContext&)> _maskProfanityHandler = [](MaskProfanityContext& context)
					{
						context.text = "****";
					};

				std::weak_ptr<IClient> _client;
				std::shared_ptr<IActionDispatcher> _dispatcher;
			};
		}

		class ProfilePlugin : public IPlugin
		{
		public:

			static constexpr const char* PLUGIN_NAME = "Profile";
			static constexpr const char* PLUGIN_VERSION = "1.0.0";

			PluginDescription getDescription() override
			{
				return PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
			}

		private:

			void registerSceneDependencies(ContainerBuilder& builder, std::shared_ptr<Scene> scene) override
			{
				if (scene)
				{
					auto name = scene->getHostMetadata("stormancer.profile");
					if (!name.empty())
					{
						builder.registerDependency<details::ProfileService, Scene, IClient>().singleInstance();
					}
				}
			}

			void registerClientDependencies(ContainerBuilder& builder) override
			{
				builder.registerDependency<details::Profiles_Impl, Users::UsersApi, IClient, IActionDispatcher>().as<ProfileApi>().singleInstance();
			}
		};
	}
}

#endif