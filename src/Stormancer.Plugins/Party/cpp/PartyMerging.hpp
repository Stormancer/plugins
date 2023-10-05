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
#include "Party/Party.hpp"

namespace Stormancer
{
	namespace Party
	{


		class PartyMergingPlugin;

		namespace details
		{
			class PartyMergingService : public std::enable_shared_from_this<PartyMergingService>
			{
			public:

				PartyMergingService(std::weak_ptr<RpcService> rpc)
					: _rpc(rpc)
				{
				}


				pplx::task<void> start(std::string& partyMerger, pplx::cancellation_token cancellationToken)
				{
					auto rpc = _rpc.lock();
					return rpc->rpc("PartyMerging.Start", cancellationToken, partyMerger);

				}

				void initialize(std::shared_ptr<Stormancer::Scene> scene)
				{
					std::weak_ptr<PartyMergingService> wThat = this->shared_from_this();
					scene->addRoute("partyMerging.connectionToken", [wThat](Packetisp_ptr packet) {
						if (auto that = wThat.lock())
						{
							Serializer serializer;
							auto connectionToken = serializer.deserializeOne<std::string>(packet->stream);

							that->raiseConnectionTokenReceived(connectionToken);
						}
					});
				}

				Stormancer::Event<std::string> onPartyConnectionTokenReceived;
			private:

				void raiseConnectionTokenReceived(std::string connectionToken)
				{
					onPartyConnectionTokenReceived(connectionToken);
				}

				std::weak_ptr<RpcService> _rpc;
			};
		}

		class PartyMergingPlugin;
		class PartyMergingApi : public std::enable_shared_from_this<PartyMergingApi>
		{
			friend class PartyMergingPlugin;
		public:

			PartyMergingApi(std::shared_ptr<Party::PartyApi> party)
				:_partyApi(party)
			{

			}
			pplx::task<void> start(std::string mergerId,pplx::cancellation_token cancellationToken = pplx::cancellation_token::none())
			{
				return _partyApi.lock()->getPartyScene()->dependencyResolver().resolve<details::PartyMergingService>()->start(mergerId,cancellationToken);
			}

			Stormancer::Event<std::string> onPartyConnectionTokenReceived;
			Stormancer::Event<std::string> onMergePartyError;
			Stormancer::Event<> onMergePartyComplete;



		private:
			void initialize(std::shared_ptr<details::PartyMergingService> service)
			{
				auto wPartyApi = _partyApi;
				std::weak_ptr<PartyMergingApi> wThis = this->shared_from_this();
				onPartyConnectionTokenReceivedSubscription = service->onPartyConnectionTokenReceived.subscribe([wPartyApi, wThis](std::string connectionToken)
				{
					auto party = wPartyApi.lock();
					

					if (party != nullptr)
					{

						party->joinParty(connectionToken).then([wThis](pplx::task<void> t)
						{
							try
							{
								t.get();
								if (auto that = wThis.lock())
								{
									that->onMergePartyComplete();
								}
							}
							catch (std::exception& ex)
							{
								if (auto that = wThis.lock())
								{
									that->onMergePartyError(ex.what());
								}
							}

						});

					}
				});
			}
			void shutdown()
			{
				onPartyConnectionTokenReceivedSubscription.reset();
			}
			std::weak_ptr<Party::PartyApi> _partyApi;

			Stormancer::Subscription onPartyConnectionTokenReceivedSubscription;
		};

		class PartyMergingPlugin : public Stormancer::IPlugin
		{
		public:

			static constexpr const char* PLUGIN_NAME = "PartyMerging";
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
					auto name = scene->getHostMetadata("stormancer.partyMerging");

					if (!name.empty())
					{
						builder.registerDependency<Stormancer::Party::details::PartyMergingService, RpcService>().singleInstance();
					}
				}

			}
			void registerClientDependencies(Stormancer::ContainerBuilder& builder) override
			{
				builder.registerDependency<Stormancer::Party::PartyMergingApi, Stormancer::Party::PartyApi>().as<Stormancer::Party::PartyMergingApi>().singleInstance();
			}

			void sceneConnected(std::shared_ptr<Scene> scene) override
			{
				if (scene)
				{
					auto name = scene->getHostMetadata("stormancer.partyMerging");

					if (!name.empty())
					{
						auto service = scene->dependencyResolver().resolve<details::PartyMergingService>();
						auto api = scene->dependencyResolver().resolve<PartyMergingApi>();
						api->initialize(service);
					}
				}
			}

			void sceneDisconnecting(std::shared_ptr<Scene> scene) override
			{
				if (scene)
				{
					auto name = scene->getHostMetadata("stormancer.partyMerging");

					if (!name.empty())
					{

						auto api = scene->dependencyResolver().resolve<PartyMergingApi>();
						api->shutdown();
					}
				}
			}


		};

	}

}

