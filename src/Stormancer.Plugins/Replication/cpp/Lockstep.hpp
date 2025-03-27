// lockstep client library for Stormancer
// Copyright (C) 2025 Stormancer
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE 
// SOFTWARE.

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
		using Time = double;
		constexpr Time TimeMaxValue = std::numeric_limits<double>::max();
		using FrameDuration = float;
		struct LockstepOptions
		{
			/// <summary>
			/// Delay in gameplay time between a command is pushed to the API and executed.
			/// </summary>
			FrameDuration MinDelaySeconds = 0.1f;
			FrameDuration MaxDelaySeconds = 0.6f;
			FrameDuration FixedDeltaTimeSeconds = 1.f / 30.f;
			FrameDuration DelayMarginSeconds = 0.066666f;

			/// <summary>
			/// How much time the system needs to wait between pauses when needing to adjust the synchronized time between clients when preventing slowly going out of sync.
			/// </summary>
			FrameDuration MinPauseDelayOnSlowAdjust = 1.0f;

		};
		enum class PauseState
		{
			Running,
			Waiting,
			Paused
		};
		struct Command
		{
			/// <summary>
			/// Id of the command for the player.
			/// </summary>
			int commandId;

			/// <summary>
			/// Id of the player who created the command.
			/// </summary>
			int playerId;
			SessionId sessionId;
			::std::vector<byte> content;

			Time timeSeconds;
		};

		struct LockstepPlayer
		{
			SessionId sessionId;
			int playerId;
			unsigned int latencyMs;

			bool localPlayer;

			Time synchronizedUntilMs;
			int lastCommandId;
			Time targetDeltaTimeSeconds;
		};


		struct Frame
		{
			Time currentTimeSeconds = 0;
			Time validatedTimeSeconds = 0;

			/// <summary>
			/// Commands performed during this frame
			/// </summary>
			::std::vector<Command> commands;

			::std::vector<byte> consistencyData;

		};

		struct Snapshot
		{
			Time gameplayTimeSeconds = 0;
			std::vector<byte> content;
		};

		struct RollbackContext
		{
			int targetFrame;
			int restoredFrame;
		};


		struct ReplayWriteEvent
		{

			std::vector<byte> data;
			bool isHeader = false;
			int playerId;
			std::string gameId;
		};

		enum class ReplayMode
		{
			Recording,
			Playing
		};

		struct ConsistencyCheckEvent
		{
			Time gameplayTime;
			std::unordered_map<int, std::vector<byte>> consistencyData;
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


				virtual FrameDuration adjustTick(FrameDuration deltaSeconds, FrameDuration realDeltaSeconds) = 0;
				virtual void tick(FrameDuration deltaSeconds, FrameDuration realDeltaSeconds) = 0;

				virtual void endFrame() = 0;
				virtual Time getCurrentTime() const = 0;
				virtual Time getTargetTime() const = 0;
				virtual Time getCommandTime() const = 0;
				virtual FrameDuration getLatency() const = 0;
				virtual int lastExecutedCommand() const = 0;
				virtual bool isPaused() const = 0;
				virtual void pause(bool pause) = 0;

				virtual std::vector<LockstepPlayer> getPlayers() const = 0;

				virtual int getCurrentPlayerId() const = 0;

				virtual ReplayMode getReplayMode() = 0;
				virtual bool trySetReplayInitialData(byte* buffer, size_t length, std::string& buildId) = 0;
				virtual bool tryGetReplayInitialData(std::vector<byte>& initialData, std::string& buildId, std::string& gameId) = 0;

				virtual void initialize() = 0;

				Stormancer::Event<Frame&> onStep;
				Stormancer::Event<Frame&> onEndFrame;

				Event<PauseState> onPauseStateChanged;
				Event<ConsistencyCheckEvent> onConsistencyCheck;
				Event<> onPlayerListChanged;
				Event<Snapshot&>  onCreateSnapshot;
				Event<Snapshot&> onInstallSnapshot;
				Event<> onStart;

				std::function<void(ReplayWriteEvent&)> replayWriter;

				virtual ~ILockstepService() {};
			};
		}

		class LockstepApi
		{
			friend LockstepPlugin;
		public:
			virtual FrameDuration adjustTick(FrameDuration deltaSeconds, FrameDuration realDeltaSeconds) = 0;

			virtual void tick(FrameDuration deltaSeconds, FrameDuration realDeltaSeconds) = 0;

			virtual Time getCurrentTime() const = 0;

			virtual Time getTargetTime() const = 0;

			virtual int lastExecutedCommand() const = 0;

			virtual Time getCommandTime() const = 0;

			virtual FrameDuration getLatency() const = 0;

			virtual bool isEnabled() const = 0;

			/// <summary>
			/// Pushes a command to the system. if frame is not specified, 
			/// </summary>
			/// <param name="buffer"></param>
			/// <param name="length"></param>
			/// <param name="frame"></param>
			virtual int pushCommand(byte* buffer, int length) = 0;

			virtual void loadReplayFile(byte* buffer, size_t length) = 0;

			virtual void endFrame() = 0;


			virtual bool isPaused() const = 0;

			virtual void pause(bool pause) = 0;

			virtual std::vector<LockstepPlayer> getPlayers() const = 0;

			virtual int getCurrentPlayerId() const = 0;

			virtual void setReplayWriter(std::function<void(ReplayWriteEvent&)> replayWriter) = 0;

			virtual ReplayMode getReplayMode() = 0;

			virtual bool trySetReplayInitialData(byte* buffer, size_t length, std::string& buildId) = 0;
			virtual bool tryGetReplayInitialData(std::vector<byte>& initialData, std::string& buildId, std::string& gameId) = 0;

			virtual pplx::task<bool> uploadPendingReplay(std::string pendingReplayFilePath) = 0;

			/// <summary>
			/// Resets the lockstep system
			/// </summary>
			/// <remarks>
			/// Online automatically resets when players join a new game session, but in offline mode, reset must be called manually. 
			/// </remarks>
			virtual void Reset() = 0;

			virtual ~LockstepApi() {};


			Event<Frame&> onStep;
			Event<Frame&> onEndFrame;

			Event<RollbackContext&> onRollback;

			Event<PauseState> onPauseStateChanged;
			Event<> onPlayerListChanged;
			Event<ConsistencyCheckEvent> onConsistencyCheck;
			Event<Snapshot&> onCreateSnapshot;
			Event<Snapshot&> onInstallSnapshot;
			Event<> onStart;




		};



	}
}
#endif
#if STORM_PLUGIN_IMPL


#undef STORM_PLUGIN_IMPL
#include "gamesession/P2PMesh.hpp"
#undef STORM_PLUGIN_IMPL
#define STORM_PLUGIN_IMPL 1

