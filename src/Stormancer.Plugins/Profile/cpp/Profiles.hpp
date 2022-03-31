#pragma once

#include "stormancer/Tasks.h"
#include "stormancer/IPlugin.h"
#include "stormancer/msgpack_define.h"
#include "Users/Users.hpp"
#include "Users/ClientAPI.hpp"
#include <unordered_map>
#include <list>

namespace Stormancer
{
	namespace Profiles
	{

		struct Profile
		{
			std::unordered_map<std::string, std::string> data;
		};

		class ProfilesApi
		{
		public:

			virtual ~ProfilesApi() = default;

			/// <summary>
			/// Gets profiles for a list of users.
			/// </summary>
			/// <remarks>For performance reasons, it is advised to call the method with many user ids, instead of calling the method a lot of times with a single user id.</remarks>
			/// <param name="userIds"></param>
			/// <param name="displayOptions"></param>
			/// <returns></returns>
			virtual pplx::task<std::unordered_map<std::string, Profile>> getProfilesByUserIds(const std::list<std::string>& userIds, const std::unordered_map<std::string, std::string>& displayOptions = defaultDisplayOptions()) = 0;

			/// <summary>
			/// Gets profiles for a list of users.
			/// </summary>
			/// <remarks>For performance reasons, it is advised to call the method with many user ids, instead of calling the method a lot of times with a single user id.</remarks>
			/// <param name="sessionIds"></param>
			/// <param name="displayOptions"></param>
			/// <returns></returns>
			virtual pplx::task<std::unordered_map<std::string, Profile>> getProfilesBySessionIds(const std::list<std::string>& sessionIds, const std::unordered_map<std::string, std::string>& displayOptions = defaultDisplayOptions()) = 0;


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
			virtual pplx::task<Profile> getProfile(const std::string& userId, const std::unordered_map<std::string, std::string>& displayOptions = defaultDisplayOptions()) = 0;

			/// <summary>
			/// Updates the pseudo stored in the user document.
			/// </summary>
			/// <param name="newPseudonym">The new pseudonym to use.</param>
			/// <remarks>
			/// The actual player pseudonym may be different from the string provided as argument. For instance, mia may become mia#4323 for unicity.
			/// Pseudo generation can be customized on the server.
			/// </remarks>
			/// <returns>A task containing the updated pseudonym.</returns>
			virtual pplx::task<std::string> updateUserHandle(const std::string& newPseudonym) = 0;



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
			virtual pplx::task<std::unordered_map<std::string, Profile>> queryProfiles(const std::string& pseudoPrefix, const int& skip, const int& take, const std::unordered_map<std::string, std::string>& displayOptions = defaultDisplayOptions()) = 0;

			static const std::unordered_map<std::string, std::string>& defaultDisplayOptions()
			{
				static const std::unordered_map<std::string, std::string> options{ { "character", "details" } };
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
			virtual pplx::task<void> updateCustomProfilePart(const std::string& partId, const Stormancer::StreamWriter& profilePartWriter, const std::string& version) = 0;

			/// <summary>
			/// Deletes a custom profile part associated with the user.
			/// </summary>
			/// <remarks>The operation is validated on the server and might be refused.</remarks>
			/// <param name="partId"></param>
			/// <returns></returns>
			virtual pplx::task<void> deleteProfilePart(const std::string& partId) = 0;
		};


		namespace details
		{
			struct ProfileDto
			{
				std::unordered_map<std::string, std::string> data;
				MSGPACK_DEFINE(data);
			};

			struct ProfilesResult
			{
				std::unordered_map<std::string, ProfileDto> profiles;
			};

			class ProfileService
			{
			public:
				ProfileService(std::shared_ptr<Scene> scene)
					: _scene(scene)
					, _rpcService(scene->dependencyResolver().resolve<RpcService>())
					, _logger(scene->dependencyResolver().resolve<ILogger>())
					, _serializer(scene->dependencyResolver().resolve<Serializer>())
				{
				}

				~ProfileService()
				{
				}

				pplx::task<ProfilesResult> getProfilesByUserIds(const std::list<std::string>& userIds, const std::unordered_map<std::string, std::string>& displayOptions)
				{
					return _rpcService->rpc<std::unordered_map<std::string, ProfileDto>>("Profile.GetProfiles", userIds, displayOptions)
						.then([](std::unordered_map<std::string, ProfileDto> result)
							{
								ProfilesResult r;
								r.profiles = result;
								return r;
							});
				}

				pplx::task<ProfilesResult> getProfilesBySessionIds(const std::list<std::string>& userIds, const std::unordered_map<std::string, std::string>& displayOptions)
				{
					return _rpcService->rpc<std::unordered_map<std::string, ProfileDto>>("Profile.GetProfilesBySessionIds", userIds, displayOptions)
						.then([](std::unordered_map<std::string, ProfileDto> result)
							{
								ProfilesResult r;
								r.profiles = result;
								return r;
							});
				}

