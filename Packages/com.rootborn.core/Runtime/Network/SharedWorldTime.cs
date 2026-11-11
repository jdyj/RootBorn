using System.Collections.Generic;
using Rootborn.Core.Save;

namespace Rootborn.Core.Network
{
    /// <summary>같이 쓰는 때(Unity 판 WorldTimeOfDay와 같은 번호). 대표 시각은 <see cref="WorldTimeHours"/>.</summary>
    public static class WorldPartOfDay
    {
        public const int Morning = 0;
        public const int Afternoon = 1;
        public const int Evening = 2;
        public const int Night = 3;
        public const int Count = 4;
    }

    /// <summary>하루 단계(Unity 판 WorldSchedulePhase와 같은 번호).</summary>
    public static class WorldSchedulePhase
    {
        public const int DayStart = 0;
        public const int Activity = 1;
        public const int DayEndReady = 2;
        public const int Count = 3;
    }

    /// <summary>
    /// 같이 쓰는 날짜·때·하루 단계. 서버가 하나를 가진다. Unity 판 Assets/Scripts/Network/Time/NetworkWorldTimeState.cs의 규칙:
    /// - 하루 끝 준비: 지금 있는 사람이 모두 준비하면 다음 날 아침(DayStart), 준비는 비운다. 한 명이라도 준비하면 단계는 DayEndReady.
    /// - 때 넘기기: 다음 때(단계 Activity). 밤 다음은 다음 날.
    /// Unity 판과 달라진 동작: 준비는 지금 있는 사람만 센다(나간 사람의 준비는 지운다). 사람이 나가거나 들어오면
    /// <see cref="PlayersChanged"/>로 다시 본다 — 남은 사람이 모두 준비했으면 그때 넘어간다.
    /// 플레이어는 안정된 신원 문자열로 가리킨다.
    /// </summary>
    public sealed class SharedWorldTime
    {
        private readonly HashSet<string> _ready = new HashSet<string>(System.StringComparer.Ordinal);

        public int Day { get; private set; } = 1;
        public int PartOfDay { get; private set; } = WorldPartOfDay.Morning;
        public int Phase { get; private set; } = WorldSchedulePhase.DayStart;
        public int ReadyCount => _ready.Count;
        public int WeekdayIndex => (Day - 1) % 7;

        public bool IsReady(string playerId) => playerId != null && _ready.Contains(playerId);

        /// <param name="present">지금 들어와 있는 사람들(서버 자신이 플레이어면 포함).</param>
        /// <returns>이 준비로 다음 날이 되었으면 true.</returns>
        public bool MarkReady(string playerId, IReadOnlyCollection<string> present)
        {
            if (string.IsNullOrEmpty(playerId) || !Contains(present, playerId)) return false;
            _ready.Add(playerId);
            Phase = WorldSchedulePhase.DayEndReady;
            return AdvanceIfAllReady(present);
        }

        /// <summary>사람이 들어오거나 나간 뒤 부른다. 나간 사람의 준비를 지우고, 남은 사람이 모두 준비했으면 다음 날로 간다.</summary>
        public bool PlayersChanged(IReadOnlyCollection<string> present)
        {
            _ready.RemoveWhere(id => !Contains(present, id));
            return AdvanceIfAllReady(present);
        }

        /// <returns>밤에서 넘겨 다음 날이 되었으면 true.</returns>
        public bool AdvancePartOfDay()
        {
            int next = PartOfDay + 1;
            if (next >= WorldPartOfDay.Count)
            {
                NextDay();
                return true;
            }

            PartOfDay = next;
            Phase = WorldSchedulePhase.Activity;
            return false;
        }

        public WorldTimeSaveData ToSave()
            => new WorldTimeSaveData { Version = WorldTimeSaveData.KnownVersion, CurrentDay = Day, TimeOfDay = PartOfDay, SchedulePhase = Phase };

        /// <summary>Unity 판 LoadServerSnapshot처럼 날은 1 이상, 때·단계는 범위 안으로 자른다. null은 첫날.</summary>
        public static SharedWorldTime FromSave(WorldTimeSaveData data)
        {
            var time = new SharedWorldTime();
            if (data == null) return time;
            time.Day = data.CurrentDay < 1 ? 1 : data.CurrentDay;
            time.PartOfDay = Clamp(data.TimeOfDay, WorldPartOfDay.Count - 1);
            time.Phase = Clamp(data.SchedulePhase, WorldSchedulePhase.Count - 1);
            return time;
        }

        public static SharedWorldTime Load(ISaveStore store, string saveSlot)
        {
            if (store == null) return new SharedWorldTime();
            return store.TryRead(SaveFileNames.WorldTimeSlot, SaveFileNames.WorldTime(saveSlot), out WorldTimeSaveData data)
                ? FromSave(data)
                : new SharedWorldTime();
        }

        public static void Save(ISaveStore store, string saveSlot, SharedWorldTime time)
        {
            if (store == null || time == null) return;
            store.Write(SaveFileNames.WorldTimeSlot, SaveFileNames.WorldTime(saveSlot), time.ToSave());
        }

        private bool AdvanceIfAllReady(IReadOnlyCollection<string> present)
        {
            if (present == null || present.Count == 0) return false;
            foreach (var id in present)
                if (!_ready.Contains(id)) return false;
            NextDay();
            return true;
        }

        private void NextDay()
        {
            Day = Day + 1 < 1 ? 1 : Day + 1;
            PartOfDay = WorldPartOfDay.Morning;
            Phase = WorldSchedulePhase.DayStart;
            _ready.Clear();
        }

        private static bool Contains(IReadOnlyCollection<string> present, string id)
        {
            if (present == null) return false;
            foreach (var p in present)
                if (p == id) return true;
            return false;
        }

        private static int Clamp(int value, int max) => value < 0 ? 0 : value > max ? max : value;
    }
}
