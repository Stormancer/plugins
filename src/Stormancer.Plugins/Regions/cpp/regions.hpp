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
#include "stormancer/cpprestsdk/cpprest/http_client.h"


namespace Stormancer
{
	namespace Regions
	{
		

		class RegionsPlugin;

		namespace details
		{
			struct TestRegionsArguments
			{
				std::unordered_map<std::string, std::string> testIps;

				MSGPACK_DEFINE(testIps)
			};
			struct LatencyTestResult
			{
				std::string regionName;
				int latency;

				MSGPACK_DEFINE(regionName,latency)
			};

			struct TestRegionsResponse
			{
				std::vector<LatencyTestResult> results;
				MSGPACK_DEFINE(results)
			};

			class RegionsService : public std::enable_shared_from_this<RegionsService>
			{
				friend RegionsPlugin;

			public:

				RegionsService(std::shared_ptr<RpcService> rpc)
					: rpc(rpc)
				{
				}

				

			private:

				void initialize(std::shared_ptr<Scene> scene)
				{
					std::weak_ptr<RegionsService> wThat = this->shared_from_this();
					rpc->addProcedure("regions.testIps", [wThat](RpcRequestContext_ptr ctx)
					{
						if (auto that = wThat.lock())
						{
							auto args = ctx->readObject<TestRegionsArguments>();
							return that->testRegions(args,ctx->cancellationToken()).then([ctx](TestRegionsResponse response) 
							{
								ctx->sendValueTemplated(response, PacketPriority::MEDIUM_PRIORITY);
							});							
						}
						else
						{
							return pplx::task_from_result();
						}

					});
				}

				pplx::task<TestRegionsResponse> testRegions(const TestRegionsArguments& args,const pplx::cancellation_token& cancellationToken)
				{
					std::vector<pplx::task<LatencyTestResult>> testResultsTasks;
					for (auto test : args.testIps)
					{
						testResultsTasks.push_back(TestLatency(test.first, test.second, cancellationToken));
					}

					return pplx::when_all(testResultsTasks.begin(), testResultsTasks.end()).then([](std::vector<LatencyTestResult> results)
					{
						return TestRegionsResponse{ results };
					});
				}

				pplx::task<LatencyTestResult> TestLatency(std::string regionName, std::string endpoint, const pplx::cancellation_token& cancellationToken)
				{
					web::http::client::http_client client = web::http::client::http_client(Stormancer::web::uri(Stormancer::utility::conversions::to_string_t(endpoint)));
					auto start = std::chrono::system_clock::now();
					
					return client.request(Stormancer::web::http::methods::GET, cancellationToken).then([start,regionName](pplx::task<web::http::http_response> task) 
					{
						try
						{
							auto end = std::chrono::system_clock::now();
							auto latency = std::chrono::duration_cast<std::chrono::milliseconds>(end-start).count() / 2;
							auto response = task.get();
							return LatencyTestResult{ regionName,(int)latency };
						}
						catch (std::exception&)
						{
							return LatencyTestResult{ regionName,std::numeric_limits<int>::max() };
						}
					});
				}
			
				std::shared_ptr<RpcService> rpc;
				
			};
		}


		

		class RegionsPlugin : public Stormancer::IPlugin
		{
		public:

			static constexpr const char* PLUGIN_NAME = "Regions";
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
					auto name = scene->id();

					if (name == "authenticator")
					{
						builder.registerDependency<Stormancer::Regions::details::RegionsService, RpcService>();
					}
				}
			}

			
			void sceneCreated(std::shared_ptr<Scene> scene) override
			{
				if (scene)
				{
					auto name = scene->id();

					if (name == "authenticator")
					{
						auto service = scene->dependencyResolver().resolve<details::RegionsService>();
						service->initialize(scene);
					}
				}
			}
		};
	}
}
