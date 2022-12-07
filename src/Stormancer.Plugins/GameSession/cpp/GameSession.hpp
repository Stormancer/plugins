#pragma once
#include "users/Users.hpp"
#include "stormancer/IPlugin.h"
#include "stormancer/msgpack_define.h"
#include "stormancer/ITokenHandler.h"
#include "stormancer/Utilities/TaskUtilities.h"

namespace Stormancer
{
	namespace GameSessions
	{
		enum class P2PRole
		{
			Host,
			Client
		};

		enum class PlayerStatus
		{
			NotConnected = 0,
			Connected = 1,
			Ready = 2,
			Faulted = 3,
			Disconnected = 4
		};

		struct SessionPlayer
		{
			SessionPlayer(std::string playerId, PlayerStatus status, bool isHost = false)
				: playerId(playerId)
				, status(status)
				, isHost(isHost)
			{
			}

			std::string playerId;
			PlayerStatus status;
			bool isHost;
		};

		struct Player
		{
			std::vector<byte> data;
			std::string sessionId;
			std::string userId;
			MSGPACK_DEFINE(data, sessionId, userId)
		};

		struct Party
		{
			std::string partyId;
			std::unordered_map<std::string, Player> players;

			std::string customData;

			MSGPACK_DEFINE(partyId, players, customData)
		};
		struct Team
		{
			std::string teamId;
			std::vector<Party> parties;

			MSGPACK_DEFINE(teamId, parties)
		};

		struct ServerStartedMessage
		{
			std::string p2pToken;
			MSGPACK_DEFINE(p2pToken);
		};

		struct PlayerUpdate
		{
			std::string userId;
			int status;
			std::string data;
			bool isHost;

			MSGPACK_DEFINE(userId, status, data, isHost);
		};

		struct GameSessionConnectionParameters
		{
			bool isHost;
			std::string hostMap;
			std::string endpoint;
			std::string hostSessionId;
		};

		class GameSessionsPlugin;

		/// <summary>
		/// public gamesession API
		/// </summary>
		/// <example>
		/// std::shared_ptr&lt;GameSession&gt; gs = client->dependencyResolver().resolve<GameSession>();
		/// </example>
		class GameSession
		{
#pragma region public

		public:

#pragma region public_destructor

			virtual ~GameSession() = default;

#pragma endregion

#pragma region public_methods

