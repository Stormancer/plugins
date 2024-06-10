
#ifndef STORM_PLUGIN_IMPL
#define STORM_PLUGIN_IMPL 0
#endif

#ifndef STORM_PLUGIN_LOCKSTEP_H
#define STORM_PLUGIN_LOCKSTEP_H

#include "stormancer/IPlugin.h"
#include "stormancer/Event.h"
#include "stormancer/SessionId.h"
#include <stdio.h>


namespace Stormancer
{
	namespace Gameplay
	{
		struct LockstepOptions
		{
			/// <summary>
			/// Delay in gameplay time between a command is pushed to the API and executed.
			/// </summary>
			float DelaySeconds = 0.1f;
			float FixedDeltaTimeSeconds = 0.034f;
		
		};
		struct Command
		{
			int playerId;
			SessionId sessionId;
			::std::vector<byte> content;

			float timeSeconds;
		};

		struct LockstepPlayer
		{
			SessionId sessionId;
			int playerId;
			unsigned int latencyMs;

			bool localPlayer;

			float synchronizedUntilMs;
			int lastCommandId;
			float targetDeltaTimeSeconds;
		};


		struct Frame
		{
			float currentTimeSeconds;

			/// <summary>
			/// Commands performed during this frame
			/// </summary>
			::std::vector<Command> commands;


		};
		struct RollbackContext
		{
			int targetFrame;
			int restoredFrame;
		};

		class LockstepPlugin : public IPlugin
		{
			PluginDescription getDescription();
			void registerClientDependencies(ContainerBuilder& clientBuilder) override;

			void registerSceneDependencies(ContainerBuilder& sceneBuilder, std::shared_ptr<Scene> scene) override;
			void sceneCreated(::std::shared_ptr<Scene> scene) override;
			void sceneDisconnecting(::std::shared_ptr<Scene> scene) override;
		};
		namespace details
		{
			class ILockstepService
			{
			public:
				virtual int pushCommand(byte* buffer, int length) = 0;
				virtual float tick(float deltaSeconds, float realDeltaSeconds) = 0;
				virtual float getCurrentTime() const = 0;
				virtual float getTargetTime() const = 0;
				virtual int lastExecutedCommand() const = 0;
				virtual bool isPaused() const = 0;
				virtual void pause(bool pause) = 0;

				virtual std::vector<LockstepPlayer> getPlayers() const = 0;

				Stormancer::Event<Frame&> onStep;
				Event<bool> onPauseStateChanged;
				Event<> onPlayerListChanged;

				virtual ~ILockstepService() {};
			};
		}
		class LockstepApi
		{
			friend LockstepPlugin;
		public:
			float tick(float deltaSeconds, float realDeltaSeconds);

			float getCurrentTime() const;
			float getTargetTime() const;
			int lastExecutedCommand() const;

			bool isEnabled() const;

			/// <summary>
			/// Pushes a command to the system. if frame is not specified, 
			/// </summary>
			/// <param name="buffer"></param>
			/// <param name="length"></param>
			/// <param name="frame"></param>
			int pushCommand(byte* buffer, int length);

			Event<Frame&> onStep;
			Event<RollbackContext&> onRollback;

			Event<bool> onPauseStateChanged;
			Event<> onPlayerListChanged;

			bool isPaused() const;

			void pause(bool pause);

			std::vector<LockstepPlayer> getPlayers() const;



			virtual ~LockstepApi() {};

		private:
			void onSceneConnected(std::shared_ptr<details::ILockstepService> service);
			void onSceneDisconnected();

			std::shared_ptr<details::ILockstepService> _service;

			Subscription _onStepSubscription;
			Subscription _onPauseStateChangedSubscription;
			Subscription _onPlayerListChangedSubscription;
		};



	}
}
#endif
#if STORM_PLUGIN_IMPL

#include "stormancer/Scene.h"
#include "stormancer/RPC/RpcService.h"
#include "stormancer/IClient.h"

#undef STORM_PLUGIN_IMPL
#include "replication/P2PMesh.hpp"
#undef STORM_PLUGIN_IMPL
#define STORM_PLUGIN_IMPL 1

