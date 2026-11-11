using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Rootborn.Core.Careers;
using Rootborn.Core.DiscoveryClues;
using Rootborn.Core.Encyclopedia;
using Rootborn.Core.Gathering;
using Rootborn.Core.Placement;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;
using Rootborn.Core.WorldState;

namespace Rootborn.Data.Tests
{
    /// <summary>
    /// 세이브 파일 이름 무늬 → Core 세이브 모양. 고정 폴더의 모든 파일은 모양이 있거나 "아직 모양 없음"에 이유와 함께 있어야 한다.
    /// 모양이 버리는 필드는 "허용 목록"에 이유와 함께 있어야 한다. 영역을 옮길 때마다 아래 두 목록이 줄어든다.
    /// </summary>
    internal static class SaveShapeCoverage
    {
        public static readonly IReadOnlyList<(string Pattern, Type Shape)> Shapes = new[]
        {
            ("quest-log.json", typeof(QuestLogSaveData)),
            ("adult-career-snapshot-*.json", typeof(AdultCareerSnapshotSaveData)),
            ("campaign-progress.json", typeof(CampaignProgressSaveData)),
            ("location-state-progress.json", typeof(LocationStateProgressSaveData)),
            ("milestone-progress.json", typeof(MilestoneProgressSaveData)),
            ("career-interest-progress*.json", typeof(CareerInterestProgressSaveData)),
            ("quest-chain-progress*.json", typeof(QuestChainLogSaveData)),
            ("campaign-progress-*.json", typeof(CampaignProgressSaveData)),
            ("location-state-progress-*.json", typeof(LocationStateProgressSaveData)),
            ("milestone-progress-*.json", typeof(MilestoneProgressSaveData)),
            ("exploration-progress*.json", typeof(ExplorationProgressSaveData)),
            // 플레이어 파일과 슬롯 공유 파일(world-state-progress-shared.json)은 같은 모양이다.
            ("world-state-progress*.json", typeof(WorldStateProgressSaveData)),
            ("world-state-usage-*.json", typeof(WorldStateUsageProgressSaveData)),
            ("encyclopedia-*.json", typeof(EncyclopediaProgressSaveData)),
            ("discovery-clues-*.json", typeof(DiscoveryClueProgressSaveData)),
            ("clue-interpretations-*.json", typeof(ClueInterpretationProgressSaveData)),
            ("house-furniture-layout-stage-*.json", typeof(FurniturePlacementLayoutSaveData)),
            ("house-furniture-layout.json", typeof(FurniturePlacementLayoutSaveData)),
            ("furniture-unlocks*.json", typeof(FurnitureUnlockProgressSaveData)),
            ("house-state.json", typeof(HouseStateSaveData)),
            ("daily-event-progress*.json", typeof(DailyEventProgressSaveData)),
            ("resource-recipe-progress*.json", typeof(ResourceRecipeProgressSaveData)),
            ("objective-journal-tracking.json", typeof(Rootborn.Core.Objectives.ObjectiveJournalTrackingSaveData)),
            // 슬롯과 무관한 설정 파일(SaveFileNames.SettingsSlot 자리). Unity 판에는 없었다.
            ("settings.json", typeof(Rootborn.Core.Settings.GameSettingsSaveData)),
            ("room-clock.json", typeof(Rootborn.Core.Time.RoomClockSaveData)),
            // 방(서버)의 슬롯: 방 설정, 요청 장부, 공유 조각마다 상태. 같이 하는 방의 플레이어 세이브: 보낼 요청 목록.
            ("room-settings.json", typeof(Rootborn.Core.Room.RoomSettingsSaveData)),
            ("room-request-ledger.json", typeof(Rootborn.Core.Room.RoomRequestLedgerSaveData)),
            ("room-piece-wallet.json", typeof(Rootborn.Core.Room.RoomWalletSaveData)),
            ("room-piece-town.json", typeof(Rootborn.Core.Room.RoomWorldFlagsSaveData)),
            ("room-outbox-*.json", typeof(Rootborn.Core.Room.RoomOutboxSaveData)),
        };

        /// <summary>
        /// 스냅샷 이전 Unity 판 영역 파일. Core가 읽기만 하고 쓰지 않아(PreSnapshotSlotFiles) 형식 버전이 없다.
        /// </summary>
        public static readonly IReadOnlyList<(string Pattern, Type Shape)> ReadOnlyUnityShapes = new[]
        {
            ("student-life-progress*.json", typeof(StudentLifeProgressSaveData)),
            ("inventory*.json", typeof(Rootborn.Core.Inventory.PlayerInventorySaveData)),
            ("daily-plan-progress*.json", typeof(DailyPlanProgressSaveData)),
            ("career-candidate-progress*.json", typeof(CareerCandidateProgressSaveData)),
        };

        public static readonly IReadOnlyList<(string Pattern, string Reason)> NotShapedYet = new[]
        {
            ("metadata.json", "슬롯 정보(이름, 시드, 캐릭터 겉모습). Host 몫"),
            ("world-progress.json", "현재 장면과 입구(Core WorldState의 월드 상태가 아니다). 월드 프로젝트(Runtime/World) 몫"),
            ("student-day-result-quest-inventory-summary.json", "하루 결과 화면 요약. 화면 몫"),
        };

        /// <summary>(파일 무늬, JSON 경로, 이유). 경로 아래 전체를 허용한다. 배열은 "[]"로 원소 형으로 내려간다.</summary>
        public static readonly IReadOnlyList<(string Pattern, string Path, string Reason)> DroppedFields =
            Array.Empty<(string Pattern, string Path, string Reason)>();

        public static Type ShapeFor(string fileName)
        {
            foreach (var (pattern, shape) in Shapes)
                if (Matches(pattern, fileName)) return shape;
            foreach (var (pattern, shape) in ReadOnlyUnityShapes)
                if (Matches(pattern, fileName)) return shape;
            return null;
        }

        public static bool IsListedAsNotShaped(string fileName)
        {
            foreach (var (pattern, _) in NotShapedYet)
                if (Matches(pattern, fileName)) return true;
            return false;
        }

        public static bool IsDropAllowed(string fileName, string path)
        {
            foreach (var (pattern, allowed, _) in DroppedFields)
                if (Matches(pattern, fileName) && (path == allowed || path.StartsWith(allowed + ".", StringComparison.Ordinal)
                    || path.StartsWith(allowed + "[]", StringComparison.Ordinal))) return true;
            return false;
        }

        public static bool Matches(string pattern, string fileName)
        {
            var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(fileName, regex);
        }
    }
}
