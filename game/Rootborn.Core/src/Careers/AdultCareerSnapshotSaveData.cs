using System;
using Rootborn.Core.Inventory;
using Rootborn.Core.Placement;
using Rootborn.Core.Quests;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Core.WorldState;

namespace Rootborn.Core.Careers
{
    /// <summary>
    /// 성인 직업 시작 때 한 번 쓰는 스냅샷 파일(adult-career-snapshot-*.json)의 모양.
    /// 필드 이름과 형은 Unity 판 Assets/Scripts/Game/Careers/AdultCareerSnapshot.cs와 같다.
    /// 스냅샷이 있으면 가구 해금의 원본은 FurnitureUnlocks다(독립 해금 파일보다 먼저 본다).
    /// Version은 Unity 판처럼 처음부터 2다. Unity 판의 버전 1 → 2 바꾸기는 아직 옮기지 않았다.
    /// 버전 3: 지갑(Wallet)을 더했다. 돈은 가방(Inventory)에 두지 않는다. 버전 2 이하에는 지갑이 없고 돈이 가방에 있다 — 읽을 때 지갑으로 옮긴다.
    /// 버전 3을 모르는 프로그램은 이 파일을 읽지 않는다(돈이 사라진 것으로 읽지 않게).
    /// </summary>
    [Serializable]
    public sealed class AdultCareerSnapshotSaveData : IVersionedSave
    {
        /// <summary>이 프로그램이 아는 가장 높은 스냅샷 버전.</summary>
        public const int KnownVersion = 3;

        public int Version = KnownVersion;
        public AdultCareerProgressSaveData Adult;
        public StudentLifeProgressSaveData Student;
        public PlayerInventorySaveData Inventory;
        public WorldStateProgressSaveData World;
        public QuestChainLogSaveData QuestChains;
        public FurnitureUnlockProgressSaveData FurnitureUnlocks;
        /// <summary>버전 3부터. 없으면(옛 세이브) 잔액 0에서 가방의 돈을 옮긴다.</summary>
        public WalletSaveData Wallet;

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }
}