namespace Stormancer
{
	namespace Gameplay
	{
		namespace details
		{
			/*public enum PlayersUpdateCommandType
			{
				Add,
				Remove
			}*/
			enum class PlayersUpdateCommandType
			{
				Add,
				Remove
			};



			/*public class PlayersUpdateCommand
			{
				[Key(0)]
					public required PlayersUpdateCommandType CommandType{ get; init; }

					[Key(1)]
					public required int UpdateId{ get; init; }

					[Key(2)]
					public required int PlayerId{ get; init; }

					[Key(3)]
					public required SessionId PlayerSessionId{ get; init; }
			}*/
			struct PlayersUpdateCommand
			{
				PlayersUpdateCommandType commandType;
				int updateId;
				int playerId;
				Stormancer::SessionId playerSessionId;

				MSGPACK_DEFINE(commandType, updateId, playerId, playerSessionId)
			};


			/*
			[MessagePackObject]
			public class PlayersSnapshotInstallCommand
			{
				[Key(0)]
				public required int UpdateId { get; init; }

				[Key(1)]
				public Dictionary<int, SessionId> Players { get; init; }
			}
			*/
			struct PlayersSnapshotInstallCommand
			{
				int updateId;
				int currentPlayerId;

				std::unordered_map<int, SessionId> players;


				MSGPACK_DEFINE(updateId, currentPlayerId, players);

			};

			//Frame status sent by remote peer.
			struct FrameDto
			{
				int64 sentOn;
				float gameplayTimeSeconds;
				float validatedGameplayTimeSeconds;
				float deltaTimePerFrameSeconds;
				unsigned int requiredCommandIdAtCurrentTime;

				int firstCommandReceived;
				int lastCommandReceived;


				MSGPACK_DEFINE(sentOn, gameplayTimeSeconds, validatedGameplayTimeSeconds, deltaTimePerFrameSeconds, requiredCommandIdAtCurrentTime, firstCommandReceived, lastCommandReceived)
			};

			struct CommandDto
			{
				int commandId = 0;
				float gameplayTimeSeconds = 0;
				std::vector<byte> content;

				MSGPACK_DEFINE(commandId, gameplayTimeSeconds, content)
			};

			struct PlayerCommandNode
			{
				PlayerCommandNode* previous = nullptr;
				PlayerCommandNode* next = nullptr;

				CommandDto command;
			};
			template<typename T, int TSamplesCount = 16>
			class Samples
			{
			public:
				T getAverage() const
				{
					return _value;
				}

				operator T() const
				{
					return getAverage();
				}
				void addValue(T value)
				{
					_samples[_offset] = value;
					_offset = (_offset + 1) % TSamplesCount;
					if (_nb < TSamplesCount)
					{
						_nb++;
					}

					_value = computeAverage();
				}

			private:
				T computeAverage()
				{
					T result = 0;

					for (int i = _offset; i < _nb + _offset; i++)
					{
						result += _samples[i % TSamplesCount];
					}
					return result / TSamplesCount;
				}

				T _value = 0;
				T _samples[TSamplesCount];
				int _offset = 0;
				int _nb = 0;

			};

			struct PlayerState
			{
				SessionId sessionId;
				int playerId = -1;
				Samples<unsigned int, 128> latency;

				bool isLocal = false;
				/// <summary>
				/// The gameplay time of the player when the frame was sent.
				/// </summary>
				float gameplayTimeSeconds = 0;
				float deltaTimePerFrameSeconds = 0;

				/// <summary>
				/// The minimum time for future commands
				/// </summary>
				float validatedGamePlayTimeSeconds = 0;


				float lastCommandTimeSeconds = 0;

				int64 receivedOn = 0;
				int64 sentOn = 0;
				/// <summary>
				/// The last command id the peer sent until the provided gameplay time.
				/// </summary>
				unsigned int requiredCommandIdAtCurrentTime = 0;

				PlayerCommandNode* _firstCommand = nullptr;
				PlayerCommandNode* _lastCommand = nullptr;
				PlayerCommandNode* _lastExecutedNode = nullptr;

				/// <summary>
				/// Did we already send commands to this peer.
				/// </summary>
				int lastSentCommand = 0;

