#pragma once
#include "stormancer/Event.h"
#include "stormancer/Tasks.h"
#include "stormancer/Streams/bytestream.h"
#include "stormancer/Packet.h"
#include "stormancer/StormancerTypes.h"
#include "stormancer/msgpack_define.h"
#include "stormancer/Scene.h"
#include "users/Users.hpp"
#include <unordered_map>

namespace Stormancer
{
	/// <summary>
	/// The <c>GameFinder</c> enables parties or indivdual players to find Game Sessions according to custom server-side logic.
	/// </summary>
	namespace GameFinder
	{
		enum class Readiness
		{
			Unknown = 0,
			Ready = 1,
			NotReady = 2
		};

		enum class GameFinderStatus
		{
			Idle = -1,
			Searching = 0,
			CandidateFound = 1,
			WaitingPlayersReady = 2,
			Success = 3,
			Failed = 4,
			Canceled = 5,
			Loading = 6
		};

		namespace details
		{
			class GameFinderService;
		}

		struct GameFinderResponse
		{
			friend class details::GameFinderService;

		public:

			std::string connectionToken;

			template<typename TData>
			TData readData()
			{
				Serializer serializer;
				return serializer.deserializeOne<TData>(packet->stream);
			}

			template<typename... TData>
			void readData(TData&... tData)
			{
				Serializer serializer;
				return serializer.deserialize<TData...>(packet->stream, tData...);
			}

		private:

			Packetisp_ptr packet;
		};

		struct GameFinderStatusChangedEvent
		{
			GameFinderStatus status;
			std::string gameFinder;
		};

		struct GameFoundEvent
		{
			std::string gameFinder;
			GameFinderResponse data;
		};

		struct FindGameFailedEvent
		{
			std::string reason;
			std::string gameFinder;
		};

		/// <summary>
		/// This class is the entry point for using the GameFinder.
		/// </summary>
		class GameFinderApi
		{
		public:
			virtual ~GameFinderApi() = default;

