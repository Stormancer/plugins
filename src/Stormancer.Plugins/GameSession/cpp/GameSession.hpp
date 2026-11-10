
#if !defined(STORM_PLUGIN_IMPL)
#define STORM_PLUGIN_IMPL 0
#endif

#if !defined(STORM_GAMESESSIONS_DEFINITION)
#define STORM_GAMESESSIONS_DEFINITION

#include "users/Users.hpp"
#include "stormancer/IPlugin.h"
#include "stormancer/msgpack_define.h"
#include "stormancer/ITokenHandler.h"
#include "stormancer/Tasks.h"
#include "stormancer/SessionId.h"
#include <string>
#include <vector>
#include <unordered_map>
#if defined(_PS5) && _PS5
#include <format>
#endif

namespace Stormancer
{
	namespace GameSessions
	{

		struct GameSessionId
		{
			GameSessionId() {}
			GameSessionId(const std::string& sceneId)
				:sceneId(sceneId)
			{}
			std::string sceneId;


			bool isValid() const
			{
				return !sceneId.empty();
			}
			bool operator==(const GameSessionId& v) const
			{
				return v.sceneId == sceneId;
			}

			bool operator!=(const GameSessionId& v) const
			{
				return !(v == *this);
			}

			const std::string& toString() const
			{
				return sceneId;
			}
		};

		enum class GameSessionConnectionState
		{
			Disconnected = 0,
			Connecting = 1,

			Connected = 3,
			HostFound = 7,
			Disconnecting = 2
		};

		enum class GameSessionHostState
		{
			Disconnected,
			Connected,
			Ready,
			Unknown,
		};



		struct TeamConfiguration
		{
			int slots;

			std::vector<SessionId> fixedSlots;
			STRM_MSGPACK_DEFINE(slots, fixedSlots)

		};

		struct GameSessionSettings
		{
			GameSessionId id;
			std::unordered_map<std::string, std::string> args;
			std::vector<TeamConfiguration> teamsConfig;
		};
		struct GameSessionSettingsRecord
		{
			std::unordered_map<std::string, std::string> args;
			std::vector<TeamConfiguration> teamsConfig;
			STRM_MSGPACK_DEFINE(args, teamsConfig)
		};


		struct GameSessionConnectionArgs
		{
			GameSessionId id;
			bool isHost;
			SessionId hostSessionId;
			GameSessionHostState hostState = GameSessionHostState::Unknown;

			GameSessionSettings settings;

			bool settingsAvailable = false;
			bool hostAvailable = false;
			bool isValid() const
			{
				return id.isValid();
			}
		};

		struct GameSessionConnectionStateChangedContext
		{
			GameSessionId id;
			std::shared_ptr<Scene> scene;
			GameSessionConnectionState oldState;
			GameSessionConnectionState newState;
			std::string reason;
		};

		struct UpdateTopologyEventArgs
		{
			GameSessionId gameSessionId;
			std::string error;
			SessionId newHost;
			bool isHost = false;
			GameSessionHostState hostState = GameSessionHostState::Unknown;

			std::string toString() const
			{
				return std::format("gameSessionId=%s, newHost=%s, isHost=%s, error=%s", gameSessionId.toString(), newHost.toString(), isHost ? "true" : "false", error);
			}
		};

		class GameSessionsPlugin;

		class GameSessionConnection
		{
		public:
			GameSessionConnection() {}
			GameSessionConnection(const GameSessionId& id, pplx::task<GameSessionConnectionArgs> task)
				:_id(id)
				, _task(task)
			{}

			pplx::task<GameSessionConnectionArgs> waitConnected()
			{
				return _task;
			}

			const GameSessionId id() const
			{
				return _id;
			}

		private:
			GameSessionId _id;
			pplx::task<GameSessionConnectionArgs> _task;
		};

		class GameSessionSubscription
		{
		public:
			GameSessionSubscription()
			{}

			GameSessionSubscription(const Stormancer::GameSessions::GameSessionId& id, std::shared_ptr<void> ptr)
				: _ptr(ptr)
				, _id(id)
			{}
			const Stormancer::GameSessions::GameSessionId& id()
			{
				return _id;
			}

			bool isValid() const
			{
				return _ptr != nullptr;
			}
		private:
			std::shared_ptr<void> _ptr;
			Stormancer::GameSessions::GameSessionId _id;

		};

		/// <summary>
		/// provides API to interact with game sessions.
		/// </summary>
		/// <remarks></remarks>
		class GameSessionApi
		{

		public:


			virtual ~GameSessionApi() = default;


