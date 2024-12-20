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
		enum class PartyMergingStatus
		{
			Unknown,
			InProgress,
			PartyFound,
			Completed,
			Cancelled,
			Error
		};

		/// <summary>
		/// The state of the merging system.
		/// </summary>
		struct PartyMergingState
		{
			/// <summary>
			/// Gets the id of the last used merger.
			/// </summary>
			std::string mergerId;

			/// <summary>
			/// Gets the last status of the merging system.
			/// </summary>
			PartyMergingStatus status = PartyMergingStatus::Unknown;

			/// <summary>
			/// Gets the last error of the merging system, if it exists.
			/// </summary>
			std::string lastError;
		};

		/// <summary>
		/// Empty merging status details structure to use by default.
		/// </summary>
		struct EmptyMergingStatusDetails
		{
			template <typename Packer> 
			void msgpack_pack(Packer& pk) const 
			{ 
				
				msgpack::type::make_define_map<>().msgpack_pack(pk);
			} 
			void msgpack_unpack(msgpack::object const& o) 
			{ 
				msgpack::type::make_define_map<>().msgpack_unpack(o);
				
			}
			template <typename MSGPACK_OBJECT> 
			void msgpack_object(MSGPACK_OBJECT* o, msgpack::zone& z) const 
			{ 
				msgpack::type::make_define_map<>().msgpack_object(o, z); 
			}
		};

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="TDetails">custom detailed merger status data provided by the merging algorithm. 
		/// Must be deserialized as a msgpack map object. The most simple way to do that is to annotate it with the MSGPACK_DEFINE_MAP macro</typeparam>
		/// <remarks>
		/// Example:
		///     struct CustomMergingStatusDetails
		///     {
		///			int customData;
		/// 
		///			MSGPACK_DEFINE_MAP(customData)
		///     }
		/// </remarks>
		template<typename TDetails>
		struct PartyMergerBaseStatus
		{
			int partiesCount;
			int playersCount;
			std::string algorithm;
			TDetails details;
			MSGPACK_DEFINE_MAP(partiesCount,playersCount,algorithm,details)
		};

		/// <summary>
		/// Response of a getPartyMergerStatus request.
		/// </summary>
		/// <typeparam name="TDetails">custom detailed merger status data provided by the merging algorithm. 
		/// Must be deserialized as a msgpack map object. The most simple way to do that is to annotate it with the MSGPACK_DEFINE_MAP macro</typeparam>
		template<typename TDetails>
		struct PartyMergerStatusResponse
		{
			/// <summary>
			/// How long to keep the result before issuing a new request to get refreshed data.
			/// </summary>
			int maxAge;

			PartyMergerBaseStatus<TDetails> data;

			MSGPACK_DEFINE(maxAge,data)
		};

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


				pplx::task<void> start(std::string& partyMerger)
				{
					auto rpc = _rpc.lock();
					return rpc->rpc("PartyMerging.Start", partyMerger);

				}

				pplx::task<void> stop(std::string& partyMerger)
				{
					auto rpc = _rpc.lock();
					return rpc->rpc("PartyMerging.Stop", partyMerger);

				}

				template<typename TDetails>
				pplx::task<PartyMergerStatusResponse<TDetails>> getMergerStatus(std::string& partyMerger)
				{
					auto rpc = _rpc.lock();
					return rpc->rpc<PartyMergerStatusResponse<TDetails>>("PartyMerging.GetMergerStatus", partyMerger);
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


		/// <summary>
		/// Interacts with the party merging plugin. Party merging matchmaker enable different parties to be merged together according to custom rules and algorithms.
		/// </summary>
		class PartyMergingApi : public std::enable_shared_from_this<PartyMergingApi>
		{
			friend class PartyMergingPlugin;
		public:

			PartyMergingApi(std::shared_ptr<Party::PartyApi> party)
				:_partyApi(party)
			{

			}


			/// <summary>
			/// Starts the merging process.
			/// </summary>
			/// <remarks>
			/// This method can only be called by the party leader.
			/// </remarks>
			/// <param name="mergerId"></param>
			/// <returns></returns>
			pplx::task<void> start(std::string mergerId)
			{
				try
				{
					return _partyApi.lock()->getPartyScene()->dependencyResolver().resolve<details::PartyMergingService>()->start(mergerId);
				}
				catch (const std::exception& ex)
				{
					return pplx::task_from_exception<void>(ex);
				}
			}

			/// <summary>
			/// Stops the merging process.
			/// </summary>
			/// <remarks>
			/// This method can only be called by the party leader.
			/// </remarks>
			/// <param name="mergerId"></param>
			/// <returns></returns>
			pplx::task<void> stop(std::string mergerId)
			{
				try
				{
					return _partyApi.lock()->getPartyScene()->dependencyResolver().resolve<details::PartyMergingService>()->stop(mergerId);
				}
				catch (const std::exception& ex)
				{
					return pplx::task_from_exception<void>(ex);
				}
			}

			template<typename TDetails = EmptyMergingStatusDetails>
			pplx::task<PartyMergerStatusResponse<TDetails>> getMergerStatus(std::string& mergerId)
			{
				try
				{
					return _partyApi.lock()->getPartyScene()->dependencyResolver().resolve<details::PartyMergingService>()->getMergerStatus<TDetails>(mergerId);
				}
				catch (const std::exception& ex)
				{
					return pplx::task_from_exception<PartyMergerStatusResponse<TDetails>>(ex);
				}
			}


			Stormancer::Event<std::string> onPartyConnectionTokenReceived;
			Stormancer::Event<std::string> onMergePartyError;
			Stormancer::Event<> onMergePartyComplete;

			PartyMergingState getStatus()
			{
				PartyMergingState state;
				if (auto party = _partyApi.lock())
				{
					if (party->isInParty())
					{
						auto data = party->getPartySettings().publicServerData;
						auto it =data.find("stormancer.partyMerging.merger");
						if (it != data.end())
						{
							state.mergerId = it->second;
						}
						it = data.find("stormancer.partyMerging.lastError");
						if (it != data.end())
						{
							state.lastError = it->second;
						}
						it = data.find("stormancer.partyMerging.status");
						if (it != data.end())
						{
							PartyMergingStatus status;
							if (it->second == "InProgress")
							{
								status = PartyMergingStatus::InProgress;
							}
							else if (it->second == "Completed")
							{
								status = PartyMergingStatus::Completed;
							}
							else if (it->second == "Cancelled")
							{
								status = PartyMergingStatus::Cancelled;
							}
							else if (it->second == "Error")
							{
								status = PartyMergingStatus::Error;
							}
							else if (it->second == "PartyFound")
							{
								status = PartyMergingStatus::PartyFound;
							}
							else
							{ 
								status = PartyMergingStatus::Unknown;
							}
							state.status = status;
						}

					}
					
				}
				return state;
			}


		private:

			bool _isProcessingMergeResponse = false;

			void initialize(std::shared_ptr<details::PartyMergingService> service)
			{
				auto wPartyApi = _partyApi;
				std::weak_ptr<PartyMergingApi> wThis = this->shared_from_this();
				onPartyConnectionTokenReceivedSubscription = service->onPartyConnectionTokenReceived.subscribe([wPartyApi, wThis](std::string connectionToken)
				{
					
					auto that = wThis.lock();
					if (that == nullptr)
					{
						return;
					}

					auto party = wPartyApi.lock();
					if (party != nullptr)
					{
						if (connectionToken.empty()) 
						{
							//We don't want to process if we didn't synchronized the party data yet, or if we are already processing a merge response.
							if (party->isInParty() && !that->_isProcessingMergeResponse)
							{
								that->onMergePartyComplete();
							}

							return;
						}
						that->_isProcessingMergeResponse = true;
						that->onPartyConnectionTokenReceived(connectionToken);

						Stormancer::taskIf(party->isInParty(), [party]() {
							return party->leaveParty();
						}).then([party, connectionToken]()
						{
							return party->joinParty(connectionToken);
						}).then([wThis](pplx::task<void> t)
						{
							try
							{
								t.get();
								if (auto that = wThis.lock())
								{
									that->_isProcessingMergeResponse = false;
									that->onMergePartyComplete();
									
								}
							}
							catch (std::exception& ex)
							{
								if (auto that = wThis.lock())
								{
									that->_isProcessingMergeResponse = false;
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

			void sceneCreated(std::shared_ptr<Scene> scene) override
			{
				if (scene)
				{
					auto name = scene->getHostMetadata("stormancer.partyMerging");

					if (!name.empty())
					{
						auto service = scene->dependencyResolver().resolve<details::PartyMergingService>();
						service->initialize(scene);
					}
				}

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

