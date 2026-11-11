using System;
using Rootborn.Core.Network;
using Rootborn.Core.Save;

namespace Rootborn.Core.Time
{
    /// <summary>
    /// 방의 시계 파일(&lt;슬롯&gt;/<see cref="SaveFileNames.RoomClock"/>)의 모양. MonoGame 판이 처음 쓰는 파일이다(Unity 판에는 없다).
    /// 필드 이름과 형은 바꾸지 않는다. 바꿀 일이 생기면 KnownVersion을 올린다.
    /// 형식 1: (날, 몇 번째 때)와 참가자별 끝낸 때·하루 마감(멀티플레이 설계 2.1절). 형식 2(흐르는 시간 4단계): (날, 분)과 참가자별 자는 중.
    /// 형식 1의 파일은 그 날의 하루 시작으로 읽는다(<see cref="RoomClock.FromSaveData"/>). 형식 1의 필드는 읽기 위해 남긴다.
    /// </summary>
    [Serializable]
    public sealed class RoomClockSaveData : IVersionedSave
    {
        public const int KnownVersion = 2;

        public int Version;
        public int Day = 1;

        /// <summary>형식 1의 몇 번째 때. 형식 2는 쓰지 않는다.</summary>
        public int PartIndex;

        /// <summary>형식 2: 그날 0시부터 센 분.</summary>
        public int Minutes;

        public RoomClockParticipantSaveData[] Participants = Array.Empty<RoomClockParticipantSaveData>();

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }

    /// <summary>참가자 한 명의 그날 상태. 방에 있는지는 저장하지 않는다. 형식 1은 끝낸 때·마감, 형식 2는 자는 중.</summary>
    [Serializable]
    public sealed class RoomClockParticipantSaveData
    {
        public string ParticipantId;

        /// <summary>형식 1. 형식 2는 쓰지 않는다.</summary>
        public int CompletedParts;

        /// <summary>형식 1. 형식 2는 쓰지 않는다.</summary>
        public bool DayEnded;

        /// <summary>형식 2: 오늘 잠들었다.</summary>
        public bool Asleep;
    }

    /// <summary>방의 시계를 세이브 슬롯에 읽고 쓴다. 방(서버) 쪽이 쓴다.</summary>
    public static class RoomClockSaveFiles
    {
        /// <summary>
        /// 시계 파일이 있으면 그것, 없고 옛 공용 시간 파일(world-time/&lt;슬롯&gt;.json)이 있으면 그 날의 하루 시작, 둘 다 없으면 첫날.
        /// 옛 파일의 때(아침·낮·저녁·밤 4칸)는 쓰지 않는다.
        /// </summary>
        public static RoomClock Load(ISaveStore store, string saveSlot, TimeRules rules)
        {
            if (store == null || string.IsNullOrEmpty(saveSlot)) return new RoomClock(rules);
            if (store.TryRead(saveSlot, SaveFileNames.RoomClock, out RoomClockSaveData data)) return RoomClock.FromSaveData(data, rules);
            if (store.TryRead(SaveFileNames.WorldTimeSlot, SaveFileNames.WorldTime(saveSlot), out WorldTimeSaveData old))
                return new RoomClock(rules, SharedWorldTime.FromSave(old).Day);
            return new RoomClock(rules);
        }

        public static void Save(ISaveStore store, string saveSlot, RoomClock clock)
        {
            if (store == null || clock == null || string.IsNullOrEmpty(saveSlot)) return;
            store.Write(saveSlot, SaveFileNames.RoomClock, clock.ToSaveData());
        }
    }
}
