using System;
using Rootborn.Core.Network;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Time
{
    /// <summary>
    /// 방의 시계 파일(&lt;슬롯&gt;/<see cref="SaveFileNames.RoomClock"/>)의 모양. MonoGame 판이 처음 쓰는 파일이다(Unity 판에는 없다).
    /// 필드 이름과 형은 바꾸지 않는다. 바꿀 일이 생기면 KnownVersion을 올린다.
    /// </summary>
    [Serializable]
    public sealed class RoomClockSaveData : IVersionedSave
    {
        public const int KnownVersion = 1;

        public int Version;
        public int Day = 1;
        public int PartIndex;
        public RoomClockParticipantSaveData[] Participants = Array.Empty<RoomClockParticipantSaveData>();

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }

    /// <summary>참가자 한 명이 그날 끝낸 때의 수와 하루 마감 여부. 방에 있는지는 저장하지 않는다.</summary>
    [Serializable]
    public sealed class RoomClockParticipantSaveData
    {
        public string ParticipantId;
        public int CompletedParts;
        public bool DayEnded;
    }

    /// <summary>방의 시계를 세이브 슬롯에 읽고 쓴다. 방(서버) 쪽이 쓴다.</summary>
    public static class RoomClockSaveFiles
    {
        /// <summary>
        /// 시계 파일이 있으면 그것, 없고 옛 공용 시간 파일(world-time/&lt;슬롯&gt;.json)이 있으면 그 날의 첫 때, 둘 다 없으면 첫날.
        /// 옛 파일의 때(아침·낮·저녁·밤 4칸)는 시간 포인트 칸과 맞지 않아 쓰지 않는다.
        /// </summary>
        public static RoomClock Load(ISaveStore store, string saveSlot, LifeCalendar calendar, RoomClockSettings settings = null)
        {
            if (store == null || string.IsNullOrEmpty(saveSlot)) return new RoomClock(calendar, settings);
            if (store.TryRead(saveSlot, SaveFileNames.RoomClock, out RoomClockSaveData data)) return RoomClock.FromSaveData(data, calendar, settings);
            if (store.TryRead(SaveFileNames.WorldTimeSlot, SaveFileNames.WorldTime(saveSlot), out WorldTimeSaveData old))
                return new RoomClock(calendar, settings, SharedWorldTime.FromSave(old).Day);
            return new RoomClock(calendar, settings);
        }

        public static void Save(ISaveStore store, string saveSlot, RoomClock clock)
        {
            if (store == null || clock == null || string.IsNullOrEmpty(saveSlot)) return;
            store.Write(saveSlot, SaveFileNames.RoomClock, clock.ToSaveData());
        }
    }
}
