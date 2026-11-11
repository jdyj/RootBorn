using System;

namespace Rootborn.Core.Save
{
    /// <summary>
    /// 같이 쓰는 시간 파일(world-time/&lt;슬롯&gt;.json)의 모양. 필드 이름은 Unity 판 NetworkWorldTimeState.WorldTimeSaveData와 같다.
    /// Version은 MonoGame 판이 더한 필드다(Unity 판 파일은 0). 서버만 쓴다.
    /// </summary>
    [Serializable]
    public sealed class WorldTimeSaveData : IVersionedSave
    {
        public const int KnownVersion = 1;

        public int Version;
        public int CurrentDay = 1;
        public int TimeOfDay;
        public int SchedulePhase;

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }
}
