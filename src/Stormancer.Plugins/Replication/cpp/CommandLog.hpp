#pragma once
#include "stormancer/IPlugin.h"
#include "stormancer/Scene.h"
#include "stormancer/RPC/RpcService.h"
#include "stormancer/Exceptions.h"
#include "stormancer/Tasks.h"

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
			LogEntry entry;
		};

		class CommandLogService : public std::enable_shared_from_this<CommandLogService>
		{
			friend class CommandLogPlugin;
		public:
			CommandLogService(std::shared_ptr<RpcService> rpc)
			{
				this->wRpc = rpc;
			}

			Subscription subscribeOnCommandReceived(std::function<void(CommandReceivedEvent&)> callback)
			{
				std::lock_guard<std::mutex> guard(_mutex);
				for (auto entry : _logEntries)
				{
					CommandReceivedEvent evt;
					evt.entry = entry;

					callback(evt);

				}
				return onCommandReceived.subscribe(callback);
			}

		private:
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

			SyncResponse syncMessageReceived(SyncRequest& request)
			{
				std::lock_guard<std::mutex> guard(_mutex);

				for (auto& logEntry : request.logEntries)
				{
					if (logEntry.id == getLastLogEntry() + 1)
					{
						_logEntries.push_back(logEntry);

						CommandReceivedEvent evt;
						evt.entry = logEntry;
						onCommandReceived(evt);
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
			std::mutex _mutex;
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
			void registerSceneDependencies(ContainerBuilder& sceneBuilder, std::shared_ptr<Scene> scene) override
			{

				if (supportsCommandLogs(scene))
				{
					sceneBuilder.registerDependency<CommandLogService>().singleInstance();
				}
			}

			void sceneCreated(std::shared_ptr<Scene> scene) override
			{
				if (supportsCommandLogs(scene))
				{
					scene->dependencyResolver().resolve<CommandLogService>()->initialize(scene);
				}
			}
		};
	}
}