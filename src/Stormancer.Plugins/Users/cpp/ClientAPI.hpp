#pragma once

#include "stormancer/Scene.h"
#include "Users/Users.hpp"

namespace Stormancer
{
	template<typename TManager, typename TService>
	class ClientAPI : public std::enable_shared_from_this<TManager>
	{
	protected:

		ClientAPI(std::weak_ptr<Users::UsersApi> users, const std::string& type = "", const std::string& name = "")
			: _wUsers(users)
			, _type(type)
			, _name(name)
		{
		}

		virtual ~ClientAPI() = default;

		std::weak_ptr<TManager> weak_from_this()
		{
			return this->shared_from_this();
		}

		pplx::task<std::shared_ptr<TService>> getService(
			std::function<void(std::shared_ptr<TManager>, std::shared_ptr<TService>, std::shared_ptr<Scene>)> initializer = [](auto, auto, auto) {},
			std::function<void(std::shared_ptr<TManager>, std::shared_ptr<Scene>)> cleanup = [](auto, auto) {},
			pplx::cancellation_token ct = pplx::cancellation_token::none()
		)
		{
			if (!_serviceTask)
			{
				auto users = _wUsers.lock();
				if (!users)
				{
					STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("UsersApi"), std::shared_ptr<TService>);
				}

				std::weak_ptr<TManager> wThat = this->shared_from_this();

				if (!_scene)
				{
					_scene = std::make_shared<pplx::task<std::shared_ptr<Scene>>>(users->getSceneForService(_type, _name, ct)
						.then([wThat, cleanup](std::shared_ptr<Scene> scene)
					{
						auto that = wThat.lock();
						if (!that)
						{
							throw ObjectDeletedException("TManager");
						}

						std::weak_ptr<Scene> wScene = scene;
						that->_connectionChangedSub = scene->subscribeConnectionStateChanged([wThat, wScene, cleanup](ConnectionState state)
						{
							auto that = wThat.lock();
							if (!that)
							{
								throw ObjectDeletedException("TManager");
							}

							if (state == ConnectionState::Disconnected || state == ConnectionState::Disconnecting)
							{
								cleanup(that, wScene.lock());
								that->_connectionChangedSub = nullptr;
								that->_scene = nullptr;
								that->_serviceTask = nullptr;
							}
						});
						if (scene->getCurrentConnectionState() == ConnectionState::Disconnected || scene->getCurrentConnectionState() == ConnectionState::Disconnecting)
						{
							cleanup(that, scene);
							that->_connectionChangedSub = nullptr;
							that->_scene = nullptr;
							that->_serviceTask = nullptr;
						}
						return scene;
					})
						.then([wThat, cleanup](pplx::task<std::shared_ptr<Scene>> t)
					{
						try
						{
							return t.get();
						}
						catch (std::exception&)
						{
							auto that = wThat.lock();
							if (!that)
							{
								throw ObjectDeletedException("TManager");
							}

							cleanup(that, nullptr);
							that->_connectionChangedSub = nullptr;
							that->_scene = nullptr;
							that->_serviceTask = nullptr;
							throw;
						}
					}));
				}

				auto taskService = _scene->then([wThat, initializer](std::shared_ptr<Scene> scene)
				{
					auto service = scene->dependencyResolver().resolve<TService>();
					auto that = wThat.lock();
					if (!that)
					{
						throw ObjectDeletedException("TManager");
					}
					initializer(that, service, scene);

					return service;
				});

				_serviceTask = std::make_shared<pplx::task<std::shared_ptr<TService>>>(taskService);
			}

			if (!_serviceTask)
			{
				STORM_RETURN_TASK_FROM_EXCEPTION(std::runtime_error("service not found"), std::shared_ptr<TService>);
			}

			return *_serviceTask;
		}

	protected:

		std::weak_ptr<Users::UsersApi> _wUsers;
		
		std::string _type;

		std::string _name;

	private:

		std::shared_ptr<pplx::task<std::shared_ptr<Scene>>> _scene;

		std::shared_ptr<pplx::task<std::shared_ptr<TService>>> _serviceTask;

		Subscription _connectionChangedSub;
	};
}
