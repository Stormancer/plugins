#ifndef STORMANCER_SOCKET_API
#define STORMANCER_SOCKET_API

#include "stormancer/Tasks.h"
#include "stormancer/SessionId.h"

namespace Stormancer
{
	namespace Socket
	{
		struct ReceivedMsgInfos
		{
			Stormancer::SessionId sessionId;
			int length;
			bool success;
		};
		class SocketApi
		{
		public:
			/// <summary>
			/// Sends data to another peer connected to a specific scene.
			/// </summary>
			/// <param name="sceneId"></param>
			/// <param name="destination"></param>
			/// <param name="buffer"></param>
			/// <param name="offset"></param>
			/// <param name="length"></param>
			virtual bool send(std::string sceneId, Stormancer::SessionId destination, Stormancer::byte* buffer, int length) = 0;

			/// <summary>
			/// Blocks the thread until a datagram is received on the specified scene.
			/// </summary>
			/// <param name="sceneId"></param>
			/// <param name="buffer"></param>
			/// <param name="maxLength"></param>
			/// <returns></returns>
			virtual ReceivedMsgInfos receive(std::string sceneId, Stormancer::byte* buffer, int maxLength) = 0;


		};
	}
}
#endif

#if !defined(STORMANCER_SOCKET_API_IMPL) && (!defined(STORMANCER_PLUGINS_SEPARATE_IMPL) || defined(STORMANCER_PLUGINS_CONFIG))
#define STORMANCER_SOCKET_API_IMPL

#include "stormancer/IPlugin.h"
#include "stormancer/Version.h"
#include "stormancer/Scene.h"
#include "stormancer/async.h"

namespace Stormancer
{
	namespace Socket
	{
		class SocketApiPlugin;
		class SocketApi_Impl;
		namespace details
		{
			class SocketApiService
			{
				friend SocketApi_Impl;
				friend SocketApiPlugin;

			public:
				std::string sceneId()
				{

					return _sceneId;
				}
			private:
				void initialize(std::shared_ptr<Scene> scene)
				{
					_sceneId = scene->id();
					Scene::RouteOptions options;
					options.filter = MessageOriginFilter::Peer;
					scene->addRoute("relay.receive", [this](Packetisp_ptr packet)
						{
							_channel.writer().tryWrite(std::make_tuple(false, packet));
						});
					scene->addRoute("Socket.SendUnreliable", [this](Packetisp_ptr packet)
						{
							_channel.writer().tryWrite(std::make_tuple(true, packet));
						}, options);
				}

				void onDisconnecting()
				{

				}

				ReceivedMsgInfos receive(byte* buffer, int maxLength)
				{

					std::tuple<bool, Packetisp_ptr> tuple;
					ReceivedMsgInfos r;
					int length = 0;
					if (_channel.reader().tryReadIf(tuple, [&length, &maxLength](std::tuple<bool, Packetisp_ptr>& tuple)
						{
							auto isP2P = std::get<0>(tuple);
							auto p = std::get<1>(tuple);
							if (isP2P)
							{
								length = (int)p->stream.totalSize();
							}
							else
							{
								length = (int)p->stream.totalSize() - 17;
							}
							return length <= maxLength;
						}))
					{

						auto packet = std::get<1>(tuple);
						auto isP2P = std::get<0>(tuple);
						r.length = length;
						r.success = true;
						if (isP2P)
						{
							r.sessionId = SessionId::parse(packet->connection->id());
						}
						else
						{
							serializer.deserialize(packet->stream, r.sessionId);
						}
						std::memcpy(buffer, packet->stream.currentPtr(), length);
						return r;

					}
					else
					{

						r.length = length;
						r.success = false;
						return r;
					}

				}