				float synchronizedUntil() const
				{
					auto lastCommandId = _lastCommand != nullptr ? _lastCommand->command.commandId : 0;
					if (lastCommandId == requiredCommandIdAtCurrentTime)
					{
						return validatedGamePlayTimeSeconds;
					}
					else if (_lastCommand != nullptr)
					{
						return _lastCommand->command.gameplayTimeSeconds;
					}
					else
					{
						return 0;
					}


				}
				unsigned int lastExecutedCommandId() const
				{
					return _lastExecutedNode != nullptr ? _lastExecutedNode->command.commandId : 0;
				}



				void addCommand(const CommandDto& command)
				{
					if (lastCommandTimeSeconds < command.gameplayTimeSeconds)
					{
						lastCommandTimeSeconds = command.gameplayTimeSeconds;
					}
					if (_firstCommand == nullptr)
					{
						auto cmd = new PlayerCommandNode();
						cmd->command = command;
						_firstCommand = _lastCommand = cmd;
						return;
					}

					if (command.commandId < _firstCommand->command.commandId)
					{
						auto cmd = new PlayerCommandNode();
						cmd->command = command;
						cmd->next = _firstCommand;
						_firstCommand = cmd;
						return;
					}
					if (command.commandId > _lastCommand->command.commandId)
					{
						auto cmd = new PlayerCommandNode();
						cmd->command = command;
						cmd->previous = _lastCommand;
						_lastCommand->next = cmd;
						_lastCommand = cmd;
						return;
					}



				}
			};




			class LockstepService :public ILockstepService, public std::enable_shared_from_this<LockstepService>
			{
				friend LockstepPlugin;

			public:
				LockstepService(std::shared_ptr<P2PMeshService> mesh, std::shared_ptr<IClient> client, std::shared_ptr<Serializer> serializer, std::shared_ptr<ILogger> logger)
					:_mesh(mesh)
					, _client(client)
					, _logger(logger)
				{


				}



				~LockstepService()
				{
					if (_firstCommand != nullptr)
					{
						PlayerCommandNode* current = _firstCommand;
						while (current != nullptr)
						{
							auto next = current->next;
							delete current;
							current = next;
						}
						_firstCommand = nullptr;
						_lastCommand = nullptr;
						_lastExecutedCommand = nullptr;

						for (auto& kvp : _playerStates)
						{
							auto& state = kvp.second;
							current = state._firstCommand;
							while (current != nullptr)
							{
								auto next = current->next;
								delete current;
								current = next;
							}
							state._firstCommand = nullptr;
							state._lastCommand = nullptr;
							state._lastExecutedNode = nullptr;
						}
					}
				}


				std::vector<LockstepPlayer> getPlayers() const override
				{
					std::vector<LockstepPlayer> result;

					for (auto& kvp : _playerStates)
					{
						auto& state = kvp.second;

						LockstepPlayer player;
						player.localPlayer = state.isLocal;
						player.synchronizedUntilMs = state.synchronizedUntil();
						player.lastCommandId = state.requiredCommandIdAtCurrentTime;
						player.latencyMs = (int)state.latency;
						player.playerId = state.playerId;
						player.sessionId = state.sessionId;
						result.push_back(player);
					}

					return result;
				}


				int pushCommand(byte* buffer, int length) override
				{
					auto client = _client.lock();
					auto node = new PlayerCommandNode;
					node->command.commandId = _lastCommand != nullptr ? _lastCommand->command.commandId + 1 : 1;
					node->command.gameplayTimeSeconds = _currentGamePlayTimeSeconds + _options.DelaySeconds;
					node->command.content.resize(length);
					byte& pointer = node->command.content.front();
					memcpy(&pointer, buffer, length);

					if (_lastCommand != nullptr)
					{
						_lastCommand->next = node;
						node->previous = _lastCommand;
					}
					else
					{
						_firstCommand = node;
					}

					_lastCommand = node;


					synchronizeCommands();
					return node->command.commandId;

				}

				float getCurrentTime() const
				{
					return _currentGamePlayTimeSeconds;
				}

