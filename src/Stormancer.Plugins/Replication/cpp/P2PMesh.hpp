#pragma once
#include "stormancer/IPlugin.h"
#include "stormancer/SessionId.h"
#include "stormancer/Streams/bytestream.h"
#include "stormancer/PacketPriority.h"

#if !defined(STRM_PLUGIN_IMPL)
#define STRM_PLUGIN_IMPL 1
#endif

namespace Stormancer
{
	class P2PMeshService
	{
	public:
		virtual void send(const SessionId& sessionId, ::std::string route, const StreamWriter writer, PacketReliability reliability) = 0;
	};

	class P2PMeshPlugin : public IPlugin
	{
		PluginDescription getDescription();
	
		void registerSceneDependencies(ContainerBuilder& sceneBuilder, ::std::shared_ptr<Scene> scene) override;
		
	};

#if STRM_PLUGIN_IMPL == 1
#include "stormancer/Scene.h"
#include "stormancer/Serializer.h"
	class P2PMeshPlugin;
	namespace details
	{
		class P2PMeshServiceImpl: public P2PMeshService
		{
		public:
			P2PMeshServiceImpl(::std::shared_ptr<Scene> scene, ::std::shared_ptr<Serializer> serializer)
				: _scene(scene)
				, _serializer(serializer)
			{

			}
			void send(const SessionId& sessionId, ::std::string route, const StreamWriter writer, PacketReliability reliability) override
			{
				if (auto scene = _scene.lock())
				{
					auto serializer = _serializer;
					scene->send("p2pmesh.relay", [writer,sessionId,reliability,serializer,route](obytestream& stream) 
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

		private:
			::std::weak_ptr<Scene> _scene;
			::std::shared_ptr<Serializer> _serializer;
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
			sceneBuilder.registerDependency < details::P2PMeshServiceImpl, Scene, Serializer >().as<P2PMeshService>().singleInstance();
		}
	}

	
#endif
}