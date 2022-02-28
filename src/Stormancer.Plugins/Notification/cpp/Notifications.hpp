// MIT License
//
// Copyright (c) 2020 Stormancer
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
#include "Users/ClientAPI.hpp"
#include "stormancer/IPlugin.h"


namespace Stormancer
{
	namespace Notifications
	{
		/// <summary>
		/// Type of acknowledgement for a notification.
		/// </summary>
		/// <remarks>
		/// An unacknowledged notification is resent the next time the user connects to the server.
		/// </remarks>
		enum class InAppNotificationDismissalType : int8
		{
			/// <summary>
			/// No acknowledgement. If the user is not connected at the time of notification creation, they will never receive it.
			/// </summary>
			None = 0,

			/// <summary>
			/// Automatic acknowledgement when the notification is sent to the user. 
			/// </summary>
			OnSend = 1,

			/// <summary>
			/// Automatic acknowledgement when the notification is handled by a subscriber in the client.
			/// If the client receives the notification but no code subscribes to notifications, 
			/// those created using this acknowledgement mode will be sent again on reconnection. 
			/// </summary>
			OnRead = 2,

			/// <summary>
			/// The program muse manually acknowledge the notification to prevent resend on reconnection.
			/// </summary>
			ByUser = 3
		};

		/// <summary>
		/// Represents a notification.
		/// </summary>
		struct InAppNotification
		{
			/// <summary>
			/// Id of the notification.
			/// </summary>
			std::string id;

			/// <summary>
			/// Type of the notification.
			/// </summary>
			std::string type;
			std::string userId;

			/// <summary>
			/// Message in the notification.
			/// </summary>
			std::string message;

			/// <summary>
			/// Additionnal custom data.
			/// </summary>
			std::string data;

			/// <summary>
			/// Notification's creation date.
			/// </summary>
			int64 createdOn;

			/// <summary>
			/// Should the notification expire?
			/// </summary>
			bool shouldExpire;

			/// <summary>
			/// Notification's expiration date.
			/// </summary>
			int64 expirationDate;

			/// <summary>
			/// Notification dismissal mode.
			/// </summary>
			InAppNotificationDismissalType dismissalMode;

			/// <summary>
			/// Possible actions offered by the notification on dismissal.
			/// </summary>
			std::vector<std::string> dismissalActions;

			MSGPACK_DEFINE(id, type, userId, message, data, createdOn, shouldExpire, expirationDate, dismissalMode, dismissalActions);
		};

		class NotificationsPlugin;

		namespace details
		{
			class NotificationsService : public std::enable_shared_from_this<NotificationsService>
			{
				friend NotificationsPlugin;

			public:

				NotificationsService(std::shared_ptr<RpcService> rpc)
					: rpc(rpc)
				{
				}

				pplx::task<void> dismissNotification(const std::string& notificationId)
				{
					return rpc->rpc<void, std::string>("inappnotification.acknowledgenotification", notificationId);
				}

				Stormancer::Subscription subscribe(std::function<void(std::vector<InAppNotification>)> callback)
				{
					auto subscription = notificationReceived.subscribe(callback);
					if (!isSubscribed)
					{
						isSubscribed = true;
						callback(pendingNotifications);
					}
					return subscription;
				}
				
			private:

				void initialize(std::shared_ptr<Scene> scene)
				{
					std::weak_ptr<NotificationsService> wThat = this->shared_from_this();
					scene->addRoute("inappnotification.push", [wThat](Packetisp_ptr packet) {
						if (auto that = wThat.lock())
						{
							Serializer serializer;
							auto notification = serializer.deserializeOne<InAppNotification>(packet->stream);
							std::vector<InAppNotification> notifications;
							notifications.push_back(notification);
							that->raiseNotificationReceived(notifications);
						}
					});
				}

				void raiseNotificationReceived(std::vector<InAppNotification>& notifications)
				{
					if (!isSubscribed)
					{
						for (auto& n : notifications)
						{
							pendingNotifications.push_back(n);
						}
					}
					notificationReceived(notifications);
				}

				bool isSubscribed = false;
				Event<std::vector<InAppNotification>> notificationReceived;
				std::shared_ptr<RpcService> rpc;
				std::vector<InAppNotification> pendingNotifications;
			};
		}


		/// <summary>
		/// Notifications API.
		/// </summary>
		class NotificationsApi
		{
			friend NotificationsPlugin;
		public:
			NotificationsApi(std::shared_ptr<ILogger> logger)
				: logger(logger)
			{
			}

			/// <summary>
			/// Subscribes to notifications.
			/// </summary>
			/// <param name="callback"></param>
			/// <param name="includeAlreadyReceived">Emit an event for already received notifications on subscription.</param>
			/// <returns></returns>
			Stormancer::Subscription subscribe(std::function<void(std::vector<InAppNotification>)> callback, bool /*includeAlreadyReceived*/ = true)
			{
				auto sub = notificationReceived.subscribe(callback);
				auto current = get();
				if (current.size() > 0)
				{
					callback(get());
				}
				return sub;
			}

			/// <summary>
			/// Gets a boolean indicating if the client is connected to the notification service.
			/// </summary>
			/// <returns></returns>
			bool available()
			{
				return service != nullptr;
			}

