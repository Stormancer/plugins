#pragma once
#include "stormancer/IPlugin.h"
#include "stormancer/Event.h"
#include "stormancer/SessionId.h"
#include <stdio.h>

#if !defined(STORM_PLUGIN_IMPL)
#define STORM_PLUGIN_IMPL 0
#endif

namespace Stormancer
{
	namespace Gameplay
	{
		struct LockstepOptions
		{
			/// <summary>
			/// Delay in gameplay time between a command is pushed to the API and executed.
			/// </summary>
			unsigned int delayMs = 100;

			/// <summary>
			/// the Minimum time in ms in the future the clients must be synchronized for the gameplay to progress.
			/// </summary>
			unsigned int minimumTimeWindowMs = 16;
		};
		struct Command
		{
			int playerId;
			::std::vector<byte> content;

			unsigned int timeMs;
		};
		struct LockstepPlayer
		{
			SessionId sessionId;
			int playerId;
			unsigned int latencyMs;

			bool localPlayer;

			unsigned int synchronizedUntilMs;
			int lastCommandId;
		};


		struct Frame
		{
			unsigned int currentTime;

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
				virtual bool tick(unsigned int deltaTimeMs) = 0;
				virtual unsigned int getCurrentTime() = 0;
				virtual unsigned int getTargetTime() = 0;
				virtual int lastExecutedCommand() = 0;
				virtual bool isPaused() = 0;
				virtual void pause(bool pause) = 0;

				virtual std::vector<LockstepPlayer> getPlayers() = 0;

				Stormancer::Event<Frame&> onStep;
			};
		}
		class LockstepApi
		{
			friend LockstepPlugin;
		public:
			bool tick(unsigned int deltaTimeMs);

			unsigned int getCurrentTime();
			unsigned int getTargetTime();
			int lastExecutedCommand();

			bool isEnabled();

			/// <summary>
			/// Pushes a command to the system. if frame is not specified, 
			/// </summary>
			/// <param name="buffer"></param>
			/// <param name="length"></param>
			/// <param name="frame"></param>
			int pushCommand(byte* buffer, int length);

			Event<Frame&> onStep;
			Event<RollbackContext&> onRollback;

			bool isPaused();

			void pause(bool pause);

			std::vector<LockstepPlayer> getPlayers();



		private:
			void onSceneConnected(std::shared_ptr<details::ILockstepService> service);
			void onSceneDisconnected();

			std::shared_ptr<details::ILockstepService> _service;

			Subscription _onStepSubscription;
		};



	}
}


#if STORM_PLUGIN_IMPL == 1