				bool send(Stormancer::SessionId destination, byte* buffer, int length)
				{
					if (auto scene = _scene.lock())
					{
						auto destStr = destination.toString();
						auto it = scene->connectedPeers().find(destStr);
						if (it == scene->connectedPeers().end())
						{
							scene->send("Socket.SendUnreliable", [buffer, length, this, destination](obytestream& stream)
								{

									serializer.serialize(stream, destination);
									stream.write(buffer, length);
								}, PacketPriority::MEDIUM_PRIORITY, PacketReliability::UNRELIABLE);
						}
						else
						{
							scene->send(PeerFilter::matchPeers(destStr), "Socket.SendUnreliable", [buffer, length, this, destination](obytestream& stream)
								{

									serializer.serialize(stream, destination);
									stream.write(buffer, length);
								}, PacketPriority::MEDIUM_PRIORITY, PacketReliability::UNRELIABLE);
						}
						return true;
					}
					else
					{
						return false;
					}
				}

				std::string _sceneId;
				std::weak_ptr<Scene> _scene;
				Stormancer::Channel<std::tuple<bool, Packetisp_ptr>> _channel;
				Stormancer::Serializer serializer;
			};
		}

		class SocketApi_Impl : public SocketApi
		{
			friend SocketApiPlugin;
		public:
			bool send(std::string sceneId, Stormancer::SessionId destination, byte* buffer, int length)
			{
				auto it = _services.find(sceneId);
				if (it != _services.end())
				{
					if (auto s = it->second.lock())
					{

						return s->send(destination, buffer, length);

					}
				}

				return false;


			}

			ReceivedMsgInfos receive(std::string sceneId, byte* buffer, int maxLength)
			{
				auto it = _services.find(sceneId);
				if (it != _services.end())
				{
					if (auto s = it->second.lock())
					{
						return s->receive(buffer, maxLength);
					}
				}

				ReceivedMsgInfos result;
				result.success = false;
				result.length = -1;
				return result;
			}



		private:
			void onConnected(std::weak_ptr<details::SocketApiService> service)
			{
				if (auto s = service.lock())
				{
					_services.emplace(s->sceneId(), service);
				}
			}

			void onDisconnecting(std::weak_ptr<details::SocketApiService> service)
			{
				if (auto s = service.lock())
				{
					_services.erase(s->sceneId());
					s->onDisconnecting();
				}

			}

			std::unordered_map<std::string, std::weak_ptr<details::SocketApiService>> _services;

		};

		class SocketApiPlugin : public IPlugin
		{
		private:
			static constexpr const char* PLUGIN_NAME = "stormancer.socket";
			static constexpr const char* METADATA_KEY = "stormancer.socketApi";
			static constexpr const char* PLUGIN_VERSION = "0.1.0";
		public:
			PluginDescription getDescription() override
			{
				return PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
			}
			void registerClientDependencies(ContainerBuilder& clientBuilder) override
			{
				clientBuilder.registerDependency<SocketApi_Impl>().as<SocketApi>().singleInstance();
			}
			void registerSceneDependencies(ContainerBuilder& sceneBuilder, std::shared_ptr<Scene> scene) override
			{
				if (!scene->getHostMetadata(METADATA_KEY).empty())
				{
					sceneBuilder.registerDependency<details::SocketApiService>().singleInstance();
				}

			}

			void sceneCreated(std::shared_ptr<Scene> scene) override
			{
				if (!scene->getHostMetadata(METADATA_KEY).empty())
				{
					auto service = scene->dependencyResolver().resolve<details::SocketApiService>();
					service->initialize(scene);
				}
			}
			void sceneConnected(std::shared_ptr<Scene> scene) override
			{
				if (!scene->getHostMetadata(METADATA_KEY).empty())
				{
					auto api = std::static_pointer_cast<SocketApi_Impl>(scene->dependencyResolver().resolve<SocketApi>());
					api->onConnected(scene->dependencyResolver().resolve<details::SocketApiService>());

				}
			}
			void sceneDisconnecting(std::shared_ptr<Scene> scene) override
			{
				if (!scene->getHostMetadata(METADATA_KEY).empty())
				{
					auto api = std::static_pointer_cast<SocketApi_Impl>(scene->dependencyResolver().resolve<SocketApi>());
					api->onDisconnecting(scene->dependencyResolver().resolve<details::SocketApiService>());
				}
			}

		private:
			static bool _registered;
		};

		bool SocketApiPlugin::_registered = Configuration::registerPlugin<SocketApiPlugin>();
	}
}
#endif