				float tick(float targetDeltaSeconds, float realDeltaSeconds)
				{
					_lastDeltaTimePerFrameSeconds = targetDeltaSeconds;
					_timeSinceLastGameplayProgress += targetDeltaSeconds;
					synchronizeState();

					float deltaSeconds;

					processPendingPlayersUpdateCommands();

					

					if (_isPaused)
					{
						deltaSeconds = 0;
					}
					else
					{
						deltaSeconds  = _options.FixedDeltaTimeSeconds;
					}
					if (_timeSinceLastGameplayProgress < deltaSeconds)
					{
						return 0;
					}
					else
					{
						_timeSinceLastGameplayProgress -= targetDeltaSeconds;
					}

					auto nextTime = _currentGamePlayTimeSeconds + deltaSeconds;
				

					Frame frame;
					frame.currentTimeSeconds = _currentGamePlayTimeSeconds;


					bool gameplayProgress = true;
					float rollbackTo = _currentGamePlayTimeSeconds;

					auto targetTime = getTargetTime();
					auto synchronizedUntil = this->synchronizedUntil();


					if (nextTime > synchronizedUntil || nextTime > targetTime)
					{

						gameplayProgress = false;
						deltaSeconds = 0;
						nextTime = _currentGamePlayTimeSeconds;
					}
					/*else
					{

						_logger->log(Stormancer::LogLevel::Info, "lockstep", std::to_string(gameplayProgress) + " " + std::to_string(_currentTime) + " " + std::to_string(nextTime) + " " + std::to_string(targetTime), "");
					}*/


					for (auto& kvp : _playerStates)
					{
						PlayerState& state = kvp.second;


						auto node = state._firstCommand;
						if (state._lastExecutedNode != nullptr)
						{
							node = state._lastExecutedNode->next;
						}
						while (node != nullptr && node->command.gameplayTimeSeconds < nextTime)
						{
							if (node->command.gameplayTimeSeconds < rollbackTo)
							{
								rollbackTo = node->command.gameplayTimeSeconds;
							}

							if (node->command.gameplayTimeSeconds >= _currentGamePlayTimeSeconds)
							{
								Command command;
								command.content = node->command.content;
								command.playerId = state.playerId;
								command.sessionId = state.sessionId;
								command.timeSeconds = node->command.gameplayTimeSeconds;
								frame.commands.push_back(command);
								state._lastExecutedNode = node;
							}

							node = node->next;

						}


					}
					auto node = _firstCommand;
					if (_lastExecutedCommand != nullptr)
					{
						node = _lastExecutedCommand->next;
					}

					while (node != nullptr && node->command.gameplayTimeSeconds < nextTime)
					{
						Command command;
						command.content = node->command.content;
						command.playerId = _currentPlayerId;
						command.timeSeconds = node->command.gameplayTimeSeconds;
						frame.commands.push_back(command);
						_lastExecutedCommand = node;
						node = node->next;
					}

					if (rollbackTo < _currentGamePlayTimeSeconds)
					{
						rollback(rollbackTo);
					}
					if ((gameplayProgress && deltaSeconds > 0) != _currentGameplayProgress)
					{
						_currentGameplayProgress = gameplayProgress && deltaSeconds > 0;
						onPauseStateChanged(!gameplayProgress);
					}

					onStep(frame);
					_currentGamePlayTimeSeconds = nextTime;
					
					
					return deltaSeconds;
				}

				bool isPaused() const
				{
					return _isPaused;
				}
				int lastExecutedCommand() const
				{
					return _lastExecutedCommand != nullptr ? _lastExecutedCommand->command.commandId : 0;
				}

				void pause(bool pause)
				{
					_isPaused = pause;
				}

				float getTargetTime() const
				{
					float result = 0;
					bool found = false;

					for (auto& kvp : _playerStates)
					{
						if (!kvp.second.isLocal)
						{
							auto& state = kvp.second;
							float time = getPlayerCurrentEstimatedGameplayTimeMs(state);
							if (time < result)
							{
								result = time;
								found = true;
							}
						}
					}
					if (!found)
					{
						result = _currentGamePlayTimeSeconds + _options.FixedDeltaTimeSeconds;
					}
					return result;
				}

			private:

