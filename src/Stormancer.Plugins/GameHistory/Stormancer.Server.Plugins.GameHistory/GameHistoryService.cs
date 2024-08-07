// MIT License
//
// Copyright (c) 2019 Stormancer
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

using Nest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Database;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameHistory
{
    class GameHistoryService
    {
      
        public GameHistoryService(
            GameHistoryStorage storage)
        {
           
            _storage = storage;
        }

        private readonly GameHistoryStorage _storage;

        public Task AddToHistoryAsync(Guid uid, IEnumerable<UserRecord> participants, JsonDocument customData,DateTime startTimeUtc, DateTime endTimeUtc)
        {
            return _storage.AddHistoryRecordAsync(new GameHistoryRecord { Id = uid, CustomData = customData, Participants = participants.ToList(), CreatedOn = startTimeUtc, CompletedOn = endTimeUtc  });
        }

        public Task<GameHistoryRecord?> GetGameHistory(Guid gameId)
        {
            return _storage.GetGameHistoryAsync(gameId);
           
        }

        public Task<IEnumerable<GameHistoryRecord>> GetPlayerHistoryAsync(string playerId,int skip, int count)
        {
            return _storage.GetLatestHistoryRecordAsync(Guid.Parse(playerId), skip, count);
        }

        public Task UpdateGameHistoryRecordAsync(GameHistoryRecord historyRecord)
        {
            return _storage.UpdateGameHistoryRecordAsync(historyRecord);
        }
    }
}