			virtual pplx::task<GameSessionConnectionParameters> connectToGameSession(std::string token, std::string mapName = "", bool openTunnel = true, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			virtual pplx::task<void> setPlayerReady(const std::string& data = "", pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;
			virtual pplx::task<std::shared_ptr<Stormancer::IP2PScenePeer>> connectP2P(Stormancer::SessionId target, pplx::cancellation_token ct) = 0;

			virtual pplx::task<std::vector<Team>> getTeams(pplx::cancellation_token cancellationToken = pplx::cancellation_token::none()) = 0;
			template<typename TServerResult, typename TClientResult>
			pplx::task<TServerResult> postResult(const TClientResult& clientResult, pplx::cancellation_token ct = pplx::cancellation_token::none())
			{
				StreamWriter streamWriter = [clientResult](obytestream& stream)
				{
					Serializer serializer;
					serializer.serialize(stream, clientResult);
				};

				return postResult(streamWriter, ct)
					.then([](Packetisp_ptr packet)
						{
							Serializer serializer;
							TServerResult serverResult = serializer.deserializeOne<TServerResult>(packet->stream);
							return serverResult;
						});
			}

			virtual pplx::task<Packetisp_ptr> postResult(const StreamWriter& streamWriter, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			virtual pplx::task<std::string> getUserFromBearerToken(const std::string& token, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			virtual pplx::task<void> disconnectFromGameSession(pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			/// <summary>
			/// Gets a boolean indicating if the client is connected to a gamesession.
			/// </summary>
			/// <returns></returns>
			bool isInSession()
			{
				return scene() != nullptr;
			}

			/// <summary>
			/// Gets the underlying scene of the current gamesession, an empty shared ptr otherwise
			/// </summary>
			virtual std::shared_ptr<Scene> scene() = 0;

			/// <summary>
			/// Get the P2P Host peer for this Game Session.
			/// </summary>
			/// <remarks>
			/// Players in a game session are connected together through a star topology centered on a single player, the Host.
			/// The players who are not the Host are called Clients.
			/// </remarks>
			/// <returns>
			/// The <c>IP2PScenePeer</c> for the host of the session.
			/// If you are the host, or you are not yet connected to a game session, it will be nullptr.
			/// </returns>
			virtual std::shared_ptr<IP2PScenePeer> getSessionHost() const = 0;

			/// <summary>
			/// Check whether you are the P2P Host of the Game Session.
			/// </summary>
			/// <seealso cref="getSessionHost()"/>
			/// <returns>
			/// <c>true</c> if you are the host of the session ; <c>false</c> if you are a client, or if you are not connected to a game session.
			/// </returns>
			virtual bool isSessionHost() const = 0;

#pragma endregion

#pragma region public_members

			Event<> onAllPlayersReady;

			Event<GameSessionConnectionParameters> onRoleReceived;
			Event<GameSessionConnectionParameters> onTunnelOpened;

			Event<SessionPlayer, std::string> onPlayerStateChanged;

			/// <summary>
			/// Event fired before connecting to a new GameSession scene.
			/// </summary>
			/// <remarks>
			/// This is the place to register additional routes if you need to.
			/// </remarks>
			Event<std::shared_ptr<Scene>> onSceneCreated;

			/// <summary>
			/// Event fired when the client connects to a gamesession scene
			/// </summary>
			Event<std::shared_ptr<Scene>> onConnectingToScene;


			/// <summary>
			/// Event fired when the client
			/// </summary>
			Event<std::string> onDisconnectedFromGameSession;

			/// <summary>
			/// Event fired when the client disconnects from a gamesession scene
			/// </summary>
			Event<std::shared_ptr<Scene>> onDisconnectingFromScene;

			/// <summary>
			/// Event that is triggered when a host migration happens.
			/// </summary>
			/// <remarks>
			/// Host migration is not currently supported.
			/// </remarks>
			Event<std::shared_ptr<IP2PScenePeer>> onSessionHostChanged;

#pragma endregion

#pragma endregion
		};


		namespace details
		{
			constexpr char GAMESESSION_P2P_SERVER_ID[] = "GameSession";

			struct HostInfosMessage
			{
				std::string p2pToken;
				bool isHost;
				std::string hostSessionId;

				MSGPACK_DEFINE(p2pToken, isHost, hostSessionId)
			};

			class GameSessionService :public std::enable_shared_from_this<GameSessionService>
			{
				friend class ::Stormancer::GameSessions::GameSessionsPlugin;

			public:

#pragma region public_constructors

				GameSessionService(std::weak_ptr<Scene> scene) :
					_scene(scene),
					_logger(scene.lock()->dependencyResolver().resolve<ILogger>())
				{
				}

#pragma endregion

#pragma region public_methods

				pplx::task<std::shared_ptr<Stormancer::IP2PScenePeer>> initializeP2P(HostInfosMessage hostInfos, bool openTunnel, pplx::cancellation_token ct)
				{
					ct = linkTokenToDisconnection(ct);

					auto scene = _scene.lock();
					if (!scene)
					{
						_logger->log(LogLevel::Error, "gamession.p2ptoken", "Scene deleted");
						STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("Scene"), std::shared_ptr<Stormancer::IP2PScenePeer>);
					}

					_logger->log(LogLevel::Trace, "gamession.p2ptoken", "recieved p2p token");

					if (_receivedP2PToken)
					{
						return pplx::task_from_result<std::shared_ptr<Stormancer::IP2PScenePeer>>(nullptr);
					}

					_receivedP2PToken = true;
					_waitServerTce.set();
					hostSessionId = hostInfos.hostSessionId;

					if (hostInfos.isHost) // Host
					{

						_logger->log(LogLevel::Info, "gamession.p2ptoken", "received host=true.");
						_myP2PRole = P2PRole::Host;
						onRoleReceived(std::make_tuple(hostInfos.hostSessionId, P2PRole::Host));
						_waitServerTce.set();
						if (openTunnel)
						{
							_tunnel = scene->registerP2PServer(GAMESESSION_P2P_SERVER_ID);
						}
						return pplx::task_from_result<std::shared_ptr<IP2PScenePeer>>(nullptr);
					}
					else // Client
					{
						_logger->log(LogLevel::Info, "gamession.p2ptoken", "received host=false.");


						std::weak_ptr<GameSessionService> wThat = this->shared_from_this();
						if (!hostInfos.p2pToken.empty())
						{
							auto& p2pToken = hostInfos.p2pToken;
							return scene->openP2PConnection(p2pToken, ct)
								.then([wThat, ct, openTunnel, hostInfos](std::shared_ptr<IP2PScenePeer> p2pPeer)
									{
										auto that = wThat.lock();
										if (!that)
										{
											STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("GameSessionService"), std::shared_ptr<IP2PScenePeer>);
										}

										that->_myP2PRole = P2PRole::Client;
										that->onRoleReceived(std::make_tuple(hostInfos.hostSessionId, P2PRole::Client));
										if (that->_onConnectionOpened)
										{
											that->_onConnectionOpened(p2pPeer);
										}

										if (openTunnel)
										{
											return p2pPeer->openP2PTunnel(GAMESESSION_P2P_SERVER_ID, ct)
												.then([wThat, p2pPeer, hostInfos](std::shared_ptr<P2PTunnel> guestTunnel)
													{
														auto that = wThat.lock();
														if (that)
														{
															that->_tunnel = guestTunnel;
															that->onTunnelOpened(std::make_tuple(hostInfos.hostSessionId, guestTunnel));
														}
														return p2pPeer;
													});
										}
										else
										{
											return pplx::task_from_result(p2pPeer);
										}
									})
								.then([wThat](pplx::task<std::shared_ptr<IP2PScenePeer>> t)
									{
										auto that = wThat.lock();
										try
										{
											auto p = t.get();
											return p;
										}
										catch (const std::exception& ex)
										{
											if (that)
											{
												that->_onConnectionFailure(ex.what());
												that->_logger->log(ex);
											}
											throw;
										}
									});
						}
						else
						{
							_myP2PRole = P2PRole::Client;
							if (!openTunnel)
							{
								onRoleReceived(std::make_tuple(hostInfos.hostSessionId, P2PRole::Client));
								return pplx::task_from_result<std::shared_ptr<Stormancer::IP2PScenePeer>>(std::shared_ptr<Stormancer::IP2PScenePeer>());
							}
							else
							{
								return pplx::task_from_exception<std::shared_ptr<Stormancer::IP2PScenePeer>>(std::runtime_error("useTunnel is not supported: P2P disabled on the server."));
							}
						}
					}
				}

				pplx::task<void> waitServerReady(pplx::cancellation_token token)
				{
					token = linkTokenToDisconnection(token);
					return pplx::create_task(_waitServerTce, pplx::task_options(token));
				}

				std::vector<SessionPlayer> getConnectedPlayers()
				{
					return this->_users;
				}

				std::weak_ptr<Scene> getScene()
				{
					return _scene;
				}

				pplx::task<std::vector<Team>> getTeams(pplx::cancellation_token cancellationToken = pplx::cancellation_token::none())
				{
					if (auto scene = _scene.lock())
					{
						auto rpc = scene->dependencyResolver().resolve<RpcService>();
						return rpc->rpc<std::vector<Team>>("GameSession.GetTeams", cancellationToken);
					}
					else
					{
						return pplx::task_from_exception<std::vector<Team>>(ObjectDeletedException("Scene"));
					}
				}

				pplx::task<std::string> getUserFromBearerToken(std::string token, pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					if (auto scene = _scene.lock())
					{
						auto rpc = scene->dependencyResolver().resolve<RpcService>();
						return rpc->rpc<std::string, std::string>("GameSession.GetUserFromBearerToken", ct, token);
					}
					else
					{
						return pplx::task_from_exception<std::string>(ObjectDeletedException("Scene"));
					}
				}

				pplx::task<HostInfosMessage> requestP2PToken(pplx::cancellation_token ct)
				{
					if (auto scene = _scene.lock())
					{
						ct = linkTokenToDisconnection(ct);
						auto rpc = scene->dependencyResolver().resolve<RpcService>();
						return rpc->rpc<HostInfosMessage, int>("GameSession.GetP2PToken", ct, 1);
					}
					else
					{
						return pplx::task_from_exception<HostInfosMessage>(ObjectDeletedException("Scene"));
					}
				}

				pplx::task<void> reset(pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					ct = linkTokenToDisconnection(ct);
					auto scene = _scene.lock();
					if (!scene)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("Scene"), void);
					}

					auto rpc = scene->dependencyResolver().resolve<RpcService>();
					return rpc->rpc("gamesession.reset", ct);
				}

				pplx::task<void> disconnect(pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					auto scene = _scene.lock();
					if (!scene)
					{
						return pplx::task_from_result();
					}

					return scene->disconnect(ct);
				}

				void onDisconnecting()
				{
					_tunnel = nullptr;
					_users.clear();
					_disconnectionCts.cancel();
				}

				void ready(std::string data)
				{
					auto scene = _scene.lock();
					if (!scene)
					{
						_logger->log(LogLevel::Error, "GameSessions", "Scene deleted");
						return;
					}
					_logger->log(LogLevel::Debug, "GameSessions", "Sending player ready");
					scene->send("player.ready", [data](obytestream& stream)
						{
							msgpack::pack(stream, data);
						});
				}
				pplx::task<std::string> connectP2P(Stormancer::SessionId target)
				{
					auto scene = _scene.lock();
					if (!scene)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("Scene"), std::string);
					}

					auto rpc = scene->dependencyResolver().resolve<RpcService>();
					return rpc->rpc<std::string>("GamesSssion.CreateP2PToken", target);
				}
				pplx::task<Packetisp_ptr> sendGameResults(const StreamWriter& streamWriter, pplx::cancellation_token ct)
				{
					auto scene = _scene.lock();
					if (!scene)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("Scene"), Packetisp_ptr);
					}

