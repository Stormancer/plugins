#pragma once
#include "stormancer/IPlugin.h"
#include "stormancer/Event.h"

#if !defined(STRM_PLUGIN_IMPL)
#define STRM_PLUGIN_IMPL 1
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
			int delayMs = 200;

			/// <summary>
			/// the Minimum time in ms in the future the clients must be synchronized for the gameplay to progress.
			/// </summary>
			int minimumTimeWindowMs = 16;
		};
		struct Command
		{
			int playerId;
			::std::vector<byte> content;

			int timeMs;
		};
		struct Frame
		{
			int currentTime;

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
			void sceneConnecting(::std::shared_ptr<Scene> scene) override;
			void sceneDisconnecting(::std::shared_ptr<Scene> scene) override;
		};
		namespace details
		{
			class ILockstepService
			{
			public:
				virtual void pushCommand(byte* buffer, int length) = 0;
				virtual bool tick(int deltaTimeMs) = 0;
				virtual int getCurrentTime() = 0;

				virtual bool isPaused() = 0;
				virtual void pause(bool pause) = 0;
			};
		}
		class LockstepApi
		{
			friend LockstepPlugin;
		public:
			bool tick(int deltaTimeMs);

			int getCurrentTime();

			bool isEnabled();

			/// <summary>
			/// Pushes a command to the system. if frame is not specified, 
			/// </summary>
			/// <param name="buffer"></param>
			/// <param name="length"></param>
			/// <param name="frame"></param>
			void pushCommand(byte* buffer, int length);

			Event<Frame> onStep;
			Event<RollbackContext&> onRollback;

			bool isPaused();

			void pause(bool pause);

		private:
			void onSceneConnected(std::shared_ptr<details::ILockstepService> service);
			void onSceneDisconnected();

			std::shared_ptr<details::ILockstepService> _service;
		};



	}
}


