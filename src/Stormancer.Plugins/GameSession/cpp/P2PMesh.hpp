#pragma once
#include "stormancer/IPlugin.h"
#include "stormancer/SessionId.h"
#include "stormancer/buffers/Memory.h"
#include "stormancer/PacketPriority.h"

#if !defined(STORM_PLUGIN_IMPL)
#define STORM_PLUGIN_IMPL 0
#endif

namespace Stormancer
{
	class P2PMeshService
	{
	public:
		virtual void send(
			const SessionId& sessionId, ::std::string route, const std::function<void(BufferWriter&)>& writer, PacketReliability reliability
		) = 0;

		virtual void addRoute(
			const std::string& route,
			std::function<void(const std::string& connectionType, const SessionId&, const byte*, size_t)> handler
		) = 0;
		virtual ~P2PMeshService() {};
	};

	using P2PMeshService_ptr = std::shared_ptr<P2PMeshService>;

	struct OnMessageReceivedArgs
	{
		std::string sceneId;
		SessionId origin;

		const byte* data;
		size_t length;
	};

	class P2PMesh
	{
		virtual bool send(
			const std::string& sceneId,
			const SessionId& sessionId,
			const std::function<void(BufferWriter&)>& writer,
			PacketReliability reliability
		) = 0;
		virtual Subscription subscribeOnMessageReceived(std::function<void(OnMessageReceivedArgs)> callback) = 0;
	};

} // namespace Stormancer
#if STORM_PLUGIN_IMPL == 1

#include "stormancer/Scene.h"
#include "stormancer/Serializer.h"
#include "stormancer/RPC/RpcService.h"
#include "stormancer/P2P/IP2PScenePeer.h"
#include "stormancer/Logger/ILogger.h"
#include "stormancer/Helpers.h"
namespace Stormancer
{
	class P2PMeshPlugin : public IPlugin
	{
		PluginDescription getDescription();
		void registerClientDependencies(ContainerBuilder& clientBuilder) override;
		void registerSceneDependencies(ContainerBuilder& sceneBuilder, ::std::shared_ptr<Scene> scene) override;
		void sceneDisconnecting(std::shared_ptr<Scene> scene) override;
		void sceneCreated(std::shared_ptr<Scene> scene) override;
	};
	namespace details
	{
		class P2PManager : public std::enable_shared_from_this<P2PManager>
		{
		public:
			P2PManager(std::shared_ptr<Scene> scene, std::shared_ptr<RpcService> rpc, std::shared_ptr<ILogger> logger)
				: _scene(scene)
				, _rpc(rpc)
				, _logger(logger)
			{}
			bool tryGetPeer(const SessionId& sessionId, std::shared_ptr<IP2PScenePeer>& peer)
			{
				std::lock_guard<std::mutex> lg{ _syncRoot };
				// return false;
				auto it = _peers.find(sessionId);
				if (it != _peers.end())
				{
					if (it->second.task.is_done())
					{
						peer = it->second.task.get();
						return true;
					}
					else
					{
						return false;
					}
				}
				else
				{
					PeerContainer container;
					auto wThat = this->weak_from_this();
					container.task = connectToPeer(sessionId).then(
						[wThat, sessionId](pplx::task<std::shared_ptr<IP2PScenePeer>> t)
						{
							try
							{
								return t.get();
							}
							catch (std::exception& ex)
							{

								if (auto that = wThat.lock())
								{
									that->_logger->log(LogLevel::Error, "mesh", "An error occured while trying to connect to {}. error={}.", sessionId, ex);
									that->onPeerDisconnected(sessionId);
								}
								return std::shared_ptr<IP2PScenePeer>(nullptr);
							}
						}
					);
					_peers.emplace(sessionId, container);
					return false;
				}
			}

			void onPeerDisconnected(const SessionId& sessionId)
			{

				std::lock_guard<std::mutex> lg{ _syncRoot };
				_peers.erase(sessionId);
			}

		private:
			struct PeerContainer
			{
				Subscription onCloseSubscription;
				pplx::task<std::shared_ptr<IP2PScenePeer>> task;
				bool success = false;
			};

			pplx::task<std::shared_ptr<IP2PScenePeer>> connectToPeer(const SessionId& sessionId)
			{
				if (auto scene = _scene.lock())
				{
					auto wThat = this->weak_from_this();
					return scene->connectP2PAsync(sessionId).then(
						[wThat, sessionId](std::shared_ptr<IP2PScenePeer> peer)
						{
							auto that = wThat.lock();
							if (that == nullptr)
							{
								return pplx::task_from_exception<std::shared_ptr<IP2PScenePeer>>(ObjectDeletedException("p2pManager"));
							}
							auto it = that->_peers.find(sessionId);
							if (it == that->_peers.end())
							{
								return pplx::task_from_exception<std::shared_ptr<IP2PScenePeer>>(std::runtime_error("disconnected"));
							}

							it->second.onCloseSubscription = peer->connection()->onClose.subscribe(
								[wThat, sessionId](std::string reason)
								{
									auto that = wThat.lock();
									if (that != nullptr)
									{
										that->onPeerDisconnected(sessionId);
									}
								}
							);
							return pplx::task_from_result(peer);
						}
					);
				}
				else
				{
					return pplx::task_from_exception<std::shared_ptr<IP2PScenePeer>>(ObjectDeletedException("scene"));
				}
			}
			std::weak_ptr<Scene> _scene;
			std::weak_ptr<RpcService> _rpc;
			std::unordered_map<SessionId, PeerContainer> _peers;
			std::shared_ptr<ILogger> _logger;