#include "stormancer/Scene.h"
#include "stormancer/RPC/RpcService.h"
#include "stormancer/IClient.h"
#include "P2PMesh.hpp"


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
				unsigned int gameplayTimeMs;
				unsigned int validatedGameplayTimeMs;
				unsigned int requiredCommandIdForTime;

				int firstCommandReceived;
				int lastCommandReceived;


				MSGPACK_DEFINE(sentOn, gameplayTimeMs, validatedGameplayTimeMs, requiredCommandIdForTime, firstCommandReceived, lastCommandReceived);
			};

			struct CommandDto
			{
				int commandId = 0;
				unsigned int gameplayTimeMs = 0;
				std::vector<byte> content;

				MSGPACK_DEFINE(commandId, gameplayTimeMs, content)
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
				unsigned int gameplayTimeMs = 0;

				/// <summary>
				/// Minimum time of futur commands.
				/// </summary>
				unsigned int validatedGamePlayTimeMs = 0;
				unsigned int lastCommandTime = 0;

				int64 receivedOn = 0;
				int64 sentOn = 0;
				/// <summary>
				/// The last command id the peer sent until the provided gameplay time.
				/// </summary>
				unsigned int requiredCommandIdForTime = 0;

				PlayerCommandNode* _firstCommand = nullptr;
				PlayerCommandNode* _lastCommand = nullptr;
				PlayerCommandNode* _lastExecutedNode = nullptr;

				/// <summary>
				/// Did we already send commands to this peer.
				/// </summary>
				int lastSentCommand = 0;

				unsigned int synchronizedUntil()
				{
					auto lastCommandId = _lastCommand != nullptr ? _lastCommand->command.commandId : 0;
					if (lastCommandId == requiredCommandIdForTime)
					{
						return validatedGamePlayTimeMs;
					}
					else if (_lastCommand != nullptr)
					{
						return _lastCommand->command.gameplayTimeMs;
					}
					else
					{
						return 0;
					}


				}
				unsigned int lastExecutedCommandId()
				{
					return _lastExecutedNode != nullptr ? _lastExecutedNode->command.commandId : 0;
				}



				void addCommand(const CommandDto& command)
				{
					if (lastCommandTime < command.gameplayTimeMs)
					{
						lastCommandTime = command.gameplayTimeMs;
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


				std::vector<LockstepPlayer> getPlayers() override
				{
					std::vector<LockstepPlayer> result;

					for (auto& kvp : _playerStates)
					{
						auto& state = kvp.second;

						LockstepPlayer player;
						player.localPlayer = state.isLocal;
						player.synchronizedUntilMs = state.synchronizedUntil();
						player.lastCommandId = state.requiredCommandIdForTime;
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
					node->command.gameplayTimeMs = _currentTime + _options.delayMs;
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

				unsigned int getCurrentTime()
				{
					return _currentTime;
				}

				bool tick(unsigned int deltaMs)
				{
					processPendingPlayersUpdateCommands();
					if (_isPaused)
					{
						deltaMs = 0;
					}


					auto nextTime = _currentTime + deltaMs;

					Frame frame;
					frame.currentTime = _currentTime;


					bool gameplayProgress = true;
					unsigned int rollbackTo = _currentTime;

					auto targetTime = getTargetTime();
					auto synchronizedUntil = this->synchronizedUntil();
					if (nextTime >= targetTime || nextTime > synchronizedUntil)
					{

						gameplayProgress = false;
					}
					else
					{

						_logger->log(Stormancer::LogLevel::Info, "lockstep", std::to_string(gameplayProgress) + " " + std::to_string(_currentTime) + " " + std::to_string(nextTime) + " " + std::to_string(targetTime), "");
					}


					for (auto& kvp : _playerStates)
					{
						PlayerState& state = kvp.second;


						auto node = state._firstCommand;
						if (state._lastExecutedNode != nullptr)
						{
							node = state._lastExecutedNode->next;
						}
						while (node != nullptr && node->command.gameplayTimeMs < nextTime)
						{
							if (node->command.gameplayTimeMs < rollbackTo)
							{
								rollbackTo = node->command.gameplayTimeMs;
							}

							if (node->command.gameplayTimeMs >= _currentTime)
							{
								Command command;
								command.content = node->command.content;
								command.playerId = state.playerId;
								command.timeMs = node->command.gameplayTimeMs;
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

					while (node != nullptr && node->command.gameplayTimeMs < nextTime)
					{
						Command command;
						command.content = node->command.content;
						command.playerId = _currentPlayerId;
						command.timeMs = node->command.gameplayTimeMs;
						frame.commands.push_back(command);
						_lastExecutedCommand = node;
						node = node->next;
					}

					if (rollbackTo < _currentTime)
					{
						rollback(rollbackTo);
					}


					onStep(frame);
					_currentTime = nextTime;
					synchronizeState();

					return gameplayProgress;
				}

				bool isPaused()
				{
					return _isPaused;
				}
				int lastExecutedCommand()
				{
					return _lastExecutedCommand != nullptr ? _lastExecutedCommand->command.commandId : 0;
				}

				void pause(bool pause)
				{
					_isPaused = pause;
				}

				unsigned int getTargetTime()
				{
					unsigned int result = 0xFFFFFFFF;


					for (auto& kvp : _playerStates)
					{
						if (!kvp.second.isLocal)
						{
							unsigned int time = getPlayerCurrentEstimatedGameplayTimeMs(kvp.second);
							if (time < result)
							{
								result = time;
							}
						}
					}
					if (result == 0xFFFFFFFF)
					{
						result = _currentTime + _options.minimumTimeWindowMs;
					}
					return result;
				}

			private:

				unsigned int getPlayerCurrentEstimatedGameplayTimeMs(PlayerState& state)
				{
					if (auto client = _client.lock())
					{
						return state.gameplayTimeMs + (int)(client->clock() - state.sentOn);
					}
					else
					{
						return 0;
					}
				}



				unsigned int synchronizedUntil()
				{
					unsigned int result = 0xFFFFFFFF;

					for (auto& kvp : _playerStates)
					{
						if (!kvp.second.isLocal)
						{
							unsigned int time = kvp.second.synchronizedUntil();
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

				void rollback(int time)
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
					frame.gameplayTimeMs = _currentTime;
					frame.validatedGameplayTimeMs = _currentTime + _options.delayMs;
					frame.sentOn = _client.lock()->clock();
					frame.requiredCommandIdForTime = getUpdateIdForTime(frame.gameplayTimeMs);
					frame.firstCommandReceived = playerState._firstCommand != nullptr ? playerState._firstCommand->command.commandId : 0;
					frame.lastCommandReceived = playerState._lastCommand != nullptr ? playerState._lastCommand->command.commandId : 0;

					auto serializer = _serializer;
					_mesh->send(playerState.sessionId, "lockstep.frame", [frame, serializer](obytestream& stream)
						{
							serializer->serialize(stream, frame);
						}, PacketReliability::UNRELIABLE_SEQUENCED);
				}


				int getUpdateIdForTime(unsigned int gameplayTime)
				{
					auto current = this->_lastExecutedCommand;
					if (current == nullptr)
					{
						return 0;
					}
					int result = 0;
					while (current != nullptr && current->command.gameplayTimeMs < gameplayTime)
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
								if (args.gameplayTimeMs >= state.gameplayTimeMs)
								{

									state.validatedGamePlayTimeMs = args.validatedGameplayTimeMs;
									state.gameplayTimeMs = args.gameplayTimeMs;
									state.requiredCommandIdForTime = args.requiredCommandIdForTime;
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
					for (int i = 0; i < _pendingPlayersUpdateCommand.size(); i++)
					{
						auto& cmd = _pendingPlayersUpdateCommand[i];
						if (cmd.updateId == _currentPlayersUpdateId + 1)
						{
							applyPlayersUpdateCommand(cmd);
							j = i;
							break;
						}
					}
					if (j != -1)
					{
						_pendingPlayersUpdateCommand.erase(_pendingPlayersUpdateCommand.begin() + j);
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
				bool _isPaused = true;
				unsigned int _currentTime = 0;
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

		bool LockstepApi::tick(unsigned int deltaMs)
		{
			return _service->tick(deltaMs);
		}

		/// <summary>
		/// Gets the current lockstep time, in ms.
		/// </summary>
		/// <returns></returns>
		unsigned int LockstepApi::getCurrentTime()
		{
			return _service->getCurrentTime();
		}
		unsigned int LockstepApi::getTargetTime()
		{
			return _service->getTargetTime();
		}

		int LockstepApi::lastExecutedCommand()
		{
			return _service->lastExecutedCommand();
		}

		bool LockstepApi::isEnabled()
		{
			return _service != nullptr;
		}

		bool LockstepApi::isPaused()
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
		}
		void LockstepApi::onSceneDisconnected()
		{
			_onStepSubscription = nullptr;
			_service.reset();
		}

		std::vector<LockstepPlayer> LockstepApi::getPlayers()
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