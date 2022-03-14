#pragma once
#include "Users/Users.hpp"
#include "stormancer/IPlugin.h"
#include "stormancer/msgpack_define.h"

namespace Stormancer
{
	namespace Limits
	{
		class ConnectionQueuePlugin;
		namespace details
		{
			class ConnectionQueueService : public std::enable_shared_from_this<ConnectionQueueService>
			{
				friend ConnectionQueuePlugin;
			public:

				ConnectionQueueService(std::shared_ptr<Serializer> serializer)
					:serializer(serializer)
				{
				}

				int getRank()
				{
					return _rank;
				}



			private:
				void initialize(std::shared_ptr<Scene> scene)
				{
					std::weak_ptr<ConnectionQueueService> wThat = this->shared_from_this();
					scene->addRoute("Queue.UpdateRank", [wThat](Packetisp_ptr packet)
					{
						if (auto that = wThat.lock())
						{
							auto newRank = that->serializer->deserializeOne<int>(packet->stream);
							if (that->_rank != newRank)
							{
								that->_rank = newRank;
							}
						}
					}, Scene::RouteOptions());
				}

				int _rank = -1;

				std::shared_ptr<Serializer> serializer;
			};
		}
		class ConnectionQueue
		{
			friend ConnectionQueuePlugin;
		public:
			ConnectionQueue(std::shared_ptr<Users::UsersApi> users)
				: users(users)
			{

			}
			bool isInQueue()
			{
				return !_service.expired() && users->connectionState() == Users::GameConnectionState::Connecting;
			}
			int getRank()
			{
				auto service = _service.lock();
				if (service && isInQueue())
				{
					return service->getRank();
				}
				else //Not in queue.
				{
					return -1;
				}

			}

		private:
			std::shared_ptr<Users::UsersApi> users;
			std::weak_ptr<details::ConnectionQueueService> _service;
		};


		class ConnectionQueuePlugin :public IPlugin
		{
		public:
			static constexpr const char* PLUGIN_NAME = "stormancer.server.plugins.limits.queue";

			static constexpr const char* PLUGIN_VERSION = "1.0.0";

			PluginDescription getDescription() override
			{
				return PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
			}
		private:
			void registerSceneDependencies(Stormancer::ContainerBuilder& builder, std::shared_ptr<Stormancer::Scene> scene) override
			{
				auto name = scene->getHostMetadata(PLUGIN_NAME);
				if (name.length() > 0)
				{
					builder.registerDependency<details::ConnectionQueueService, Serializer>().singleInstance();
				}
			}

			void sceneCreated(std::shared_ptr<Scene> scene) override
			{
				auto name = scene->getHostMetadata(PLUGIN_NAME);
				if (name.length() > 0)
				{
					auto service = scene->dependencyResolver().resolve<details::ConnectionQueueService>();
					service->initialize(scene);
					scene->dependencyResolver().resolve<ConnectionQueue>()->_service = service;
				}
			}

		

			void sceneDisconnecting(std::shared_ptr<Scene> scene) override
			{
				auto name = scene->getHostMetadata(PLUGIN_NAME);
				if (name.length() > 0)
				{
					scene->dependencyResolver().resolve<ConnectionQueue>()->_service.reset();
				}
			}

			void registerClientDependencies(ContainerBuilder& builder) override
			{
				builder.registerDependency<ConnectionQueue, Users::UsersApi>().as<ConnectionQueue>().singleInstance();
			}
		};
	}
}