					auto rpc = scene->dependencyResolver().resolve<RpcService>();
					return rpc->rpc<Packetisp_ptr>("gamesession.postresults", ct, streamWriter);
				}

				P2PRole getMyP2PRole() const
				{
					return _myP2PRole;
				}

#pragma endregion

#pragma region public_members

				Event<> onAllPlayersReady;
				Event<std::tuple<std::string, P2PRole>> onRoleReceived;
				Event<std::tuple<std::string, std::shared_ptr<Stormancer::P2PTunnel>>> onTunnelOpened;
				Event<> onShutdownReceived;
				Event<SessionPlayer, std::string> onPlayerStateChanged;

				std::string hostSessionId;


#pragma endregion

			private:

#pragma region private_methods

				void initialize()
				{
					_disconnectionCts = pplx::cancellation_token_source();
					std::weak_ptr<GameSessionService> wThat = this->shared_from_this();

					_scene.lock()->addRoute("player.update", [wThat](Packetisp_ptr packet)
						{
							auto that = wThat.lock();
							if (that)
							{
								auto update = packet->readObject<Stormancer::GameSessions::PlayerUpdate>();
								SessionPlayer player(update.userId, (PlayerStatus)update.status, update.isHost);

								if (player.playerId != "server")
								{
									auto end = that->_users.end();
									auto it = std::find_if(that->_users.begin(), end, [player](SessionPlayer p) { return p.playerId == player.playerId; });
									if (it == end)
									{
										that->_users.push_back(player);
									}
									else
									{
										*it = player;
									}
								}
								that->onPlayerStateChanged(player, update.data);
							}
						});

					_scene.lock()->addRoute("players.allReady", [wThat](Packetisp_ptr)
						{
							auto that = wThat.lock();
							if (that)
							{
								that->onAllPlayersReady();
							}
						});
				}