			/// <summary>
			/// Start a GameFinder query.
			/// Only if you do not use the Party system.
			/// </summary>
			/// <remarks>
			/// This method will attempt to connect to the server and the scene for the given <c>gameFinder</c> if the client is not yet connected to them.
			/// After the query has started, the server will notify you when a status update occurs.
			/// You should listen to these updates by providing callbacks to <c>subscribeGameFinderStateChanged()</c> and <c>subscribeGameFound()</c>.
			/// If you want to cancel the request, you should call <c>cancel()</c>, with the same <c>gameFinder</c> as the one passed to <c>findGame()</c>.
			/// We use this technique here instead of the more common <c>pplx::cancellation_token</c>-based one in order to support party scenarios,
			/// where a member of a party can cancel the party-wide GameFinder query.
			/// For parties:
			/// Do not use this method if you are in a party, as the GameFinder query will be initiated automatically by the server when all party members are ready.
			/// </remarks>
			/// <param name="gameFinder">Name of the server-side GameFinder to connect to.
			/// This will typically be the name of a scene, configured in the serviceLocator of the server application.</param>
			/// <param name="provider">Name of the provider to use for the given <c>gameFinder</c>.</param>
			/// <param name="json">Custom JSON data to send along the FindGame request.</param>
			/// <returns>A <c>pplx::task</c> that completes when the request is done.
			/// This task will complete when either one of the following happens:
			/// * A game is found
			/// * An error occurs on the server-side GameFinder
			/// * The request is canceled with a call to <c>cancel()</c>.</returns>
			virtual pplx::task<void> findGame(const std::string& gameFinder, const std::string& provider, const StreamWriter& streamWriter, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			template<typename... TData>
			pplx::task<void> findGame(const std::string& gameFinder, const std::string &provider, pplx::cancellation_token ct = pplx::cancellation_token::none(), TData... tData)
			{
				StreamWriter streamWriter = [tData...](obytestream& stream)
				{
					Serializer serializer;
					serializer.serialize(stream, tData...);
				};
				return findGame(gameFinder, provider, streamWriter, ct);
			}

			/// <summary>
			/// Cancel an ongoing <c>findGame</c> request.
			/// </summary>
			/// <remarks>
			/// You should call this method only after you have received the initial <c>GameFinderStatusChangedEvent</c> with <c>GameFinderStatus::Searching</c>,
			/// or else you might run into a race condition and the cancel request might not register.
			/// </remarks>
			/// <param name="gameFinder">Name of the GameFinder for which you want to cancel the search.</param>
			virtual void cancel(const std::string& gameFinder) = 0;

			/// <summary>
			/// Retrieve the current status of ongoing <c>findGame</c> requests for each GameFinder.
			/// </summary>
			/// <returns>A map with the GameFinder name as key, and the <c>findGame</c> request status as value.</returns>
			virtual std::unordered_map<std::string, GameFinderStatusChangedEvent> getPendingFindGameStatus() = 0;

			/// <summary>
			/// Connect to the scene that contains the given GameFinder.
			/// </summary>
			/// <remarks>This will use the server application's ServiceLocator configuration to determine which scene to connect to for the given <c>gameFinderName</c>.</remarks>
			/// <param name="gameFinderName">Name of the GameFinder to connect to.</param>
			/// <returns>A <c>pplx::task</c> that completes when the connection to the scene that contains <c>gameFinderName</c> has completed.</returns>
			virtual pplx::task<void> connectToGameFinder(const std::string& gameFinderName, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			/// <summary>
			/// Disconnect from the scene that contains the given GameFinder.
			/// </summary>
			/// <param name="gameFinderName">Name of the GameFinder which scene you want to disconnect from.</param>
			/// <returns>A <c>pplx::task</c> that completes when the scene disconnection has completed.</returns>
			virtual pplx::task<void> disconnectFromGameFinder(const std::string& gameFinderName, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			/// <summary>
			/// Subscribe to <c>findGame</c> status notifications.
			/// </summary>
			/// <param name="callback">Callable object to be called when a <c>findGame</c> request status update occurs.</param>
			/// <returns>A reference-counted <c>Subscription</c> object that tracks the lifetime of the subscription.
			/// When the reference count of this object drops to zero, the subscription will be canceled.</returns>
			virtual Subscription subscribeGameFinderStateChanged(std::function<void(GameFinderStatusChangedEvent)> callback) = 0;

			/// <summary>
			/// Subscribe to <c>GameFoundEvent</c> notifications.
			/// </summary>
			/// <remarks>A <c>GameFoundEvent</c> is triggered when a <c>findGame</c> request succeeds.
			/// It carries the information you need to join the game that the GameFinder has found for you.</remarks>
			/// <param name="callback">Callable object to be called when a <c>GameFoundEvent</c> occurs.</param>
			/// <returns>A reference-counted <c>Subscription</c> object that tracks the lifetime of the subscription.
			/// When the reference count of this object drops to zero, the subscription will be canceled.</returns>
			virtual Subscription subscribeGameFound(std::function<void(GameFoundEvent)> callback) = 0;

			/// <summary>
			/// Subscribe to findGameFailed event.
			/// </summary>
			/// <remarks>
			/// A FindGame failure could be caused by a variety of reasons, including but not limited to custom GameFinder logic.
			/// The <c>FindGameFailedEvent</c> argument passed to the <c>callback</c> may contain the reason for the failure.
			/// </remarks>
			/// <param name="callback">Callable object to be called when a FindGame failure occurs.</param>
			/// <returns>A reference-counted <c>Subscription</c> object that tracks the lifetime of the subscription.
			/// When the reference count of this object drops to zero, the subscription will be canceled.</returns>
			virtual Subscription subscribeFindGameFailed(std::function<void(FindGameFailedEvent)> callback) = 0;

			virtual pplx::task<std::unordered_map<std::string, int>> getMetrics(const std::string& gameFinderName, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			/// <summary>
			/// Returns a task that completes the next time a game is found and fails when game finding fails.
			/// </summary>
			/// <returns>A task that completes </returns>
			pplx::task<GameFoundEvent> waitGameFound(pplx::cancellation_token ct = pplx::cancellation_token::none())
			{
				pplx::task_completion_event<GameFoundEvent> tce;

				if (ct.is_cancelable())
				{
					ct.register_callback([tce]()
					{
						tce.set_exception(pplx::task_canceled());
					});
				}

				auto foundSubscription = this->subscribeGameFound([tce](GameFoundEvent ev)
				{
					tce.set(ev);
				});

				auto failedSubscription = this->subscribeFindGameFailed([tce](FindGameFailedEvent ev)
				{
					tce.set_exception(std::runtime_error(ev.reason));
				});

				//The continuation is there only to make sure the subscriptions don't expire before task completion.
				return pplx::create_task(tce).then([foundSubscription, failedSubscription](GameFoundEvent ev) { return ev; });
			}
		};

		namespace details
		{
			class GameFinderService : public std::enable_shared_from_this<GameFinderService>
			{
			public:

				GameFinderService(std::shared_ptr<Scene> scene)
					: _scene(scene)
					, _rpcService(scene->dependencyResolver().resolve<RpcService>())
				{
				}

				~GameFinderService()
				{
					// In case the scene gets brutally destroyed without a chance to trigger onDisconnecting, make sure to notify subscribers
					onSceneDisconnecting();
				}

				GameFinderService(const GameFinderService& other) = delete;
				GameFinderService(const GameFinderService&& other) = delete;
				GameFinderService& operator=(const GameFinderService&& other) = delete;

				void initialize()
				{
					std::weak_ptr<GameFinderService> wThat = this->shared_from_this();
					_scene.lock()->addRoute("gamefinder.update", [wThat](Packetisp_ptr packet)
					{
						byte gameStateByte;
						packet->stream.read(&gameStateByte, 1);
						int32 gameState = gameStateByte;

						if (auto that = wThat.lock())
						{
							that->_currentState = (GameFinderStatus)gameState;

							auto ms = std::to_string(gameState);

							that->GameFinderStatusUpdated(that->_currentState);

							switch (that->_currentState)
							{
							case GameFinderStatus::Success:
							{
								auto connectionToken = that->_serializer.deserializeOne<std::string>(packet->stream);

								GameFinderResponse response;
								response.connectionToken = connectionToken;
								response.packet = packet;

								that->GameFound(response);
								that->_currentState = GameFinderStatus::Idle;
								that->GameFinderStatusUpdated(that->_currentState);
								break;
							}
							case GameFinderStatus::Canceled:
							{
								that->_currentState = GameFinderStatus::Idle;
								that->GameFinderStatusUpdated(that->_currentState);
								break;
							}
							case GameFinderStatus::Failed:
							{
								std::string reason;
								// There may or may not be a reason string supplied with the failure notification, so check if the stream has more data
								if (packet->stream.good() && packet->stream.availableSize() > 0)
								{
									reason = that->_serializer.deserializeOne<std::string>(packet->stream);
								}
								that->FindGameRequestFailed(reason);
								that->_currentState = GameFinderStatus::Idle;
								that->GameFinderStatusUpdated(that->_currentState);
								break;
							}
							default:
								// ignore
								break;
							}
						}
					});
				}

				GameFinderStatus currentState() const
				{
					return _currentState;
				}

				pplx::task<void> findGame(const std::string &provider, const StreamWriter& streamWriter, pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					return findGameInternal(provider, streamWriter, ct);
				}

				void resolve(bool acceptGame)
				{
					auto scene = _scene.lock();
					scene->send("gamefinder.ready.resolve", [=](obytestream& stream)
					{
						stream << acceptGame;
					}, PacketPriority::MEDIUM_PRIORITY, PacketReliability::RELIABLE_ORDERED);
				}

				// If cancel() is called very shortly after findGame(), there might be a race condition.
				// It can be prevented by waiting for the first GameFinderStatusUpdated event received after calling findGame() before calling cancel().
				void cancel()
				{
					if (currentState() != GameFinderStatus::Idle)
					{
						auto scene = _scene.lock();
						_gameFinderCTS.cancel();
						scene->send("gamefinder.cancel", [](obytestream&) {}, PacketPriority::IMMEDIATE_PRIORITY, PacketReliability::RELIABLE_ORDERED);
					}
				}

				// This should only be called by GameFinderPlugin.
				void onSceneDisconnecting()
				{
					if (_currentState != GameFinderStatus::Idle &&
						_currentState != GameFinderStatus::Canceled &&
						_currentState != GameFinderStatus::Failed &&
						_currentState != GameFinderStatus::Success)
					{
						_currentState = GameFinderStatus::Failed;
						GameFinderStatusUpdated(_currentState);
					}
				}

				template<typename... TData>
				pplx::task<void> findGame(const std::string &provider, pplx::cancellation_token ct = pplx::cancellation_token::none(), const TData&... tData)
				{
					return findGameInternal(provider, ct, tData...);
				}

				pplx::task<std::unordered_map<std::string, int>> getMetrics(pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					return _rpcService->rpc<std::unordered_map<std::string, int>>("gamefinder.getmetrics", ct);
				}

				Event<GameFinderStatus> GameFinderStatusUpdated;
				Event<GameFinderResponse> GameFound;
				Event<std::string> FindGameRequestFailed;

			private:

				pplx::task<void> findGameInternal(const std::string& provider, const StreamWriter& streamWriter, pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					if (currentState() != GameFinderStatus::Idle)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION(std::runtime_error("Already finding a game !"), void);
					}

					_currentState = GameFinderStatus::Searching;
					_gameFinderCTS = pplx::cancellation_token_source();

					StreamWriter streamWriter2 = [provider, streamWriter](obytestream& stream)
					{
						Serializer serializer;
						serializer.serialize(stream, provider);
						streamWriter(stream);
					};

					std::weak_ptr<GameFinderService> wThat = this->shared_from_this();
					return _rpcService->rpc("gamefinder.find", ct, streamWriter2)
						.then([wThat](pplx::task<void> res)
					{
						// If the RPC fails (e.g. because of a disconnection), we might not have received a failed/canceled status update.
						// Make sure we go back to Idle state anyway.
						try
						{
							res.get();
						}
						catch (...)
						{
							if (auto that = wThat.lock())
							{
								if (that->_currentState != GameFinderStatus::Idle)
								{
									that->_currentState = GameFinderStatus::Idle;
									that->GameFinderStatusUpdated(that->_currentState);
								}
							}
							throw;
						}
						return res;
					});
				}

				template<typename... TData>
				pplx::task<void> findGameInternal(const std::string& provider, pplx::cancellation_token ct = pplx::cancellation_token::none(), TData... tData)
				{
					StreamWriter streamWriter = [tData...](obytestream* stream)
					{
						Serializer serializer;
						serializer.serialize(stream, tData...);
					};
					return findGameInternal(provider, streamWriter, ct);
				}

				std::weak_ptr<Scene> _scene;
				std::shared_ptr<RpcService> _rpcService;

				pplx::cancellation_token_source _gameFinderCTS;

				GameFinderStatus _currentState = GameFinderStatus::Idle;
				Serializer _serializer;

				std::shared_ptr<ILogger> _logger;
				std::string _logCategory = "GameFinder";
			};

			struct GameFinderContainer
			{
				~GameFinderContainer()
				{
					if (connectionStateChangedSubscription.is_subscribed())
					{
						connectionStateChangedSubscription.unsubscribe();
					}
				}

				//Keep game finder scene alive
				std::shared_ptr<Scene> scene;
				std::shared_ptr<GameFinderService> service()
				{
					return scene->dependencyResolver().resolve<GameFinderService>();
				}

				Subscription gameFoundSubscription;
				Subscription gameFinderStateUpdatedSubscription;
				Subscription findGamefailedSubscription;
				rxcpp::subscription connectionStateChangedSubscription;
			};

			class GameFinder_Impl : public std::enable_shared_from_this<GameFinder_Impl>, public GameFinderApi
			{
			public:

				GameFinder_Impl(std::weak_ptr<Users::UsersApi> users)
				{
					_users = users;
				}

				pplx::task<void> findGame(const std::string& gameFinder, const std::string& provider, const StreamWriter& streamWriter, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					std::weak_ptr<GameFinder_Impl> wThat = this->shared_from_this();

					pplx::cancellation_token_source pendingRequest;

					{
						std::lock_guard<std::recursive_mutex> lg(_lock);

						if (_pendingFindGameRequests.find(gameFinder) != _pendingFindGameRequests.end())
						{
							STORM_RETURN_TASK_FROM_EXCEPTION(std::runtime_error("A findGame request is already running for GameFinder '" + gameFinder + "'"), void);
						}
						_pendingFindGameRequests.emplace(gameFinder, pendingRequest);
					}

					auto cts = create_linked_source(ct, pendingRequest.get_token());
					auto newCt = cts.get_token();

					return getGameFinderContainer(gameFinder, newCt)
						.then([provider, streamWriter, gameFinder, newCt](std::shared_ptr<GameFinderContainer> gameFinderContainer)
					{
						if (newCt.is_canceled())
						{
							pplx::cancel_current_task();
						}
						auto findGameTask = gameFinderContainer->service()->findGame(provider, streamWriter, newCt);
						newCt.register_callback([gameFinderContainer] { gameFinderContainer->service()->cancel(); });
						return findGameTask;
					})
						.then([wThat, gameFinder](pplx::task<void> task)
					{
						if (auto that = wThat.lock())
						{
							std::lock_guard<std::recursive_mutex> lg(that->_lock);
							that->_pendingFindGameRequests.erase(gameFinder);
						}
						return task;
					});
				}

				void cancel(const std::string& gameFinder) override
				{
					std::lock_guard<std::recursive_mutex> lg(this->_lock);

					auto findGameRequest = _pendingFindGameRequests.find(gameFinder);
					if (findGameRequest != _pendingFindGameRequests.end())
					{
						findGameRequest->second.cancel();
					}
				}

				Event<GameFinderStatusChangedEvent> gameFinderStateChanged;
				Event<GameFoundEvent> gameFound;
				Event<FindGameFailedEvent> findGameFailed;

				std::unordered_map<std::string, GameFinderStatusChangedEvent> getPendingFindGameStatus() override
				{
					std::unordered_map<std::string, GameFinderStatusChangedEvent> result;
					for (auto gameFinder : _gameFinders)
					{
						GameFinderStatusChangedEvent status;
						status.gameFinder = gameFinder.first;
						auto task = gameFinder.second;
						if (task.is_done())
						{

							auto container = task.get();
							status.status = container->service()->currentState();
						}
						else
						{
							status.status = GameFinderStatus::Loading;
						}
						result.emplace(gameFinder.first, status);
					}

					return result;
				}

				pplx::task<void> connectToGameFinder(const std::string& gameFinderName, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					return getGameFinderContainer(gameFinderName, ct).then([](std::shared_ptr<GameFinderContainer>) {});
				}

				pplx::task<void> disconnectFromGameFinder(const std::string& gameFinderName, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					std::lock_guard<std::recursive_mutex> lg(this->_lock);
					auto it = _gameFinders.find(gameFinderName);
					if (it != _gameFinders.end())
					{
						auto containerTask = it->second;
						_gameFinders.erase(it);
						return containerTask.then([ct](std::shared_ptr<GameFinderContainer> gameFinder)
						{
							return gameFinder->scene->disconnect(ct);
						});
					}
					return pplx::task_from_result();
				}

				Subscription subscribeGameFinderStateChanged(std::function<void(GameFinderStatusChangedEvent)> callback) override
				{
					return gameFinderStateChanged.subscribe(callback);
				}

				Subscription subscribeGameFound(std::function<void(GameFoundEvent)> callback)  override
				{
					return gameFound.subscribe(callback);
				}

				Subscription subscribeFindGameFailed(std::function<void(FindGameFailedEvent)> callback) override
				{
					return findGameFailed.subscribe(callback);
				}

				pplx::task<std::unordered_map<std::string, int>> getMetrics(const std::string& gameFinderName, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					return getGameFinderContainer(gameFinderName, ct)
						.then([ct](std::shared_ptr<GameFinderContainer> gameFinderContainer)
					{
						return gameFinderContainer->service()->getMetrics(ct);
					});
				}

			private:

				pplx::task<std::shared_ptr<GameFinderContainer>> connectToGameFinderImpl(std::string gameFinderName, pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					auto users = _users.lock();

					if (!users)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("UsersApi"), std::shared_ptr<GameFinderContainer>);
					}

					std::weak_ptr<GameFinder_Impl> wThat = this->shared_from_this();

					return users->getSceneForService("stormancer.plugins.gamefinder", gameFinderName, ct)
						.then([gameFinderName, wThat](pplx::task<std::shared_ptr<Scene>> task)
					{
						try
						{
							auto container = std::make_shared<GameFinderContainer>();
							container->scene = task.get();
							container->connectionStateChangedSubscription = container->scene->getConnectionStateChangedObservable().subscribe([wThat, gameFinderName](ConnectionState s)
							{
								if (auto that = wThat.lock())
								{
									if (s == ConnectionState::Disconnecting)
									{
										std::lock_guard<std::recursive_mutex> lg(that->_lock);
										auto it = that->_gameFinders.find(gameFinderName);
										if (it != that->_gameFinders.end())
										{
											that->_gameFinders.erase(it);
										}
									}
								}
							});
							auto service = container->service();
							container->gameFoundSubscription = service->GameFound.subscribe([wThat, gameFinderName](GameFinderResponse r)
							{
								if (auto that = wThat.lock())
								{
									GameFoundEvent ev;
									ev.gameFinder = gameFinderName;
									ev.data = r;
									that->gameFound(ev);
								}
							});
							container->gameFinderStateUpdatedSubscription = service->GameFinderStatusUpdated.subscribe([wThat, gameFinderName](GameFinderStatus s)
							{
								if (auto that = wThat.lock())
								{
									GameFinderStatusChangedEvent ev;
									ev.gameFinder = gameFinderName;
									ev.status = s;
									that->gameFinderStateChanged(ev);
								}
							});
							container->findGamefailedSubscription = service->FindGameRequestFailed.subscribe([wThat, gameFinderName](std::string reason)
							{
								if (auto that = wThat.lock())
								{
									FindGameFailedEvent ev;
									ev.gameFinder = gameFinderName;
									ev.reason = reason;
									that->findGameFailed(ev);
								}
							});
							return container;
						}
						catch (const std::exception& ex)
						{
							throw std::runtime_error("Failed to connect to game finder. sceneName=" + gameFinderName + " reason=" + ex.what());
						}
					})
						.then([wThat, gameFinderName](pplx::task<std::shared_ptr<GameFinderContainer>> task)
					{
						try
						{
							return task.get();
						}
						catch (std::exception&)
						{
							auto that = wThat.lock();
							if (that)
							{
								std::lock_guard<std::recursive_mutex> lg(that->_lock);
								that->_gameFinders.erase(gameFinderName);
							}
							throw;
						}
					});
				}

				pplx::task<std::shared_ptr<GameFinderContainer>> getGameFinderContainer(std::string id, pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					std::lock_guard<std::recursive_mutex> lg(_lock);
					auto it = _gameFinders.find(id);
					if (it != _gameFinders.end())
					{
						return (*it).second;
					}
					else
					{
						auto t = connectToGameFinderImpl(id, ct);
						_gameFinders.emplace(id, t);
						return t;
					}
				}

				// recursive_mutex needed because of some corner error cases where continuations are run synchronously
				std::recursive_mutex _lock;
				std::unordered_map<std::string, pplx::task<std::shared_ptr<GameFinderContainer>>> _gameFinders;
				std::unordered_map<std::string, pplx::cancellation_token_source> _pendingFindGameRequests;
				std::weak_ptr<Users::UsersApi> _users;
			};
		}

