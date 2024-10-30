#pragma once

#include "Users/ClientAPI.hpp"
#include "Users/Users.hpp"
#include "stormancer/StormancerTypes.h"
#include "stormancer/msgpack_define.h"
#include "stormancer/Event.h"
#include "stormancer/Tasks.h"
#include "stormancer/IPlugin.h"
#include "stormancer/Scene.h"
#include "stormancer/Utilities/PointerUtilities.h"
#include <unordered_map>
#include <functional>
#include <string>
#include <vector>
#include <memory>

namespace Stormancer
{
	namespace Friends
	{
		enum class FriendStatus
		{
			/// <summary>
			/// The user is disconnected.
			/// </summary>
			Disconnected = 0,

			/// <summary>
			/// The user status is set as away, but he's either online or in game.
			/// </summary>
			/// 
			Away = 1,
			/// <summary>
			/// The use is in the game client, connected to the social system.
			/// </summary>
			Connected = 2,
		};

		enum class FriendListStatusConfig
		{
			Online = 0,
			Invisible = 1,
			Away = 2
		};

		struct Friend
		{
			std::vector<Stormancer::Users::UserId> userIds;
			std::unordered_map<std::string, FriendStatus> status;
			std::vector<std::string> tags;
			std::string customData;

			FriendStatus getStatusForPlatform(std::string platform) const
			{
				auto it = status.find(platform);
				if (it != status.end())
				{
					return it->second;
				}
				else
				{
					return FriendStatus::Disconnected;
				}
			}

			FriendStatus getStatus() const
			{
				for (auto s : status)
				{
					if (s.second != FriendStatus::Disconnected)
					{
						return s.second;
					}
				}
				return FriendStatus::Disconnected;
			}

			bool isOnPlatform(const std::string& platform) const
			{
				for (auto& userId : userIds)
				{
					if (userId.platform == platform)
					{
						return true;
					}
				}

				return false;
			}

			MSGPACK_DEFINE(userIds, status, tags, customData)
		};

		enum class FriendListUpdateOperationInternal
		{
			AddOrUpdate = 0,
			Remove = 1,
			UpdateStatus = 2
		};

		struct FriendListUpdateDto
		{
			FriendListUpdateOperationInternal operation;
			Friend data;

			uint64 timestamp;

			MSGPACK_DEFINE(operation, data, timestamp)
		};

		enum class FriendListUpdateOperation
		{
			AddOrUpdate = 0,
			Remove = 1
		};

		/// <summary>
		/// Represents a friend list update event.
		/// </summary>
		struct FriendListUpdatedEvent
		{
			FriendListUpdateOperation operation;
			std::shared_ptr<Friend> value;
		};

		/// <summary>
		/// Abstract class representing the contract for friends event handlers
		/// </summary>
		class IFriendsEventHandler
		{
		public:

			///<summary>
			/// Called by the friend system to listen to platform friend changes.
			///</summary>
			/// <remarks>
			/// This method is called whenever the client connects or reconnects to the friends system. The platform implementation is expected to call the callback
			/// to notify the friend system whenever it needs to post updates to the friend list, for instance to add all platform friends on startup, when friend
			/// status changes or when a  friend is added/removed in the platform.
			/// </remarks>
			virtual Subscription subscribeFriendsChanged(std::function<void(std::vector<FriendListUpdateDto>)> callback) = 0;
		};

		/// <summary>
		/// Represents the result of a get friends operation
		/// </summary>
		struct FriendsResult
		{
			/// <summary>
			/// true if the friends list is ready, false if not.
			/// </summary>
			/// <remarks>
			/// If false, friends is empty.
			/// </remarks>
			bool isReady = false;

			/// <summary>
			/// List of friends indexed by id
			/// </summary>
			/// <remarks>
			/// If the friend list is not ready (isReady = false), the list is empty.
			/// </remarks>
			std::vector<Friend> friends;
		};

		/// <summary>
		/// Friends API
		/// </summary>
		/// <remarks>
		/// 
		/// </remarks>
		class FriendsApi
		{
		public:
			virtual ~FriendsApi() = default;
			/// <summary>
			/// Gets the list of friends.
			/// </summary>
			/// <remarks>
			/// The Friends class maintains locally a list of friends and fires event if it is updated. 
			/// </remarks>
			/// <returns>A FriendsResult object</returns>
			virtual FriendsResult friends() = 0;