			std::mutex _syncRoot;
		};

		class P2PMeshServiceImpl
			: public P2PMeshService
			, public std::enable_shared_from_this<P2PMeshServiceImpl>
		{
		public:
			P2PMeshServiceImpl(
				::std::shared_ptr<Scene> scene,
				::std::shared_ptr<Serializer> serializer,
				std::shared_ptr<P2PManager> p2pManager,
				std::shared_ptr<IClient> client,
				std::shared_ptr<ILogger> logger
			)
				: _logger(logger)
				, _scene(scene)
				, _client(client)
				, _serializer(serializer)
				, _p2pManager(p2pManager)

			{}

			void initialize()
			{
				if (auto scene = this->_scene.lock())
				{
				}
			}
			std::unordered_map<std::string, std::function<void(const std::string&, const SessionId&, const byte*, size_t)>> _handlers;

			void addRoute(const std::string& route, std::function<void(const std::string& connectionType, const SessionId&, const byte*, size_t)> handler) override
			{
				Scene::RouteOptions p2pOptions;
				p2pOptions.filter = MessageOriginFilter::All;

				if (auto scene = this->_scene.lock())
				{
					auto logger = scene->dependencyResolver().resolve<ILogger>();
					scene->addRoute(
						route,
						[handler, logger, route, wScene = _scene](Packetisp_ptr packet)
						{
							try
							{
								SessionId sessionId = packet->connection->connection()->sessionId();
								if (!sessionId.isValid())
								{

									SessionId::tryParse(packet->content().data(), 16, sessionId);
									packet->advance(16);
								}
								auto span = packet->content();
								/* if (auto scene = wScene.lock())
								{

									scene->dependencyResolver().resolve<ILogger>()->log(LogLevel::Info, "p2p",std::format("received
								data ({}) {} : {}",packet->connection->connection()->type(), route, stringifyBytesArray(span.data(), span.size())));
								}*/

								handler(packet->connection->connection()->type(), sessionId, span.data(), span.size());
							}
							catch (std::exception& ex)
							{
								logger->log(LogLevel::Error, "mesh", "Error handling route {}. error={}", route, ex);
							}
						},
						p2pOptions
					);
				}
				_handlers.emplace(route, handler);
			}

			void send(const SessionId& sessionId, ::std::string route, const std::function<void(BufferWriter&)>& writer, PacketReliability reliability) override
			{
				auto client = LockOrThrow(_client);
				auto& localSessionId = client->sessionId();

				if (sessionId == localSessionId)
				{
					_logger->log(LogLevel::Warn, "mesh", "Sending a message to self.");
				}

				std::shared_ptr<::Stormancer::IP2PScenePeer> peer;
				if (sessionId == localSessionId)
				{
					auto it = _handlers.find(route);
					if (it != _handlers.end())
					{
						BufferWriter stream;
						writer(stream);
						it->second("local", sessionId, stream.content().data(), stream.content().size());
					}
				}
				if (reliability != PacketReliability::RELIABLE && _p2pManager->tryGetPeer(sessionId, peer) && peer != nullptr
					&& peer->connection()->getConnectionState() == ConnectionState::Connected)
				{
					try
					{

						peer->send(
							route,
							[writer, localSessionId, wScene = _scene, route](BufferWriter& stream)
							{
								writer(stream);
								auto span = stream.content();
								/*if (auto scene = wScene.lock())
								{
									scene->dependencyResolver().resolve<ILogger>()->log(LogLevel::Info,"p2p", std::format("sending
								data (p2p) {} : {}", route, stringifyBytesArray(span.data(), span.size())));
								}*/
							},
							PacketPriority::IMMEDIATE_PRIORITY,
							reliability
						); // const StreamWriter& streamWriter, PacketPriority priority = PacketPriority::MEDIUM_PRIORITY, PacketReliability
						// reliability = PacketReliability::RELIABLE_ORDERED, const std::string& channelIdentifier = "")
					}
					catch (...)
					{
						_p2pManager->onPeerDisconnected(sessionId);
					}
				}
				else
				{
					if (auto scene = _scene.lock())
					{
						auto serializer = _serializer;
						scene->send(
							"p2pmesh.relay",
							[writer, sessionId, reliability, serializer, route, wScene = _scene](BufferWriter& stream)
							{
								int length = sessionId.getLength();
								auto buffer = stream.getSpan(length + 1);

								sessionId.tryWrite(buffer.data(), buffer.size());

								buffer[length] = reliability;
								stream.advance(length + 1);

								serializer->serialize(stream, route);
								auto headerLength = stream.content().size();
								writer(stream);
								//auto span = stream.content().subspan(headerLength);
								//if (auto scene = wScene.lock())
								//{
								//	scene->dependencyResolver().resolve<ILogger>()->log(
								//		LogLevel::Trace,
								//		"p2p",
								//		std::format("sending data (relay) {} : {}", route, stringifyBytesArray(span.data(), span.size()))
								//	);
								//}
							},
							PacketPriority::IMMEDIATE_PRIORITY,
							reliability
						);
					}
				}
			}
			~P2PMeshServiceImpl() override {};

		private:
			::std::shared_ptr<ILogger> _logger;
			::std::weak_ptr<Scene> _scene;
			::std::weak_ptr<IClient> _client;
			::std::shared_ptr<Serializer> _serializer;
			::std::shared_ptr<P2PManager> _p2pManager;
		};

