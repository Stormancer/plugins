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
	namespace Analytics
	{
		
		struct AnalyticsDocument
		{
			/// <summary>
			/// Type of the event
			/// </summary>
			std::string type;

			/// <summary>
			/// Json content of the document.
			/// </summary>
			std::string content;

			/// <summary>
			/// Category
			/// </summary>
			std::string category;

			/// <summary>
			/// timestamp
			/// </summary>
			long event_timestamp;
		
			MSGPACK_DEFINE(type,content,category,event_timestamp)
		};


		class AnalyticsPlugin;

		namespace details
		{
			class AnalyticsService
			{
			public:

				AnalyticsService(std::weak_ptr<Scene> scene, std::shared_ptr<Serializer> serializer)
					: _scene(scene)
					, _serializer(serializer)
				{
				}

				//push analytics
				void pushAnalyticDocuments(std::vector<AnalyticsDocument>& documents)
				{
					auto scene = _scene.lock();
					auto serializer = _serializer;
					scene->send("Analytics.Push", [serializer, &documents](obytestream& s)
					{
						serializer->serialize(s, documents);
					});
				
				}

			

			private:

				std::weak_ptr<Scene> _scene;
				std::shared_ptr<Serializer> _serializer;
			};
		}

		class AnalyticsPlugin;
		class AnalyticsApi : public std::enable_shared_from_this<AnalyticsApi>
		{
			friend class AnalyticsPlugin;
		public:

			AnalyticsApi(std::weak_ptr<IActionDispatcher> wActionDispatcher)
				: _wActionDispatcher(wActionDispatcher)
			{

			}

			~AnalyticsApi() 
			{
				_cts.cancel();
			}

			void pushAnalyticDocuments(std::vector<AnalyticsDocument>& documents)
			{
				std::lock_guard<std::mutex> lg(_mutex);
				for (auto d : documents)
				{
					this->_documents.push_back(d);
				}
				
			}
			void pushAnalyticsDocuments(AnalyticsDocument& document)
			{
				std::lock_guard<std::mutex> lg(_mutex);
				this->_documents.push_back(document);
			}
		

		private:
			void initialize()
			{
				scheduleTryPushAnalytics();
			}

			void tryPushAnalytics()
			{
				std::lock_guard<std::mutex> lg(_mutex);

				auto scene = _wScene.lock();
				if (scene)
				{
					auto service = scene->dependencyResolver().resolve<details::AnalyticsService>();
					service->pushAnalyticDocuments(_documents);
					_documents.clear();
				}
				
			}
			void scheduleTryPushAnalytics()
			{
				if (!_cts.get_token().is_canceled())
				{
					auto now = std::chrono::system_clock::now();
					using namespace std::chrono_literals;
					if (_lastRun + 1s < now)
					{
						_lastRun = now;
						tryPushAnalytics();
					}
					

					if (auto actionDispatcher = _wActionDispatcher.lock())
					{
						auto wSteamImpl = STORM_WEAK_FROM_THIS();
						actionDispatcher->post([wSteamImpl]()
						{
							if (auto steamImpl = wSteamImpl.lock())
							{
								steamImpl->scheduleTryPushAnalytics(); // recursive call.
							}
						});
					}
				}
			}

			void OnAnalyticsSceneConnected(std::shared_ptr<Scene> scene)
			{
				this->_wScene = scene;

			}
			void OnAnalyticsSceneDisconnected()
			{
				_wScene.reset();
			}
			
			std::weak_ptr<Scene> _wScene;
			std::weak_ptr<IActionDispatcher> _wActionDispatcher;
			pplx::cancellation_token_source _cts;
			std::vector<AnalyticsDocument> _documents;
			std::chrono::system_clock::time_point _lastRun = std::chrono::system_clock::time_point::min();
			
			std::mutex _mutex;
		};

		class AnalyticsPlugin : public Stormancer::IPlugin
		{
		public:

			static constexpr const char* PLUGIN_NAME = "Analytics";
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
					auto name = scene->getHostMetadata("stormancer.analytics");

					if (!name.empty())
					{
						builder.registerDependency<Stormancer::Analytics::details::AnalyticsService, Scene, Serializer>().singleInstance();
					}
				}

			}
			void registerClientDependencies(Stormancer::ContainerBuilder& builder) override
			{
				builder.registerDependency<Stormancer::Analytics::AnalyticsApi, IActionDispatcher>().as<Stormancer::Analytics::AnalyticsApi>().singleInstance();
			}

			void clientCreated(std::shared_ptr<IClient> client) override
			{
				client->dependencyResolver().resolve<AnalyticsApi>()->initialize();
			}
			void sceneConnected(std::shared_ptr<Scene> scene) override
			{
				if (scene)
				{
					auto name = scene->getHostMetadata("stormancer.analytics");

					if (!name.empty())
					{
						auto api = scene->dependencyResolver().resolve<AnalyticsApi>();
						api->OnAnalyticsSceneConnected(scene);
					}
				}
			}
			void sceneDisconnecting(std::shared_ptr<Scene> scene) override
			{
				if (scene)
				{
					auto name = scene->getHostMetadata("stormancer.analytics");

					if (!name.empty())
					{
						auto api = scene->dependencyResolver().resolve<AnalyticsApi>();
						api->OnAnalyticsSceneDisconnected();
					}
				}
			}
		};

	}

}