			/// <summary>
			/// Gets a boolean value indicating whether the friend list is loaded or not.
			/// </summary>
			/// <returns>True if the friend list is loaded, false otherwise.</returns>
			virtual bool isLoaded() = 0;

			/// <summary>
			/// Connects to the friend service.
			/// </summary>
			/// <returns></returns>
			virtual pplx::task<void> connect() = 0;

			/// <summary>
			/// Invites an user to the friend list.
			/// </summary>
			/// <param name="userId">Id of the user to invite.</param>
			/// <returns>A task that completes when the server acknowledged the invitation request.</returns>
			virtual pplx::task<void> inviteFriend(const Stormancer::Users::UserId& userId) = 0;

			/// <summary>
			/// Answers a friend invitation.
			/// </summary>
			/// <param name="originId">Id of the user that sent the friend invitation.</param>
			/// <param name="accept">If true, accepts the invitation. If false, refuse.</param>
			/// <returns>A task that completes when the server acknowledged the request.</returns>
			virtual pplx::task<void> answerFriendInvitation(const Stormancer::Users::UserId& originId, bool accept = true) = 0;

			/// <summary>
			/// Removes a friend from the list.
			/// </summary>
			/// <param name="userId">Id of the user to remove.</param>
			/// <returns>A task that completes when the server acknowledged the request.</returns>
			virtual pplx::task<void> removeFriend(const Stormancer::Users::UserId& userId) = 0;

			/// <summary>
			/// Updates the current user's status in the friend system.
			/// </summary>
			/// <remarks>
			/// The details argument is made available to server side plugins that react to player status updates to perform additional tasks, for instance:
			/// Modifying or augmenting the status before save, performing operation on other systems like guilds or profiles, etc...
			/// </remarks>
			/// <param name="status">The updated status information.</param>
			/// <param name="details">Optional data to be processed by the server.</param>
			/// <returns></returns>
			virtual pplx::task<void> setStatus(FriendListStatusConfig status, std::string details) = 0;

			/// <summary>
			/// Event fired whenever the friend list content changes.
			/// </summary>
			/// <param name="callback">Callback called when the event is fired.</param>
			/// <returns>An object that controls the lifetime of the event subscription. If all copies of this object are destroyed, the callback is automatically unregistered.</returns>
			virtual Event<FriendListUpdatedEvent>::Subscription subscribeFriendListUpdatedEvent(std::function<void(FriendListUpdatedEvent)> callback) = 0;

			/// <summary>
			/// Ask the friend list for a full refresh. This should be called only in platform events when users are added or removed from the user friend list.
			/// </summary>
			virtual pplx::task<void> refresh() = 0;

