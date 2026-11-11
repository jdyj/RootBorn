using System;
using System.Collections.Generic;
using Rootborn.Core.Save;

namespace Rootborn.Core.StudentLife
{
    /// <summary>세이브 파일(daily-event-progress*.json) 모양. 필드 이름을 바꾸지 않는다. Version은 Unity 판 파일에 없다(0으로 읽힘).</summary>
    [Serializable]
    public sealed class DailyEventProgressSaveData : IVersionedSave
    {
        public const int KnownVersion = 1;

        public int Version;
        public string SaveSlot;
        public string PlayerId;
        public Record[] Records = Array.Empty<Record>();

        [Serializable]
        public sealed class Record
        {
            public string EventId;
            public string State;
            public int FirstSeenDay;
            public int LastSeenDay;
            public int DueDay;
            public int DueWorldTime;
            public string SelectedChoiceId;
            public string CompletionRequestId;
            public int RepeatCount;
            public int CooldownUntilDay;
            public string[] ResultSummaryLogIds = Array.Empty<string>();
        }

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }

    /// <summary>이벤트 하나의 상태 기록.</summary>
    public sealed class DailyEventRecord
    {
        private readonly List<string> _resultSummaryLogIds = new List<string>();

        public DailyEventRecord(string eventId)
        {
            EventId = string.IsNullOrEmpty(eventId) ? string.Empty : eventId;
            State = DailyEventStates.Unseen;
            SelectedChoiceId = string.Empty;
            CompletionRequestId = string.Empty;
        }

        public string EventId { get; }
        public string State { get; private set; }
        public int FirstSeenDay { get; private set; }
        public int LastSeenDay { get; private set; }
        public int DueDay { get; private set; }
        public int DueWorldTime { get; private set; }
        public string SelectedChoiceId { get; private set; }
        public string CompletionRequestId { get; private set; }
        public int RepeatCount { get; private set; }
        public int CooldownUntilDay { get; private set; }
        public string[] ResultSummaryLogIds => _resultSummaryLogIds.ToArray();

        public void MarkAvailable(int day)
        {
            Seen(day);
            State = DailyEventStates.Available;
        }

        public void MarkDeferred(int currentDay, int dueDay)
        {
            int safeDay = Seen(currentDay);
            DueDay = Math.Max(safeDay, dueDay);
            State = DailyEventStates.Deferred;
        }

        public void MarkCompleted(string choiceId, string requestId, int day, string[] logs)
        {
            Seen(day);
            SelectedChoiceId = string.IsNullOrEmpty(choiceId) ? string.Empty : choiceId;
            CompletionRequestId = string.IsNullOrEmpty(requestId) ? string.Empty : requestId;
            State = DailyEventStates.Completed;
            RepeatCount++;
            _resultSummaryLogIds.Clear();
            AddLogs(logs);
        }

        public void MarkDeclined(int day)
        {
            Seen(day);
            State = DailyEventStates.Declined;
        }

        public void MarkExpired(int day, int dueWorldTime)
        {
            DueDay = Seen(day);
            DueWorldTime = Math.Max(0, dueWorldTime);
            State = DailyEventStates.Expired;
        }

        public void MarkCooldown(int untilDay)
        {
            CooldownUntilDay = Math.Max(0, untilDay);
            State = DailyEventStates.Cooldown;
        }

        public bool HasCompletionRequest(string requestId) => !string.IsNullOrEmpty(requestId) && CompletionRequestId == requestId;

        public DailyEventProgressSaveData.Record ToSaveData()
        {
            return new DailyEventProgressSaveData.Record
            {
                EventId = EventId,
                State = State,
                FirstSeenDay = FirstSeenDay,
                LastSeenDay = LastSeenDay,
                DueDay = DueDay,
                DueWorldTime = DueWorldTime,
                SelectedChoiceId = SelectedChoiceId,
                CompletionRequestId = CompletionRequestId,
                RepeatCount = RepeatCount,
                CooldownUntilDay = CooldownUntilDay,
                ResultSummaryLogIds = _resultSummaryLogIds.ToArray(),
            };
        }