				pplx::cancellation_token linkTokenToDisconnection(pplx::cancellation_token tokenToLink)
				{
					if (tokenToLink.is_cancelable())
					{
						auto tokens = { tokenToLink, _disconnectionCts.get_token() };
						return pplx::cancellation_token_source::create_linked_source(tokens.begin(), tokens.end()).get_token();
					}
					else
					{
						return _disconnectionCts.get_token();
					}
				}

#pragma endregion

#pragma region private_members

				std::shared_ptr<P2PTunnel> _tunnel;
				Event<std::string> _onConnectionFailure;
				std::function<void(std::shared_ptr<Stormancer::IP2PScenePeer>)> _onConnectionOpened;
				pplx::task_completion_event<void> _waitServerTce;
				std::weak_ptr<Scene> _scene;
				std::vector<SessionPlayer> _users;
				std::shared_ptr<Stormancer::ILogger> _logger;
				bool _receivedP2PToken = false;
				pplx::cancellation_token_source _disconnectionCts;
				P2PRole _myP2PRole = P2PRole::Client;

#pragma endregion
			};

			//Private gamesession container implementation that deals with the lifecycle of a gamesession connection
			struct GameSessionContainer
			{
			public:

#pragma region public_constructors

				GameSessionContainer() = default;

#pragma endregion

#pragma region public_destructor

				~GameSessionContainer()
				{
					cts.cancel();
					_hostIsReadyTce.set_exception(pplx::task_canceled());
					try
					{
						pplx::create_task(_hostIsReadyTce).get();
					}
					catch (...)
					{
					}
				}

#pragma endregion

#pragma region public_methods

				pplx::cancellation_token cancellationToken()
				{
					return cts.get_token();
				}

				pplx::task<std::shared_ptr<GameSessionService>> service()
				{
					return scene.then([](std::shared_ptr<Scene> s)
						{
							return s->dependencyResolver().resolve<GameSessionService>();
						});
				}

				pplx::task<GameSessionConnectionParameters> sessionReadyAsync()
				{
					return pplx::create_task(sessionReadyTce);
				}

#pragma endregion

#pragma region public_members

				// Keep game finder scene alive
				pplx::task<std::shared_ptr<Scene>> scene;
				std::string sceneId;
				std::string mapName;

				std::shared_ptr<IP2PScenePeer>	p2pHost;

				Subscription allPlayerReady;
				Subscription onRoleReceived;
				Subscription onTunnelOpened;
				Subscription onShutdownRecieved;
				Subscription onPlayerChanged;

				pplx::task_completion_event<void> _hostIsReadyTce;
				pplx::task_completion_event<GameSessionConnectionParameters> sessionReadyTce;

#pragma endregion

			private:

#pragma region private_members

				pplx::cancellation_token_source cts;

#pragma endregion
			};

			class GameSession_Impl : public GameSession, public std::enable_shared_from_this<GameSession_Impl>
			{
				friend class ::Stormancer::GameSessions::GameSessionsPlugin;

			public:

#pragma region public_constructors

				GameSession_Impl(std::weak_ptr<IClient> client, std::shared_ptr<ITokenHandler> tokens, std::shared_ptr<ILogger> logger, std::shared_ptr<IActionDispatcher> dispatcher)
					: _logger(logger)
					, _tokens(tokens)
					, _wDispatcher(dispatcher)
					, _wClient(client)
					, _currentGameSession(nullptr)
				{
				}

#pragma endregion

#pragma region public_methods

				pplx::task<GameSessionConnectionParameters> connectToGameSession(std::string token, std::string mapName, bool openTunnel, pplx::cancellation_token ct) override
				{
					std::lock_guard<std::mutex> lg(_lock);

					if (token.empty())
					{
						STORM_RETURN_TASK_FROM_EXCEPTION(std::runtime_error("Empty connection token"), GameSessionConnectionParameters);
					}

					// Client should never be null here
					auto dispatcher = _wClient.lock()->dependencyResolver().resolve<IActionDispatcher>();

					std::weak_ptr<GameSession_Impl> wThat = this->shared_from_this();

					_currentGameSession = std::make_shared<GameSessionContainer>();
					_currentGameSession->mapName = mapName;
					if (ct.is_cancelable())
					{
						ct.register_callback([wThat]()
							{
								if (auto that = wThat.lock())
								{
									that->_currentGameSession = nullptr;
								}
							});
					}

					auto infos = _tokens->getSceneEndpointInfo(token);
					_currentGameSession->sceneId = infos.tokenData.SceneId;

					auto cancellationToken = _currentGameSession->cancellationToken();
					std::weak_ptr<GameSessionContainer> wContainer = _currentGameSession;

					auto scene = connectToGameSessionImpl(token, openTunnel, cancellationToken, wContainer)
						.then([wThat, openTunnel, cancellationToken, wContainer, logger = _logger](std::shared_ptr<Scene> scene)
							{
								auto that = wThat.lock();

								if (!that)
								{
									throw ObjectDeletedException("GameSession");
								}

								logger->log(LogLevel::Debug, "GameSession", "Requesting P2P token", "");
								return that->requestP2PToken(scene, cancellationToken)
									.then([scene, openTunnel, cancellationToken](pplx::task<HostInfosMessage> task)
										{
											auto service = scene->dependencyResolver().resolve<GameSessionService>();
											auto logger = scene->dependencyResolver().resolve<ILogger>();
											try
											{
												auto token = task.get();
												logger->log(LogLevel::Debug, "GameSession", "Initialize P2Ps", "");
												return service->initializeP2P(token, openTunnel, cancellationToken);
											}
											catch (std::exception& e)
											{
												throw std::runtime_error(std::string() + "Cannot get p2pToken : " + e.what());
											}
										}, cancellationToken)
									.then([scene, wContainer](std::shared_ptr<IP2PScenePeer> peer)
										{
											auto c = wContainer.lock();
											if (!c)
											{
												pplx::cancel_current_task();
											}
											c->p2pHost = peer;
											if (!peer)
											{
												c->_hostIsReadyTce.set();
											}
											return scene;
										}, cancellationToken);
							}, cancellationToken);

					_currentGameSession->scene = scene;

					return scene
						.then([cancellationToken, wContainer, logger = _logger](std::shared_ptr<Scene>)
							{
								auto c = wContainer.lock();
								if (!c)
								{
									pplx::cancel_current_task();
								}

								logger->log(LogLevel::Info, "GameSession", "Waiting role", "");

								auto hostReadyTce = c->_hostIsReadyTce;
								return c->sessionReadyAsync()
									.then([hostReadyTce, cancellationToken, logger](GameSessionConnectionParameters gameSessionConnectionParameters)
										{
											if (gameSessionConnectionParameters.isHost) // Host = connect immediately
											{
												return pplx::task_from_result(gameSessionConnectionParameters);
											}
											else // Client = waiting for host to be ready
											{
												logger->log(LogLevel::Info, "GameSession", "Waiting host is ready", "");

												return pplx::create_task(hostReadyTce, cancellationToken)
													.then([gameSessionConnectionParameters, logger]()
														{
															logger->log(LogLevel::Info, "GameSession", "Host is ready", "");
															return gameSessionConnectionParameters;
														}, cancellationToken);
											}
										});
							})
						.then([wThat, logger = _logger](pplx::task<GameSessionConnectionParameters> task)
							{
								try
								{
									task.get();
								}
								catch (...)
								{
									if (auto that = wThat.lock())
									{
										std::exception_ptr ptrEx = std::current_exception();
										return that->disconnectFromGameSession()
											.then([ptrEx, logger](pplx::task<void> task) -> GameSessionConnectionParameters
												{
													try
													{
														task.get();
													}
													catch (const std::exception& ex)
													{
														logger->log(LogLevel::Warn, "GameSessionConnection", "Cannot disconnect from game session after connection timeout or cancel.", ex.what());
													}

													std::rethrow_exception(ptrEx);
												});
									}
								}
								return task;
							}, dispatcher);
				}

				pplx::task<std::shared_ptr<Stormancer::IP2PScenePeer>> connectP2P(Stormancer::SessionId target, pplx::cancellation_token ct) override
				{
					if (auto dispatcher = _wDispatcher.lock())
					{
						return getCurrentGameSession(ct)
							.then([target](std::shared_ptr<Scene> scene)
								{
									if (scene)
									{
										auto gameSessionService = scene->dependencyResolver().resolve<GameSessionService>();
										return gameSessionService->connectP2P(target).then([scene](std::string token) {return std::make_tuple(scene, token); });
									}
									else
									{
										throw std::runtime_error("Not connected to a game session");
									}
								})
							.then([](std::tuple<std::shared_ptr<Scene>,std::string> tuple) {
									return std::get<0>(tuple)->openP2PConnection(std::get<1>(tuple));
							}, dispatcher);
					}
					else
					{
						STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("IActionDispatcher"), std::shared_ptr<Stormancer::IP2PScenePeer>);
					}
				}

				pplx::task<void> setPlayerReady(const std::string& data, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					if (auto dispatcher = _wDispatcher.lock())
					{
						return getCurrentGameSession(ct)
							.then([data](std::shared_ptr<Scene> scene)
								{
									if (scene)
									{
										auto gameSessionService = scene->dependencyResolver().resolve<GameSessionService>();
										gameSessionService->ready(data);
									}
									else
									{
										throw std::runtime_error("Not connected to any game session");
									}
								}, dispatcher);
					}
					else
					{
						STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("IActionDispatcher"), void);
					}
				}

				pplx::task<std::vector<Team>> getTeams(pplx::cancellation_token cancellationToken = pplx::cancellation_token::none()) override
				{
					if (auto dispatcher = _wDispatcher.lock())
					{
						return getCurrentGameSession(cancellationToken)
							.then([cancellationToken](std::shared_ptr<Scene> scene)
								{
									if (scene)
									{
										auto gameSessionService = scene->dependencyResolver().resolve<GameSessionService>();
										return gameSessionService->getTeams(cancellationToken);
									}
									else
									{
										throw std::runtime_error("Not connected to any game session");
									}
								}, dispatcher);
					}
					else
					{
						STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("IActionDispatcher"), std::vector<Team>);
					}
				}

				pplx::task<Packetisp_ptr> postResult(const StreamWriter& streamWriter, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					auto dispatcher = _wDispatcher.lock();
					auto taskOptions = dispatcher ? pplx::task_options(dispatcher) : pplx::task_options();

					return getCurrentGameSession(ct)
						.then([streamWriter, ct](std::shared_ptr<Scene> scene)
							{
								if (scene)
								{
									auto gameSessionService = scene->dependencyResolver().resolve<GameSessionService>();
									return gameSessionService->sendGameResults(streamWriter, ct);
								}
								else
								{
									throw std::runtime_error("Not connected to any game session");
								}
							}, taskOptions);
				}

				pplx::task<std::string> getUserFromBearerToken(const std::string& token, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					return getCurrentGameSession(ct)
						.then([token, ct](std::shared_ptr<Scene> scene)
							{
								if (scene)
								{
									auto gameSessionService = scene->dependencyResolver().resolve<GameSessionService>();
									return gameSessionService->getUserFromBearerToken(token, ct);
								}
								else
								{
									throw std::runtime_error("Not connected to any game session");
								}
							});
				}

				pplx::task<void> disconnectFromGameSession(pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					auto dispatcher = _wDispatcher.lock();
					auto taskOptions = dispatcher ? pplx::task_options(dispatcher) : pplx::task_options();
					std::weak_ptr<GameSession_Impl> wThat = this->shared_from_this();
					// catch err
					return this->getCurrentGameSession(ct)
						.then([ct, logger = _logger, wThat](pplx::task<std::shared_ptr<Scene>> task)
							{
								try
								{
									auto scene = task.get();
									if (scene)
									{
										logger->log(LogLevel::Info, "GameSession", "Disconnecting from previous games session", scene->id());
										auto gameSessionService = scene->dependencyResolver().resolve<GameSessionService>();
										if (auto that = wThat.lock())
										{
											that->_currentGameSession = nullptr;
										}
										return gameSessionService->disconnect(ct);
									}
									else
									{
										if (auto that = wThat.lock())
										{
											that->_currentGameSession = nullptr;
										}
										return pplx::task_from_result();
									}
								}
								catch (std::exception&)
								{
									if (auto that = wThat.lock())
									{
										that->_currentGameSession = nullptr;
									}
									return pplx::task_from_result();
								}
							}, taskOptions);
				}

				std::shared_ptr<Scene> scene() override
				{
					if (this->_currentGameSession && this->_currentGameSession->scene.is_done())
					{
						try
						{
							return this->_currentGameSession->scene.get();
						}
						catch (std::exception&)//Ignore errors : They mean that we are not in a gamesession.
						{
							return nullptr;
						}
					}
					else
					{
						return nullptr;
					}
				}

				std::shared_ptr<IP2PScenePeer> getSessionHost() const override
				{
					// Copy the task to avoid a possible race condition with it being reassigned while we are inside this method
					auto container = _currentGameSession;
					if (!container || !container->scene.is_done())
					{
						return nullptr;
					}

					return container->p2pHost;
				}

				bool isSessionHost() const override
				{
					try
					{
						auto container = _currentGameSession;
						if (!container || !container->scene.is_done())
						{
							return false;
						}

						auto session = container->scene.get();
						if (session)
						{
							auto service = session->dependencyResolver().resolve<GameSessionService>();
							return service->getMyP2PRole() == P2PRole::Host;
						}

						return false;
					}
					catch (std::exception&)
					{
						return false;
					}
				}

