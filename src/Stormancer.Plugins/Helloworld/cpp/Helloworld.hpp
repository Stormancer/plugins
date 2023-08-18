#pragma once
#include "Users/ClientAPI.hpp"
#include "Users/Users.hpp"
#include "stormancer/IPlugin.h"

namespace Stormancer
{
	namespace Helloworld
	{
		class HelloworldPlugin;
		namespace details
		{

			class HelloService :public std::enable_shared_from_this<HelloService>
			{
				friend class Helloworld::HelloworldPlugin;
			public:
				HelloService(std::shared_ptr<Stormancer::RpcService> rpc) : _rpcService(rpc)
				{
				}

				pplx::task<std::string> world(std::string name)
				{
					auto rpc = _rpcService.lock();
					if (!rpc)
					{
						throw Stormancer::ObjectDeletedException("Scene");
					}
					return rpc->rpc<std::string>("Hello.World", name);
				}
			};
		}

		class Hello : public Stormancer::ClientAPI<Hello, details::HelloService>
		{
			friend class HelloworldPlugin;

		public:

			Hello(std::weak_ptr<Users::UsersApi> users)
				: Stormancer::ClientAPI<Hello, details::HelloService>(users, "helloworld")
			{
			}

			pplx::task<std::string> world(std::string name)
			{
				return this->getService()
					.then([name](std::shared_ptr<details::HelloService> hello)
				{
					return hello->world(name);
				});
			}
			

		
		};

		class HelloworldPlugin : public Stormancer::IPlugin
		{
		public:

			static constexpr const char* PLUGIN_NAME = "Helloworld";
			static constexpr const char* PLUGIN_VERSION = "1.0.0";

			PluginDescription getDescription() override
			{
				return PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
			}

		private:

			void registerSceneDependencies(Stormancer::ContainerBuilder& builder, std::shared_ptr<Stormancer::Scene> scene) override
			{

				auto name = scene->getHostMetadata("helloworld");

				if (!name.empty())
				{
					builder.registerDependency<details::HelloService,Stormancer::RpcService>().singleInstance();
				}

			}

			void registerClientDependencies(Stormancer::ContainerBuilder& builder) override
			{
				builder.registerDependency<Hello, Stormancer::Users::UsersApi>().singleInstance();
			}
		};
	}
}
