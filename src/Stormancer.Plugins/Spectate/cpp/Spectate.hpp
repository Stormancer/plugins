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

		class SpectateService
		{
		public:

			SpectateService(std::shared_ptr<Scene> scene, std::shared_ptr<ILogger> logger, std::shared_ptr<IActionDispatcher> dispatcher, std::shared_ptr<Serializer> serializer)
				: _scene(scene)
				, _rpcService(scene->dependencyResolver().resolve<RpcService>())
				, _logger(logger)
				, _dispatcher(dispatcher)
				, _serializer(serializer)
			{
			}

			pplx::task<void> sendFrames(std::vector<FrameDataDto> frames)
			{
				return _rpcService->rpc("Spectate.SendFrames", frames);
			}

			pplx::task<std::vector<FrameList>> GetFrames(uint64 startTime, uint64 endTime)
			{
				return _rpcService->rpc<std::vector<FrameList>>("Spectate.GetFrames", startTime, endTime);
			}

			pplx::task<void> subscribeToFrames(std::function<void(std::vector<Frame> frames)> callback, pplx::cancellation_token ct = pplx::cancellation_token::none())
			{
				if (_subscription && _subscription->is_subscribed())
				{
					STORM_RETURN_TASK_FROM_EXCEPTION_OPT(std::runtime_error("Already subscribe"), _dispatcher, void);
				}

				RpcService::ClientRpcOptions options;
				options.dispatchMethod = DispatchMethod::ActionDispatcher;
				options.priority = PacketPriority::MEDIUM_PRIORITY;

				std::string route = "Spectate.SubscribeToFrames";

				auto observable = _rpcService->rpcObservable(route, StreamWriter(), options);

				pplx::cancellation_token_registration ctr;

				if (ct.is_cancelable())
				{
					ctr = ct.register_callback([this]()
					{
						if (this->_subscription && this->_subscription->is_subscribed())
						{
							this->_subscription->unsubscribe();
							this->_subscription = nullptr;
						}
					});
				}

				pplx::task_completion_event<void> tce;

				auto onNext = [logger = _logger, callback, ctr](Packetisp_ptr packet)
				{
					auto frames = packet->readObject<std::vector<Frame>>();
					logger->log(LogLevel::Debug, "Spectate", "Frames received", std::to_string(frames.size()));
					if (callback)
					{
						callback(frames);
					}
				};

				auto onComplete = [tce, ctr]()
				{
					tce.set();
				};

				auto onError = [route, logger = _logger, tce, ctr](std::exception_ptr error)
				{
					logger->log(LogLevel::Trace, "Rpc", "An exception occurred during the rpc '" + route + "'");
					tce.set_exception(error);
				};

				_subscription = std::make_shared<rxcpp::composite_subscription>(observable.subscribe(onNext, onError, onComplete));

				return pplx::create_task(tce, _dispatcher);
			}

		private:

			std::shared_ptr<Scene> _scene;
			std::shared_ptr<RpcService> _rpcService;
			std::shared_ptr<ILogger> _logger;
			std::shared_ptr<IActionDispatcher> _dispatcher;
			std::shared_ptr<Serializer> _serializer;

			std::shared_ptr<rxcpp::composite_subscription> _subscription;
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

			void registerSceneDependencies(Stormancer::ContainerBuilder& builder, std::shared_ptr<Stormancer::Scene> scene) override
			{
				auto name = scene->getHostMetadata("stormancer.spectate");
				if (name.length() > 0)
				{
					builder.registerDependency<SpectateService, Scene, ILogger, IActionDispatcher, Serializer>().singleInstance();
				}
			}
		};
	}
}

MSGPACK_ADD_ENUM(Stormancer::Spectate::FrameType);