				float getPlayerCurrentEstimatedGameplayTimeMs(const PlayerState& state) const
				{
					if (auto client = _client.lock())
					{
						return state.gameplayTimeSeconds + ((float)(client->clock() - state.sentOn))/1000;
					}
					else
					{
						return 0;
					}
				}



				float synchronizedUntil() const
				{
					float result = std::numeric_limits<float>::max();

					

					for (auto& kvp : _playerStates)
					{
						if (!kvp.second.isLocal)
						{
							float time = kvp.second.synchronizedUntil();
							if (time < result)
							{
								result = time;
							}
						}
					}
					return result;
				}

				void synchronizeCommands()
				{
					for (auto& kvp : _playerStates)
					{
						if (!kvp.second.isLocal)
						{
							synchronizeCommands(kvp.second);
						}
					}
				}

				void synchronizeCommands(PlayerState& state)
				{
					auto lastCmdId = _lastCommand != nullptr ? _lastCommand->command.commandId : 0;

					if (lastCmdId > state.lastSentCommand)
					{
						std::vector<CommandDto> commands;

						auto current = _firstCommand;

						while (current != nullptr && current->command.commandId <= state.lastSentCommand)
						{
							current = current->next;
						}

						while (current != nullptr)
						{
							commands.push_back(current->command);
							state.lastSentCommand = current->command.commandId;
							current = current->next;
						}
						auto serializer = _serializer;
						_mesh->send(state.sessionId, "lockstep.command", [commands, serializer](obytestream& stream)
							{
								serializer->serialize(stream, commands);
							}, PacketReliability::RELIABLE_ORDERED);

					}
				}

				void rollback(float time)
				{

				}
				void synchronizeState()
				{

					for (auto& playerState : _playerStates)
					{
						if (!playerState.second.isLocal)
						{
							sendStateToPlayer(playerState.second);
						}
					}
				}

				void sendStateToPlayer(const PlayerState& playerState)
				{
					FrameDto frame;
					frame.gameplayTimeSeconds = _currentGamePlayTimeSeconds;
					frame.deltaTimePerFrameSeconds = _lastDeltaTimePerFrameSeconds;
					frame.validatedGameplayTimeSeconds = _currentGamePlayTimeSeconds + _options.DelaySeconds;
					frame.sentOn = _client.lock()->clock();
					frame.requiredCommandIdAtCurrentTime = getUpdateIdForTime(frame.gameplayTimeSeconds);
					frame.firstCommandReceived = playerState._firstCommand != nullptr ? playerState._firstCommand->command.commandId : 0;
					frame.lastCommandReceived = playerState._lastCommand != nullptr ? playerState._lastCommand->command.commandId : 0;

					auto serializer = _serializer;
					_mesh->send(playerState.sessionId, "lockstep.frame", [frame, serializer](obytestream& stream)
						{
							serializer->serialize(stream, frame);
						}, PacketReliability::UNRELIABLE_SEQUENCED);
				}


				int getUpdateIdForTime(float gameplayTimeSeconds) const
				{
					auto current = this->_lastExecutedCommand;
					if (current == nullptr)
					{
						return 0;
					}
					int result = 0;
					while (current != nullptr && current->command.gameplayTimeSeconds < gameplayTimeSeconds)
					{
						result = current->command.commandId;
						current = current->next;
					}
					return result;
				}

