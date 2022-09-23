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
	class SessionId
	{
	public:

		SessionId() = default;

		SessionId(const std::vector<byte>& byteArray)
			: _data(byteArray)
		{
		}

		SessionId(const byte* data, const std::size_t size)
		{
			if (size > 0)
			{
				_data.assign(data, data + size);
			}
		}

		SessionId(const std::string& base64Str)
			: _data(utility::conversions::from_base64(utility::conversions::to_string_t(base64Str)))
		{
		}

		std::vector<byte> toByteArray() const
		{
			return _data;
		}

		std::string toString()
		{
			return utility::conversions::to_utf8string(utility::conversions::to_base64(_data));
		}

		bool isEmpty()
		{
			return (_data.size() == 0);
		}

		bool operator==(const SessionId& other)
		{
			return (_data == other._data);
		}

	private:

		std::vector<byte> _data;
	};
}

namespace msgpack
{
	MSGPACK_API_VERSION_NAMESPACE(MSGPACK_DEFAULT_API_NS)
	{
		namespace adaptor
		{
			// MsgPack Serialization support for GVector

			template<>
			struct convert<Stormancer::SessionId>
			{
				msgpack::object const& operator()(msgpack::object const& o, Stormancer::SessionId& v) const
				{
					if (o.type != msgpack::type::STR)
					{
						throw msgpack::type_error();
					}

					if (o.via.bin.size == 0)
					{
						v = Stormancer::SessionId();
					}
					else
					{
						v = Stormancer::SessionId(reinterpret_cast<const Stormancer::byte*>(o.via.bin.ptr), o.via.bin.size);
					}

					return o;
				}
			};

			template<>
			struct pack<Stormancer::SessionId>
			{
				template <typename Stream>
				packer<Stream>& operator()(msgpack::packer<Stream>& o, Stormancer::SessionId const& v) const
				{
					auto data = v.toByteArray();
					auto size = data.size();
					o.pack_bin(size);
					if (size > 0)
					{
						o.pack_bin_body(data.data(), size);
					}
					return o;
				}
			};
		}
	}
}

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

		class SpectateService: public std::enable_shared_from_this<SpectateService>
		{
		public:

			SpectateService(std::shared_ptr<RpcService> rpcService, std::shared_ptr<ILogger> logger)
				: _rpcService(rpcService)
				, _logger(logger)
				
			{
			}

			void initialize(std::shared_ptr<Scene> scene)
			{
				std::weak_ptr<SpectateService> wThat;
				scene->addRoute("Spectate.SendFrames", [wThat](Stormancer::Packetisp_ptr packet)
				{
					if(auto that = wThat.lock())
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


			Stormancer::Subscription SubscribeToFrames(std::function<void(std::vector<Frame>)> callback)
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