#pragma endregion

			private:

#pragma region private_methods

				pplx::task<std::shared_ptr<Scene>> connectToGameSessionImpl(std::string token, bool useTunnel, pplx::cancellation_token ct, std::weak_ptr<GameSessionContainer> wContainer)
				{
					std::weak_ptr<GameSession_Impl> wThat = this->shared_from_this();
					return _wClient.lock()->connectToPrivateScene(token, [wContainer, useTunnel, wThat](std::shared_ptr<Scene> scene)
						{
							auto gameSessionContainer = wContainer.lock();
							if (!gameSessionContainer)
							{
								throw pplx::task_canceled();
							}

							auto service = scene->dependencyResolver().resolve<GameSessionService>();

							gameSessionContainer->onRoleReceived = service->onRoleReceived.subscribe([wThat, useTunnel, wContainer](std::tuple<std::string, P2PRole> tuple)
								{
									auto role = std::get<1>(tuple);
									auto hostSessionId = std::get<0>(tuple);

									auto gameSessionContainer = wContainer.lock();
									auto that = wThat.lock();
									if (that && gameSessionContainer)
									{
										if ((role == P2PRole::Host) || (role == P2PRole::Client && !useTunnel))
										{
											GameSessionConnectionParameters gameSessionParameters;
											gameSessionParameters.endpoint = gameSessionContainer->mapName;
											gameSessionParameters.isHost = (role == P2PRole::Host);
											gameSessionParameters.hostSessionId = hostSessionId;
											that->onRoleReceived(gameSessionParameters);
											gameSessionContainer->sessionReadyTce.set(gameSessionParameters);
										}
									}
								});

							if (useTunnel)
							{
								gameSessionContainer->onTunnelOpened = service->onTunnelOpened.subscribe([wThat, wContainer](std::tuple<std::string, std::shared_ptr<Stormancer::P2PTunnel>> tuple)
									{
										auto p2pTunnel = std::get<1>(tuple);
										auto hostSessionId = std::get<0>(tuple);
										auto gameSessionContainer = wContainer.lock();
										auto that = wThat.lock();
										if (gameSessionContainer && that)
										{
											GameSessionConnectionParameters gameSessionParameters;
											gameSessionParameters.isHost = false;
											gameSessionParameters.hostSessionId = hostSessionId;
											gameSessionParameters.endpoint = p2pTunnel->ip + ":" + std::to_string(p2pTunnel->port);

											that->onTunnelOpened(gameSessionParameters);
											gameSessionContainer->sessionReadyTce.set(gameSessionParameters);
										}
									});
							}

							gameSessionContainer->allPlayerReady = service->onAllPlayersReady.subscribe([wThat]()
								{
									if (auto that = wThat.lock())
									{
										that->onAllPlayersReady();
									}
								});

							auto hostIsReadyTce = gameSessionContainer->_hostIsReadyTce;
							gameSessionContainer->onPlayerChanged = service->onPlayerStateChanged.subscribe([wThat, hostIsReadyTce](SessionPlayer player, std::string data)
								{
									if (auto that = wThat.lock())
									{

										that->onPlayerStateChanged(player, data);
										if (player.isHost && player.status == PlayerStatus::Ready)
										{
											hostIsReadyTce.set();
										}
									}
								});
						}, ct);
				}

				pplx::task<std::shared_ptr<Scene>> getCurrentGameSession(pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					if (_currentGameSession)
					{
						return pplx::create_task([sceneTask = _currentGameSession->scene]() { return sceneTask; }, ct);
					}
					else
					{
						return pplx::task_from_result<std::shared_ptr<Scene>>(nullptr);
					}
				}

				pplx::task<HostInfosMessage> requestP2PToken(std::shared_ptr<Scene> scene, pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					std::shared_ptr<GameSessionService> gameSessionService = scene->dependencyResolver().resolve<GameSessionService>();
					return gameSessionService->requestP2PToken(ct);
				}

				void onDisconnectingFromGameSession(std::shared_ptr<Scene> scene)
				{
					_currentGameSession = nullptr;
					onDisconnectingFromScene(scene);
				}
				void raiseOnDisconnectedFromGameSession(std::shared_ptr<Scene>, std::string reason)
				{
					onDisconnectedFromGameSession(reason);
				}