		class P2PMesh_Impl
			: public P2PMesh
			, public std::enable_shared_from_this<P2PMesh_Impl>
		{
		public:
			virtual ~P2PMesh_Impl()
			{}

			void onCreated(std::shared_ptr<Scene> scene, P2PMeshService_ptr service)
			{
				auto wThat = weak_from_this();
				service->addRoute("p2p.msg", [wThat, id = scene->id()](const std::string& connectionType, const SessionId& origin, const byte* data, size_t length)
					{
						if (auto that = wThat.lock())
						{
							OnMessageReceivedArgs args;
							args.sceneId = id;
							args.origin = origin;
							args.data = data;
							args.length = length;
							that->_onMessageReceived(args);
						}
					}
				);
				_services.emplace(scene->id(), service);
			}

			void onDisconnecting(std::shared_ptr<Scene> scene, P2PMeshService_ptr service)
			{
				_services.erase(scene->id());
			}

			virtual bool send(
				const std::string& sceneId,
				const SessionId& sessionId,
				const std::function<void(BufferWriter&)>& writer,
				PacketReliability reliability
			) override
			{
				auto it = _services.find(sceneId);
				if (it != _services.end())
				{
					P2PMeshService_ptr service = it->second;
					service->send(sessionId, "p2p.msg", writer, reliability);
					return true;
				}
				else
				{
					return false;
				}
			}
			virtual Subscription subscribeOnMessageReceived(std::function<void(OnMessageReceivedArgs)> callback) override
			{
				return _onMessageReceived.subscribe(callback);
			}

		private:
			Event<OnMessageReceivedArgs> _onMessageReceived;

			std::unordered_map<std::string, P2PMeshService_ptr> _services;
		};
	} // namespace details

	PluginDescription P2PMeshPlugin::getDescription()
	{
		return PluginDescription("P2PMesh", "1.0.0");
	}

	void P2PMeshPlugin::registerClientDependencies(ContainerBuilder& clientBuilder)
	{
		clientBuilder.registerDependency<details::P2PMesh_Impl>().as<P2PMesh>().singleInstance();
	}

	void P2PMeshPlugin::registerSceneDependencies(ContainerBuilder& sceneBuilder, ::std::shared_ptr<Scene> scene)
	{
		if (!scene->getHostMetadata("stormancer.p2pmesh").empty())
		{
			sceneBuilder
				.registerDependency<details::P2PMeshServiceImpl, Scene, Serializer, details::P2PManager, IClient, ILogger>()
				.as<P2PMeshService>()
				.singleInstance();
			sceneBuilder.registerDependency<details::P2PManager, Scene, RpcService, ILogger>().singleInstance();
		}
	}

	void P2PMeshPlugin::sceneCreated(std::shared_ptr<Scene> scene)
	{
		if (!scene->getHostMetadata("stormancer.p2pmesh").empty())
		{
			std::static_pointer_cast<details::P2PMeshServiceImpl>(scene->dependencyResolver().resolve<P2PMeshService>())
				->initialize();

			auto api = std::static_pointer_cast<details::P2PMesh_Impl>(scene->dependencyResolver().resolve<P2PMesh>());
			api->onCreated(scene, scene->dependencyResolver().resolve<P2PMeshService>());
		}
	}

	void P2PMeshPlugin::sceneDisconnecting(std::shared_ptr<Scene> scene)
	{
		if (!scene->getHostMetadata("stormancer.p2pmesh").empty())
		{
			auto api = std::static_pointer_cast<details::P2PMesh_Impl>(scene->dependencyResolver().resolve<P2PMesh>());
			api->onDisconnecting(scene, scene->dependencyResolver().resolve<P2PMeshService>());
		}
	}

} // namespace Stormancer
#endif