				void initialize(std::shared_ptr<Scene> scene)
				{
					std::weak_ptr<LockstepService> wService = this->shared_from_this();
					auto wClient = this->_client;

					scene->addRoute("lockstepPlayers.installSnapshot", [wService](Packetisp_ptr packet)
						{
							auto cmd = packet->readObject<PlayersSnapshotInstallCommand>();
							auto service = wService.lock();
							if (service)
							{
								service->onPlayersInstallSnapshot(cmd);
							}
						});
					scene->addRoute("lockstepPlayers.update", [wService](Packetisp_ptr packet)
						{
							auto cmd = packet->readObject<PlayersUpdateCommand>();
							auto service = wService.lock();
							if (service)
							{
								service->onPlayersUpdate(cmd);
							}
						});

					Scene::RouteOptions p2pOptions;
					p2pOptions.filter = MessageOriginFilter::All;
					scene->addRoute("lockstep.frame", [wService, wClient](Packetisp_ptr packet)
						{
							byte buffer[16];
							packet->stream.read(buffer, 16);
							SessionId sessionId;
							SessionId::tryParse(buffer, 16, sessionId);

							auto args = packet->readObject<FrameDto>();
							auto service = wService.lock();
							auto client = wClient.lock();

							if (service)
							{


								auto& state = service->_playerStates[sessionId];
								state.receivedOn = client->clock();
								state.sentOn = args.sentOn;
								auto latency = (int)(state.receivedOn - args.sentOn);
								state.latency.addValue(latency > 0 ? latency : 0);
								if (args.gameplayTimeSeconds >= state.gameplayTimeSeconds)
								{
									state.deltaTimePerFrameSeconds = args.deltaTimePerFrameSeconds;
									state.validatedGamePlayTimeSeconds = args.validatedGameplayTimeSeconds;
									state.gameplayTimeSeconds = args.gameplayTimeSeconds;
									state.requiredCommandIdAtCurrentTime = args.requiredCommandIdAtCurrentTime;
								}
							}

						}, p2pOptions);


					scene->addRoute("lockstep.command", [wService](Packetisp_ptr packet)
						{
							byte buffer[16];
							packet->stream.read(buffer, 16);
							SessionId sessionId;
							SessionId::tryParse(buffer, 16, sessionId);

							auto commands = packet->readObject < std::vector<CommandDto>>();
							auto service = wService.lock();
							if (service)
							{
								auto& state = service->_playerStates[sessionId];
								for (auto& command : commands)
								{
									state.addCommand(command);
								}
							}
						}, p2pOptions);

				}



				void onPlayersInstallSnapshot(PlayersSnapshotInstallCommand& cmd)
				{
					_currentPlayerId = cmd.currentPlayerId;
					_playerStates.clear();
					for (auto& p : cmd.players)
					{
						PlayerState state;
						state.playerId = p.first;
						state.sessionId = p.second;
						_playerStates[p.second] = state;
					}
					_currentPlayersUpdateId = cmd.updateId;

				}

				void onPlayersUpdate(PlayersUpdateCommand& cmd)
				{
					_pendingPlayersUpdateCommand.push_back(cmd);

				}

				void processPendingPlayersUpdateCommands()
				{
					int j = -1;
					bool modified = false;
					for (int i = 0; i < _pendingPlayersUpdateCommand.size(); i++)
					{
						auto& cmd = _pendingPlayersUpdateCommand[i];
						if (cmd.updateId == _currentPlayersUpdateId + 1)
						{
							modified = true;
							applyPlayersUpdateCommand(cmd);
							j = i;
							break;
						}
					}
					if (j != -1)
					{
						_pendingPlayersUpdateCommand.erase(_pendingPlayersUpdateCommand.begin() + j);
					}

					if (modified)
					{
						onPlayerListChanged();
					}

				}

				void applyPlayersUpdateCommand(PlayersUpdateCommand& cmd)
				{
					auto client = _client.lock();
					if (client)
					{
						switch (cmd.commandType)
						{
						case PlayersUpdateCommandType::Add:
						{
							PlayerState state;
							state.playerId = cmd.playerId;
							state.sessionId = cmd.playerSessionId;
							state.isLocal = (state.sessionId == SessionId::parse(client->sessionId()));
							_playerStates[cmd.playerSessionId] = state;

							synchronizeCommands(state);
							break;
						}
						case PlayersUpdateCommandType::Remove:
						{
							_playerStates.erase(cmd.playerSessionId);
							break;
						}
						}
						_currentPlayersUpdateId = cmd.updateId;
					}
				}


			private:
				float _timeSinceLastGameplayProgress = 0;
				float _lastDeltaTimePerFrameSeconds = 0;
				bool _isPaused = true;
				bool _currentGameplayProgress = false;

				float _currentGamePlayTimeSeconds = 0;
				float _currentRealTimeSeconds = 0;
				int _currentPlayersUpdateId = 0;
				int _currentPlayerId = 0;

