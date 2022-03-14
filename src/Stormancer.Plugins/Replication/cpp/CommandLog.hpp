#pragma once
#include "stormancer/IPlugin.h"
#include "stormancer/Scene.h"
#include "stormancer/RPC/RpcService.h"
#include "stormancer/Exceptions.h"
#include "stormancer/Tasks.h"

#include <functional>

namespace Stormancer
{
	namespace CommandLog
	{
		class CommandLogPlugin;

		struct LogEntry
		{
			int id;
			std::string type;
			std::vector<byte> content;

			MSGPACK_DEFINE(id, type, content)
		};
		struct SyncRequest
		{
			std::vector<LogEntry> logEntries;
			MSGPACK_DEFINE(logEntries);
		};

		struct SyncResponse
		{
			int lastLogId;
			MSGPACK_DEFINE(lastLogId);
		};

		struct CommandReceivedEvent
		{
			bool accepted = true;
			std::weak_ptr<Scene> scene;
			LogEntry entry;
		};

		namespace details
		{
			class CommandLogService : public std::enable_shared_from_this<CommandLogService>
			{
				friend class CommandLogPlugin;
			public:
				CommandLogService(std::shared_ptr<Scene> scene, std::shared_ptr<RpcService> rpc)
				{
					this->wRpc = rpc;
					this->wScene = scene;
				}

				void subscribeOnCommandReceived(::std::function<void(CommandReceivedEvent&)> callback)
				{
					std::lock_guard<std::mutex> guard(_mutex);
					for (auto entry : _logEntries)
					{
						CommandReceivedEvent evt;
						evt.entry = entry;
						evt.scene = wScene;

						callback(evt);

					}

					//We are supposed to only subscribe once.
					//By storing the subscription in the service, we will automatically destroy it with the scene.
					_apiSubscription = onCommandReceived.subscribe(callback);
				}

				pplx::task<bool> addCommandToLog(std::string type, std::vector<byte> data)
				{
					if (auto rpc = wRpc.lock())
					{
						return rpc->rpc<bool>("Replication.AddCommand", type, data, getLastLogEntry());
					}
					else
					{
						return pplx::task_from_exception<bool>(ObjectDeletedException("RpcService"));
					}

				}



				void initialize(std::shared_ptr<Scene> scene)
				{
					if (auto rpc = wRpc.lock())
					{
						std::weak_ptr<CommandLogService> wThat = this->shared_from_this();
						rpc->addProcedure("transactionLog.sync", [wThat](RpcRequestContext_ptr ctx)
							{
								auto request = ctx->readObject<SyncRequest>();
								if (auto that = wThat.lock())
								{

									ctx->sendValueTemplated(that->syncMessageReceived(request), PacketPriority::MEDIUM_PRIORITY);
								}
								else
								{
									throw Stormancer::ObjectDeletedException("CommandLogService");
								}


								return pplx::task_from_result();
							});
					}
				}
			private:
				SyncResponse syncMessageReceived(SyncRequest& request)
				{
					std::lock_guard<std::mutex> guard(_mutex);

					for (auto& logEntry : request.logEntries)
					{
						if (logEntry.id == getLastLogEntry() + 1)
						{


							CommandReceivedEvent evt;
							evt.entry = logEntry;
							evt.scene = wScene;
							onCommandReceived(evt);

							if (evt.accepted)
							{
								_logEntries.push_back(logEntry);
							}
						}
					}


					SyncResponse response;
					response.lastLogId = getLastLogEntry();
					return response;
				}

				/// <summary>
				/// gets the id of the last log entrie. 0 means that the log is empty.
				/// </summary>
				/// <returns></returns>
				int getLastLogEntry()
				{
					auto count = _logEntries.size();
					if (count != 0)
					{
						return _logEntries[count - 1].id;
					}
					else
					{
						return 0;
					}

				}
				Event<CommandReceivedEvent&> onCommandReceived;
				std::vector<LogEntry> _logEntries;
				std::weak_ptr<RpcService> wRpc;
				std::weak_ptr<Scene> wScene;
				std::mutex _mutex;
				Subscription _apiSubscription;
			};
		}

