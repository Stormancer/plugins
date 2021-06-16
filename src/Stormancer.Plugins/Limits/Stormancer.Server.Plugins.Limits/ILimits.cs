using Stormancer.Core;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Limits
{
    /// <summary>
    /// provides data about the limits enforced by the <see cref="Stormancer.Server.Plugins.Limits"/> plugin.
    /// </summary>
    public interface ILimits
    {
        /// <summary>
        /// Returns statistics about user limits.
        /// </summary>
        /// <returns></returns>
        UserConnectionLimitStatus GetUserLimitsStatus();

    }

    /// <summary>
    /// Informations about the current status of the user connection limits enforced on the server.
    /// </summary>
    public record UserConnectionLimitStatus
    {
        /// <summary>
        /// Maximum number of concurrent users in the server.
        /// </summary>
        public int MaxConcurrentPlayers { get; init; }

        /// <summary>
        /// Current number of concurrent users in the server.
        /// </summary>
        public int CurrentConcurrentUsers { get; init; }

        /// <summary>
        /// Number of currently reserver slots for disconnected players.
        /// </summary>
        public int ReservedSlots { get; init; }

        /// <summary>
        /// Maximum queue length.
        /// </summary>
        public int MaxQueueLength { get; init; }

        /// <summary>
        /// Current number of users in the waiting queue.
        /// </summary>
        public int CurrentUsersInQueue { get; init; }

        /// <summary>
        /// Current entry speed, defined as average time in seconds between 2 connections, using the last 64 entries.
        /// </summary>
        public double AvgEntryInterval { get; init; }
    }

    internal class Limits : ILimits, IConfigurationChangedEventHandler
    {
        private class WaitingQueueRecord
        {
            public DateTime EntryTime { get; set; } = DateTime.UtcNow;
            public TaskCompletionSource TaskCompletionSource { get; set; } = new TaskCompletionSource();
            public IScenePeerClient Peer { get; set; } = default!;
            public bool IsDirty { get; set; }
        }

        Dictionary<string, DateTime> _reservedSlots = new Dictionary<string, DateTime>();


        private int _connectedUsers = 0;


        private object syncRoot = new object();

        private readonly IConfiguration configService;
        private readonly ISerializer serializer;

        public LimitsConfiguration Config { get; private set; }

        public Limits(IConfiguration config, ISerializer serializer)
        {
            this.configService = config;
            this.serializer = serializer;
            OnConfigurationChanged();

            Debug.Assert(Config != null);
        }
        public void OnConfigurationChanged()
        {
            Config = configService.GetValue("limits", new LimitsConfiguration());
        }


        internal async Task<bool> WaitForEntryAsync(IScenePeerClient peer, string? userId, CancellationToken cancellationToken)
        {
            Task task;
            lock (syncRoot)
            {
                if (Config.connections.max < 0)
                {
                    _connectedUsers++;
                    return true;
                }
                else if (userId != null && _reservedSlots.Remove(userId))
                {
                    _connectedUsers++;
                    return true;
                }
                else if (_reservedSlots.Count + _connectedUsers + _waitQueue.Count < Config.connections.max)
                {
                    _connectedUsers++;
                    return true;
                }
                else if (_reservedSlots.Count + _connectedUsers + _waitQueue.Count < Config.connections.max + Config.connections.queue)
                {
                    task = WaitInQueueAsync(peer, cancellationToken);
                }
                else
                {
                    return false;
                }

            }
            await task.ConfigureAwait(false);
            return true;



        }

        public async Task RunQueueAsync(CancellationToken cancellationToken)
        {
            using var timer = new Timer(TimerCallback, null, 1000, 1000);

            await cancellationToken.WaitHandle.WaitOneAsync();
        }


        private void TimerCallback(object? state)
        {
            List<Task> sendTasks;
            lock (syncRoot)
            {
                var slotsToDelete = new List<string>();
                foreach (var entry in _reservedSlots)
                {
                    if (entry.Value < DateTime.UtcNow)
                    {
                        slotsToDelete.Add(entry.Key);
                    }
                }

                foreach (var slotId in slotsToDelete)
                {
                    _reservedSlots.Remove(slotId);
                }
                bool connectedPeers = false;
                while (_waitQueue.Count > 0 && _reservedSlots.Count + _connectedUsers < Config.connections.max)
                {
                    var first = _waitQueue.First;
                    Debug.Assert(first != null);
                    _waitQueue.RemoveFirst();
                    _connectedUsers++;
                    connectedPeers = true;
                    first.Value.TaskCompletionSource.SetResult();

                    _lastEntryTimes[_nextEntryTimeOffset] = DateTime.UtcNow;
                    _lastEntryTimeOffset = _nextEntryTimeOffset;
                    _nextEntryTimeOffset = (_nextEntryTimeOffset + 1) % _lastEntryTimes.Length;
                    if (_lastEntryTimeOffset == _firstEntryTimesOffset)
                    {
                        _firstEntryTimesOffset = (_firstEntryTimesOffset + 1) % _lastEntryTimes.Length;
                    }
                }
                if (connectedPeers)
                {
                    foreach (var record in _waitQueue)
                    {
                        record.IsDirty = true;
                    }
                }
                var rank = 1;
                sendTasks = _waitQueue
                    .Select(r => (rank++, r))
                    .Where(r => r.Item2.IsDirty)
                    .Select(async t =>
                    {
                        t.r.IsDirty = false;
                        await t.r.Peer.Send("Queue.UpdateRank", s => serializer.Serialize(rank, s), PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE_SEQUENCED);
                    })
                    .ToList();


            }

            

        }

        internal void Logout(string sessionId, string? userId, DisconnectionReason disconnectionReason)
        {
            lock (syncRoot)
            {
                _connectedUsers--;
                if (userId != null)
                {
                    ReserveSlot(userId, disconnectionReason);
                }

                RemoveFromQueue(sessionId);
            }
        }

        LinkedList<WaitingQueueRecord> _waitQueue = new LinkedList<WaitingQueueRecord>();
        private DateTime[] _lastEntryTimes = new DateTime[64];
        private int _lastEntryTimeOffset;
        private int _nextEntryTimeOffset = 0;
        private int _firstEntryTimesOffset = 0;

        public void RemoveFromQueue(string sessionId)
        {
            lock (syncRoot)
            {
                LinkedListNode<WaitingQueueRecord>? current = _waitQueue.First;
                bool found = false;
                while (current != null)
                {
                    if (current.Value.Peer.SessionId == sessionId)
                    {
                        _waitQueue.Remove(current);

                        current.Value.TaskCompletionSource.SetCanceled();
                        found = true;
                    }
                   
                        
                    current = current.Next;
                    if(found && current!=null)
                    {
                        current.Value.IsDirty = true;
                    }
                    
                }
            }

        }

        private Task WaitInQueueAsync(IScenePeerClient peer, CancellationToken cancellationToken)
        {

            using var registration = cancellationToken.Register(() => RemoveFromQueue(peer.SessionId));
            var record = new WaitingQueueRecord { Peer = peer, IsDirty = true };

            _waitQueue.AddLast(record);

            return record.TaskCompletionSource.Task;

        }


        private void ReserveSlot(string userId, DisconnectionReason disconnectionReason)
        {
            var delay = GetSlotExpirationDelay(disconnectionReason);
            if (delay > 0)
            {
                _reservedSlots[userId] = DateTime.UtcNow + TimeSpan.FromSeconds(delay);
            }
        }

        private int GetSlotExpirationDelay(DisconnectionReason disconnectionReason)
        {
            switch (disconnectionReason)
            {
                case DisconnectionReason.ClientDisconnected:
                    return Config.connections.slots.disconnected;
                case DisconnectionReason.ConnectionLoss:
                    return Config.connections.slots.connectionLost;
                default:
                    return 0;
            }

        }

        public UserConnectionLimitStatus GetUserLimitsStatus()
        {
            lock (syncRoot)
            {
                return new UserConnectionLimitStatus
                {

                    CurrentConcurrentUsers = _connectedUsers,
                    CurrentUsersInQueue = _waitQueue.Count,
                    ReservedSlots = _reservedSlots.Count,
                    MaxConcurrentPlayers = Config.connections.max,
                    MaxQueueLength = Config.connections.queue,
                    AvgEntryInterval = ComputeQueueSpeed()
                };

            }
        }

        private double ComputeQueueSpeed()
        {
            //No users ever in queue: Cannot compute
            if (_firstEntryTimesOffset == _nextEntryTimeOffset)
            {
                return 0;
            }
            //1 item in queue.
            else if (_nextEntryTimeOffset - _firstEntryTimesOffset == 1)
            {
                return 0;
            }
            else
            {
                var totalInterval = (_lastEntryTimes[_lastEntryTimeOffset] - _lastEntryTimes[_firstEntryTimesOffset]).TotalSeconds;
                var count = _lastEntryTimeOffset >= _firstEntryTimesOffset ? _lastEntryTimeOffset - _firstEntryTimesOffset + 1 : _lastEntryTimeOffset - _firstEntryTimesOffset + _lastEntryTimes.Length + 1;
                return totalInterval / count;
            }



        }
    }
}