				PlayerCommandNode* _firstCommand = nullptr;
				PlayerCommandNode* _lastCommand = nullptr;
				PlayerCommandNode* _lastExecutedCommand = nullptr;


				LockstepOptions _options;

				std::vector<PlayersUpdateCommand> _pendingPlayersUpdateCommand;

				std::unordered_map<SessionId, PlayerState> _playerStates;

				std::shared_ptr<P2PMeshService> _mesh;
				std::weak_ptr<IClient>  _client;
				std::shared_ptr<Serializer> _serializer;
				std::shared_ptr<ILogger> _logger;

			};
		}

		static constexpr const char* PLUGIN_NAME = "Lockstep";
		static constexpr const char* PLUGIN_VERSION = "1.0.0";
		static constexpr const char* LOCKSTEP_HOST_METADATA = "stormancer.lockstep";

		float LockstepApi::tick(float deltaSeconds, float realDeltaSeconds)
		{
			return _service->tick(deltaSeconds,realDeltaSeconds);
		}

		/// <summary>
		/// Gets the current lockstep time, in ms.
		/// </summary>
		/// <returns></returns>
		float LockstepApi::getCurrentTime() const
		{
			return _service->getCurrentTime();
		}
		float LockstepApi::getTargetTime() const
		{
			return _service->getTargetTime();
		}

		int LockstepApi::lastExecutedCommand() const
		{
			return _service->lastExecutedCommand();
		}

		bool LockstepApi::isEnabled() const
		{
			return _service != nullptr;
		}

		bool LockstepApi::isPaused() const
		{
			return _service->isPaused();
		}

		void LockstepApi::pause(bool pause)
		{
			return _service->pause(pause);
		}

		int LockstepApi::pushCommand(byte* buffer, int length)
		{
			return _service->pushCommand(buffer, length);
		}

		void LockstepApi::onSceneConnected(std::shared_ptr<details::ILockstepService> service)
		{
			_service = service;
			_onStepSubscription = service->onStep.subscribe([this](Frame& frame)
				{
					this->onStep(frame);
				});
			_onPauseStateChangedSubscription = service->onPauseStateChanged.subscribe([this](bool paused) {
				this->onPauseStateChanged(paused);

				});
			_onPlayerListChangedSubscription = service->onPlayerListChanged.subscribe([this]() {
				this->onPlayerListChanged();

				});
		}
		void LockstepApi::onSceneDisconnected()
		{
			_onStepSubscription = nullptr;
			_service.reset();
		}

		std::vector<LockstepPlayer> LockstepApi::getPlayers() const
		{
			return _service->getPlayers();
		}
		PluginDescription LockstepPlugin::getDescription()
		{
			return PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
		}

		void LockstepPlugin::registerClientDependencies(ContainerBuilder& clientBuilder)
		{
			clientBuilder.registerDependency<LockstepApi>().singleInstance();
		}

		void LockstepPlugin::registerSceneDependencies(ContainerBuilder& sceneBuilder, std::shared_ptr<Scene> scene)
		{
			if (!scene->getHostMetadata(LOCKSTEP_HOST_METADATA).empty())
			{
				sceneBuilder.registerDependency < details::LockstepService, P2PMeshService, IClient, Serializer, ILogger >().singleInstance();
			}
		}

		void LockstepPlugin::sceneCreated(std::shared_ptr<Scene> scene)
		{
			if (!scene->getHostMetadata(LOCKSTEP_HOST_METADATA).empty())
			{
				auto api = scene->dependencyResolver().resolve<LockstepApi>();
				auto service = scene->dependencyResolver().resolve<details::LockstepService>();
				service->initialize(scene);
				api->onSceneConnected(service);
			}
		}
		void LockstepPlugin::sceneDisconnecting(std::shared_ptr<Scene> scene)
		{
			if (!scene->getHostMetadata(LOCKSTEP_HOST_METADATA).empty())
			{
				auto api = scene->dependencyResolver().resolve<LockstepApi>();
				api->onSceneDisconnected();
			}
		}

	}
}

MSGPACK_ADD_ENUM(Stormancer::Gameplay::details::PlayersUpdateCommandType)
#endif