#pragma endregion

#pragma region private_members

				std::shared_ptr<ILogger> _logger;
				std::shared_ptr<ITokenHandler> _tokens;
				std::weak_ptr<IActionDispatcher> _wDispatcher;
				std::weak_ptr<IClient> _wClient;
				std::shared_ptr<GameSessionContainer> _currentGameSession;
				std::mutex _lock;

#pragma endregion
			};
		}

		class GameSessionsPlugin : public Stormancer::IPlugin
		{
		public:

			static constexpr const char* PLUGIN_NAME = "GameSession";
			static constexpr const char* PLUGIN_VERSION = "1.0.0";

			PluginDescription getDescription() override
			{
				return PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
			}

		private:

			void registerSceneDependencies(Stormancer::ContainerBuilder& builder, std::shared_ptr<Stormancer::Scene> scene) override
			{
				auto name = scene->getHostMetadata("stormancer.gamesession");
				if (name.length() > 0)
				{
					builder.registerDependency<details::GameSessionService, Scene>().singleInstance();
				}
			}

			void sceneCreated(std::shared_ptr<Scene> scene) override
			{
				auto name = scene->getHostMetadata("stormancer.gamesession");
				if (name.length() > 0)
				{
					scene->dependencyResolver().resolve<details::GameSessionService>()->initialize();
					scene->dependencyResolver().resolve<GameSession>()->onSceneCreated(scene);
				}
			}

			void sceneConnecting(std::shared_ptr<Scene> scene) override
			{
				auto name = scene->getHostMetadata("stormancer.gamesession");
				if (name.length() > 0)
				{
					scene->dependencyResolver().resolve<GameSession>()->onConnectingToScene(scene);
				}
			}

			void sceneDisconnecting(std::shared_ptr<Scene> scene) override
			{
				auto name = scene->getHostMetadata("stormancer.gamesession");
				if (name.length() > 0)
				{
					auto gameSession = scene->dependencyResolver().resolve<details::GameSessionService>();
					if (gameSession)
					{
						std::static_pointer_cast<details::GameSession_Impl>(scene->dependencyResolver().resolve<GameSession>())->onDisconnectingFromGameSession(scene);
						gameSession->onDisconnecting();
					}
				}
			}

			void registerClientDependencies(ContainerBuilder& builder) override
			{
				builder.registerDependency<details::GameSession_Impl, IClient, ITokenHandler, ILogger, IActionDispatcher>().as<GameSession>().singleInstance();
			}

			void sceneDisconnected(std::shared_ptr<Scene> scene, std::string reason) override
			{
				auto name = scene->getHostMetadata("stormancer.gamesession");
				if (name.length() > 0)
				{
					auto gameSession = scene->dependencyResolver().resolve<details::GameSessionService>();
					if (gameSession)
					{
						std::static_pointer_cast<details::GameSession_Impl>(scene->dependencyResolver().resolve<GameSession>())->raiseOnDisconnectedFromGameSession(scene, reason);

					}
				}
			}
		};
	}
}