        public static DailyEventRecord FromSaveData(DailyEventProgressSaveData.Record saveData)
        {
            var record = new DailyEventRecord(saveData != null ? saveData.EventId : string.Empty);
            if (saveData == null) return record;
            record.State = string.IsNullOrEmpty(saveData.State) ? DailyEventStates.Unseen : saveData.State;
            record.FirstSeenDay = Math.Max(0, saveData.FirstSeenDay);
            record.LastSeenDay = Math.Max(0, saveData.LastSeenDay);
            record.DueDay = Math.Max(0, saveData.DueDay);
            record.DueWorldTime = Math.Max(0, saveData.DueWorldTime);
            record.SelectedChoiceId = string.IsNullOrEmpty(saveData.SelectedChoiceId) ? string.Empty : saveData.SelectedChoiceId;
            record.CompletionRequestId = string.IsNullOrEmpty(saveData.CompletionRequestId) ? string.Empty : saveData.CompletionRequestId;
            record.RepeatCount = Math.Max(0, saveData.RepeatCount);
            record.CooldownUntilDay = Math.Max(0, saveData.CooldownUntilDay);
            record.AddLogs(saveData.ResultSummaryLogIds);
            return record;
        }

        /// <summary>처음 본 날과 마지막 본 날을 적고, 1 이상으로 맞춘 날을 돌려준다.</summary>
        private int Seen(int day)
        {
            int safeDay = Math.Max(1, day);
            if (FirstSeenDay <= 0) FirstSeenDay = safeDay;
            LastSeenDay = safeDay;
            return safeDay;
        }

        private void AddLogs(string[] logs)
        {
            if (logs == null) return;
            for (int i = 0; i < logs.Length; i++)
                if (!string.IsNullOrEmpty(logs[i]) && !_resultSummaryLogIds.Contains(logs[i])) _resultSummaryLogIds.Add(logs[i]);
        }
    }

    /// <summary>한 플레이어의 하루 이벤트 기록들. 없는 기록은 처음 물을 때 만든다.</summary>
    public sealed class DailyEventProgress
    {
        private readonly Dictionary<string, DailyEventRecord> _records = new Dictionary<string, DailyEventRecord>(StringComparer.Ordinal);

        public DailyEventProgress(string saveSlot, string playerId)
        {
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
        }

        public string SaveSlot { get; }
        public string PlayerId { get; }

        public DailyEventRecord GetRecord(string eventId)
        {
            string key = string.IsNullOrEmpty(eventId) ? string.Empty : eventId;
            if (!_records.TryGetValue(key, out DailyEventRecord record))
            {
                record = new DailyEventRecord(key);
                _records[key] = record;
            }
            return record;
        }

        public bool HasCompletionRequest(string requestId)
        {
            if (string.IsNullOrEmpty(requestId)) return false;
            foreach (var pair in _records)
                if (pair.Value != null && pair.Value.HasCompletionRequest(requestId)) return true;
            return false;
        }

        public DailyEventProgressSaveData ToSaveData()
        {
            var records = new DailyEventProgressSaveData.Record[_records.Count];
            int index = 0;
            foreach (var pair in _records) records[index++] = pair.Value.ToSaveData();
            return new DailyEventProgressSaveData { SaveSlot = SaveSlot, PlayerId = PlayerId, Records = records };
        }

        public static DailyEventProgress FromSaveData(DailyEventProgressSaveData saveData)
        {
            var progress = new DailyEventProgress(saveData != null ? saveData.SaveSlot : "default", saveData != null ? saveData.PlayerId : "player");
            if (saveData == null || saveData.Records == null) return progress;
            for (int i = 0; i < saveData.Records.Length; i++)
            {
                DailyEventRecord record = DailyEventRecord.FromSaveData(saveData.Records[i]);
                if (!string.IsNullOrEmpty(record.EventId)) progress._records[record.EventId] = record;
            }
            return progress;
        }
    }

    /// <summary>하루 이벤트 진행 파일을 읽고 쓴다(원본 DailyEventProgressPersistence). 이름은 SaveFileNames.DailyEvent.</summary>
    public static class DailyEventSaveFiles
    {
        /// <summary>파일이 없으면 빈 기록. 파일의 슬롯·플레이어가 달라도(복사한 슬롯) 지금 슬롯·플레이어로 읽는다.</summary>
        public static DailyEventProgress LoadOrCreate(ISaveStore store, string saveSlot, string playerId)
        {
            string slot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            string player = string.IsNullOrEmpty(playerId) ? SaveFileNames.DefaultPlayerId : playerId;
            if (store == null || !store.TryRead(slot, SaveFileNames.DailyEvent(player), out DailyEventProgressSaveData data)) return new DailyEventProgress(slot, player);
            data.SaveSlot = slot;
            data.PlayerId = player;
            return DailyEventProgress.FromSaveData(data);
        }

        public static void Save(ISaveStore store, DailyEventProgress progress)
        {
            if (store == null || progress == null) return;
            store.Write(progress.SaveSlot, SaveFileNames.DailyEvent(progress.PlayerId), progress.ToSaveData());
        }
    }
}
