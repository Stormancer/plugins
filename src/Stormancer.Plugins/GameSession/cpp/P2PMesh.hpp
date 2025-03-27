#pragma once
#include "stormancer/IPlugin.h"
#include "stormancer/SessionId.h"
#include "stormancer/Streams/bytestream.h"
#include "stormancer/PacketPriority.h"


#if !defined(STORM_PLUGIN_IMPL)
#define STORM_PLUGIN_IMPL 0
#endif

namespace Stormancer
{
	class P2PMeshService
	{
	public:
		virtual void send(const SessionId& sessionId, ::std::string route, const StreamWriter writer, PacketReliability reliability) = 0;

		virtual ~P2PMeshService() {};
	};

	class P2PMeshPlugin : public IPlugin
	{
		PluginDescription getDescription();
	
		void registerSceneDependencies(ContainerBuilder& sceneBuilder, ::std::shared_ptr<Scene> scene) override;
		
	};

#if STORM_PLUGIN_IMPL

#include "stormancer/Scene.h"
#include "stormancer/Serializer.h"
#include "P2PManager.hpp"
#include "stormancer/RPC/RpcService.h"
#include "stormancer/P2P/IP2PScenePeer.h"
#include "stormancer/Logger/ILogger.h"
	class P2PMeshPlugin;
	namespace details
	{
		class P2PManager : public std::enable_shared_from_this<P2PManager>
		{
		public:

			P2PManager(std::shared_ptr<Scene> scene, std::shared_ptr<RpcService> rpc) :
				_scene(scene),
				_rpc(rpc)
			{

			}
			bool tryGetPeer(const SessionId& sessionId, std::shared_ptr<IP2PScenePeer>& peer)
			{
				//return false;
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
					container.task = connectToPeer(sessionId).then([wThat, sessionId](pplx::task<std::shared_ptr<IP2PScenePeer>> t) 
					{
						try
						{
							return t.get();
						}
						catch (std::exception& )
						{
							if (auto that = wThat.lock())
							{
								that->onPeerDisconnected(sessionId);
							}
							return std::shared_ptr<IP2PScenePeer>(nullptr);
						}
					});
					_peers[sessionId] = container;
					return false;
				}
			}

			void onPeerDisconnected(const SessionId& sessionId)
			{
				_peers.erase(sessionId);
			}

			
		private:

			struct PeerContainer
			{
				Subscription onCloseSubscription;
				pplx::task<std::shared_ptr<IP2PScenePeer>> task;
			};

			pplx::task<std::shared_ptr<IP2PScenePeer>> connectToPeer(const SessionId& sessionId)
			{
				auto rpc = _rpc.lock();
				auto wThat = this->weak_from_this();
				if (rpc == nullptr)
				{
					return pplx::task_from_exception< std::shared_ptr<IP2PScenePeer>>(ObjectDeletedException("rpc"));
				}
				auto wScene = _scene;
				return rpc->rpc<std::string, SessionId>("p2pmesh.getP2PToken", sessionId)
					.then([wScene](std::string token)
						{
							auto scene = wScene.lock();
							return scene->openP2PConnection(token);
						})
					.then([wThat, sessionId](std::shared_ptr<IP2PScenePeer> peer)
						{
							auto that = wThat.lock();
							if (that == nullptr)
							{
								return pplx::task_from_exception< std::shared_ptr<IP2PScenePeer>>(ObjectDeletedException("p2pManager"));
							}
							auto it = that->_peers.find(sessionId);
							if (it == that->_peers.end())
							{
								return pplx::task_from_exception< std::shared_ptr<IP2PScenePeer>>(std::runtime_error("disconnected"));
							}

							it->second.onCloseSubscription = peer->connection()->onClose.subscribe([wThat, sessionId](std::string reason)
								{
									auto that = wThat.lock();
									if (that != nullptr)
									{
										that->onPeerDisconnected(sessionId);
									}
								});
							return pplx::task_from_result(peer);
						});
			}
			std::weak_ptr<Scene> _scene;
			std::weak_ptr<RpcService> _rpc;
			std::unordered_map<SessionId, PeerContainer> _peers;


		};

		class P2PMeshServiceImpl: public P2PMeshService
		{
		public:
			P2PMeshServiceImpl(::std::shared_ptr<Scene> scene, ::std::shared_ptr<Serializer> serializer, std::shared_ptr<P2PManager> p2pManager, std::shared_ptr<Stormancer::IClient> client,std::shared_ptr<ILogger> logger)
				: _logger(logger)
				, _scene(scene)
				, _client(client)
				, _serializer(serializer)
				, _p2pManager(p2pManager)
				
				
			{

			}
			void send(const SessionId& sessionId, ::std::string route, const StreamWriter writer, PacketReliability reliability) override
			{
				auto client = _client.lock();
				auto& localSessionId = client->sessionId();
				if (sessionId == localSessionId)
				{
					_logger->log(LogLevel::Warn, "mesh", "Sending a message to self.");
				}

				std::shared_ptr<::Stormancer::IP2PScenePeer> peer;
				if (false && _p2pManager->tryGetPeer(sessionId, peer) && sessionId !=localSessionId)
				{

					peer->send(route, [writer, localSessionId](obytestream& stream) 
					{
						byte buffer[16];
						localSessionId.tryWrite(buffer, 16);
						int length = localSessionId.getLength();

						stream.write(buffer, length);

						writer(stream);
					}, PacketPriority::IMMEDIATE_PRIORITY, reliability);// const StreamWriter& streamWriter, PacketPriority priority = PacketPriority::MEDIUM_PRIORITY, PacketReliability reliability = PacketReliability::RELIABLE_ORDERED, const std::string& channelIdentifier = "")
				}
				else
				{
					if (auto scene = _scene.lock())
					{
						auto serializer = _serializer;
						scene->send("p2pmesh.relay", [writer, sessionId, reliability, serializer, route](obytestream& stream)
							{
								byte buffer[17];
								sessionId.tryWrite(buffer, 17);
								int length = sessionId.getLength();
								buffer[length] = reliability;

								stream.write(buffer, length + 1);
								serializer->serialize(stream, route);
								writer(stream);
							},
							PacketPriority::IMMEDIATE_PRIORITY, reliability);
					}
				}
			}
			~P2PMeshServiceImpl() override {};

		private:
			::std::shared_ptr<ILogger> _logger;
			::std::weak_ptr<Scene> _scene;
			::std::weak_ptr<Stormancer::IClient> _client;
			::std::shared_ptr<Serializer> _serializer;
			::std::shared_ptr<P2PManager> _p2pManager;
		};

		
	}
	PluginDescription P2PMeshPlugin::getDescription()
	{
		return PluginDescription("P2PMesh","1.0.0");
	}

	void P2PMeshPlugin::registerSceneDependencies(ContainerBuilder& sceneBuilder, ::std::shared_ptr<Scene> scene)
	{
		if (!scene->getHostMetadata("stormancer.p2pmesh").empty())
		{
			sceneBuilder.registerDependency < details::P2PMeshServiceImpl, Scene, Serializer, details::P2PManager, Stormancer::IClient,ILogger>().as<P2PMeshService>().singleInstance();
			sceneBuilder.registerDependency<details::P2PManager, Scene, RpcService>().singleInstance();
		}
	}

	

	
#endif
}