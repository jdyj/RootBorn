using System;
using Rootborn.Core.Inventory;
using Rootborn.Core.Quests;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Core.WorldState;

namespace Rootborn.Core.Careers
{
    /// <summary>
    /// 성인 직업 시작 때 한 번 쓰는 스냅샷 파일(adult-career-snapshot-*.json)의 모양.
    /// 필드 이름과 형은 Unity 판 Assets/Scripts/Game/Careers/AdultCareerSnapshot.cs와 같다.
    /// FurnitureUnlocks(가구 해금)는 Core에 모양이 없어 아직 넣지 않았다. 읽을 때 버려지며,
    /// 고정 파일 테스트(SaveShapeCoverage)의 허용 목록에 이유와 함께 있다.
    /// Version은 Unity 판처럼 처음부터 2다. Unity 판의 버전 1 → 2 바꾸기는 아직 옮기지 않았다.
    /// </summary>
    [Serializable]
    public sealed class AdultCareerSnapshotSaveData : IVersionedSave
    {
        /// <summary>이 프로그램이 아는 가장 높은 스냅샷 버전.</summary>
        public const int KnownVersion = 2;

        public int Version = KnownVersion;
        public AdultCareerProgressSaveData Adult;
        public StudentLifeProgressSaveData Student;
        public PlayerInventorySaveData Inventory;
        public WorldStateProgressSaveData World;
        public QuestChainLogSaveData QuestChains;

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }
}