				pplx::task<ProfileDto> getProfile(const std::string& userId, const std::unordered_map<std::string, std::string>& displayOptions)
				{
					return getProfilesByUserIds(std::list<std::string> { userId }, displayOptions)
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

				pplx::task<std::string> updateUserHandle(const std::string& newHandle)
				{
					return _rpcService->rpc<std::string, std::string>("Profile.UpdateUserHandle", newHandle);
				}

				pplx::task<ProfilesResult> queryProfiles(const std::string& pseudoPrefix, const int& skip, const int& take, const std::unordered_map<std::string, std::string>& displayOptions)
				{
					return _rpcService->rpc<std::unordered_map<std::string, ProfileDto>>("Profile.QueryProfiles", pseudoPrefix, skip, take, displayOptions)
						.then([](std::unordered_map<std::string, ProfileDto> result)
							{
								ProfilesResult r;
								r.profiles = result;
								return r;
							});
				}
				pplx::task<void> updateCustomProfilePart(const std::string& partId, const Stormancer::StreamWriter& profilePartWriter, const std::string& version)
				{
					auto serializer = _serializer;
					return _rpcService->rpc("Profile.UpdateCustomProfilePart", [serializer, partId, profilePartWriter, version](Stormancer::obytestream& s) {
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
				std::weak_ptr<Scene> _scene;
				std::shared_ptr<RpcService> _rpcService;
				std::shared_ptr<Serializer> _serializer;
				std::shared_ptr<Stormancer::ILogger> _logger;
				std::string _logCategory = "Profile";
			};

			class Profiles_Impl : public ClientAPI<Profiles_Impl, ProfileService>, public ProfilesApi
			{
			public:
				Profiles_Impl(std::weak_ptr<Users::UsersApi> users)
					: ClientAPI(users, "stormancer.profiles")
				{
				}

				pplx::task<std::unordered_map<std::string, Profile>> getProfilesByUserIds(const std::list<std::string>& userIds, const std::unordered_map<std::string, std::string>& displayOptions) override
				{
					return getProfileService()
						.then([userIds, displayOptions](std::shared_ptr<ProfileService> gr)
							{
								return gr->getProfilesByUserIds(userIds, displayOptions);
							})
						.then([](ProfilesResult profiles)
							{
								std::unordered_map<std::string, Profile> result;
								for (auto& dto : profiles.profiles)
								{
									Profile p;
									p.data = dto.second.data;
									result.emplace(dto.first, p);
								}
								return result;
							});
				}

				pplx::task<std::unordered_map<std::string, Profile>> getProfilesBySessionIds(const std::list<std::string>& userIds, const std::unordered_map<std::string, std::string>& displayOptions) override
				{
					return getProfileService()
						.then([userIds, displayOptions](std::shared_ptr<ProfileService> gr)
							{
								return gr->getProfilesBySessionIds(userIds, displayOptions);
							})
						.then([](ProfilesResult profiles)
							{
								std::unordered_map<std::string, Profile> result;
								for (auto& dto : profiles.profiles)
								{
									Profile p;
									p.data = dto.second.data;
									result.emplace(dto.first, p);
								}
								return result;
							});
				}

				pplx::task<Profile> getProfile(const std::string& userId, const std::unordered_map<std::string, std::string>& displayOptions) override
				{
					return getProfileService()
						.then([userId, displayOptions](std::shared_ptr<ProfileService> gr)
							{
								return gr->getProfile(userId, displayOptions);
							})
						.then([](ProfileDto profile)
							{
								Profile p;
								p.data = profile.data;
								return p;
							});
				}

				pplx::task<std::string> updateUserHandle(const std::string& userIds) override
				{
					std::weak_ptr<Users::UsersApi> wUsers = this->_wUsers;
					return getProfileService()
						.then([userIds](std::shared_ptr<ProfileService> gr) {
						return gr->updateUserHandle(userIds);
							})
						.then([wUsers](pplx::task<std::string> t) {
								auto users = wUsers.lock();
								if (!users)
								{
									throw Stormancer::ObjectDeletedException("users destroyed.");
								}
								auto pseudo = t.get();
								users->setPseudo(pseudo);
								return pseudo;
							});
				}

				pplx::task<std::unordered_map<std::string, Profile>> queryProfiles(const std::string& pseudoPrefix, const int& skip, const int& take, const std::unordered_map<std::string, std::string>& displayOptions) override
				{
					return getProfileService()
						.then([pseudoPrefix, skip, take, displayOptions](std::shared_ptr<ProfileService> gr) {return gr->queryProfiles(pseudoPrefix, skip, take, displayOptions); })
						.then([](ProfilesResult profiles) {
						std::unordered_map<std::string, Profile> result;
						for (auto& dto : profiles.profiles)
						{
							Profile p;
							p.data = dto.second.data;
							result.emplace(dto.first, p);
						}
						return result;
							});
				}

				pplx::task<void> updateCustomProfilePart(const std::string& partId, const Stormancer::StreamWriter& profilePartWriter, const std::string& version = "1.0.0") override
				{
					return getProfileService()
						.then([partId, profilePartWriter, version](std::shared_ptr<ProfileService> gr) {return gr->updateCustomProfilePart(partId, profilePartWriter, version); });

				}

				pplx::task<void> deleteProfilePart(const std::string& partId)
				{
					return getProfileService()
						.then([partId](std::shared_ptr<ProfileService> gr) {return gr->deleteProfilePart(partId); });
				}
			private:
				pplx::task<std::shared_ptr<ProfileService>> getProfileService()
				{
					return this->getService();
				}
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
					auto name = scene->getHostMetadata("stormancer.profiles");
					if (!name.empty())
					{
						builder.registerDependency<details::ProfileService, Scene>().singleInstance();
					}
				}
			}

			void registerClientDependencies(ContainerBuilder& builder) override
			{
				builder.registerDependency<details::Profiles_Impl, Users::UsersApi>().as<ProfilesApi>().singleInstance();
			}
		};
	}
}