			std::vector<InAppNotification> get()
			{
				std::vector<InAppNotification> result;
				for (auto& notification : notifications)
				{
					result.push_back(notification.second);
				}

				std::sort(result.begin(), result.end(), [](InAppNotification& first, InAppNotification& second) {
					return first.createdOn > second.createdOn; //Order descending
				});
				return result;
			}

			/// <summary>
			/// Acknowledges that a notification was read. The program shall call this function once the user has been presented the notification. 
			/// </summary>
			/// <param name="notificationId"></param>
			bool setAsread(const std::string& notificationId)
			{
				auto it = notifications.find(notificationId);
				if (it == notifications.end())
				{
					return false;
				}

				if (it->second.dismissalMode == InAppNotificationDismissalType::OnRead)
				{
					if (service == nullptr)
					{
						return false;
					}

					service->dismissNotification(notificationId).then([logger = this->logger, notificationId](pplx::task<void> t)
					{
						try
						{
							t.get();
						}
						catch (std::exception& ex)
						{
							logger->log(LogLevel::Error, "notifications", "An error occured while dismissing notification " + notificationId, ex);
						}

					});
				}

				return true;
			}

			/// <summary>
			/// Dismisses a notification and definitely remove it.
			/// </summary>
			/// <param name="notificationId">Id of the notification to acknowledge.</param>
			/// <param name="action">Optional action id to perform on the server on dismissal. Only has effect on notifications with "byUser" acknowledgment mode.</param>
			/// <returns>A task that completes when the operations is complete..</returns>
			bool dismiss(const std::string& notificationId, const std::string& /*action*/ = "")
			{
				auto it = notifications.find(notificationId);
				if (it == notifications.end())
				{
					return false;
				}

				notifications.erase(notificationId);

				if (it->second.dismissalMode == InAppNotificationDismissalType::ByUser)
				{
					if (service == nullptr)
					{
						return false;
					}
					service->dismissNotification(notificationId)
						.then([logger = this->logger, notificationId](pplx::task<void> t)
					{
						try
						{
							t.get();
						}
						catch (std::exception& ex)
						{
							logger->log(LogLevel::Error, "notifications", "An error occured while dismissing notification " + notificationId, ex);
						}
					});
				}

				return true;
			}

		private:

			void Initialize(std::shared_ptr<details::NotificationsService> notificationService)
			{
				this->service = notificationService;
				using std::placeholders::_1;
				notificationReceivedByClientSubscription = service->subscribe(std::bind(&NotificationsApi::onNotificationsReceived, this, _1));
			}

			void shutdown()
			{
				//Unsubscribe.
				notificationReceivedByClientSubscription = nullptr;
				service = nullptr;
			}

			void onNotificationsReceived(std::vector<InAppNotification> pendingNotifications)
			{
				std::vector<InAppNotification> newNotifications;
				for (auto& notification : pendingNotifications)
				{
					if (this->notifications.find(notification.id) == this->notifications.end())
					{
						this->notifications[notification.id] = notification;
						newNotifications.push_back(notification);
					}
				}

				if (newNotifications.size() > 0)
				{
					notificationReceived(newNotifications);
				}
			}

			Stormancer::Subscription notificationReceivedByClientSubscription;
			std::unordered_map<std::string, InAppNotification> notifications;
			Event< std::vector<InAppNotification>> notificationReceived;
			std::shared_ptr<details::NotificationsService> service;
			std::shared_ptr<ILogger> logger;
		};

		class NotificationsPlugin : public Stormancer::IPlugin
		{
		public:

			static constexpr const char* PLUGIN_NAME = "Notifications";
			static constexpr const char* PLUGIN_VERSION = "1.0.0";

			PluginDescription getDescription() override
			{
				return PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
			}

		private:

			void registerSceneDependencies(Stormancer::ContainerBuilder& builder, std::shared_ptr<Stormancer::Scene> scene) override
			{
				if (scene)
				{
					auto name = scene->getHostMetadata("stormancer.inappnotification");

					if (!name.empty())
					{
						builder.registerDependency<Stormancer::Notifications::details::NotificationsService, RpcService>().singleInstance();
					}
				}
			}

			void registerClientDependencies(Stormancer::ContainerBuilder& builder) override
			{
				builder.registerDependency<Stormancer::Notifications::NotificationsApi, Stormancer::ILogger>().as<NotificationsApi>().singleInstance();
			}

			void sceneCreated(std::shared_ptr<Scene> scene) override
			{
				if (scene)
				{
					auto name = scene->getHostMetadata("stormancer.inappnotification");

					if (!name.empty())
					{
						auto service = scene->dependencyResolver().resolve<details::NotificationsService>();
						service->initialize(scene);
					}
				}
			}

			void sceneConnected(std::shared_ptr<Scene> scene) override
			{
				if (scene)
				{
					auto name = scene->getHostMetadata("stormancer.inappnotification");

					if (!name.empty())
					{
						auto service = scene->dependencyResolver().resolve<details::NotificationsService>();
						auto api = scene->dependencyResolver().resolve<NotificationsApi>();
						api->Initialize(service);
					}
				}
			}

			void sceneDisconnecting(std::shared_ptr<Scene> scene) override
			{
				if (scene)
				{
					auto name = scene->getHostMetadata("stormancer.inappnotification");

					if (!name.empty())
					{
						
						auto api = scene->dependencyResolver().resolve<NotificationsApi>();
						api->shutdown();
					}
				}
			}
		};
	}
}

MSGPACK_ADD_ENUM(Stormancer::Notifications::InAppNotificationDismissalType);