			/// <summary>
			/// Connects to a game session using a connection token.
			/// </summary>
			/// <param name="token"></param>
			/// <param name="ct"></param>
			/// <returns></returns>
			virtual GameSessionConnection connectToGameSession(std::string token, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			/// <summary>
			/// Sets the current user as ready in a game session.
			/// </summary>
			/// <remarks>
			/// Use this method to signal to the game session that the current user is ready to accept remote connections from other users in the game session. 
			/// For instance, an host might call this method once the map is loaded and networking ready.
			/// </remarks>
			/// <param name="gameSessionId"></param>
			/// <param name="ct"></param>
			/// <returns></returns>
			virtual pplx::task<void> setPlayerReady(const GameSessionId& gameSessionId, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;

			template<typename TServerResult, typename TClientResult>
			pplx::task<TServerResult> postResult(const GameSessionId& gameSessionId, const TClientResult& clientResult, pplx::cancellation_token ct = pplx::cancellation_token::none())
			{
				auto streamWriter = [clientResult](BufferWriter& stream)
					{
						Serializer serializer;
						serializer.serialize(stream, clientResult);
					};

				return postResult(gameSessionId, streamWriter, ct)
					.then([](Packetisp_ptr packet)
						{
							Serializer serializer;
							TServerResult serverResult = packet->readObject<TServerResult>();
							return serverResult;
						});
			}

			virtual pplx::task<Packetisp_ptr> postResult(const GameSessionId& id, const std::function<void(BufferWriter&)>& streamWriter, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;


			virtual pplx::task<void> disconnectFromGameSession(const GameSessionId& id, pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;


			/// <summary>
			/// Gets the underlying scene of the current gamesession, an empty shared ptr otherwise
			/// </summary>
			virtual bool tryGetScene(const GameSessionId& sessionId, std::shared_ptr<Scene>& scene) = 0;

			virtual GameSessionConnectionState getConnectionState(const GameSessionId& id) const = 0;

			/// <summary>
			/// Wait for an active session to be ready.
			/// </summary>
			/// <param name="id"></param>
			/// <param name="fireCurrentTopoloty">Set to true to trigger an OnTopologyUpdated event if the session is already ready.</param>
			/// <param name="ct"></param>
			/// <remarks>The method fails on inactive session.</remarks>
			/// <returns></returns>
			virtual pplx::task<GameSessionConnectionArgs> waitSessionReadyAsync(const GameSessionId& id, bool fireCurrentTopology, pplx::cancellation_token ct) = 0;

			/// <summary>
			/// Gets the settings of a game session, including current host, team config and settings.
			/// </summary>
			/// <param name="gameSessionId"></param>
			/// <param name="args"></param>
			/// <returns></returns>
			virtual bool tryGetArgs(const GameSessionId& gameSessionId, GameSessionConnectionArgs& args) const = 0;

			virtual bool isInSession(const GameSessionId& gameSessionId) const = 0;

			virtual bool isInAnySession() const = 0;

			virtual std::vector<GameSessionId> activeSessions() const = 0;

			/// <summary>
			/// Updates the list of peers that are candidates to host the game session.
			/// </summary>
			/// <param name="ids"></param>
			/// <returns></returns>
			virtual pplx::task<void> updateHostCandidates(const GameSessionId& gameSessionId, const std::vector<SessionId>& ids, pplx::cancellation_token ct) = 0;

			/// <summary>
			/// Updates a single setting value in the target game session.
			/// </summary>
			/// <param name="gameSessionId"></param>
			/// <param name="key"></param>
			/// <param name="value"></param>
			/// <param name="ct"></param>
			/// <remarks>Only the host of a game session can perform this operation.</remarks>
			/// <returns></returns>
			virtual pplx::task<void> updateSingleGameSessionSettingValue(const GameSessionId& gameSessionId, const std::string& key, const std::string& value, pplx::cancellation_token ct) = 0;

			/// <summary>
			/// Updates all settings and team configuration in a game session.
			/// </summary>
			/// <param name="gameSessionId"></param>
			/// <param name="args"></param>
			/// <param name="ct"></param>
			/// <remarks>Only the host of a game session can perform this operation.</remarks>
			/// <returns></returns>
			virtual pplx::task<void> updateGameSessionSettings(const GameSessionId& gameSessionId, const GameSessionSettingsRecord& args, pplx::cancellation_token ct) = 0;

			/// <summary>
			/// Gets the game session id encoded in a connection token.
			/// </summary>
			/// <param name="token"></param>
			/// <remarks>This method does not validate the signature of the token.</remarks>
			/// <returns></returns>
			virtual GameSessionId getGameSessionIdFromToken(std::string token) const = 0;

			/// <summary>
			/// WIP: Automatically disconnects a gamesession when every instance of its subscriptions are destroyed.
			/// </summary>
			/// <param name="gameSessionId"></param>
			/// <returns></returns>
			virtual bool tryAcquireGameSession(const GameSessionId& gameSessionId, GameSessionSubscription& sub) = 0;

			/// <summary>
			/// Event fired whenever the connection state to a game session changes.
			/// </summary>
			/// <remarks>
			/// This is the place to register additional routes if you need to.
			/// </remarks>
			virtual Subscription subscribeOnConnectionStateChanged(std::function<void(GameSessionConnectionStateChangedContext)> callback, bool fireCurrentState) = 0;
			virtual Subscription subscribeOnNetworkTopologyUpdated(std::function<void(UpdateTopologyEventArgs)> callback, bool fireCurrentState) = 0;


		};


	}
}

template<>
struct std::hash<Stormancer::GameSessions::GameSessionId>
{
	std::size_t operator()(const Stormancer::GameSessions::GameSessionId& k) const
	{
		return std::hash<std::string>()(k.sceneId);
	}
};

namespace std
{

	template<>
	struct std::formatter<Stormancer::GameSessions::GameSessionId>
	{
		constexpr auto parse(std::format_parse_context& ctx)
		{
			return ctx.begin(); // No custom format specifiers
		}

		auto format(const Stormancer::GameSessions::GameSessionId& p, std::format_context& ctx) const
		{
			return std::format_to(ctx.out(), "{}", p.toString());
		}
	};

	template<>
	struct std::formatter<Stormancer::GameSessions::GameSessionHostState>
	{
		constexpr auto parse(std::format_parse_context& ctx)
		{
			return ctx.begin(); // No custom format specifiers
		}

		auto format(const Stormancer::GameSessions::GameSessionHostState& p, std::format_context& ctx) const
		{
			switch (p)
			{
			case Stormancer::GameSessions::GameSessionHostState::Disconnected:
				return std::format_to(ctx.out(), "Disconnected");
			case Stormancer::GameSessions::GameSessionHostState::Connected:
				return std::format_to(ctx.out(), "Connected");
			case Stormancer::GameSessions::GameSessionHostState::Ready:
				return std::format_to(ctx.out(), "Ready");
			case Stormancer::GameSessions::GameSessionHostState::Unknown:
				return std::format_to(ctx.out(), "Unknown");
			default:
				return std::format_to(ctx.out(), "Invalid");
			}

		}
	};
}

#endif
#if defined(STORM_PLUGIN_IMPL)
#if !defined(STORM_GAMESESSIONS_IMPL)
#define STORM_GAMESESSIONS_IMPL
#include <memory>
#include <string>





namespace Stormancer
{
	namespace GameSessions
	{
		namespace details
		{
			constexpr char GAMESESSION_P2P_SERVER_ID[] = "GameSession";

			enum class UpdateRecordType
			{
				NetworkTopology,
				Settings,
				Snapshot

			};

			struct UpdateRecordHeader
			{
				int version;
				UpdateRecordType type;

				STRM_MSGPACK_DEFINE(version, type)
			};

			struct GameSessionSnapshotHeader
			{
				bool topology;
				bool settings;
				STRM_MSGPACK_DEFINE(topology, settings)
			};


			struct NetworkingTopologyUpdateRecord
			{
				std::string error;
				SessionId newHost;

				GameSessionHostState state;
				STRM_MSGPACK_DEFINE(error, newHost, state)
			};




			class GameSessionService :public std::enable_shared_from_this<GameSessionService>
			{
				friend class ::Stormancer::GameSessions::GameSessionsPlugin;
				struct PendingRecord
				{
					PendingRecord()
						: body({})
					{}


					PendingRecord(const UpdateRecordHeader& header, std::span<const byte> body)
						: header(header)
						, body(body)
					{

					}
					UpdateRecordHeader header;
					Memory body;
				};

			public:

				GameSessionService(std::shared_ptr<Scene> scene) :
					_scene(scene),
					_logger(scene->dependencyResolver().resolve<ILogger>())
				{}

				void onConnecting(std::shared_ptr<Scene> scene)
				{
					Stormancer::Scene::RouteOptions o;
					o.filter = Stormancer::MessageOriginFilter::Host;

					auto wThat = weak_from_this();
					scene->addRoute(
						"gamesession.records",
						[wThat](Stormancer::Packetisp_ptr ptr)
						{

							if (auto that = wThat.lock())
							{
								auto span = ptr->buffer.getReadOnlySpan();
								size_t read = 0;
								while (that->tryProcessRecord(span, read))
								{
									span = span.subspan(read);
									read = 0;
								}
							}
						}
					);
				}
				pplx::task<void> updateHostCandidates(const std::vector<SessionId>& ids, pplx::cancellation_token ct)
				{
					auto scene = LockOrThrow(_scene, "scene");

					auto rpc = scene->dependencyResolver().resolve<RpcService>();
					return rpc->rpc("GameSession.UpdateHostCandidates", ct, ids);
				}

				pplx::task<void> updateGameSessionSettings(const GameSessionSettingsRecord& args, pplx::cancellation_token ct)
				{
					auto scene = LockOrThrow(_scene, "scene");

					auto rpc = scene->dependencyResolver().resolve<RpcService>();
					return rpc->rpc("GameSession.UpdateSettings", ct, args);
				}

				pplx::task<void> reset(pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					ct = linkTokenToDisconnection(ct);
					auto scene = LockOrThrow(_scene, "scene");


					auto rpc = scene->dependencyResolver().resolve<RpcService>();
					return rpc->rpc("gamesession.reset", ct);
				}

				pplx::task<void> getArgs(pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					ct = linkTokenToDisconnection(ct);
					auto scene = _scene.lock();
					if (!scene)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("Scene"), void);
					}

					auto rpc = scene->dependencyResolver().resolve<RpcService>();
					return rpc->rpc("GameSession.GetArgs", ct);
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
					_logger->log(LogLevel::Debug, "GameSessions", "Notify gamesession {} : player ready...", scene->id());
					scene->send("player.ready", [data](BufferWriter& stream)
						{
							Serializer serializer;
							serializer.serialize(stream, data);
						});
				}

				pplx::task<Packetisp_ptr> sendGameResults(const std::function<void(BufferWriter&)>& streamWriter, pplx::cancellation_token ct)
				{
					auto scene = _scene.lock();
					if (!scene)
					{
						STORM_RETURN_TASK_FROM_EXCEPTION(ObjectDeletedException("Scene"), Packetisp_ptr);
					}

					auto rpc = scene->dependencyResolver().resolve<RpcService>();
					return rpc->rpc<Packetisp_ptr>("gamesession.postresults", ct, streamWriter);
				}


				Event<> onShutdownReceived;
				Event<NetworkingTopologyUpdateRecord> onNetworkTopologyUpdated;
				Event<GameSessionSettingsRecord> onSettingsUpdated;


				const bool tryGetCurrentNetworkingTopologyState(NetworkingTopologyUpdateRecord& networkTopology)
				{
					if (_networkTopologyRecordReceived)
					{
						networkTopology = _currentNetworkingTopologyUpdate;
						return true;
					}
					return false;
				}

				const bool tryGetCurrentGamesessionSettings(GameSessionSettingsRecord& settings)
				{
					if (_gameSessionSettingsReceived)
					{
						settings = _currentGameSessionSettings;
						return true;
					}
					return false;
				}

			private:

				bool tryReadRecordHeader(std::span<const byte> buffer, UpdateRecordHeader& header, size_t& read) const
				{
					try
					{
						Serializer serializer;
						header = serializer.deserializeOne<UpdateRecordHeader>(buffer, read);
						return true;
					}
					catch (...)
					{
						return false;
					}
				}

				bool tryProcessRecord(std::span<const byte> buffer, size_t& read)
				{
					if (buffer.size() == 0)
					{
						return false;
					}
					UpdateRecordHeader header;

					if (!tryReadRecordHeader(buffer, header, read))
					{
						return false;
					}

					buffer = buffer.subspan(read);

					size_t recordReadBytes = 0;
					bool success = false;
					bool queued = false;
					if (header.type == UpdateRecordType::Snapshot)
					{

						if (tryProcessSnapshot(buffer, recordReadBytes))
						{

							success = true;
						}
						else
						{
							_logger->log(LogLevel::Error, "gamesession.protocol", "Failed to process snapshot");

							if (auto scene = _scene.lock())
							{
								Stormancer::catchErrors(scene->disconnect(), [](const char* msg) {});
							}

						}
					}
					else
					{
						if (header.version == _currentRecordVersion + 1)
						{

							success = tryProcessRecordBody(header, buffer, recordReadBytes);



						}
						else
						{
							queued = true;

							enqueuePendingRecord(header, buffer);

						}
					}

					if (success && !queued)
					{
						_currentRecordVersion = header.version;
						processEnqueuedRecord();
					}
					read += recordReadBytes;
					return success;
				}
				bool tryProcessRecordBody(const UpdateRecordHeader& header, std::span<const byte> buffer, size_t& recordReadBytes)
				{
					bool success = false;
					switch (header.type)
					{
					case UpdateRecordType::NetworkTopology:
						success = tryProcessNetworkTopologyRecord(buffer, recordReadBytes);
						break;
					case UpdateRecordType::Settings:
						success = tryProcessSettingsRecord(buffer, recordReadBytes);
						break;

					default:
						auto type = static_cast<int>(header.type);
						_logger->log(LogLevel::Error, "gamesession.protocol", "Unknown record Id={}", type);

						assert(false);
						if (auto scene = _scene.lock())
						{
							Stormancer::catchErrors(
								scene->disconnect(),
								[](const char* msg) {}
							);
						}
					}
					return success;
				}

				bool tryProcessSnapshot(std::span<const byte> buffer, size_t& read)
				{
					try
					{
						size_t readData;
						Serializer serializer;
						auto header = serializer.deserializeOne<GameSessionSnapshotHeader>(buffer, readData);
						buffer = buffer.subspan(readData);
						read += readData;


						if (header.settings)
						{
							bool s = tryProcessSettingsRecord(buffer, readData);

							assert(s);
							read += readData;
							buffer = buffer.subspan(readData);
						}
						if (header.topology)
						{
							bool s = tryProcessNetworkTopologyRecord(buffer, readData);

							assert(s);
							read += readData;
						}
						return true;
					}
					catch (...)
					{
						return false;
					}
				}

				bool tryProcessNetworkTopologyRecord(std::span<const byte> buffer, size_t& read)
				{
					try
					{
						Serializer serializer;
						auto record = serializer.deserializeOne<NetworkingTopologyUpdateRecord>(buffer, read);

						_logger->log(LogLevel::Info, "gamesession", "Processing topology updated event. host={} newState={}", record.newHost, record.state);
						_currentNetworkingTopologyUpdate = record;
						_networkTopologyRecordReceived = true;
						onNetworkTopologyUpdated(record);

						return true;
					}
					catch (...)
					{
						return false;
					}
				}

				bool tryProcessSettingsRecord(std::span<const byte> buffer, size_t& read)
				{
					try
					{
						Serializer serializer;
						auto record = serializer.deserializeOne<GameSessionSettingsRecord>(buffer, read);
						_logger->log(LogLevel::Info, "gamesession", "Processing settings updated event.");
						_currentGameSessionSettings = record;
						_gameSessionSettingsReceived = true;
						onSettingsUpdated(record);

						return true;
					}
					catch (...)
					{
						return false;
					}
				}


				void enqueuePendingRecord(const UpdateRecordHeader& header, std::span<const byte> body)
				{
					int i;
					for (i = 0; i < _pendingRecords.size(); i++)
					{
						if (_pendingRecords[i].header.version > header.version)
						{
							break;
						}
					}
					_pendingRecords.insert(_pendingRecords.begin() + i, PendingRecord{ header, body });
				}
				void processEnqueuedRecord()
				{
					PendingRecord next;
					while (tryPopNextPendingRecord(next))
					{
						size_t read;
						tryProcessRecordBody(next.header, next.body.getReadOnlySpan(), read);
					}
				}
				bool tryPopNextPendingRecord(PendingRecord& record)
				{

					if (_pendingRecords.size() != 0 && _pendingRecords[0].header.version == _currentRecordVersion + 1)
					{
						record = _pendingRecords[0];
						_pendingRecords.erase(_pendingRecords.begin());
						return true;
					}
					else
					{
						return false;
					}
				}
				std::vector<PendingRecord> _pendingRecords;

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



				pplx::task_completion_event<void> _waitServerTce;
				std::weak_ptr<Scene> _scene;
				std::shared_ptr<Stormancer::ILogger> _logger;
				pplx::cancellation_token_source _disconnectionCts;

				bool _gameSessionSettingsReceived = false;
				GameSessionSettingsRecord _currentGameSessionSettings;

				bool _networkTopologyRecordReceived = false;
				NetworkingTopologyUpdateRecord _currentNetworkingTopologyUpdate;
				int _currentRecordVersion = 0;

			};

			//Private gamesession container implementation that deals with the lifecycle of a gamesession connection
			struct GameSessionContainer : public std::enable_shared_from_this< GameSessionContainer>
			{
			public:

				GameSessionContainer() {}
				GameSessionContainer(const GameSessionId& id, std::shared_ptr<IActionDispatcher> dispatcher, std::shared_ptr<ILogger> logger)
					: _id(id)
					, _dispatcher(dispatcher)
					, _logger(logger)
				{}



				pplx::cancellation_token cancellationToken()
				{
					return cts.get_token();
				}

				pplx::task<std::shared_ptr<GameSessionService>> service(pplx::cancellation_token ct)
				{
					return getSceneAsync(ct).then([](std::shared_ptr<Scene> s)
						{
							return s->dependencyResolver().resolve<GameSessionService>();
						});
				}


				void setInactive()
				{
					using namespace std::chrono_literals;
					Stormancer::catchErrors(
						Stormancer::taskDelay(5000ms).then(
							[wThat = weak_from_this()]()
							{
								if (auto that = wThat.lock())
								{
									if (that->_guard.expired())
									{
										that->_logger->log(LogLevel::Info, "gamesession", "Game session {} expired. Disconnecting....", that->args.id.toString());
										return that->disconnectAsync(pplx::cancellation_token::none());
									}
								}

								return pplx::task_from_result();

							}
						), [](auto msg) {});

				}


				pplx::task<void> disconnectAsync(pplx::cancellation_token ct)
				{
					_disconnectionPending = true;
					return service(ct)
						.then([ct](std::shared_ptr<GameSessionService> service)
							{
								return service->disconnect(ct);
							});
				}

				pplx::task<void> waitDisconnectedAsync(pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					return pplx::create_task(_disconnectedTce, ct);
				}

				pplx::task<GameSessionConnectionArgs> waitSessionReadyAsync(bool fireCurrentTopology, pplx::cancellation_token ct)
				{

					if (args.hostAvailable && args.settingsAvailable)
					{
						if (fireCurrentTopology)
						{
							// If the session is already ready, we artificially trigger onTopologyUpdated manually to ensure clients can process it even if they

							//We must dispatch the event asynchronously to prevent lock reentry.
							_dispatcher->post([wThat = weak_from_this()]()
								{
									if (auto that = wThat.lock())
									{
										UpdateTopologyEventArgs a;
										a.gameSessionId = that->_id;
										a.isHost = that->args.isHost;
										a.newHost = that->args.hostSessionId;
										a.hostState = that->args.hostState;
										that->onTopologyUpdated(a);
									}
								});

						}
						return pplx::task_from_result(args);
					}
					else
					{
						return  pplx::create_task(_sessionReadyTce, ct);
					}

				}

				pplx::task<void> waitConnectedAsync(pplx::cancellation_token ct)
				{
					return pplx::create_task(_connectedTce, ct);
				}


				// Keep game finder scene alive


				pplx::task<std::shared_ptr<Scene>> getSceneAsync(pplx::cancellation_token ct = pplx::cancellation_token::none())
				{
					return pplx::create_task(_sceneTce, ct);
				}


				GameSessionConnectionArgs args;


				GameSessionConnectionState getConnectionState() const
				{
					return _state;
				}
				const GameSessionId& id() const
				{
					return _id;
				}


				void onSceneCreated(std::shared_ptr<Scene> scene)
				{

					auto service = scene->dependencyResolver().resolve<GameSessionService>();
					auto client = scene->dependencyResolver().resolve<IClient>();

					service->onConnecting(scene);
					auto wThat = weak_from_this();
					onTopologyUpdatedSubscription = service->onNetworkTopologyUpdated.subscribe(
						[id = _id, sessionId = client->sessionId(), wThat](NetworkingTopologyUpdateRecord record)
						{
							if (auto that = wThat.lock())
							{
								if (record.newHost.isValid())
								{
									that->args.hostSessionId = record.newHost;
									that->args.hostAvailable = true;
									that->args.id = id;
									that->args.isHost = record.newHost == sessionId;
									that->args.hostState = record.state;

									that->updateStatus();

									UpdateTopologyEventArgs args;
									args.gameSessionId = id;
									args.error = record.error;
									args.isHost = that->args.isHost;
									args.hostState = that->args.hostState;
									args.newHost = record.newHost;

									// if host, do not send your own Ready events
									// if not host do not send Disconnected events.
									if ((args.isHost && record.state != GameSessionHostState::Ready) ||
										(!args.isHost && record.state != GameSessionHostState::Disconnected))
									{
										that->onTopologyUpdated(args);
									}

								}
							}
						}
					);
					onSettingsUpdatedSubscription = service->onSettingsUpdated.subscribe(
						[wThat, id = _id](GameSessionSettingsRecord settings)
						{
							if (auto that = wThat.lock())
							{
								that->args.settings.args = settings.args;
								that->args.settings.teamsConfig = settings.teamsConfig;
								that->args.settings.id = id;
								that->args.settingsAvailable = true;
								that->updateStatus();
								that->onSettingsUpdated(that->args.settings);

							}
						}
					);
				}

				void updateStatus()
				{
					if (args.hostAvailable && args.settingsAvailable)
					{
						setConnectionState(GameSessionConnectionState::HostFound);
						_sessionReadyTce.set(args);
					}
				}
				void onSceneConnected(std::shared_ptr<Scene> scene)
				{
					_sceneTce.set(scene);
					_connectedTce.set();
					setConnectionState(Stormancer::GameSessions::GameSessionConnectionState::Connected);
					/*UpdateTopologyEventArgs topologyArgs;
					topologyArgs.gameSessionId = _id;
					topologyArgs.id = _topologyUpdateId;

					onTopologyUpdated(topologyArgs);*/

					_topologyUpdateId++;

					if (!_disconnectionPending)
					{
						auto service = scene->dependencyResolver().resolve<GameSessionService>();

						/*catchErrors(service->requestP2PToken(cancellationToken())
							.then([id = _id, wThat = weak_from_this()](HostInfosMessage msg)
								{
									auto that = LockOrThrow(wThat, "GameSession");
									{
										that->setConnectionState(GameSessionConnectionState::HostFound);
										that->args.settings = msg.settings;
										that->args.hostSessionId = msg.hostSessionId;
										that->args.id = id;
										that->args.isHost = msg.isHost;
										that->_sessionReadyTce.set(that->args);
									}
									{

										UpdateTopologyEventArgs args;
										args.gameSessionId = id;
										args.id = 1;
										args.isHost = msg.isHost;
										args.newHost = msg.hostSessionId;

										that->onTopologyUpdated(args);
									}

								}),
							[](auto msg) {});*/
					}

				}

				void onSceneDisconnecting(std::shared_ptr<Scene> scene)
				{
					setConnectionState(GameSessionConnectionState::Disconnecting);
				}

				void onSceneDisconnected(std::shared_ptr<Scene> scene, const std::string& reason)
				{
					_disconnectedTce.set();
					cts.cancel();
					setConnectionState(GameSessionConnectionState::Disconnected, reason);
				}

				class GameSessionGuard
				{
				public:
					GameSessionGuard(std::weak_ptr<GameSessionContainer> container)
						: _container(container)
					{}

					~GameSessionGuard()
					{
						if (auto container = _container.lock())
						{
							container->setInactive();
						}
					}
				private:
					std::weak_ptr<GameSessionContainer> _container;
				};

				GameSessionSubscription acquire()
				{
					if (auto guard = _guard.lock())
					{
						return GameSessionSubscription{ this->id(), guard };
					}
					else
					{
						guard = std::make_shared<GameSessionGuard>(weak_from_this());
						_guard = guard;
						return GameSessionSubscription{ this->id(), guard };

					}
				}

				Event<GameSessionConnectionStateChangedContext> onConnectionStateChanged;
				Event< UpdateTopologyEventArgs> onTopologyUpdated;
				Event<GameSessionSettings> onSettingsUpdated;

				Subscription parentOnTopologyUpdatedSubscription;
				Subscription parentOnSettingsUpdatedSubscription;
				Subscription parentOnConnectionStateChangedSubscription;

				Subscription onTopologyUpdatedSubscription;
				Subscription onSettingsUpdatedSubscription;
			private:
				std::weak_ptr<void> _guard;

				void setConnectionState(GameSessionConnectionState newState, const std::string& reason = "")
				{
					if (newState != _state)
					{
						GameSessionConnectionStateChangedContext ctx;
						ctx.id = _id;
						ctx.oldState = _state;
						ctx.newState = newState;
						ctx.reason = reason;
						_state = newState;

						if (onConnectionStateChanged.hasSubscribers())
						{
							onConnectionStateChanged(ctx);
						}

					}

				}
				GameSessionId _id;
				std::shared_ptr<IActionDispatcher> _dispatcher;
				std::shared_ptr<ILogger> _logger;
				int _topologyUpdateId;

				Subscription _onShutdownReceived;

				GameSessionConnectionState _state = GameSessionConnectionState::Connecting;

				pplx::task_completion_event<GameSessionConnectionArgs> _sessionReadyTce;
				pplx::task_completion_event<void> _connectedTce;
				pplx::task_completion_event<void> _disconnectedTce;
				pplx::cancellation_token_source cts;
				pplx::task_completion_event<std::shared_ptr<Scene>> _sceneTce;

				bool _disconnectionPending = false;

			};

			class GameSession_Impl : public GameSessionApi, public std::enable_shared_from_this<GameSession_Impl>
			{
				friend class ::Stormancer::GameSessions::GameSessionsPlugin;

			public:


				GameSession_Impl(std::weak_ptr<IClient> client, std::shared_ptr<ITokenHandler> tokens, std::shared_ptr<ILogger> logger, std::shared_ptr<IActionDispatcher> dispatcher)
					: _logger(logger)
					, _tokenReader(tokens)
					, _wDispatcher(dispatcher)
					, _wClient(client)
				{}



				GameSessionConnection connectToGameSession(std::string token, pplx::cancellation_token ct) override
				{
					std::lock_guard<std::mutex> lg(_lock);

					if (token.empty())
					{
						throw std::runtime_error("Empty connection token");
					}

					auto client = LockOrThrow(_wClient, "client");
					// Client should never be null here
					auto dispatcher = client->dependencyResolver().resolve<IActionDispatcher>();


					auto id = getGameSessionIdFromToken(token);

					pplx::task<void> currentOperation;
					auto it = _gameSessions.find(id);
					if (it != _gameSessions.end())
					{
						auto container = it->second;
						switch (container->getConnectionState())
						{
						case GameSessionConnectionState::Connecting:
						case GameSessionConnectionState::Connected:
						case GameSessionConnectionState::HostFound:
							return { id,container->waitSessionReadyAsync(true,ct) };
						case GameSessionConnectionState::Disconnecting:
						case GameSessionConnectionState::Disconnected:
							currentOperation = container->waitDisconnectedAsync(ct);
						default:
							assert(false);
						}
					}

					return { id,connectToGameSessionImpl(token,id,ct) };
				}

				GameSessionConnectionState getConnectionState(const Stormancer::GameSessions::GameSessionId& id) const override
				{
					std::lock_guard<std::mutex> lg(_lock);
					std::shared_ptr<GameSessionContainer> container;
					if (tryGetContainer(id, container))
					{
						return container->getConnectionState();

					}
					else
					{
						return GameSessionConnectionState::Disconnected;
					}
				}

				bool isInSession(const GameSessionId& id) const override
				{
					auto state = getConnectionState(id);
					return ((int)state & (int)GameSessionConnectionState::Connected) != 0;
				}

				bool isInAnySession() const override
				{
					std::lock_guard<std::mutex> lg(_lock);
					for (auto& kvp : _gameSessions)
					{
						if (((int)kvp.second->getConnectionState() & (int)GameSessionConnectionState::Connected) != 0)
						{
							return true;
						}
					}
					return false;
				}

				GameSessionId getGameSessionIdFromToken(std::string token) const override
				{
					auto sceneInfos = _tokenReader->getSceneEndpointInfo(token);
					auto index = sceneInfos.tokenData.SceneId.find_last_of('/');


					return { sceneInfos.tokenData.SceneId.substr(index + 1) };
				}

				std::vector<GameSessionId> activeSessions() const override
				{
					std::lock_guard<std::mutex> lg(_lock);

					std::vector<GameSessionId> result;
					for (auto& kvp : _gameSessions)
					{
						result.push_back(kvp.first);
					}
					return result;
				}

				pplx::task<void> setPlayerReady(const GameSessionId& gameSessionId, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					std::lock_guard<std::mutex> lg(_lock);
					auto dispatcher = LockOrThrow(_wDispatcher, "IActionDispatcher");

					return getScene(gameSessionId, ct).then([ct](std::shared_ptr<Scene> scene)
						{
							auto gameSessionService = scene->dependencyResolver().resolve<GameSessionService>();
							gameSessionService->ready("");

						}, dispatcher);


				}

				pplx::task<Packetisp_ptr> postResult(const GameSessionId& gameSessionId, const std::function<void(BufferWriter&)>& streamWriter, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					std::lock_guard<std::mutex> lg(_lock);
					auto dispatcher = LockOrThrow(_wDispatcher, "IActionDispatcher");
					auto taskOptions = dispatcher ? pplx::task_options(dispatcher) : pplx::task_options();

					return getScene(gameSessionId, ct)
						.then([streamWriter, ct](std::shared_ptr<Scene> scene)
							{
								auto gameSessionService = scene->dependencyResolver().resolve<GameSessionService>();
								return gameSessionService->sendGameResults(streamWriter, ct);

							}, taskOptions);
				}


				pplx::task<void> disconnectFromGameSession(const GameSessionId& id, pplx::cancellation_token ct = pplx::cancellation_token::none()) override
				{
					std::lock_guard<std::mutex> lg(_lock);
					auto dispatcher = LockOrThrow(_wDispatcher, "IActionDispatcher");
					auto taskOptions = dispatcher ? pplx::task_options(dispatcher) : pplx::task_options();


					auto it = _gameSessions.find(id);
					if (it == _gameSessions.end())
					{
						return pplx::task_from_result();
					}

					auto container = it->second;
					pplx::task<void> task = pplx::task_from_result();
					switch (container->getConnectionState())
					{
					case GameSessionConnectionState::Disconnecting:
					case GameSessionConnectionState::Disconnected:
						return container->waitDisconnectedAsync(ct);
						break;
					case GameSessionConnectionState::Connecting:
						task = container->waitConnectedAsync(ct);
					}

					auto wThat = this->weak_from_this();
					return task
						.then([container, ct]()
							{
								return container->disconnectAsync(ct);
							})
						.then([wThat, id](pplx::task<void> t)
							{
								auto that = LockOrThrow(wThat, "GameSessionApi");
								try
								{
									t.get();
								}
								catch (std::exception)
								{
									throw;
								}

							}, taskOptions);

				}

				pplx::task<GameSessionConnectionArgs> waitSessionReadyAsync(const GameSessionId& id, bool fireCurrentTopology, pplx::cancellation_token ct) override
				{
					std::lock_guard<std::mutex> lg(_lock);
					auto dispatcher = LockOrThrow(_wDispatcher, "IActionDispatcher");
					std::shared_ptr<GameSessionContainer> container;
					if (!tryGetContainer(id, container))
					{
						return pplx::task_from_exception< GameSessionConnectionArgs>(std::runtime_error("notFound"));
					}

					return container->waitSessionReadyAsync(fireCurrentTopology, ct)
						.then([](GameSessionConnectionArgs args) {return args; }, dispatcher);
				}

				bool tryGetScene(const GameSessionId& sessionId, std::shared_ptr<Scene>& scene) override
				{
					std::lock_guard<std::mutex> lg(_lock);
					std::shared_ptr<GameSessionContainer> container;
					if (tryGetContainer(sessionId, container))
					{
						auto task = container->getSceneAsync();
						if (task.is_done())
						{
							try
							{
								scene = task.get();
								return true;
							}
							catch (std::exception&)
							{
							}
						}

					}

					return false;

				}

				bool tryGetArgs(const GameSessionId& gameSessionId, GameSessionConnectionArgs& args) const override
				{
					std::lock_guard<std::mutex> lg(_lock);
					std::shared_ptr<GameSessionContainer> container;
					if (tryGetContainer(gameSessionId, container) && container->args.isValid())
					{
						args = container->args;
						return true;
					}
					else
					{
						return false;
					}
				}

				Subscription subscribeOnNetworkTopologyUpdated(std::function<void(UpdateTopologyEventArgs)> callback, bool fireCurrentState) override
				{
					return this->onTopologyUpdated.subscribe(callback);
				}

				Subscription subscribeOnConnectionStateChanged(std::function<void(GameSessionConnectionStateChangedContext)> callback, bool fireCurrentState) override
				{
					return this->onConnectionStateChanged.subscribe(callback);
				}



				pplx::task<void> updateHostCandidates(const GameSessionId& gameSessionId, const std::vector<SessionId>& ids, pplx::cancellation_token ct) override
				{
					std::lock_guard<std::mutex> lg(_lock);
					std::shared_ptr<GameSessionContainer> container;
					if (tryGetContainer(gameSessionId, container))
					{
						return container->service(ct).then(
							[ids, ct](std::shared_ptr<Stormancer::GameSessions::details::GameSessionService> service)
							{
								return service->updateHostCandidates(ids, ct);
							}
						);
					}
					else
					{
						return pplx::task_from_exception<void>(std::runtime_error("disconnected"));
					}
				}

				pplx::task<void> updateSingleGameSessionSettingValue(const GameSessionId& gameSessionId, const std::string& key, const std::string& value, pplx::cancellation_token ct) override
				{
					std::lock_guard<std::mutex> lg(_lock);
					std::shared_ptr<GameSessionContainer> container;
					if (tryGetContainer(gameSessionId, container))
					{
						return container->service(ct).then(
							[key, value, ct](std::shared_ptr<Stormancer::GameSessions::details::GameSessionService> service)
							{
								GameSessionSettingsRecord settings;
								if (service->tryGetCurrentGamesessionSettings(settings))
								{
									settings.args[key] = value;
									return service->updateGameSessionSettings(settings, ct);

								}
								else
								{
									return pplx::task_from_exception<void>(std::runtime_error("disconnected"));
								}
							}
						);
					}
					else
					{
						return pplx::task_from_exception<void>(std::runtime_error("disconnected"));
					}
				}

				pplx::task<void> updateGameSessionSettings(const GameSessionId& gameSessionId, const GameSessionSettingsRecord& args, pplx::cancellation_token ct) override
				{
					std::lock_guard<std::mutex> lg(_lock);
					std::shared_ptr<GameSessionContainer> container;
					if (tryGetContainer(gameSessionId, container))
					{
						return container->service(ct).then(
							[args, ct](std::shared_ptr<Stormancer::GameSessions::details::GameSessionService> service)
							{
								return service->updateGameSessionSettings(args, ct);
							}
						);
					}
					else
					{
						return pplx::task_from_exception<void>(std::runtime_error("disconnected"));
					}
				}

				bool tryAcquireGameSession(const GameSessionId& gameSessionId, GameSessionSubscription& sub) override
				{
					std::lock_guard<std::mutex> lg(_lock);

					std::shared_ptr<GameSessionContainer> container;
					if (tryGetContainer(gameSessionId, container))
					{
						sub = container->acquire();

						return true;
					}
					else
					{
						return false;
					}
				}

			private:

				bool tryGetContainer(const GameSessionId& gameSessionId, std::shared_ptr<GameSessionContainer>& container) const
				{
					auto it = this->_gameSessions.find(gameSessionId);
					if (it != _gameSessions.end())
					{
						container = it->second;
						return true;
					}
					else
					{
						return false;
					}
				}

				pplx::task<std::shared_ptr<Scene>> getScene(const GameSessionId& gameSessionId, pplx::cancellation_token ct) const
				{
					std::shared_ptr<GameSessionContainer> container;
					if (tryGetContainer(gameSessionId, container))
					{
						return container->getSceneAsync(ct);
					}
					else
					{
						return pplx::task_from_exception<std::shared_ptr<Scene>>(std::runtime_error("gameSessionDisconnected"));
					}
				}


				pplx::task<GameSessionConnectionArgs> connectToGameSessionImpl(const std::string& token, const GameSessionId& id, pplx::cancellation_token ct)
				{
					auto dispatcher = LockOrThrow(_wDispatcher, "IActionDispatcher");
					auto taskOptions = dispatcher ? pplx::task_options(dispatcher) : pplx::task_options();
					auto wThat = weak_from_this();
					auto client = LockOrThrow(_wClient, "Client");
					auto container = std::make_shared<GameSessionContainer>(id, _wDispatcher.lock(), _logger);


					container->parentOnConnectionStateChangedSubscription = container->onConnectionStateChanged.subscribe([wThat](GameSessionConnectionStateChangedContext ctx)
						{
							if (auto that = wThat.lock())
							{

								that->onConnectionStateChanged(ctx);
							}
						});
					container->parentOnTopologyUpdatedSubscription = container->onTopologyUpdated.subscribe([wThat](UpdateTopologyEventArgs args)
						{
							if (auto that = wThat.lock())
							{

								that->onTopologyUpdated(args);
							}
						});
					container->parentOnSettingsUpdatedSubscription = container->onSettingsUpdated.subscribe(
						[wThat](GameSessionSettings settings)
						{
							if (auto that = wThat.lock())
							{
								that->onGameSettingsUpdated(settings);
							}
						}
					);


					_gameSessions.emplace(id, container);
					return client->connectToPrivateScene(token)
						.then([container, id, ct](std::shared_ptr<Scene> scene)
							{
								return container->waitSessionReadyAsync(false, ct);
							})
						.then([wThat, id](pplx::task<GameSessionConnectionArgs> t)
							{
								auto that = LockOrThrow(wThat, "GameSessionApi");
								try
								{
									return t.get();
								}
								catch (std::exception)
								{
									std::lock_guard<std::mutex> lg(that->_lock);
									that->_gameSessions.erase(id);
									throw;
								}

							}, taskOptions);
				}



				void onSceneCreated(std::shared_ptr<Scene> scene)
				{
					std::lock_guard<std::mutex> lg(_lock);
					std::shared_ptr<GameSessionContainer> container;
					GameSessionId id{ scene->id() };
					if (tryGetContainer(id, container))
					{
						container->onSceneCreated(scene);
					}
				}

				void onConnectingToScene(std::shared_ptr<Scene> scene)
				{
					std::lock_guard<std::mutex> lg(_lock);
					std::shared_ptr<GameSessionContainer> container;
					GameSessionId id{ scene->id() };
					if (tryGetContainer(id, container))
					{

					}
				}

				void onConnectedToScene(std::shared_ptr<Scene> scene)
				{
					std::lock_guard<std::mutex> lg(_lock);
					std::shared_ptr<GameSessionContainer> container;
					GameSessionId id{ scene->id() };
					if (tryGetContainer(id, container))
					{
						container->onSceneConnected(scene);
					}
				}

				void onDisconnectingFromGameSession(std::shared_ptr<Scene> scene)
				{
					std::lock_guard<std::mutex> lg(_lock);
					std::shared_ptr<GameSessionContainer> container;
					GameSessionId id{ scene->id() };
					if (tryGetContainer(id, container))
					{
						container->onSceneDisconnecting(scene);
					}
				}
				void onDisconnectedFromGameSession(std::shared_ptr<Scene> scene, std::string reason)
				{
					std::lock_guard<std::mutex> lg(_lock);
					std::shared_ptr<GameSessionContainer> container;
					GameSessionId id{ scene->id() };
					if (tryGetContainer(id, container))
					{
						container->onSceneDisconnected(scene, reason);
					}

					_gameSessions.erase(scene->id());
				}

				Event<GameSessionConnectionStateChangedContext> onConnectionStateChanged;
				Event<UpdateTopologyEventArgs> onTopologyUpdated;
				Event<GameSessionSettings> onGameSettingsUpdated;

				std::shared_ptr<ILogger> _logger;
				std::shared_ptr<ITokenHandler> _tokenReader;
				std::weak_ptr<IActionDispatcher> _wDispatcher;
				std::weak_ptr<IClient> _wClient;
				std::unordered_map<GameSessionId, std::shared_ptr<GameSessionContainer>> _gameSessions;
				mutable std::mutex _lock;

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
				if (!name.empty())
				{
					builder.registerDependency<details::GameSessionService, Scene>().singleInstance();
				}
			}

			void sceneCreated(std::shared_ptr<Scene> scene) override
			{
				auto name = scene->getHostMetadata("stormancer.gamesession");
				if (!name.empty())
				{
					std::static_pointer_cast<details::GameSession_Impl>(scene->dependencyResolver().resolve<GameSessionApi>())->onSceneCreated(scene);
				}
			}

			void sceneConnecting(std::shared_ptr<Scene> scene) override
			{
				auto name = scene->getHostMetadata("stormancer.gamesession");
				if (!name.empty())
				{
					std::static_pointer_cast<details::GameSession_Impl>(scene->dependencyResolver().resolve<GameSessionApi>())->onConnectingToScene(scene);
				}
			}
			void sceneConnected(std::shared_ptr<Scene> scene) override
			{
				auto name = scene->getHostMetadata("stormancer.gamesession");
				if (!name.empty())
				{
					std::static_pointer_cast<details::GameSession_Impl>(scene->dependencyResolver().resolve<GameSessionApi>())->onConnectedToScene(scene);
				}
			}

			void sceneDisconnecting(std::shared_ptr<Scene> scene) override
			{
				auto name = scene->getHostMetadata("stormancer.gamesession");
				if (!name.empty())
				{
					std::static_pointer_cast<details::GameSession_Impl>(scene->dependencyResolver().resolve<GameSessionApi>())->onDisconnectingFromGameSession(scene);
				}
			}

			void registerClientDependencies(ContainerBuilder& builder) override
			{
				builder.registerDependency<details::GameSession_Impl, IClient, ITokenHandler, ILogger, IActionDispatcher>().as<GameSessionApi>().singleInstance();
			}

			void sceneDisconnected(std::shared_ptr<Scene> scene, const std::string& reason) override
			{
				auto name = scene->getHostMetadata("stormancer.gamesession");
				if (!name.empty())
				{
					std::static_pointer_cast<details::GameSession_Impl>(scene->dependencyResolver().resolve<GameSessionApi>())->onDisconnectedFromGameSession(scene, reason);
				}
			}
		};
	}
} // namespace Stormancer

STRM_MSGPACK_ADD_ENUM(Stormancer::GameSessions::details::UpdateRecordType);
STRM_MSGPACK_ADD_ENUM(Stormancer::GameSessions::GameSessionHostState);
#endif
#endif