		class GameFinderPlugin : public IPlugin
		{
		public:

			static constexpr const char* PLUGIN_NAME = "GameFinder";
			static constexpr const char* PLUGIN_REVISION = "202006091";
			static constexpr const char* PLUGIN_PROTOCOL_KEY = "stormancer.plugins.gamefinder.protocol";

			PluginDescription getDescription() override
			{
				return PluginDescription(PLUGIN_NAME, PLUGIN_REVISION);
			}

		private:

			void registerSceneDependencies(ContainerBuilder& builder, std::shared_ptr<Scene> scene) override
			{
				auto name = scene->getHostMetadata("stormancer.plugins.gamefinder");
				if (!name.empty())
				{
					builder.registerDependency<details::GameFinderService, Scene>().singleInstance();
				}
			}

			void sceneCreated(std::shared_ptr<Scene> scene) override
			{
				auto name = scene->getHostMetadata("stormancer.plugins.gamefinder");
				if (!name.empty())
				{
					scene->dependencyResolver().resolve<details::GameFinderService>()->initialize();
				}
			}

			void registerClientDependencies(ContainerBuilder& builder) override
			{
				builder.registerDependency<details::GameFinder_Impl, Users::UsersApi>().as<GameFinderApi>().singleInstance();
			}

			void sceneDisconnecting(std::shared_ptr<Scene> scene) override
			{
				auto name = scene->getHostMetadata("stormancer.plugins.gamefinder");
				if (!name.empty())
				{
					scene->dependencyResolver().resolve<details::GameFinderService>()->onSceneDisconnecting();
				}
			}

			void clientCreated(std::shared_ptr<IClient> client) override
			{
				client->setMetadata(PLUGIN_PROTOCOL_KEY, PLUGIN_REVISION);
			}
		};
	}
}