			virtual pplx::task<void> block(const Stormancer::Users::UserId& userIdToBlock, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			virtual pplx::task<void> unblock(const Stormancer::Users::UserId& userIdToUnblock, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			virtual pplx::task<std::vector<std::string>> getBlockedList(pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;
		};

		namespace details
		{
			class FriendsService : public std::enable_shared_from_this<FriendsService>
			{
			public:

				FriendsService(std::shared_ptr<Scene> scene, std::shared_ptr<ILogger> logger, std::shared_ptr<Serializer> serializer, std::vector<std::shared_ptr<IFriendsEventHandler>> friendsEventHandlers)
					: _scene(scene)
					, _logger(logger)
					, _rpcService(scene->dependencyResolver().resolve<RpcService>())
					, _serializer(serializer)
					, _friendsEventHandlers(friendsEventHandlers)
				{
				}

				Event<FriendListUpdatedEvent> friendListChanged;
				std::vector<std::shared_ptr<Friend>> friends;

				void initialize()
				{
					std::weak_ptr<FriendsService> wFriendsService = this->shared_from_this();
					_scene.lock()->addRoute<std::vector<FriendListUpdateDto>>("friends.notification", [wFriendsService](std::vector<FriendListUpdateDto> friendUpdates)
						{
							if (auto friendsService = wFriendsService.lock())
							{
								for (auto friendUpdate : friendUpdates)
								{
									friendsService->onFriendNotification(friendUpdate);
								}
								friendsService->_isLoaded = true;
							}
						});
				}

				bool tryGet(const std::vector<Friend>& friendsList, const Stormancer::Users::UserId& userId, Friend& item)
				{
					for (auto& i : friendsList)
					{
						for (auto& uid : i.userIds)
						{
							if (uid == userId)
							{
								item = i;
								return true;
							}
						}
					}
					return false;
				}


				bool tryGet(const std::vector<Friend>& friendsList, const std::vector<Stormancer::Users::UserId> ids, Friend& item)
				{
					for (auto& i : friendsList)
					{
						for (auto& uid : i.userIds)
						{
							for (auto& lookupUid : ids)
							{
								if (uid == lookupUid)
								{
									item = i;
									return true;
								}
							}
						}
					}
					return false;
				}

				bool tryGet(const std::vector<std::shared_ptr<Friend>>& friendsList, const std::vector<Stormancer::Users::UserId> ids, std::shared_ptr<Friend>& item)
				{
					for (auto& i : friendsList)
					{
						for (auto& uid : i->userIds)
						{
							for (auto& lookupUid : ids)
							{
								if (uid == lookupUid)
								{
									item = i;
									return true;
								}
							}
						}
					}
					return false;
				}


				pplx::task<void> subscribe()
				{
					std::weak_ptr<FriendsService> wFriendsService = this->shared_from_this();
					return _rpcService->rpc<void>("Friends.Subscribe").then([wFriendsService]()
						{
							if (auto that = wFriendsService.lock())
							{
								that->subscribeFriendsChangedForAllEventHandlers();
							}
						});
				}


				pplx::task<void> inviteFriend(const Stormancer::Users::UserId& userId)
				{
					return _rpcService->rpc<void, Stormancer::Users::UserId>("friends.invitefriend", userId);
				}

				pplx::task<void> answerFriendInvitation(const Stormancer::Users::UserId& originId, bool accept = true)
				{
					return _rpcService->rpc<void, const Stormancer::Users::UserId&, bool>("friends.acceptfriendinvitation", originId, accept);
				}

				pplx::task<void> removeFriend(const Stormancer::Users::UserId& userId)
				{
					return _rpcService->rpc<void, Stormancer::Users::UserId>("friends.removefriend", userId);
				}

				pplx::task<void> setStatus(FriendListStatusConfig status, std::string details)
				{
					return _rpcService->rpc<void, FriendListStatusConfig, std::string >("friends.setstatus", status, details);
				}

				pplx::task<void> refresh()
				{
					friends.clear();
					_eventHandlerSubscriptions.clear();
					return _rpcService->rpc<void>("Friends.RefreshSubscription").then([wFriendsService = weak_from_this()]()
						{
							if (auto that = wFriendsService.lock())
							{
								that->subscribeFriendsChangedForAllEventHandlers();
							}
						});;
				}




				bool isLoaded()
				{
					return _isLoaded;
				}

				pplx::task<void> block(const Stormancer::Users::UserId& userIdToBlock, pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					return _rpcService->rpc<void, Stormancer::Users::UserId, std::string>("Friends.Block", ct, userIdToBlock, "");
				}

				pplx::task<void> unblock(const Stormancer::Users::UserId& userIdToUnblock, pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					return _rpcService->rpc<void, Stormancer::Users::UserId>("Friends.Unblock", ct, userIdToUnblock);
				}

				pplx::task<std::vector<std::string>> getBlockedList(pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					return _rpcService->rpc<std::vector<std::string>>("Friends.GetBlockedList", ct);
				}

			private:

				void updateFriendList(std::vector<FriendListUpdateDto> updates)
				{
					auto serializer = this->_serializer;
					_scene.lock()->send("Friends.UpdateFriendList", [serializer, updates](obytestream& s)
						{
							serializer->serialize(s, updates);
						});
				}

				void onFriendNotification(const FriendListUpdateDto& update)
				{
					switch (update.operation)
					{
					case FriendListUpdateOperationInternal::Remove:
						onFriendRemove(update);
						break;
					case FriendListUpdateOperationInternal::AddOrUpdate:
						onFriendAddOrUpdate(update);
						break;
					case FriendListUpdateOperationInternal::UpdateStatus:
						onFriendUpdateStatus(update);
						break;
					default:
						_logger->log(LogLevel::Error, "friends", "Unknown friends operation: " + std::to_string((int)update.operation));
						break;
					}
				}

				void onFriendAddOrUpdate(const FriendListUpdateDto& update)
				{

					std::shared_ptr<Friend> fr;
					if (tryGet(friends, update.data.userIds, fr))
					{
						if (fr)
						{
							fr->status = update.data.status;
							fr->tags = update.data.tags;
							fr->userIds = update.data.userIds;
							fr->customData = update.data.customData;
							friendListChanged(FriendListUpdatedEvent{ FriendListUpdateOperation::AddOrUpdate, fr });
						}
					}
					else
					{
						fr = std::make_shared<Friend>(update.data);
						friends.push_back(fr);
						friendListChanged(FriendListUpdatedEvent{ FriendListUpdateOperation::AddOrUpdate, fr });
					}
				}

				void onFriendUpdateStatus(const FriendListUpdateDto& update)
				{
					std::shared_ptr<Friend> fr;
					if (tryGet(friends, update.data.userIds, fr))
					{
						if (fr)
						{
							fr->status = update.data.status;
							friendListChanged(FriendListUpdatedEvent{ FriendListUpdateOperation::AddOrUpdate, fr });
						}
					}
				}

				void onFriendRemove(const FriendListUpdateDto& update)
				{

					for (auto it = friends.begin(); it != friends.end(); it++)
					{
						for (auto& uid : update.data.userIds)
						{
							auto fr = *it;
							for (auto& uid2 : fr->userIds)
							{
								if (uid == uid2)
								{
									friends.erase(it);
									friendListChanged(FriendListUpdatedEvent{ FriendListUpdateOperation::Remove, fr });
									return;
								}
							}
						}
					}

				}

				void subscribeFriendsChangedForAllEventHandlers()
				{
					for (auto& handler : _friendsEventHandlers)
					{
						_eventHandlerSubscriptions.push_back(handler->subscribeFriendsChanged([wFriendsService = weak_from_this()](auto updates)
							{
								if (auto that = wFriendsService.lock())
								{
									that->updateFriendList(updates);
								}
							}));
					}
				}

				std::weak_ptr<Scene> _scene;
				std::shared_ptr<ILogger> _logger;
				std::shared_ptr<RpcService> _rpcService;
				std::shared_ptr<Serializer> _serializer;
				std::vector<std::shared_ptr<IFriendsEventHandler>> _friendsEventHandlers;
				std::vector<Subscription> _eventHandlerSubscriptions;
				bool _isLoaded = false;
			};

			class Friends_Impl : public ClientAPI<Friends_Impl, FriendsService>, public ::Stormancer::Friends::FriendsApi, public ::Stormancer::Users::IAuthenticationEventHandler
			{
			public:

				Friends_Impl(std::weak_ptr<Users::UsersApi> users, std::shared_ptr<ILogger> logger)
					: ClientAPI(users, "stormancer.friends")
					, _logger(logger)
				{
				}

				FriendsResult friends() override
				{
					FriendsResult result;
					if (isLoaded())
					{
						auto service = getFriendService();
						result.isReady = true;

						for (auto ptr : service.get()->friends)
						{
							result.friends.push_back(*ptr);
						}
					}

					return result;
				}

				pplx::task<void> connect()
				{
					return getFriendService().then([](std::shared_ptr<FriendsService>) {});
				}

				bool isLoaded() override
				{
					if (auto users = _wUsers.lock())
					{
						if (users->connectionState() != Stormancer::Users::GameConnectionState::Authenticated)
						{
							return false;
						}
						auto task = getFriendService();
						return task.is_done() && task.get() && task.get()->isLoaded();
					}
					else
					{
						return false;
					}

				}

				pplx::task<void> inviteFriend(const Stormancer::Users::UserId& userId) override
				{
					return getFriendService().then([userId](std::shared_ptr<FriendsService> s) { return s->inviteFriend(userId); });
				}

				pplx::task<void> answerFriendInvitation(const Stormancer::Users::UserId& originId, bool accept = true) override
				{
					return getFriendService().then([originId, accept](std::shared_ptr<FriendsService> s) { return s->answerFriendInvitation(originId, accept); });
				}

				pplx::task<void> removeFriend(const Stormancer::Users::UserId& userId) override
				{
					return getFriendService().then([userId](std::shared_ptr<FriendsService> s) { return s->removeFriend(userId); });
				}

				pplx::task<void> setStatus(FriendListStatusConfig status, std::string details) override
				{
					return getFriendService().then([status, details](std::shared_ptr<FriendsService> s) { return s->setStatus(status, details); });
				}

				virtual pplx::task<void> refresh() override
				{
					return getFriendService().then([](std::shared_ptr<FriendsService> s)
						{
							return s->refresh();
						});
				}

				Subscription subscribeFriendListUpdatedEvent(std::function<void(FriendListUpdatedEvent)> callback) override
				{
					return friendListChanged.subscribe(callback);
				}

				pplx::task<void> block(const Stormancer::Users::UserId& userIdToBlock, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					return getFriendService().then([userIdToBlock, ct](std::shared_ptr<FriendsService> s) { return s->block(userIdToBlock, ct); });
				}

				pplx::task<void> unblock(const Stormancer::Users::UserId& userIdToUnblock, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					return getFriendService().then([userIdToUnblock, ct](std::shared_ptr<FriendsService> s) { return s->unblock(userIdToUnblock, ct); });
				}

				pplx::task<std::vector<std::string>> getBlockedList(pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					return getFriendService().then([ct](std::shared_ptr<FriendsService> s) { return s->getBlockedList(ct); });
				}

				Event<FriendListUpdatedEvent> friendListChanged;

			private:

				pplx::task<std::shared_ptr<FriendsService>> getFriendService()
				{
					auto initializer = [](auto that, auto friends, auto /*scene*/)
						{
							auto wThat = that->weak_from_this();
							that->_friendListChangedSubscription = friends->friendListChanged.subscribe([wThat](FriendListUpdatedEvent ev)
								{
									auto that = wThat.lock();
									if (!that)
									{
										throw ObjectDeletedException("Friends");
									}

									that->friendListChanged(ev);
								});
						};

					auto cleanup = [](auto that, auto /*type*/)
						{
							that->_friendListChangedSubscription = nullptr;
						};

					auto logger = _logger;
					auto result = this->getService(initializer, cleanup);

					//observe the possible exception
					result.then([logger](pplx::task<std::shared_ptr<FriendsService>> task)
						{
							try
							{
								task.get();
							}
							catch (const std::exception& ex)
							{
								logger->log(LogLevel::Debug, "friends", "Could not get friends service", ex.what());
							}
						});

					return result;
				}

				Event<FriendListUpdatedEvent>::Subscription _friendListChangedSubscription;

				std::shared_ptr<ILogger> _logger;
			};

		}

		class FriendsPlugin : public IPlugin
		{
		public:

			static constexpr const char* PLUGIN_NAME = "Friends";
			static constexpr const char* PLUGIN_VERSION = "2.0.0";
			static constexpr const char* METADATA_KEY = "stormancer.friends";

			PluginDescription getDescription() override
			{
				return PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
			}

		private:

			void registerClientDependencies(ContainerBuilder& builder) override
			{
				builder.registerDependency<details::Friends_Impl, Users::UsersApi, ILogger>().as<FriendsApi>().singleInstance();
			}

			void registerSceneDependencies(ContainerBuilder& builder, std::shared_ptr<Scene> scene) override
			{
				auto name = scene->getHostMetadata(METADATA_KEY);
				if (name.length() > 0)
				{
					builder.registerDependency<details::FriendsService, Scene, ILogger, Serializer, ContainerBuilder::All<IFriendsEventHandler>>().singleInstance();
				}
			}

			void sceneCreated(std::shared_ptr<Scene> scene) override
			{
				auto name = scene->getHostMetadata(METADATA_KEY);
				if (!name.empty())
				{
					scene->dependencyResolver().resolve<details::FriendsService>()->initialize();
				}
			}

			void sceneConnected(std::shared_ptr<Scene> scene) override
			{
				auto name = scene->getHostMetadata(METADATA_KEY);
				if (name.length() > 0)
				{
					scene->dependencyResolver().resolve<details::FriendsService>()->subscribe();
				}
			}
		};
	}
}

MSGPACK_ADD_ENUM(Stormancer::Friends::FriendListUpdateOperationInternal);
MSGPACK_ADD_ENUM(Stormancer::Friends::FriendStatus);
MSGPACK_ADD_ENUM(Stormancer::Friends::FriendListStatusConfig);
