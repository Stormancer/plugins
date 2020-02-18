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

using MsgPack.Serialization;
using Newtonsoft.Json.Linq;
using System;

namespace Stormancer.Server.Plugins.Leaderboards
{
    public class ScoreRecordBase
    {
        public ScoreRecordBase()
        {
        }

        public ScoreRecordBase(ScoreDtoBase dto)
        {
            Id = dto.Id;
            Scores = dto.Scores;
            CreatedOn = dto.CreatedOn;
        }

        public string Id { get; set; }

        public string LeaderboardName { get; set; }

        public JObject Scores { get; set; }

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    }

    public class ScoreRecord : ScoreRecordBase
    {
        public ScoreRecord()
        {
            Document = new JObject();
        }

        public ScoreRecord(ScoreDto dto)
            : base(dto)
        {
            Document = JObject.Parse(dto.Document);
        }

        public JObject Document { get; set; }
    }

    public class ScoreDtoBase
    {
        public ScoreDtoBase()
        {
        }

        public ScoreDtoBase(ScoreRecordBase record)
        {
            Id = record.Id;
            Scores = record.Scores;
            CreatedOn = record.CreatedOn;
        }

        [MessagePackMember(0)]
        public string Id { get; set; }

        [MessagePackMember(1)]
        public JObject Scores { get; set; }

        [MessagePackMember(2), MessagePackDateTimeMember(DateTimeConversionMethod = DateTimeMemberConversionMethod.UnixEpoc)]
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    }

    public class ScoreDto : ScoreDtoBase
    {
        public ScoreDto()
        {
            _document = new JObject();
        }

        public ScoreDto(ScoreRecord record)
            : base(record)
        {
            _document = record.Document;
        }

        [MessagePackMember(3)]
        public string Document
        {
            get
            {
                return _document.ToString();
            }
            set
            {
                _document = JObject.Parse(value);
            }
        }

        [MessagePackIgnore]
        private JObject _document;
    }

    public class ScoreRecord<T> : ScoreRecordBase
    {
        public ScoreRecord()
        {
        }

        public ScoreRecord(ScoreRecord record)
        {
            Id = record.Id;
            Scores = record.Scores;
            CreatedOn = record.CreatedOn;
        }

        public ScoreRecord(ScoreDto<T> dto)
            : base(dto)
        {
            Document = dto.Document;
        }

        public T Document { get; set; }
    }

    public class ScoreDto<T> : ScoreDtoBase
    {
        public ScoreDto()
        {
        }

        public ScoreDto(ScoreDto dto)
        {
            Id = dto.Id;
            Scores = dto.Scores;
            CreatedOn = dto.CreatedOn;
        }

        public ScoreDto(ScoreRecord<T> record)
            : base(record)
        {
            Document = record.Document;
        }

        [MessagePackMember(3)]
        public T Document { get; set; }
    }
}