		class CommandLogApi
		{

			friend class CommandLogPlugin;
		public:

			CommandLogApi(std::shared_ptr<Serializer> serializer)
			{
				_serializer = serializer;
			}
			Subscription subscribeOnCommandReceived(::std::function<void(CommandReceivedEvent&)> callback)
			{
				return _onCommandReceived.subscribe(callback);
			}

			template<typename T>
			pplx::task<bool> addCommandToLog(const std::string& sceneId, const std::string& type, const T& data)
			{
				Stormancer::obytestream stream;
				_serializer->serialize(stream, data);

				return addCommandToLog(sceneId, type, stream.bytes());
			}

			pplx::task<bool> addCommandToLog(const std::string& sceneId, const std::string& type, const std::vector<byte>& data)
			{
				auto it = _connectedScenes.find(sceneId);
				if (it != _connectedScenes.end())
				{
					if (auto scene = it->second.lock())
					{
						return scene->dependencyResolver().resolve<details::CommandLogService>()->addCommandToLog(type, data);
					}
				}

				return pplx::task_from_exception<bool>(std::runtime_error(("notConnectedToScene?id=" + sceneId).c_str()));
			}
		private:

			void onCommandReceived(CommandReceivedEvent& evt)
			{
				_onCommandReceived(evt);
			}
			void onConnected(std::shared_ptr<Scene> scene, std::shared_ptr<details::CommandLogService> service)
			{
				_connectedScenes.emplace(scene->id(), scene);
				service->subscribeOnCommandReceived([this](CommandReceivedEvent& evt) { onCommandReceived(evt); });
			}
			void onDisconnected(std::shared_ptr<Scene> scene)
			{
				_connectedScenes.erase(scene->id());
			}

			Event<CommandReceivedEvent&> _onCommandReceived;

			/// <summary>
			/// Cached weak pointers to the scenes that support the command log feature for quick access.
			/// </summary>
			/// <param name="scene"></param>
			std::unordered_map<std::string, std::weak_ptr<Scene>> _connectedScenes;
			std::shared_ptr<Serializer> _serializer;

		};
		class CommandLogPlugin : public IPlugin
		{
			static constexpr const char* PLUGIN_NAME = "replication.commandLog";
			static constexpr const char* PLUGIN_REVISION = "1.0";
			static constexpr const char* PLUGIN_METADATA_KEY = "stormancer.replication.commandLog";

			PluginDescription getDescription() override
			{
				return PluginDescription(PLUGIN_NAME, PLUGIN_REVISION);
			}

			bool supportsCommandLogs(std::shared_ptr<Scene> scene)
			{
				auto version = scene->getHostMetadata(CommandLogPlugin::PLUGIN_METADATA_KEY);
				return !version.empty();
			}
			void registerClientDependencies(ContainerBuilder& clientBuilder) override
			{
				clientBuilder.registerDependency<CommandLogApi, Serializer>().singleInstance();
			}
			void registerSceneDependencies(ContainerBuilder& sceneBuilder, std::shared_ptr<Scene> scene) override
			{

				if (supportsCommandLogs(scene))
				{
					sceneBuilder.registerDependency<details::CommandLogService, Scene, RpcService>().singleInstance();
				}
			}

			void sceneCreated(std::shared_ptr<Scene> scene) override
			{
				if (supportsCommandLogs(scene))
				{
					scene->dependencyResolver().resolve<details::CommandLogService>()->initialize(scene);
				}
			}
			void sceneConnected(std::shared_ptr<Scene> scene) override
			{
				if (supportsCommandLogs(scene))
				{
					scene->dependencyResolver().resolve<CommandLogApi>()->onConnected(scene, scene->dependencyResolver().resolve<details::CommandLogService>());
				}
			}

			void sceneDisconnecting(std::shared_ptr<Scene> scene) override
			{
				if (supportsCommandLogs(scene))
				{
					scene->dependencyResolver().resolve<CommandLogApi>()->onDisconnected(scene);
				}
			}
		};
	}
}