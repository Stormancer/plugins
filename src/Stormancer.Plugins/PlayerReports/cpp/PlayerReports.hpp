// MIT License
//
// Copyright (c) 2020 Stormancer
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

#pragma once
#include "Users/ClientAPI.hpp"
#include "stormancer/IPlugin.h"

namespace Stormancer
{
	namespace Reports
	{


		class ReportsPlugin;

		namespace details
		{
			class ReportsService
			{
			public:

				ReportsService(std::weak_ptr<RpcService> rpc, std::shared_ptr<Serializer> serializer)
					: _serializer(serializer)
					, _rpc(rpc)
				{
				}

				template<class T>
				pplx::task<void> createPlayerReport(std::string targetUserId, std::string message, T& customContext)
				{
					auto rpc = _rpc.lock();
					return rpc->rpc("Reports.CreatePlayerReport", targetUserId, message, customContext);

				}


				template<class T>
				pplx::task<void> createBugReport(const std::string message, const T& customContext, const char* data, const int length)
				{
					auto rpc = _rpc.lock();
					auto serializer = _serializer;
					return rpc->rpc("Reports.CreateBugReport", [serializer, message, customContext, data, length](obytestream& stream)
					{
						serializer->serialize(stream, message, customContext, length);
						stream.write(data, length);
					});
				}


			private:

				std::shared_ptr<Serializer> _serializer;
				std::weak_ptr<RpcService> _rpc;
			};
		}

		class ReportsPlugin;
		class ReportsApi : public  ClientAPI<ReportsApi, details::ReportsService>
		{
			friend class ReportsPlugin;
		public:

			ReportsApi(std::weak_ptr<Users::UsersApi> users)
				: ClientAPI(users, "stormancer.reports")
			{

			}


			template<class T>
			pplx::task<void> createPlayerReport(std::string targetUserId, std::string message, T& customContext)
			{
				return getService().then([targetUserId, message, customContext](std::shared_ptr<details::ReportsService> service)
				{
					return service->createPlayerReport(targetUserId, message, customContext);
				});
			}

			template<class T>
			pplx::task<void> createBugReport(const std::string message, const T& customContext, const char* data, const int length)
			{
				if (length > 500 * 1024)//500ko max
				{
					rturn pplx::task_from_exception<bool>(std::runtime_error("data connot be more than 500kb"));
				}
				return getService().then([message, customContext, data, length](std::shared_ptr<details::ReportsService> service)
				{
					return service->createBugReport(message, customContext, data, length);
				});
			}


		private:

		};

		class ReportsPlugin : public Stormancer::IPlugin
		{
		public:

			static constexpr const char* PLUGIN_NAME = "PlayerReports";
			static constexpr const char* PLUGIN_VERSION = "1.0.0";

			PluginDescription getDescription() override
			{
				return PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
			}

		private:

			void registerSceneDependencies(Stormancer::ContainerBuilder& builder, std::shared_ptr<Stormancer::Scene> scene) override
			{

				if (scene)
				{
					auto name = scene->getHostMetadata("stormancer.reports");

					if (!name.empty())
					{
						builder.registerDependency<Stormancer::Reports::details::ReportsService, RpcService, Serializer>().singleInstance();
					}
				}

			}
			void registerClientDependencies(Stormancer::ContainerBuilder& builder) override
			{
				builder.registerDependency<Stormancer::Reports::ReportsApi, Stormancer::Users::UsersApi>().as<Stormancer::Reports::ReportsApi>().singleInstance();
			}



		};

	}

}

