#pragma once

#include "GameFinder/GameFinder.hpp"
#include "Users/ClientAPI.hpp"
#include "Users/Users.hpp"

#include "stormancer/IClient.h"
#include "stormancer/IPlugin.h"
#include "stormancer/ITokenHandler.h"
#include "stormancer/Event.h"
#include "stormancer/msgpack_define.h"
#include "stormancer/Scene.h"
#include "stormancer/Serializer.h"
#include "stormancer/StormancerTypes.h"
#include "stormancer/Tasks.h"
#include "stormancer/Utilities/TaskUtilities.h"
#include "stormancer/Utilities/Macros.h"
#include "stormancer/cpprestsdk/cpprest/asyncrt_utils.h"
#include <bitset>
#include <string>
#include <unordered_map>



namespace Stormancer
{
	namespace Spectate
	{
		enum class FrameType
		{
			Snapshot = 0,
			Diff = 1
		};

		struct FrameDataDto
		{
			FrameType type;
			uint64 time;
			std::vector<byte> data;

			MSGPACK_DEFINE(type, time, data);
		};

		struct Frame
		{
			FrameType type;
			uint64 time;
			std::vector<byte> data;
			SessionId origin;

			MSGPACK_DEFINE(type, time, data, origin);
		};

		struct FrameList
		{
			uint64 time;
			std::vector<Frame> frames;

			MSGPACK_DEFINE(time, frames);
		};

		class SpectateService : public std::enable_shared_from_this<SpectateService>
		{
		public:

			SpectateService(std::shared_ptr<RpcService> rpcService, std::shared_ptr<ILogger> logger)
				: _rpcService(rpcService)
				, _logger(logger)

			{
			}

			void initialize(std::shared_ptr<Scene> scene)
			{
				std::weak_ptr<SpectateService> wThat = this->shared_from_this();
				scene->addRoute("Spectate.SendFrames", [wThat](Stormancer::Packetisp_ptr packet)
					{
						if (auto that = wThat.lock())
						{
							auto frames = packet->readObject<std::vector<Frame>>();
							that->_onFramesReceived(frames);
						}
					});

			}
			pplx::task<void> sendFrames(std::vector<FrameDataDto> frames)
			{
				return _rpcService->rpc("Spectate.SendFrames", frames);
			}

			pplx::task<std::vector<FrameList>> GetFrames(uint64 startTime, uint64 endTime)
			{
				return _rpcService->rpc<std::vector<FrameList>>("Spectate.GetFrames", startTime, endTime);
			}

			pplx::task<uint64> startReceiveFrames(pplx::cancellation_token ct = pplx::cancellation_token::none())
			{
				return _rpcService->rpc<uint64>("Spectate.SubscribeToFrames", StreamWriter());
			}

			pplx::task<void> stopReceiveFrames()
			{
				return _rpcService->rpc("Spectate.Stop", StreamWriter());
			}


			Stormancer::Subscription subscribeToFrames(std::function<void(std::vector<Frame>)> callback)
			{
				return _onFramesReceived.subscribe(callback);
			}
		private:


			std::shared_ptr<RpcService> _rpcService;
			std::shared_ptr<ILogger> _logger;
			Stormancer::Event<std::vector<Frame>> _onFramesReceived;
		};

		class SpectatePlugin : public Stormancer::IPlugin
		{
		public:

			static constexpr const char* PLUGIN_NAME = "Spectate";
			static constexpr const char* PLUGIN_VERSION = "1.0.0";

			PluginDescription getDescription() override
			{
				return PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
			}

		private:
			void sceneCreated(std::shared_ptr<Scene> scene) override
			{
				if (!scene->getHostMetadata("stormancer.spectate").empty())
				{
					scene->dependencyResolver().resolve<SpectateService>()->initialize(scene);
				}
			}
			void registerSceneDependencies(Stormancer::ContainerBuilder& builder, std::shared_ptr<Stormancer::Scene> scene) override
			{
				auto name = scene->getHostMetadata("stormancer.spectate");
				if (name.length() > 0)
				{
					builder.registerDependency<SpectateService, RpcService, ILogger>().singleInstance();
				}
			}
		};
	}
}

MSGPACK_ADD_ENUM(Stormancer::Spectate::FrameType);