#include "stormancer/Scene.h"
#include "stormancer/RPC/RpcService.h"
#include "stormancer/IClient.h"
#include "Users/ClientAPI.hpp"

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

			namespace Replays
			{

				struct FileHeader
				{
					int version = 2;

					std::string buildId;

					int playerId;

					std::string gameId;
					std::vector<byte> initializationData;
					MSGPACK_DEFINE(version, buildId, playerId, gameId, initializationData)
				};
				struct RecordHeader
				{
					byte type = 0;
					Time gameTime = 0.0;
					MSGPACK_DEFINE(type, gameTime)
				};
				struct LoadSnapshotRecord
				{
					constexpr static byte Type = 1;


					Time gameplayTimeSeconds;
					std::vector<byte> data;

					MSGPACK_DEFINE(data)

				};

				struct AddCommandRecord
				{
					constexpr static byte Type = 2;

					Time gameTime;
					int playerId;
					int commandId;

					std::vector<byte> data;

					MSGPACK_DEFINE(playerId, gameTime, commandId, data)
				};

				struct ExecuteCommandRecord
				{
					constexpr static byte Type = 3;


					int playerId;
					int commandId;
					MSGPACK_DEFINE(playerId, commandId)
				};

				struct FrameRecord
				{
					constexpr static byte  Type = 4;

					MSGPACK_DEFINE()

				};
				struct UpdatePlayerListRecord
				{
					constexpr static byte Type = 5;

					PlayersUpdateCommand playerUpdate;

					MSGPACK_DEFINE(playerUpdate);
				};
				class ReplayReader
				{
				public:
					ReplayReader(byte* buffer, size_t length)
						: _buffer(buffer)
						, _length(length)
					{
						readHeader();
					}

					void readHeader()
					{

						msgpack::unpacked unp;
						size_t readOffset2 = msgpack::unpack(unp, reinterpret_cast<const char*>(_buffer), getRemainingLength());

						_offset += readOffset2;

						unp.get().convert(&header);
					}

					bool tryReadRecordHeader(RecordHeader& type)
					{
						return tryReadRecord(type);
					}

					template<typename T>
					bool tryReadRecord(T& record)
					{
						auto remainingLength = getRemainingLength();
						if (remainingLength == 0)
						{
							return false;
						}

						msgpack::unpacked unp;
						auto pointer = reinterpret_cast<const char*>(_buffer + _offset);
						size_t readOffset2 = msgpack::unpack(unp, pointer, remainingLength);

						if (readOffset2 > 0)
						{
							_offset += readOffset2;

							unp.get().convert(&record);
							return true;
						}
						else
						{
							return false;
						}
					}


					FileHeader header;
				private:
					size_t getRemainingLength()
					{
						return _length - _offset;
					}
					byte* _buffer;
					size_t _offset = 0;
					size_t _length;

				};

				class ReplayWriter
				{
				public:
					ReplayWriter(std::string& gameId, int playerId, std::vector<byte>& initializationData, std::function<void(ReplayWriteEvent&)> writer)
					{
						header.playerId = playerId;
						header.gameId = gameId;
						header.initializationData = initializationData;
						_writer = writer;

					}

					ReplayWriter(std::string gameId, int playerId, std::function<void(ReplayWriteEvent&)> writer)
					{
						header.playerId = playerId;
						header.gameId = gameId;
						_writer = writer;

					}
					ReplayWriter(std::function<void(ReplayWriteEvent&)> writer)
					{
						_writer = writer;
					}


					bool trySetInitializationData(byte* buffer, size_t length, std::string& buildId)
					{
						if (_fileHeaderWritten)
						{
							return false;
						}
						header.initializationData.resize(length);
						std::memcpy(header.initializationData.data(), buffer, length);
						header.buildId = buildId;
						return true;
					}

					void writeFrameRecord(double gameTime)
					{
						FrameRecord record;
						writeRecord(gameTime, record);
					}
					void writeExecuteCommandRecord(double gameTime, int playerId, int commandId)
					{
						ExecuteCommandRecord record;
						record.playerId = playerId;
						record.commandId = commandId;
						writeRecord(gameTime, record);
					}

					void writeAddCommandRecord(double gameTime, Time commandExecutionTime, int playerId, int commandId, const std::vector<byte>& data)
					{
						AddCommandRecord record;
						record.playerId = playerId;
						record.commandId = commandId;
						record.data = data;
						record.gameTime = commandExecutionTime;
						writeRecord(gameTime, record);
					}

					void writeLoadSnapshotRecord(double gameTime, double snapshotGameTime, const std::vector<byte>& data)
					{
						LoadSnapshotRecord record;
						record.data = data;
						record.gameplayTimeSeconds = snapshotGameTime;
						writeRecord(gameTime, record);
					}

					void writeUpdatePlayersCommand(double gameTime, const PlayersUpdateCommand& command)
					{
						UpdatePlayerListRecord record;
						record.playerUpdate = command;
						writeRecord(gameTime, record);

					}

					template<typename T>
					void writeRecord(double gameTime, const T& record)
					{
						RecordHeader rheader;
						rheader.gameTime = gameTime;
						rheader.type = T::Type;
						obytestream stream;

						msgpack::pack(&stream, rheader);
						msgpack::pack(&stream, record);

						ReplayWriteEvent evt;
						evt.data = stream.bytes();
						evt.playerId = header.playerId;
						evt.gameId = header.gameId;
						write(evt);
					}
					FileHeader header;

					void start()
					{
						if (_started)
						{
							return;
						}
						writeFileHeader();

						while (!_pendingEvents.empty())
						{
							auto& evt = _pendingEvents.front();

							_writer(evt);
							_pendingEvents.pop();
						}
						_started = true;


					}
				private:

					void write(ReplayWriteEvent& evt)
					{
						if (!_started)
						{
							_pendingEvents.push(evt);
						}
						else
						{
							_writer(evt);
						}
					}

					void writeFileHeader()
					{
						if (_fileHeaderWritten)
						{
							return;
						}
						_fileHeaderWritten = true;
						obytestream stream;

						msgpack::pack(&stream, header);

						ReplayWriteEvent evt;
						evt.isHeader = true;
						evt.playerId = header.playerId;
						evt.gameId = header.gameId;
						evt.data = stream.bytes();

						_writer(evt);
					}

					bool _started = false;
					bool _fileHeaderWritten = false;
					std::queue<ReplayWriteEvent> _pendingEvents;
					std::function<void(ReplayWriteEvent&)> _writer;
				};


			}
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
			struct CommandDto
			{
				int commandId = 0;
				Time gameplayTimeSeconds = 0;
				std::vector<byte> content;

				MSGPACK_DEFINE(commandId, gameplayTimeSeconds, content)
			};
			//Frame status sent by remote peer.
			struct FrameDto
			{
				int64 sentOn;
				Time gameplayTimeSeconds;

				//The timestamp we are sure there wouldn't be any 
				Time validatedGameplayTimeSeconds;
				Time deltaTimePerFrameSeconds;

				int firstCommandReceived;
				int lastCommandReceived;

				std::vector<byte> consistencyData;
				std::vector<CommandDto> commands;
				MSGPACK_DEFINE(sentOn, gameplayTimeSeconds, validatedGameplayTimeSeconds, deltaTimePerFrameSeconds, firstCommandReceived, lastCommandReceived, consistencyData, commands)
			};

			struct SnapshotDto
			{
				Time gameplayTimeSeconds;

				std::vector<byte> content;
				MSGPACK_DEFINE(gameplayTimeSeconds, content)
			};



			struct PlayerCommandNode
			{
				PlayerCommandNode* previous = nullptr;
				PlayerCommandNode* next = nullptr;

				CommandDto command;
			};
			template<typename T, T defaultValue, int TSamplesCount = 16>
			class Samples
			{
			public:
				Samples()
				{
					for (int i = 0; i < TSamplesCount; i++)
					{
						_samples[i] = defaultValue;
					}
				}
				T getAverage() const
				{
					return _avg;
				}

				T getMax() const
				{
					return _max;
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

					computeAverage();
				}

			private:
				void computeAverage()
				{
					T sum = 0;
					T max = 0;

					for (int i = _offset; i < _nb + _offset; i++)
					{
						if (_samples[i % TSamplesCount] > max)
						{
							max = _samples[i % TSamplesCount];
						}
						sum += _samples[i % TSamplesCount];
					}
					_avg = sum / TSamplesCount;
					_max = max;
				}

				T _avg = 0;
				T _max = 0;
				T _maxValue = 0;
				T _samples[TSamplesCount];
				int _offset = 0;
				int _nb = 0;

			};
			struct FrameConsistencyData
			{
				FrameConsistencyData() { isValid = false; }
				FrameConsistencyData(Time s, std::vector<byte>& h)
				{
					gameplayTimeSeconds = s;
					hash = h;
					isValid = true;
				}
				bool isValid;
				Time gameplayTimeSeconds;
				std::vector<byte> hash = {};
			};




			struct PlayerState
			{
				SessionId sessionId;
				int playerId = -1;
				Samples<unsigned int, 0, 128> latency;

				bool isLocal = false;
				/// <summary>
				/// The gameplay time of the player when the frame was sent.
				/// </summary>
				Time gameplayTimeSeconds = 0;
				Time deltaTimePerFrameSeconds = 0;

				std::array<FrameConsistencyData, 8> _framesConsistencyHistory;
				int _offset = 0;
				int _count = 0;


				bool isSynchronized = false;

				void addFrame(Time s, std::vector<byte>& h)
				{
					_framesConsistencyHistory[_offset] = FrameConsistencyData(s, h);
					_offset = (_offset + 1) % 8;
					if (_count < 8)
					{
						_count++;
					}
				}
				bool tryGetOldestConsistencyData(FrameConsistencyData& data)
				{
					if (_count == 0)
					{
						return false;
					}
					else
					{
						data = *(_framesConsistencyHistory.data() + ((8 + _offset - _count) % 8));
						return true;
					}
				}


				void removeOldestConsistencyData()
				{
					if (_count > 0)
					{
						_count--;
					}
				}



				/// <summary>
				/// The minimum time for future commands
				/// </summary>
				Time validatedGamePlayTimeSeconds = 0;


				Time lastCommandTimeSeconds = 0;

				int64 receivedOn = 0;
				int64 sentOn = 0;

				int64 lastCommandUpdateOn = 0;

				PlayerCommandNode* lastLocalCommandReceivedByRemotePeer = nullptr;

				PlayerCommandNode* _firstCommand = nullptr;
				PlayerCommandNode* _lastCommand = nullptr;
				PlayerCommandNode* _lastExecutedCommand = nullptr;


				/// <summary>
				/// Did we already send commands to this peer.
				/// </summary>
				int lastSentCommand = 0;

				Time synchronizedUntil() const
				{
					return validatedGamePlayTimeSeconds;

				}
				unsigned int lastExecutedCommandId() const
				{
					return _lastExecutedCommand != nullptr ? _lastExecutedCommand->command.commandId : 0;
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

			_NODISCARD bool operator==(const PlayerState& left, const PlayerState& right) {
				return left.sessionId == right.sessionId;
			}
			_NODISCARD bool operator<(const PlayerState& left, const PlayerState& right) {
				return left.playerId < right.playerId;
			}





			/// <summary>
			/// Lockstep service designed to play a replay file. 
			/// </summary>
			class ReplayLockstepService : public ILockstepService, public std::enable_shared_from_this<ReplayLockstepService>
			{
			public:
				bool endOfRecording = false;

				ReplayLockstepService(byte* buffer, size_t length)
					:_reader(buffer, length)
				{

				}

				void initialize() override
				{

				}

				int pushCommand(byte* buffer, int length) override
				{
					//Does not support pushing commands
					return -1;
				}

				ReplayMode getReplayMode() override
				{
					return ReplayMode::Playing;
				}

				bool trySetReplayInitialData(byte* buffer, size_t length, std::string& buildId) override
				{
					return false;
				}
				bool tryGetReplayInitialData(std::vector<byte>& initialData, std::string& buildId, std::string& gameId) override
				{
					initialData = _reader.header.initializationData;
					buildId = _reader.header.buildId;
					gameId = _reader.header.gameId;
					return true;
				}

				FrameDuration adjustTick(FrameDuration deltaSeconds, FrameDuration realDeltaSeconds) override
				{
					if (_isPaused)
					{
						deltaSeconds = 0;
					}
					else
					{
						_timeSinceLastGameplayProgress += deltaSeconds;
						deltaSeconds = deltaSeconds;
					}

					if (_timeSinceLastGameplayProgress < deltaSeconds)
					{
						return 0;
					}
					else
					{
						_timeSinceLastGameplayProgress -= deltaSeconds;
						return deltaSeconds;
					}
				}
				void tick(FrameDuration deltaSeconds, FrameDuration realDeltaSeconds) override
				{


					Frame previousFrame = _currentFrame;
					Frame frame;
					frame.currentTimeSeconds = previousFrame.currentTimeSeconds + deltaSeconds;

					_currentFrame = frame;
					if (_currentHeader.type == 0)
					{
						if (!_reader.tryReadRecordHeader(_currentHeader))
						{
							endOfRecording = true;
							return;
						}
					}



					while (_currentHeader.gameTime <= _currentFrame.currentTimeSeconds && (!_isPaused || canExecuteDuringPause()))
					{
						readCurrentRecord(_currentHeader.gameTime >= previousFrame.currentTimeSeconds, previousFrame);

						if (!_reader.tryReadRecordHeader(_currentHeader))
						{
							endOfRecording = true;
							return;
						}

					}
					if (deltaSeconds > 0 || previousFrame.commands.size() > 0)
					{
						this->onStep(previousFrame);
						this->onEndFrame(previousFrame);
					}


				}
				bool canExecuteDuringPause()
				{
					return _currentHeader.type == Replays::LoadSnapshotRecord::Type || _currentHeader.type == Replays::UpdatePlayerListRecord::Type;
				}

				void readCurrentRecord(bool execute, Frame& frame)
				{
					switch (_currentHeader.type)
					{
					case Replays::FrameRecord::Type:
						break;
					case Replays::AddCommandRecord::Type:
					{
						Replays::AddCommandRecord record;
						if (_reader.tryReadRecord(record) && execute)
						{
							process(record);
						}
					}
					case Replays::ExecuteCommandRecord::Type:
					{
						Replays::ExecuteCommandRecord record;
						if (_reader.tryReadRecord(record) && execute)
						{
							process(record, frame);
						}
						break;
					}
					case Replays::LoadSnapshotRecord::Type:
					{
						Replays::LoadSnapshotRecord record;
						if (_reader.tryReadRecord(record) && execute)
						{
							process(record);
						}
						break;
					}
					case Replays::UpdatePlayerListRecord::Type:
					{
						Replays::UpdatePlayerListRecord record;
						if (_reader.tryReadRecord(record) && execute)
						{
							process(record);
						}
						break;
					}
					default:
						break;

					}
				}
				void process(Replays::AddCommandRecord record)
				{
					Command cmd;
					cmd.commandId = record.commandId;
					cmd.playerId = record.playerId;
					cmd.timeSeconds = record.gameTime;
					cmd.content = record.data;
					_commands.push_back(cmd);
				}
				void process(Replays::UpdatePlayerListRecord record)
				{
					auto cmd = record.playerUpdate;
					switch (cmd.commandType)
					{
					case PlayersUpdateCommandType::Add:
					{
						LockstepPlayer player;
						player.playerId = cmd.playerId;
						player.sessionId = cmd.playerSessionId;
						player.localPlayer = player.playerId == getCurrentPlayerId();
						_players.push_back(player);



						break;
					}
					case PlayersUpdateCommandType::Remove:
					{

						auto it = _players.begin();
						while (it != _players.end())
						{
							if (it->sessionId == cmd.playerSessionId)
							{
								_players.erase(it);
							}
						}

						break;
					}
					}
					this->onPlayerListChanged();
				}
				void process(Replays::LoadSnapshotRecord record)
				{

					Snapshot snapshot;
					snapshot.gameplayTimeSeconds = record.gameplayTimeSeconds;
					snapshot.content = record.data;
					_currentFrame.currentTimeSeconds = snapshot.gameplayTimeSeconds;
					_currentFrame.validatedTimeSeconds = snapshot.gameplayTimeSeconds;

					onInstallSnapshot(snapshot);
				}

				void process(Replays::ExecuteCommandRecord record, Frame& frame)
				{
					for (int i = 0; i < _commands.size(); i++)
					{
						auto& c = _commands[i];
						if (c.commandId == record.commandId && c.playerId == record.playerId)
						{
							frame.commands.push_back(c);
							_commands.erase(_commands.begin() + i);
							return;
						}
					}
				}

				void endFrame() override
				{
				
				}

				Time getCurrentTime() const override
				{
					return _currentFrame.currentTimeSeconds;
				}

				Time getTargetTime() const override
				{
					return _currentFrame.currentTimeSeconds + _options.FixedDeltaTimeSeconds;
				}

				Time getCommandTime() const  override
				{
					//It's not possible to emit commands
					return _currentFrame.currentTimeSeconds;
				}

				FrameDuration getLatency() const override
				{
					return 0;
				}
				int lastExecutedCommand() const  override
				{
					return 0;
				}

				bool isPaused() const  override
				{
					return _isPaused;
				}

				void pause(bool pause) override
				{
					_isPaused = pause;
				}

				std::vector<LockstepPlayer> getPlayers() const  override
				{
					return _players;
				}

				int getCurrentPlayerId() const override
				{
					return _reader.header.playerId;
				}


				~ReplayLockstepService() override {};
			private:

				Frame _currentFrame;
				bool _isPaused = true;

				std::vector<LockstepPlayer> _players;
				std::vector<Command> _commands;
				Time _timeSinceLastGameplayProgress = 0;
				LockstepOptions _options;
				Replays::ReplayReader _reader;
				Replays::RecordHeader _currentHeader;
			};

			class OfflineLockstepService : public ILockstepService, public std::enable_shared_from_this<OfflineLockstepService>
			{

			public:
				OfflineLockstepService(std::shared_ptr<ILogger> logger, std::function<void(ReplayWriteEvent&)> replayWriter)
					: _logger(logger)
					, _replayWriter("offline", 0, replayWriter)
				{

				}

				void initialize() override
				{

				}

				ReplayMode getReplayMode() override
				{
					return ReplayMode::Recording;
				}

				bool trySetReplayInitialData(byte* buffer, size_t length, std::string& buildId) override
				{
					return _replayWriter.trySetInitializationData(buffer, length, buildId);
				}

				bool tryGetReplayInitialData(std::vector<byte>& initialData, std::string& buildId, std::string& gameId) override
				{
					initialData = _replayWriter.header.initializationData;
					buildId = _replayWriter.header.buildId;
					gameId = _replayWriter.header.gameId;

					return true;
				}

				int getCurrentPlayerId() const override
				{
					return 0;
				}
				int pushCommand(byte* buffer, int length) override
				{
					tryInitialize();

					if (length == 0)
					{
						this->_logger->log(LogLevel::Error, "lockstep", "Received command of length 0");
					}
					command cmd;
					cmd.content.resize(length);
					cmd.executionTime = getCommandTime();
					byte& pointer = cmd.content.front();

					memcpy(&pointer, buffer, length);

					_lastCmdId++;
					cmd.id = _lastCmdId;

					_cmds.push_back(cmd);
					_replayWriter.writeAddCommandRecord(_currentFrame.currentTimeSeconds, cmd.executionTime, 0, cmd.id, cmd.content);
					if (cmd.content.size() == 0)
					{
						this->_logger->log(LogLevel::Error, "lockstep", "Enqueued command of length 0");
					}

					return _lastCmdId;
				}

				FrameDuration adjustTick(FrameDuration deltaSeconds, FrameDuration realDeltaSeconds)
				{
					if (_isPaused)
					{
						deltaSeconds = 0;
					}
					else
					{
						_timeSinceLastGameplayProgress += deltaSeconds;
						deltaSeconds = deltaSeconds;
					}

					if (_timeSinceLastGameplayProgress < deltaSeconds)
					{
						return 0;
					}
					else
					{
						_timeSinceLastGameplayProgress -= deltaSeconds;
						return deltaSeconds;
					}
				}


				void tick(FrameDuration deltaSeconds, FrameDuration realDeltaSeconds) override
				{



					if (deltaSeconds == 0)
					{
						return;
					}
					tryInitialize();
					Frame previousFrame = _currentFrame;
					Frame frame;
					frame.currentTimeSeconds = previousFrame.currentTimeSeconds + deltaSeconds;

					_currentFrame = frame;

					while (_cmds.size() > 0)
					{
						auto& cmd = _cmds.front();
						if (cmd.executionTime < previousFrame.currentTimeSeconds)
						{
							throw std::runtime_error("Cannot run command because it's scheduled to run before the previous frame.");
						}
						if (cmd.executionTime < getCurrentTime())
						{
							Command command;
							command.content = cmd.content;
							command.playerId = getCurrentPlayerId();
							command.commandId = cmd.id;
							command.timeSeconds = cmd.executionTime;

							frame.commands.push_back(command);

							_replayWriter.writeExecuteCommandRecord(previousFrame.currentTimeSeconds, 0, command.commandId);
							if (command.content.size() == 0)
							{
								this->_logger->log(LogLevel::Error, "lockstep", "executing command of length 0");
							}
							_cmds.pop_front();
						}
						else
							break;

					}
					onStep(frame);
					//_replayWriter.writeFrameRecord(previousFrame.currentTimeSeconds);
					if ((deltaSeconds > 0) != _currentGameplayProgress)
					{
						_currentGameplayProgress = deltaSeconds > 0;
						PauseState pauseState = _isPaused ? PauseState::Paused : deltaSeconds == 0 ? PauseState::Waiting : PauseState::Running;
						onPauseStateChanged(pauseState);
					}
				}

				void endFrame() override
				{
					return;
				}

				Time getCurrentTime() const
				{
					return _currentFrame.currentTimeSeconds;
				}

				Time getCommandTime() const
				{
					return  _currentFrame.currentTimeSeconds + 0.05;
				}

				FrameDuration getLatency() const
				{
					return 0;
				}

				Time getTargetTime() const
				{
					return _currentFrame.currentTimeSeconds + _options.FixedDeltaTimeSeconds;
				}

				int lastExecutedCommand() const
				{
					if (_cmds.size() > 0)
					{
						return _cmds.back().id;
					}
					else
					{
						return 0;
					}
				}

				bool isPaused() const
				{
					return _isPaused;
				}
				void pause(bool pause)
				{
					_isPaused = pause;
					if (!pause)
					{
						_replayWriter.start();
					}
				}


				std::vector<LockstepPlayer> getPlayers() const override
				{
					std::vector<LockstepPlayer> result;

					LockstepPlayer player;
					player.latencyMs = 0;
					player.localPlayer = true;
					player.playerId = 0;

					//When offline, use minimum delay
					Time syncTime = getCurrentTime() + _options.MinDelaySeconds;

					if (_cmds.size() > 0)
					{
						auto& lastCmd = _cmds.back();
						Time lastTime = lastCmd.executionTime;
						if (lastTime > syncTime)
						{
							syncTime = lastTime;
						}
						player.lastCommandId = lastCmd.id;
					}
					player.synchronizedUntilMs = syncTime;

					result.push_back(player);
					return result;
				}

			private:
				void tryInitialize()
				{
					if (!_initialized)
					{
						_initialized = true;

						std::vector<byte> snapshotData;

						PlayersUpdateCommand playerUpdateCommand;
						playerUpdateCommand.commandType = PlayersUpdateCommandType::Add;
						playerUpdateCommand.playerId = 0;
						playerUpdateCommand.updateId = 0;
						playerUpdateCommand.playerSessionId = {};

						Snapshot snapshot;
						snapshot.content = snapshotData;
						snapshot.gameplayTimeSeconds = 0.0;

						onInstallSnapshot(snapshot);

						_replayWriter.writeUpdatePlayersCommand(0.0, playerUpdateCommand);

						_replayWriter.writeLoadSnapshotRecord(0.0, snapshot.gameplayTimeSeconds, snapshot.content);

						onStart();

					}
				}
				LockstepOptions _options;
				bool _initialized = false;

				int _lastCmdId = 0;

				std::shared_ptr<ILogger> _logger;
				Replays::ReplayWriter _replayWriter;


				bool _currentGameplayProgress = false;
				Time _timeSinceLastGameplayProgress = 0;
				Frame _currentFrame;
				bool _isPaused = true;

				struct command
				{
					std::vector<byte> content;

					Time executionTime;
					int id;
				};

				std::list<command> _cmds;

			};

			class LockstepReplayUploadService
			{
			public:
				LockstepReplayUploadService(std::shared_ptr<RpcService> rpc)
					: _rpc(rpc)
				{

				}
				pplx::task<bool> tryUploadReplay(std::string filePath)
				{
					auto rpc = _rpc.lock();
					return pplx::task_from_result(false);
				}
			private:
				std::weak_ptr<RpcService> _rpc;
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

				std::unique_ptr<Replays::ReplayWriter> _writer;
				void initialize() override
				{
					_writer = std::make_unique< Replays::ReplayWriter>(replayWriter);
					_writer->header.gameId = _gameId;
				}

				~LockstepService()
				{

					for (PlayerState& state : _playerStates)
					{

						auto current = state._firstCommand;
						while (current != nullptr)
						{
							auto next = current->next;
							delete current;
							current = next;
						}
						state._firstCommand = nullptr;
						state._lastCommand = nullptr;
						state._lastExecutedCommand = nullptr;
					}

				}

				ReplayMode getReplayMode() override
				{
					return ReplayMode::Recording;
				}

				bool trySetReplayInitialData(byte* buffer, size_t length, std::string& buildId) override
				{
					if (_writer == nullptr)
					{
						return false;
					}
					else
					{
						return _writer->trySetInitializationData(buffer, length, buildId);
					}

				}
				bool tryGetReplayInitialData(std::vector<byte>& initialData, std::string& buildId, std::string& gameId) override
				{
					if (!_writer)
					{
						return false;
					}
					else
					{
						initialData = _writer->header.initializationData;
						buildId = _writer->header.buildId;
						gameId = _writer->header.gameId;
						return true;
					}
				}

				FrameDuration _latency = 0;

				void updateLatency(Time delta)
				{
					unsigned int l = 0;
					Time highestGameplayTime = _currentFrame.currentTimeSeconds;

					for (auto& state : _playerStates)
					{
						if (!state.isLocal)
						{

							auto v = (unsigned int)state.latency.getMax();
							if (v > l)
							{
								l = v;
							}
							Time gameplayTime = state.gameplayTimeSeconds + (Time)l / 1000.0f;
							if (gameplayTime > highestGameplayTime)
							{
								highestGameplayTime = gameplayTime;
							}
						}

					}
					_latency = (Time)l / 1000.0f + _options.DelayMarginSeconds;


					if (_latency < _options.MinDelaySeconds)
					{
						_latency = _options.MinDelaySeconds;
					}
					if (_latency > _options.MaxDelaySeconds)
					{
						_latency = _options.MaxDelaySeconds;
					}


					auto candidateCommandTime = highestGameplayTime + _latency;

					if (candidateCommandTime > _currentCommandTime)
					{
						_currentCommandTime = candidateCommandTime;
					}



				}

				FrameDuration getLatency() const override
				{
					return _latency;
				}

				int getCurrentPlayerId() const override
				{
					return this->_currentPlayerId;
				}

				std::vector<LockstepPlayer> getPlayers() const override
				{
					std::vector<LockstepPlayer> result;

					for (auto& state : _playerStates)
					{


						LockstepPlayer player;
						player.localPlayer = state.isLocal;
						player.synchronizedUntilMs = state.synchronizedUntil();
						player.lastCommandId = state.lastLocalCommandReceivedByRemotePeer != nullptr ? state.lastLocalCommandReceivedByRemotePeer->command.commandId : 0;
						player.latencyMs = (int)state.latency;
						player.playerId = state.playerId;
						player.sessionId = state.sessionId;
						result.push_back(player);
					}

					return result;
				}

				Time _currentCommandTime = 0.0f;
				Time getCommandTime() const
				{
					return _currentCommandTime;

				}

				bool tryGetState(const SessionId& sessionId, PlayerState*& state) const
				{

					for (auto& s : _playerStates)
					{
						if (s.sessionId == sessionId)
						{
							state = (PlayerState*)(&s);
							return true;
						}
					}

					return false;

				}

				int pushCommand(byte* buffer, int length) override
				{

					if (!_initialized)
					{
						return -1;
					}
					if (length == 0)
					{
						this->_logger->log(LogLevel::Error, "lockstep", "Received command of length 0");
					}

					auto& sessionId = _client.lock()->sessionId();


					PlayerState* currentPlayerState = nullptr;

					if (!tryGetState(sessionId, currentPlayerState))
					{
						return -1;
					}
					auto client = _client.lock();
					auto node = new PlayerCommandNode;
					node->command.commandId = currentPlayerState->_lastCommand != nullptr ? currentPlayerState->_lastCommand->command.commandId + 1 : 1;
					auto time = getCommandTime();
					if (time == 0.0f) // command time not updated yet.
					{
						return -1;
					}
					for (auto& state : this->_playerStates)
					{
						if (state.gameplayTimeSeconds > time)
						{
							return -1;
						}
					}

					node->command.gameplayTimeSeconds = time;

					node->command.content.resize(length);
					byte& pointer = node->command.content.front();
					memcpy(&pointer, buffer, length);

					if (node->command.content.size() == 0)
					{
						this->_logger->log(LogLevel::Error, "lockstep", std::to_string(_currentFrame.currentTimeSeconds) + "|" + std::to_string(_currentPlayerId) + "Enqueuing command of length 0", std::to_string(node->command.commandId));
					}

					if (currentPlayerState->_lastCommand != nullptr)
					{

						currentPlayerState->_lastCommand->next = node;
						node->previous = currentPlayerState->_lastCommand;
					}
					else
					{
						currentPlayerState->_firstCommand = node;
					}

					currentPlayerState->_lastCommand = node;

					for (auto& playerState : _playerStates)
					{
						//Reset last command update time on every player to trigger an immediate reset.
						playerState.lastCommandUpdateOn = 0;
					}

					auto n = currentPlayerState->_firstCommand;
					if (_currentFrame.validatedTimeSeconds >= time)
					{
						DebugBreak();
					}
					_writer->writeAddCommandRecord(getCurrentTime(), node->command.gameplayTimeSeconds, _currentPlayerId, node->command.commandId, node->command.content);
					this->_logger->log(LogLevel::Info, "lockstep", std::to_string(_currentFrame.currentTimeSeconds) + "| Enqueued command " + std::to_string(_currentPlayerId) + "/" + std::to_string(node->command.commandId) + "for time " + std::to_string(node->command.gameplayTimeSeconds));

					//synchronizeCommands(currentPlayerState);
					return node->command.commandId;

				}


				Time getCurrentTime() const
				{
					return _currentFrame.currentTimeSeconds;
				}

				FrameDuration adjustTick(FrameDuration targetDeltaSeconds, FrameDuration realDeltaSeconds)
				{

					if (!_initialized)
					{

						return 0;
					}

					_timeSinceLastGameplayProgress += targetDeltaSeconds;

					Time deltaSeconds;




					if (_isPaused)
					{
						deltaSeconds = 0;
					}
					else
					{
						deltaSeconds = _options.FixedDeltaTimeSeconds;
					}

					if (_timeSinceLastGameplayProgress < deltaSeconds)
					{

						_logger->log(LogLevel::Info, "lockstep", std::to_string(_currentFrame.currentTimeSeconds) + "|" + std::to_string(_currentPlayerId) + " frame pause timeSinceLastGameplayProgress<deltaSeconds", std::to_string(_timeSinceLastGameplayProgress) + "<" + std::to_string(deltaSeconds));
						return 0;
					}

					auto nextTime = _currentFrame.currentTimeSeconds + deltaSeconds;

					auto targetTime = getTargetTime();
					auto synchronizedUntil = this->synchronizedUntil();

					if (nextTime > synchronizedUntil)
					{
						//_logger->log(LogLevel::Info, "lockstep", std::to_string(this->_currentPlayerId) + " frame pause nextTime > synchronizedUntil ", std::to_string(nextTime) + ">" + std::to_string(synchronizedUntil));


						deltaSeconds = 0;
						nextTime = _currentFrame.currentTimeSeconds;

					}
					else if (nextTime > targetTime + _options.FixedDeltaTimeSeconds && (getCurrentTime() - _lastPausedOn) > _options.MinPauseDelayOnSlowAdjust)
					{
						//_logger->log(LogLevel::Info, "lockstep", std::to_string(this->_currentPlayerId) + " nextTime > targetTime", std::to_string(nextTime) + ">" + std::to_string(targetTime));


						deltaSeconds = 0;
						nextTime = _currentFrame.currentTimeSeconds;
					}

					updateLatency(deltaSeconds);


					return deltaSeconds;
				}

				void tick(FrameDuration deltaSeconds, FrameDuration realDeltaSeconds)
				{

					processPendingPlayersUpdateCommands();

					PlayerState* currentPlayerState = nullptr;
					if (!tryGetState(_client.lock()->sessionId(), currentPlayerState))
					{
						return;
					}
					synchronizeState(currentPlayerState);

					if (!_initialized && canInitialize())
					{
						tryStartInitialize();
					}
					if (!_initialized)
					{
						return;
					}

					auto oldTime = _currentFrame.currentTimeSeconds;
					auto currentTime = oldTime + deltaSeconds;
					if (deltaSeconds == 0)
					{
						_lastPausedOn = currentTime;
						return;
					}


					_lastDeltaTimePerFrameSeconds = deltaSeconds;






					if (!_started && deltaSeconds > 0)
					{
						onStart();
						_started = true;
					}


					_currentFrame = Frame();

					auto nextTime = currentTime;

					_currentFrame.currentTimeSeconds = currentTime;
					bool gameplayProgress = deltaSeconds != 0;


					auto targetTime = getTargetTime();
					auto synchronizedUntil = this->synchronizedUntil();



					_timeSinceLastGameplayProgress = 0;
					/*else
					{

						_logger->log(Stormancer::LogLevel::Info, "lockstep", std::to_string(gameplayProgress) + " " + std::to_string(_currentTime) + " " + std::to_string(nextTime) + " " + std::to_string(targetTime), "");
					}*/


					for (auto& state : _playerStates)
					{

						auto node = state._firstCommand;
						if (state._lastExecutedCommand != nullptr)
						{
							node = state._lastExecutedCommand->next;
						}
						while (node != nullptr && node->command.gameplayTimeSeconds < nextTime)
						{


							if (node->command.gameplayTimeSeconds < nextTime && node->command.gameplayTimeSeconds > oldTime)
							{
								Command command;
								command.commandId = node->command.commandId;
								command.content = node->command.content;
								command.playerId = state.playerId;
								command.sessionId = state.sessionId;
								command.timeSeconds = node->command.gameplayTimeSeconds;

								if (command.content.size() == 0)
								{
									this->_logger->log(LogLevel::Error, "lockstep", "Executing remote cmd of length 0", std::to_string(command.commandId));

								}
								_currentFrame.commands.push_back(command);
								_writer->writeExecuteCommandRecord(oldTime, command.playerId, command.commandId);
								state._lastExecutedCommand = node;
							}
							else if (node->command.gameplayTimeSeconds <= oldTime)
							{
								this->_logger->log(LogLevel::Info, "lockstep", std::to_string(_currentFrame.currentTimeSeconds) + "|" + std::to_string(_currentPlayerId) + " Skipped executing command " + std::to_string(oldTime) + " " + std::to_string(node->command.gameplayTimeSeconds) + " " + std::to_string(nextTime), std::to_string(node->command.commandId));
								state._lastExecutedCommand = node;
							}

							node = node->next;

						}



					}

					if ((gameplayProgress && deltaSeconds > 0) != _currentGameplayProgress)
					{
						_currentGameplayProgress = gameplayProgress && deltaSeconds > 0;
						PauseState pauseState = _isPaused ? PauseState::Paused : !gameplayProgress ? PauseState::Waiting : PauseState::Running;
						onPauseStateChanged(pauseState);
					}

					onStep(_currentFrame);

				}

				void endFrame()
				{
					onEndFrame(_currentFrame);
				}

				bool isPaused() const
				{
					return _isPaused;
				}
				int lastExecutedCommand() const
				{
					PlayerState* state = nullptr;
					if (tryGetState(_client.lock()->sessionId(), state))
					{
						return state->_lastExecutedCommand != nullptr ? state->_lastExecutedCommand->command.commandId : 0;
					}
					else
					{
						return -1;
					}
				}

				void pause(bool pause)
				{
					_isPaused = pause;
					if (!pause)
					{
						if (_writer)
						{
							_writer->start();
						}
					}
				}

				Time getTargetTime() const
				{
					Time result = TimeMaxValue;
					bool found = false;

					for (auto& state : _playerStates)
					{
						if (!state.isLocal)
						{

							Time time = getPlayerCurrentEstimatedGameplayTimeMs(state);
							if (time < result)
							{
								result = time;
								found = true;
							}
						}
					}
					if (!found)
					{
						result = _currentFrame.currentTimeSeconds + _options.FixedDeltaTimeSeconds;
					}
					return result;
				}

			private:

				Time getPlayerCurrentEstimatedGameplayTimeMs(const PlayerState& state) const
				{
					if (auto client = _client.lock())
					{
						return state.gameplayTimeSeconds + ((Time)(client->clock() - state.sentOn)) / 1000;
					}
					else
					{
						return 0;
					}
				}



				Time synchronizedUntil() const
				{
					Time result = std::numeric_limits<Time>::max();



					for (auto& state : _playerStates)
					{
						if (!state.isLocal)
						{
							Time time = state.synchronizedUntil();
							if (time < result)
							{
								result = time;
							}
						}
					}
					return result;
				}







				void synchronizeState(const PlayerState* currentPlayerState)
				{
					_currentFrame.validatedTimeSeconds = getCommandTime();
					for (auto& playerState : _playerStates)
					{
						if (!playerState.isLocal)
						{
							sendStateToPlayer(currentPlayerState, playerState);
						}
						else
						{
							playerState.validatedGamePlayTimeSeconds = _currentFrame.validatedTimeSeconds;
						}
					}
				}

				void sendStateToPlayer(const PlayerState* currentPlayerState, PlayerState& playerState)
				{
					auto client = _client.lock();
					auto currentTimeMs = client->clock();
					FrameDto frame;



					frame.gameplayTimeSeconds = _currentFrame.currentTimeSeconds; //_currentGamePlayTimeSeconds;
					frame.consistencyData = _currentFrame.consistencyData;
					frame.deltaTimePerFrameSeconds = _lastDeltaTimePerFrameSeconds;
					frame.validatedGameplayTimeSeconds = _currentFrame.validatedTimeSeconds;//  _currentFrame.currentTimeSeconds /*_currentGamePlayTimeSeconds*/ + getCommandDelay();

					frame.sentOn = _client.lock()->clock();
					frame.firstCommandReceived = playerState._firstCommand != nullptr ? playerState._firstCommand->command.commandId : 0;
					frame.lastCommandReceived = playerState._lastCommand != nullptr ? playerState._lastCommand->command.commandId : 0;

					auto cmd = playerState.lastLocalCommandReceivedByRemotePeer;

					if (cmd == nullptr)
					{
						cmd = currentPlayerState->_firstCommand;
					}
					else
					{
						cmd = cmd->next;
					}

					if ((currentTimeMs - playerState.lastCommandUpdateOn) > playerState.latency.getAverage() * 2)
					{
						while (cmd != nullptr)
						{

							//_logger->log(LogLevel::Info, "lockstep", std::to_string(_currentFrame.currentTimeSeconds) + "| Send command " + std::to_string(_currentPlayerId) + "/" + std::to_string(cmd->command.commandId) + " to " + std::to_string(playerState.playerId));
							frame.commands.push_back(cmd->command);

							cmd = cmd->next;
						}
						playerState.lastCommandUpdateOn = currentTimeMs;
					}

					auto serializer = _serializer;
					_mesh->send(playerState.sessionId, "lockstep.frame", [frame, serializer](obytestream& stream)
						{
							serializer->serialize(stream, frame);
						}, PacketReliability::UNRELIABLE_SEQUENCED);
				}


				std::string _gameId;

				void initialize(std::shared_ptr<Scene> scene)
				{
					_gameId = scene->id();

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

					scene->addRoute("lockstep.installSnapshot", [wService, wClient](Packetisp_ptr packet)
						{
							auto service = wService.lock();
							if (service)
							{
								byte buffer[16];
								packet->stream.read(buffer, 16);
								SessionId sessionId;
								SessionId::tryParse(buffer, 16, sessionId);
								auto args = packet->readObject<SnapshotDto>();
								service->installSnapshot(sessionId, args);

							}
						}, p2pOptions);
					scene->addRoute("lockstep.requestSnapshot", [wService, wClient](Packetisp_ptr packet)
						{
							auto service = wService.lock();
							if (service)
							{
								byte buffer[16];
								packet->stream.read(buffer, 16);
								SessionId sessionId;
								SessionId::tryParse(buffer, 16, sessionId);
								service->onRequestSnapshot(sessionId);

							}
						}, p2pOptions);
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
								PlayerState* state = nullptr;

								if (service->tryGetState(sessionId, state))
								{


									state->receivedOn = client->clock();
									state->sentOn = args.sentOn;
									auto latency = (int)(state->receivedOn - args.sentOn);
									state->latency.addValue(latency > 0 ? latency : 0);
									state->isSynchronized = true;
									if (args.gameplayTimeSeconds >= state->gameplayTimeSeconds)
									{
										state->deltaTimePerFrameSeconds = args.deltaTimePerFrameSeconds;
										state->validatedGamePlayTimeSeconds = args.validatedGameplayTimeSeconds;
										state->gameplayTimeSeconds = args.gameplayTimeSeconds;
										state->addFrame(args.gameplayTimeSeconds, args.consistencyData);

										for (auto& command : args.commands)
										{
											if (command.gameplayTimeSeconds <= service->_currentFrame.currentTimeSeconds)
											{

												service->_logger->log(LogLevel::Error, "lockstep", std::to_string(service->_currentFrame.currentTimeSeconds) + "|" + std::to_string(service->_currentPlayerId) + "detected desync : adding command " + std::to_string(state->playerId) + "/" + std::to_string(command.commandId) + " for frame " + std::to_string(command.gameplayTimeSeconds) + " but current time is" + std::to_string(service->_currentFrame.currentTimeSeconds) + ". Validated time for origin player is " + std::to_string(state->validatedGamePlayTimeSeconds));
											}
											else
											{
												service->_logger->log(LogLevel::Info, "lockstep", std::to_string(service->_currentFrame.currentTimeSeconds) + "|" + std::to_string(service->_currentPlayerId) + " added command " + std::to_string(state->playerId) + "/" + std::to_string(command.commandId) + " for frame " + std::to_string(command.gameplayTimeSeconds) + ". Current time" + std::to_string(service->_currentFrame.currentTimeSeconds) + ". Validated time for player is " + std::to_string(state->validatedGamePlayTimeSeconds));
											}

											state->addCommand(command);
											service->_writer->writeAddCommandRecord(service->getCurrentTime(), command.gameplayTimeSeconds, state->playerId, command.commandId, command.content);
										}
										auto node = state->lastLocalCommandReceivedByRemotePeer;
										if (node == nullptr)
										{
											PlayerState* currentState = nullptr;
											if (service->tryGetState(service->_client.lock()->sessionId(), currentState))
											{
												if (currentState->_firstCommand != nullptr && currentState->_firstCommand->command.commandId <= args.lastCommandReceived)
												{
													node = currentState->_firstCommand;
												}
											}
										}

										while (node != nullptr && node->command.commandId < args.lastCommandReceived)
										{
											node = node->next;
										}

										/*if (state->lastLocalCommandReceivedByRemotePeer == nullptr && node != nullptr)
										{
											service->_logger->log(LogLevel::Info, "lockstep", std::to_string(service->_currentFrame.currentTimeSeconds) + "|" + std::to_string(service->_currentPlayerId) + "Set first command.");
										}*/
										state->lastLocalCommandReceivedByRemotePeer = node;
										service->checkConsistency();


										//service->_logger->log(LogLevel::Info, "lockstep", std::to_string(service->_currentFrame.currentTimeSeconds) + "|" + std::to_string(service->_currentPlayerId) + " received frame from " + std::to_string(state->playerId) + " validatedGamePlayTime" + std::to_string(state->validatedGamePlayTimeSeconds));
									}
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

								PlayerState* state = nullptr;
								if (service->tryGetState(sessionId, state))
								{

									for (auto& command : commands)
									{
										service->_logger->log(LogLevel::Info, "lockstep", std::to_string(service->_currentFrame.currentTimeSeconds) + "|" + std::to_string(service->_currentPlayerId) + " added command from " + std::to_string(state->playerId) + " for frame " + std::to_string(command.gameplayTimeSeconds) + ". current time" + std::to_string(service->_currentFrame.currentTimeSeconds), std::to_string(command.commandId));
										state->addCommand(command);
									}
								}
								else
								{
									service->_logger->log(Stormancer::LogLevel::Warn, "lockstep", "Received command but no corresponding player found.");
								}

							}
						}, p2pOptions);

				}

				Time _targetConsistencyCheck = 0;

				bool tryPerformConsistencyCheck()
				{

					ConsistencyCheckEvent evt;
					evt.gameplayTime = _targetConsistencyCheck;
					for (auto& state : _playerStates)
					{
						FrameConsistencyData data;

						while (state.tryGetOldestConsistencyData(data))
						{
							if (data.gameplayTimeSeconds >= _targetConsistencyCheck)
							{
								break;
							}
							else if (data.gameplayTimeSeconds < _targetConsistencyCheck)
							{
								state.removeOldestConsistencyData();
							}
						}
						if (!data.isValid)
						{
							return false;
						}
						else if (data.gameplayTimeSeconds == _targetConsistencyCheck)
						{
							evt.consistencyData.emplace(state.playerId, data.hash);
						}
					}

					if (evt.consistencyData.size() > 0)
					{
						onConsistencyCheck(evt);
					}
					return true;
				}

				void checkConsistency()
				{
					while (tryPerformConsistencyCheck())
					{
						_targetConsistencyCheck += _options.FixedDeltaTimeSeconds;
					}
				}

				PlayerState& addPlayerState(const SessionId& sessionId, int playerId)
				{
					PlayerState state;
					state.playerId = playerId;
					state.sessionId = sessionId;
					auto it = _playerStates.begin();
					while (it != _playerStates.end())
					{
						if (it->playerId < state.playerId)
						{
							it++;
						}
					}
					it = _playerStates.insert(it, state);
					return *it;
				}
				void onPlayersInstallSnapshot(PlayersSnapshotInstallCommand& cmd)
				{
					_currentPlayerId = cmd.currentPlayerId;
					_playerStates.clear();
					for (auto& p : cmd.players)
					{
						addPlayerState(p.second, p.first);
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


							PlayerState& state = addPlayerState(cmd.playerSessionId, cmd.playerId);
							state.isLocal = (state.sessionId == client->sessionId());


							if (state.isLocal)
							{
								state.isSynchronized = true;
							}

							break;
						}
						case PlayersUpdateCommandType::Remove:
						{

							auto it = _playerStates.begin();
							while (it != _playerStates.end())
							{
								if (it->sessionId == cmd.playerSessionId)
								{
									_playerStates.erase(it);
								}
							}

							break;
						}
						}

						_writer->writeUpdatePlayersCommand(_currentFrame.currentTimeSeconds, cmd);

						_currentPlayersUpdateId = cmd.updateId;
					}
				}


				void onRequestSnapshot(const SessionId& origin)
				{
					Snapshot snapshot;
					this->onCreateSnapshot(snapshot);
					SnapshotDto dto;
					dto.gameplayTimeSeconds = snapshot.gameplayTimeSeconds;
					dto.content = snapshot.content;
					auto serializer = _serializer;
					_mesh->send(origin, "lockstep.installSnapshot", [dto, serializer](obytestream& stream)
						{
							serializer->serialize(stream, dto);
						}, PacketReliability::RELIABLE);
				}

				void requestSnapshot(const SessionId& target)
				{
					_mesh->send(target, "lockstep.requestSnapshot", [](obytestream& stream)
						{

						}, PacketReliability::RELIABLE);
				}

				void installSnapshot(const SessionId& origin, SnapshotDto& dto)
				{
					Snapshot snapshot;
					snapshot.gameplayTimeSeconds = dto.gameplayTimeSeconds;
					snapshot.content = dto.content;
					_currentFrame.currentTimeSeconds = snapshot.gameplayTimeSeconds;
					_currentFrame.validatedTimeSeconds = snapshot.gameplayTimeSeconds;

					onInstallSnapshot(snapshot);
					_writer->writeLoadSnapshotRecord(this->_currentFrame.currentTimeSeconds, snapshot.gameplayTimeSeconds, snapshot.content);
					for (auto& state : _playerStates)
					{
						if (!state.isLocal)
						{
							auto node = state._firstCommand;
							while (node != nullptr && node->command.gameplayTimeSeconds <= snapshot.gameplayTimeSeconds)
							{
								state._lastExecutedCommand = node;
								node = node->next;
							}
						}
					}
					_initialized = true;
					updateLatency(0);

				}

				bool isOnlyPlayerLocal()
				{
					for (auto& kvp : _playerStates)
					{

					}
				}

				bool canInitialize()
				{
					if (_playerStates.size() == 0)
					{
						return false;
					}
					for (auto& state : _playerStates)
					{
						if (!state.isSynchronized && !state.isLocal)
						{
							return false;
						}
					}
					return true;
				}

				void tryStartInitialize()
				{
					if (_initializing || _initialized)
					{
						return;
					}
					_initializing = true;

					SessionId target;
					Time time = 0;
					for (auto& state : _playerStates)
					{
						if (state.gameplayTimeSeconds > time && !state.isLocal)
						{
							time = state.gameplayTimeSeconds;
							target = state.sessionId;
						}
					}

					if (!target.isValid()) //Single player : we install a frame 0 empty snapshot.
					{
						_writer->header.playerId = 0;


						Snapshot snapshot;
						onInstallSnapshot(snapshot);

						_writer->writeLoadSnapshotRecord(0.0, 0.0, snapshot.content);
						_initialized = true;

						updateLatency(0);

					}
					else
					{
						_writer->header.playerId = getCurrentPlayerId();

						requestSnapshot(target);
					}


				}


			private:
				Time _timeSinceLastGameplayProgress = 0;
				Time _lastDeltaTimePerFrameSeconds = 0;
				bool _isPaused = true;
				bool _currentGameplayProgress = false;

				Frame _currentFrame;
				Time _lastPausedOn = 0;
				int _currentPlayersUpdateId = 0;
				int _currentPlayerId = -1;
				bool _initialized = false;
				bool _initializing = false;
				bool _started = false;



				LockstepOptions _options;

				std::vector<PlayersUpdateCommand> _pendingPlayersUpdateCommand;

				std::vector<PlayerState> _playerStates;

				std::shared_ptr<P2PMeshService> _mesh;
				std::weak_ptr<IClient>  _client;
				std::shared_ptr<Serializer> _serializer;
				std::shared_ptr<ILogger> _logger;

			};

			class LockstepApiImpl : public LockstepApi, public ::Stormancer::ClientAPI<LockstepApiImpl, LockstepReplayUploadService>
			{
				friend LockstepPlugin;
			public:
				LockstepApiImpl(std::shared_ptr<Users::UsersApi> users, std::shared_ptr<ILogger> logger);

				FrameDuration adjustTick(FrameDuration deltaSeconds, FrameDuration realDeltaSeconds) override;

				void tick(FrameDuration deltaSeconds, FrameDuration realDeltaSeconds) override;

				Time getCurrentTime() const override;

				Time getTargetTime() const override;

				int lastExecutedCommand() const override;

				Time getCommandTime() const override;

				FrameDuration getLatency() const override;

				bool isEnabled() const override;

				/// <summary>
				/// Pushes a command to the system. if frame is not specified, 
				/// </summary>
				/// <param name="buffer"></param>
				/// <param name="length"></param>
				/// <param name="frame"></param>
				int pushCommand(byte* buffer, int length) override;

				void loadReplayFile(byte* buffer, size_t length) override;

				pplx::task<bool> uploadPendingReplay(std::string pendingReplayFilePath) override;

				void endFrame() override;


				bool isPaused() const override;

				void pause(bool pause) override;

				std::vector<LockstepPlayer> getPlayers() const override;

				int getCurrentPlayerId() const override;

				void setReplayWriter(std::function<void(ReplayWriteEvent&)> replayWriter)  override;

				ReplayMode getReplayMode() override;

				bool trySetReplayInitialData(byte* buffer, size_t length, std::string& buildId) override;
				bool tryGetReplayInitialData(std::vector<byte>& initialData, std::string& buildId, std::string& gameId) override;

				/// <summary>
				/// Resets the lockstep system
				/// </summary>
				/// <remarks>
				/// Online automatically resets when players join a new game session, but in offline mode, reset must be called manually. 
				/// </remarks>
				void Reset() override;

				virtual ~LockstepApiImpl() {};
			private:
				void onSceneConnected(std::shared_ptr<details::ILockstepService> service);
				void onSceneDisconnected();

				std::shared_ptr<Stormancer::ILogger> _logger;


				std::shared_ptr<details::ILockstepService> _service;
				std::shared_ptr <details::ILockstepService> _offlineService;

				Subscription _onStepSubscription;
				Subscription _onEndFrameSubscription;
				Subscription _onPauseStateChangedSubscription;
				Subscription _onPlayerListChangedSubscription;
				Subscription _onConsistencyCheckSubscription;
				Subscription _onCreateSnapshotSubscription;
				Subscription _onInstallSnapshotSubscription;
				Subscription _onStartSubscription;

				std::function<void(ReplayWriteEvent&)> _replayWriter = [](auto _) {};


			};
		}

		static constexpr const char* PLUGIN_NAME = "Lockstep";
		static constexpr const char* PLUGIN_VERSION = "1.0.0";
		static constexpr const char* LOCKSTEP_HOST_METADATA = "stormancer.lockstep";

		details::LockstepApiImpl::LockstepApiImpl(std::shared_ptr<Users::UsersApi> users, std::shared_ptr<ILogger> logger)
			: ClientAPI< LockstepApiImpl, LockstepReplayUploadService>(users, "stormancer.plugins.lockstep.replays")
		{

			auto service = std::make_shared<details::OfflineLockstepService>(logger, _replayWriter);

			onSceneConnected(service);
		}

		FrameDuration details::LockstepApiImpl::adjustTick(FrameDuration deltaSeconds, FrameDuration realDeltaSeconds)
		{
			return _service->adjustTick(deltaSeconds, realDeltaSeconds);
		}

		void details::LockstepApiImpl::tick(FrameDuration deltaSeconds, FrameDuration realDeltaSeconds)
		{

			_service->tick(deltaSeconds, realDeltaSeconds);
		}

		void details::LockstepApiImpl::Reset()
		{
			auto service = std::make_shared<details::OfflineLockstepService>(_logger, _replayWriter);
			service->replayWriter = _replayWriter;
			onSceneConnected(service);
		}

		/// <summary>
		/// Gets the current lockstep time, in ms.
		/// </summary>
		/// <returns></returns>
		Time details::LockstepApiImpl::getCurrentTime() const
		{
			return _service->getCurrentTime();
		}
		Time details::LockstepApiImpl::getTargetTime() const
		{
			return _service->getTargetTime();
		}

		Time details::LockstepApiImpl::getCommandTime() const
		{
			return _service->getCommandTime();
		}
		FrameDuration details::LockstepApiImpl::getLatency() const
		{
			return _service->getLatency();
		}

		int details::LockstepApiImpl::lastExecutedCommand() const
		{
			return _service->lastExecutedCommand();
		}

		bool details::LockstepApiImpl::isEnabled() const
		{
			return _service != nullptr;
		}

		bool details::LockstepApiImpl::isPaused() const
		{
			return _service->isPaused();
		}

		void details::LockstepApiImpl::pause(bool pause)
		{
			return _service->pause(pause);
		}

		[[nodiscard]] int details::LockstepApiImpl::pushCommand(byte* buffer, int length)
		{
			if (length == 0)
			{
				return -1;
			}
			return _service->pushCommand(buffer, length);
		}

		void details::LockstepApiImpl::loadReplayFile(byte* buffer, size_t length)
		{
			//TODO load replay file.
			auto service = std::make_shared<ReplayLockstepService>(buffer, length);
			service->replayWriter = _replayWriter;

			onSceneConnected(service);

		}

		pplx::task<bool> details::LockstepApiImpl::uploadPendingReplay(std::string pendingReplayFilePath)
		{
			return this->getService().then([pendingReplayFilePath](std::shared_ptr<LockstepReplayUploadService> service)
				{
					return service->tryUploadReplay(pendingReplayFilePath);
				});
		}

		void  details::LockstepApiImpl::setReplayWriter(std::function<void(ReplayWriteEvent&)> replayWriter)
		{
			_replayWriter = replayWriter;
		}

		ReplayMode  details::LockstepApiImpl::getReplayMode()
		{
			return _service->getReplayMode();
		}

		bool  details::LockstepApiImpl::trySetReplayInitialData(byte* buffer, size_t length, std::string& buildId)
		{
			return _service->trySetReplayInitialData(buffer, length, buildId);
		}

		bool details::LockstepApiImpl::tryGetReplayInitialData(std::vector<byte>& initialData, std::string& buildId, std::string& gameId)
		{
			return _service->tryGetReplayInitialData(initialData, buildId, gameId);

		}

		void details::LockstepApiImpl::endFrame()
		{
			return _service->endFrame();
		}

		int details::LockstepApiImpl::getCurrentPlayerId() const
		{
			return _service->getCurrentPlayerId();
		}

		void details::LockstepApiImpl::onSceneConnected(std::shared_ptr<details::ILockstepService> service)
		{

			_service = service;
			_service->replayWriter = _replayWriter;
			_service->initialize();

			_onStepSubscription = service->onStep.subscribe([this](Frame& frame)
				{
					this->onStep(frame);
				});
			_onEndFrameSubscription = service->onEndFrame.subscribe([this](Frame& frame)
				{
					this->onEndFrame(frame);
				});
			_onPauseStateChangedSubscription = service->onPauseStateChanged.subscribe([this](PauseState paused) {
				this->onPauseStateChanged(paused);

				});
			_onPlayerListChangedSubscription = service->onPlayerListChanged.subscribe([this]() {
				this->onPlayerListChanged();

				});
			_onConsistencyCheckSubscription = service->onConsistencyCheck.subscribe([this](ConsistencyCheckEvent evt) {
				this->onConsistencyCheck(evt);
				});
			_onCreateSnapshotSubscription = service->onCreateSnapshot.subscribe([this](Snapshot& snapshot) {
				this->onCreateSnapshot(snapshot);
				});
			_onInstallSnapshotSubscription = service->onInstallSnapshot.subscribe([this](Snapshot& snapshot)
				{
					this->onInstallSnapshot(snapshot);
				});
			_onStartSubscription = service->onStart.subscribe([this]()
				{
					this->onStart();
				});
		}
		void details::LockstepApiImpl::onSceneDisconnected()
		{
			onSceneConnected(std::make_shared<details::OfflineLockstepService>(_logger, _replayWriter));
		}

		std::vector<LockstepPlayer> details::LockstepApiImpl::getPlayers() const
		{
			return _service->getPlayers();
		}
		PluginDescription LockstepPlugin::getDescription()
		{
			return PluginDescription(PLUGIN_NAME, PLUGIN_VERSION);
		}

		void LockstepPlugin::registerClientDependencies(ContainerBuilder& clientBuilder)
		{
			clientBuilder.registerDependency<details::LockstepApiImpl, Users::UsersApi, ILogger>().as<LockstepApi>().singleInstance();
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
				auto api = std::static_pointer_cast<::Stormancer::Gameplay::details::LockstepApiImpl>(scene->dependencyResolver().resolve<LockstepApi>());
				auto service = scene->dependencyResolver().resolve<details::LockstepService>();
				service->initialize(scene);
				api->onSceneConnected(service);
			}
		}
		void LockstepPlugin::sceneDisconnecting(std::shared_ptr<Scene> scene)
		{
			if (!scene->getHostMetadata(LOCKSTEP_HOST_METADATA).empty())
			{
				auto api = std::static_pointer_cast<details::LockstepApiImpl>(scene->dependencyResolver().resolve<LockstepApi>());
				api->onSceneDisconnected();
			}
		}

	}
}

MSGPACK_ADD_ENUM(Stormancer::Gameplay::details::PlayersUpdateCommandType)
#endif