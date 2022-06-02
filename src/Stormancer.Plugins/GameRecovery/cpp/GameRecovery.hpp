#pragma once

#include "stormancer/msgpack_define.h"
#include "stormancer/Tasks.h"
#include "stormancer/Scene.h"
#include "stormancer/RPC/RpcService.h"
#include "stormancer/IPlugin.h"
#include "Users/ClientAPI.hpp"
#include "Users/Users.hpp"
#include <string>

namespace Stormancer
{
	namespace GameRecovery
	{
		struct RecoverableGameDto
		{
			std::string gameId;
			std::string userData;
			MSGPACK_DEFINE(gameId, userData)
		};

		struct RecoverableGame
		{
			std::string gameId;
			std::string userData;
		};

		class GameRecovery
		{
		public:
			virtual ~GameRecovery() {}
			virtual pplx::task<std::shared_ptr<RecoverableGame>> getCurrent() = 0;

			virtual pplx::task<void> cancelCurrent() = 0;
		};

		namespace details
		{
			class GameRecoveryService
			{
			public:
				GameRecoveryService(std::shared_ptr<Scene> scene)
					: _scene(scene)
					, _rpcService(scene->dependencyResolver().resolve<RpcService>())
				{
				}

				pplx::task<std::shared_ptr<RecoverableGameDto>> getCurrent()
				{
					return _rpcService.lock()->rpc<std::shared_ptr<RecoverableGameDto>>("gamerecovery.getcurrent");
				}

				pplx::task<void> cancelCurrent()
				{
					return _rpcService.lock()->rpc<void>("gamerecovery.cancelcurrent");
				}

			private:
				std::weak_ptr<Scene> _scene;
				std::weak_ptr<RpcService> _rpcService;
			};

			class GameRecovery_Impl : public ClientAPI<GameRecovery_Impl, GameRecoveryService>, public GameRecovery
			{
			public:
				GameRecovery_Impl(std::weak_ptr<Users::UsersApi> users)
					: ClientAPI(users, "stormancer.gameRecovery")
				{
				}

				pplx::task<std::shared_ptr<RecoverableGame>> getCurrent() override
				{
					return getGRService()
						.then([](std::shared_ptr<GameRecoveryService> gr) {
						return gr->getCurrent();
					})
						.then([](std::shared_ptr<RecoverableGameDto> dto) {

						if (dto)
						{
							auto result = std::make_shared<RecoverableGame>();
							result->gameId = dto->gameId;
							result->userData = dto->userData;
							return result;
						}
						else
						{
							return (std::shared_ptr<RecoverableGame>)nullptr;
						}
					});
				}

				pplx::task<void> cancelCurrent() override
				{
					return getGRService().then([](std::shared_ptr<GameRecoveryService> gr) { return gr->cancelCurrent(); });
				}

			private:
				pplx::task<std::shared_ptr<GameRecoveryService>> getGRService()
				{
					return this->getService();
				}
			};
		}

		class GameRecoveryPlugin : public IPlugin
		{
		public:

			static constexpr const char* PLUGIN_NAME = "GameRecovery";
			static constexpr const char* PLUGIN_VERSION = "1.0.0";

			PluginDescription getDescription() override
			{
				return PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
			}

		private:

			void registerSceneDependencies(ContainerBuilder& builder, std::shared_ptr<Scene> scene) override
			{
				if (scene)
				{
					auto name = scene->getHostMetadata("stormancer.gameRecovery");

					if (!name.empty())
					{
						builder.registerDependency<details::GameRecoveryService, Scene>().singleInstance();
					}
				}
			}

			void registerClientDependencies(ContainerBuilder& builder) override
			{
				builder.registerDependency<details::GameRecovery_Impl, Users::UsersApi>().as<GameRecovery>().singleInstance();
			}
		};
	}
}