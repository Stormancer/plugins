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
	namespace PeerConfiguration
	{
		
	

		class PeerConfigurationPlugin;

		namespace details
		{
			class PeerConfigurationService : public std::enable_shared_from_this<PeerConfigurationService>
			{
				friend PeerConfigurationPlugin;

			public:

				PeerConfigurationService(std::shared_ptr<RpcService> rpc)
					: rpc(rpc)
				{
				}

				

				Stormancer::Subscription subscribe(std::function<void(std::string)> callback)
				{
					return peerConfigurationReceived.subscribe(callback);
					if (!isSubscribed)
					{
						isSubscribed = true;
						callback(config);
					}
				}

			private:

				void initialize(std::shared_ptr<Scene> scene)
				{
					std::weak_ptr<PeerConfigurationService> wThat = this->shared_from_this();
					scene->addRoute("peerConfig.update", [wThat](Packetisp_ptr packet) {
						if (auto that = wThat.lock())
						{
							Serializer serializer;
							auto config = serializer.deserializeOne<std::string>(packet->stream);
							
							that->raisePeerConfigurationReceived(config);
						}
					});
				}

				void raisePeerConfigurationReceived(std::string& config)
				{
					if (!isSubscribed)
					{
						this->config = config;
					}
					peerConfigurationReceived(config);
				}

				bool isSubscribed = false;
				Event<std::string> peerConfigurationReceived;
				std::shared_ptr<RpcService> rpc;
				std:: string config;
			};
		}


		/// <summary>
		/// PeerConfiguration API.
		/// </summary>
		class PeerConfigurationApi
		{
			friend PeerConfigurationPlugin;
		public:
			PeerConfigurationApi(std::shared_ptr<ILogger> logger)
				: logger(logger)
			{
			}

			/// <summary>
			/// Subscribes to notifications.
			/// </summary>
			/// <param name="callback"></param>
			/// <param name="includeAlreadyReceived">Emit an event for already received notifications on subscription.</param>
			/// <returns></returns>
			Stormancer::Subscription subscribe(std::function<void(std::string)> callback, bool /*includeAlreadyReceived*/ = true)
			{
				auto sub = configurationReceived.subscribe(callback);
				
				if (isAvailable())
				{
					callback(get());
				}
				return sub;
			}

			/// <summary>
			/// Gets a boolean indicating if the client is connected to the notification service.
			/// </summary>
			/// <returns></returns>
			bool isAvailable()
			{
				return !currentConfiguration.empty();
			}

			std::string get()
			{
				return currentConfiguration;
			}

		private:

			void Initialize(std::shared_ptr<details::PeerConfigurationService> notificationService)
			{
				this->service = notificationService;
				using std::placeholders::_1;
				notificationReceivedByClientSubscription = service->subscribe(std::bind(&PeerConfigurationApi::onPeerConfigurationReceived, this, _1));
			}

			void shutdown()
			{
				//Unsubscribe.
				notificationReceivedByClientSubscription = nullptr;
				service = nullptr;
			}

			void onPeerConfigurationReceived(std::string config)
			{
				currentConfiguration = config;

				configurationReceived(config);
			}

			Stormancer::Subscription notificationReceivedByClientSubscription;
			std::string currentConfiguration;
			Event<std::string> configurationReceived;
			std::shared_ptr<details::PeerConfigurationService> service;
			std::shared_ptr<ILogger> logger;
		};

		class PeerConfigurationPlugin : public Stormancer::IPlugin
		{
		public:

			static constexpr const char* PLUGIN_NAME = "PeerConfiguration";
			static constexpr const char* METADATA_KEY = "stormancer.peerConfig";
			static constexpr const char* PLUGIN_VERSION = "1.0.0";

			PluginDescription getDescription() override
			{
				return PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
			}

		private:
			void clientCreated(std::shared_ptr<IClient> client) override
			{
				client->setMetadata(METADATA_KEY,PLUGIN_VERSION);
			}

			void registerSceneDependencies(Stormancer::ContainerBuilder& builder, std::shared_ptr<Stormancer::Scene> scene) override
			{
				if (scene)
				{
					auto name = scene->getHostMetadata(METADATA_KEY);

					if (!name.empty())
					{
						builder.registerDependency<Stormancer::PeerConfiguration::details::PeerConfigurationService, RpcService>().singleInstance();
					}
				}
			}

			void registerClientDependencies(Stormancer::ContainerBuilder& builder) override
			{
				builder.registerDependency<Stormancer::PeerConfiguration::PeerConfigurationApi, Stormancer::ILogger>().as<PeerConfigurationApi>().singleInstance();
			}

			void sceneCreated(std::shared_ptr<Scene> scene) override
			{
				if (scene)
				{
					auto name = scene->getHostMetadata(METADATA_KEY);

					if (!name.empty())
					{
						auto service = scene->dependencyResolver().resolve<details::PeerConfigurationService>();
						service->initialize(scene);
					}
				}
			}

			void sceneConnected(std::shared_ptr<Scene> scene) override
			{
				if (scene)
				{
					auto name = scene->getHostMetadata(METADATA_KEY);

					if (!name.empty())
					{
						auto service = scene->dependencyResolver().resolve<details::PeerConfigurationService>();
						auto api = scene->dependencyResolver().resolve<PeerConfigurationApi>();
						api->Initialize(service);
					}
				}
			}

			void sceneDisconnecting(std::shared_ptr<Scene> scene) override
			{
				if (scene)
				{
					auto name = scene->getHostMetadata(METADATA_KEY);

					if (!name.empty())
					{

						auto api = scene->dependencyResolver().resolve<PeerConfigurationApi>();
						api->shutdown();
					}
				}
			}
		};
	}
}