#if STRM_PLUGIN_IMPL == 1

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
				int gameplayTimeMs;
				int requiredCommandIdForTime;

				int firstCommandReceived;
				int lastCommandReceived;


				MSGPACK_DEFINE(sentOn, gameplayTimeMs, requiredCommandIdForTime, firstCommandReceived, lastCommandReceived);
			};

			struct CommandDto
			{
				int commandId;
				int gameplayTimeMs;
				std::vector<byte> content;

				MSGPACK_DEFINE(commandId, gameplayTimeMs, content)
			};

			struct PlayerCommandNode
			{
				PlayerCommandNode* previous;
				PlayerCommandNode* next;

				CommandDto command;
			};


			struct PlayerState
			{
				SessionId sessionId;
				int playerId = -1;
				int64 latency;

				/// <summary>
				/// The next gameplay time.
				/// </summary>
				int gameplayTimeMs = 0;

				/// <summary>
				/// The last command id the peer sent until the provided gameplay time.
				/// </summary>
				int requiredCommandIdForTime = 0;

				PlayerCommandNode* _firstCommand = nullptr;
				PlayerCommandNode* _lastCommand = nullptr;
				PlayerCommandNode* _lastExecutedNode = nullptr;

				/// <summary>
				/// Did we already send commands to this peer.
				/// </summary>
				int lastSentCommand = 0;

				int synchronizedUntil()
				{
					if (_firstCommand != nullptr && _firstCommand->command.commandId != 1)
					{
						return 0;
					}
					auto lastReceivedCmdTime = _lastCommand != nullptr ? _lastCommand->command.gameplayTimeMs : 0;
					if (requiredCommandIdForTime <= (_lastCommand != nullptr ? _lastCommand->command.commandId : 0))
					{
						return lastReceivedCmdTime;
					}
					return 0;
				}
				int lastExecutedCommandId()
				{
					return _lastExecutedNode != nullptr ? _lastExecutedNode->command.commandId : 0;
				}
				void addCommand(const CommandDto& command)
				{
					if (_firstCommand == nullptr)
					{
						auto cmd = new PlayerCommandNode();
						cmd->command = command;
						_firstCommand = _lastCommand =cmd;
						return;
					}

					if (command.commandId < _firstCommand->command.commandId)
					{
						auto cmd = new PlayerCommandNode();
						cmd->command = command;
						cmd->next = _firstCommand;
						_firstCommand  = cmd;
						return;
					}
					if (command.commandId > _lastCommand->command.commandId)
					{
						auto cmd = new PlayerCommandNode();
						cmd->command = command;
						cmd->previous = _lastCommand;
						_lastCommand = cmd;
						return;
					}
					


				}
			};

			


			class LockstepService :public ILockstepService, public std::enable_shared_from_this<LockstepService>
			{
				friend LockstepPlugin;

			public:
				LockstepService(std::shared_ptr<P2PMeshService> mesh, std::shared_ptr<IClient> client, std::shared_ptr<Serializer> serializer)
					:_mesh(mesh)
					, _client(client)
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
				Event<Frame> onStep;

				void pushCommand(byte* buffer, int length)
				{
					auto client = _client.lock();
					auto node = new PlayerCommandNode;
					node->command.commandId = _firstCommand != nullptr ? _firstCommand->command.commandId + 1 : 1;
					node->command.gameplayTimeMs = _currentTime + _options.delayMs;
					node->command.content.resize(length);
					byte& pointer = node->command.content.front();
					memcpy(&pointer, buffer, length);
					if (_firstCommand == nullptr)
					{
						_firstCommand = node;

					}
					_lastCommand = node;


					synchronizeCommands();
					
				}

				int getCurrentTime()
				{
					return _currentTime;
				}

				bool tick(int deltaMs)
				{
					if (_isPaused)
					{
						return false;
					}
					processPendingPlayersUpdateCommands();

					auto nextTime = _currentTime + deltaMs;

					Frame frame;
					frame.currentTime = _currentTime;


					bool gameplayProgress = true;
					int rollbackTo = _currentTime;
					for (auto& kvp : _playerStates)
					{
						PlayerState& state = kvp.second;
						if (state.synchronizedUntil() < _currentTime + _options.minimumTimeWindowMs)
						{
							gameplayProgress = false;
							break;
						}
						
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

					if (rollbackTo <= _currentTime)
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

				void pause(bool pause)
				{
					_isPaused = pause;
				}

			private:

				void synchronizeCommands()
				{
					for (auto& kvp : _playerStates)
					{
						synchronizeCommands(kvp.second);
					}
				}

				void synchronizeCommands(PlayerState& state)
				{
					auto lastCmdId = _lastCommand != nullptr ? _lastCommand->command.commandId : 0;

					if (lastCmdId > state.lastSentCommand)
					{
						std::vector<CommandDto> commands;

						auto& first = _firstCommand;

						while (first->command.commandId <= state.lastSentCommand)
						{
							first = first->next;
						}

						while (first != nullptr)
						{
							commands.push_back(first->command);
							first = first->next;
						}

					}
				}

				void rollback(int time)
				{

				}
				void synchronizeState()
				{

					for (auto& playerState : _playerStates)
					{
						sendStateToPlayer(playerState.second);
					}
				}

				void sendStateToPlayer(const PlayerState& playerState)
				{
					FrameDto frame;
					frame.gameplayTimeMs = _currentTime + _options.delayMs;
					frame.sentOn = _client.lock()->clock();
					frame.requiredCommandIdForTime = getUpdateIdForTime(frame.gameplayTimeMs);
					frame.firstCommandReceived = playerState._firstCommand != nullptr ? playerState._firstCommand->command.commandId : 0;
					frame.lastCommandReceived = playerState._lastCommand != nullptr ? playerState._lastCommand->command.commandId : 0;

					auto serializer = _serializer;
					_mesh->send(playerState.sessionId, "", [frame, serializer](obytestream& stream)
						{
							serializer->serialize(stream, frame);
						}, PacketReliability::UNRELIABLE_SEQUENCED);
				}


				int getUpdateIdForTime(int gameplayTime)
				{
					auto current = this->_lastExecutedCommand;
					if (current == nullptr)
					{
						return 0;
					}
					int result;
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
					p2pOptions.filter = MessageOriginFilter::Peer;
					scene->addRoute("lockstep.frame", [wService, wClient](Packetisp_ptr packet)
						{
							auto args = packet->readObject<FrameDto>();
							auto service = wService.lock();
							auto client = wClient.lock();

							if (service)
							{

								auto sessionId = SessionId::parse(packet->connection->id());
								auto& state = service->_playerStates[sessionId];
								if (args.gameplayTimeMs > state.gameplayTimeMs)
								{

									state.latency = client->clock() - args.sentOn;
									state.gameplayTimeMs = args.gameplayTimeMs;
									state.requiredCommandIdForTime = args.requiredCommandIdForTime;
								}
							}

						}, p2pOptions);


					scene->addRoute("lockstep.command", [wService](Packetisp_ptr packet)
						{
							auto commands = packet->readObject < std::vector<CommandDto>>();
							auto service = wService.lock();
							if (service)
							{
								auto sessionId = SessionId::parse(packet->connection->id());
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
					while (_pendingPlayersUpdateCommand.size() > 0)
					{
						for (auto& cmd : _pendingPlayersUpdateCommand)
						{
							if (cmd.updateId == _currentPlayersUpdateId + 1)
							{
								applyPlayersUpdateCommand(cmd);
							}
						}
						int currentId = _currentPlayerId;
						auto it = std::remove_if(_pendingPlayersUpdateCommand.begin(), _pendingPlayersUpdateCommand.end(), [currentId](PlayersUpdateCommand& cmd) {return cmd.updateId <= currentId; });
						_pendingPlayersUpdateCommand.erase(it, _pendingPlayersUpdateCommand.end());
					}
				}

				void applyPlayersUpdateCommand(PlayersUpdateCommand& cmd)
				{
					switch (cmd.commandType)
					{
					case PlayersUpdateCommandType::Add:
					{
						PlayerState state;
						state.playerId = cmd.playerId;
						state.sessionId = cmd.playerSessionId;
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
				}


			private:
				bool _isPaused = true;
				int _currentTime = 0;
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


			};
		}

		static constexpr const char* PLUGIN_NAME = "Lockstep";
		static constexpr const char* PLUGIN_VERSION = "1.0.0";
		static constexpr const char* LOCKSTEP_HOST_METADATA = "stormancer.lockstep";

		bool LockstepApi::tick(int deltaMs)
		{
			return _service->tick(deltaMs);
		}

		/// <summary>
		/// Gets the current lockstep time, in ms.
		/// </summary>
		/// <returns></returns>
		int LockstepApi::getCurrentTime()
		{
			return _service->getCurrentTime();
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

		void LockstepApi::pushCommand(byte* buffer, int length)
		{
			return _service->pushCommand(buffer, length);
		}

		void LockstepApi::onSceneConnected(std::shared_ptr<details::ILockstepService> service)
		{
			_service = service;
		}
		void LockstepApi::onSceneDisconnected()
		{
			_service.reset();
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
				sceneBuilder.registerDependency < details::LockstepService, P2PMeshService, IClient,Serializer >().singleInstance();
			}
		}

		void LockstepPlugin::sceneConnecting(std::shared_ptr<Scene> scene)
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