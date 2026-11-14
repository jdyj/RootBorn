using System.Globalization;
using System.Text;

namespace Rootborn.Core.Save
{
    /// <summary>
    /// 모든 세이브 파일 이름을 한 곳에서 만든다. 기본 플레이어의 이름은 Unity 판이 쓴 파일 이름과 같다.
    /// 다른 플레이어는 id를 <see cref="Sanitize"/>로 정리해 붙인다. Unity 판이 정리하지 않고 쓰던 파일(도감, 발견 단서,
    /// 단서 해석, 탐색)은 Legacy* 이름으로 옛 파일을 찾을 수 있다(<see cref="SaveStoreReads.TryReadWithLegacyName"/>).
    /// </summary>
    public static class SaveFileNames
    {
        /// <summary>기본 플레이어 id(PlayerIdentity.DefaultPlayerId). 이것과 "player"는 옛 공용 파일을 같이 쓴다.</summary>
        public const string DefaultPlayerId = "local-player";

        /// <summary>빈 플레이어 id 대신 쓰는 옛 기본값.</summary>
        public const string LegacyPlayerId = "player";

        /// <summary>기본 플레이어의 일반 퀘스트 기록 파일. 플레이어마다 다른 이름은 <see cref="PlayerQuestLog"/>.</summary>
        public const string QuestLog = "quest-log.json";

        /// <summary>
        /// Unity PlayerQuestRuntime.LogFileName과 같다: 빈 id와 "local-player"만 공용 파일(quest-log.json)이고,
        /// 그 밖(옛 기본값 "player" 포함)은 id를 정리해 붙인다.
        /// </summary>
        public static string PlayerQuestLog(string playerId)
            => string.IsNullOrEmpty(playerId) || playerId == DefaultPlayerId ? QuestLog : "quest-log-" + Sanitize(playerId) + ".json";

        /// <summary>슬롯 정보(Unity SaveService).</summary>
        public const string Metadata = "metadata.json";

        /// <summary>지금 있는 장면과 입구(Unity WorldEntryService). 월드 상태(WorldState) 파일이 아니다.</summary>
        public const string WorldProgress = "world-progress.json";

        /// <summary>옷 선택(Unity WearableWardrobeStore.FileNameFor): 글자·숫자·'-'·'_' 밖은 '_'. 빈 id는 "wearables-.json".</summary>
        public static string Wearables(string playerId) => "wearables-" + Sanitize(playerId) + ".json";

        /// <summary>
        /// 슬롯과 무관한 설정 파일이 놓이는 슬롯 자리(ISaveStore의 saveSlot에 넣는다). 세이브 슬롯 이름("slot-0" 등)과 겹치지 않게 '_'로 시작한다.
        /// </summary>
        public const string SettingsSlot = "_settings";

        /// <summary>설정 파일(언어, 켬/끔 값). Unity 판에는 없었다.</summary>
        public const string Settings = "settings.json";

        /// <summary>목표 수첩의 추적 지정(Unity ObjectiveJournalTrackingStore). 슬롯마다 하나.</summary>
        public const string ObjectiveJournalTracking = "objective-journal-tracking.json";

        /// <summary>월드 상태 중 Shared·SaveSlot 범위만 담는 슬롯 공유 파일.</summary>
        public const string WorldStateShared = "world-state-progress-shared.json";

        /// <summary>방의 시계(Core Time RoomClock). 슬롯마다 하나이고 방을 가진 쪽(서버)만 쓴다. 혼자 하는 판은 쓰지 않는다. Unity 판에는 없었다.</summary>
        public const string RoomClock = "room-clock.json";

        /// <summary>방 설정(Core Room RoomShareSettings: 무엇을 같이 쓰는지). 방을 처음 만들 때 한 번 쓴다. 방을 가진 쪽만 쓴다.</summary>
        public const string RoomSettings = "room-settings.json";

        /// <summary>방이 답한 요청 장부(Core Room RoomRequestLedger). 같은 요청 id를 두 번 반영하지 않게 한다. 방을 가진 쪽만 쓴다.</summary>
        public const string RoomRequestLedger = "room-request-ledger.json";

        /// <summary>방의 공유 조각 하나의 상태(지갑, 마을 상태 등). 조각 이름은 글자·숫자·'-'·'_'만 쓴다(Core Room RoomPieceCatalog).</summary>
        public static string RoomPiece(string pieceName) => "room-piece-" + Sanitize(pieceName) + ".json";

        /// <summary>
        /// 같이 하는 방에 있는 플레이어가 방에 보냈고 아직 답을 받지 않은 요청(Core Room RoomOutbox). 그 플레이어의 저장과 함께 방으로 가고,
        /// 방은 저장을 받을 때 이 파일의 요청을 반영한다(저장과 요청이 한 번에). 혼자 하는 판은 쓰지 않는다.
        /// </summary>
        public static string RoomOutbox(string playerId) => PerPlayer("room-outbox-", playerId);

        /// <summary>
        /// 옛 공용 시간 파일이 놓이는 슬롯 자리. 지금은 방의 시계(<see cref="RoomClock"/>)가 없는 옛 방을 읽을 때만 쓴다.
        /// 같이 쓰는 시간 파일이 놓이는 슬롯 자리(Unity SaveService.WorldTimeDirectory의 "world-time" 폴더). 파일 이름은 <see cref="WorldTime"/>.
        /// Unity 판은 세이브 폴더를 따로 준 실행(-saveRoot)에서 &lt;root&gt;/world-time/&lt;슬롯&gt;.json에 썼다.
        /// </summary>
        public const string WorldTimeSlot = "world-time";

        /// <summary>Unity NetworkWorldTimeState.SavePath: 빈 슬롯은 "default", 경로 구분자 등은 '_'.</summary>
        public static string WorldTime(string saveSlot)
            => (string.IsNullOrEmpty(saveSlot) ? "default" : Sanitize(saveSlot)) + ".json";

        public static bool IsDefaultPlayer(string playerId)
            => string.IsNullOrEmpty(playerId) || playerId == DefaultPlayerId || playerId == LegacyPlayerId;

        /// <summary>플레이어 id를 파일 이름에 넣을 때 글자·숫자·'-'·'_' 밖의 글자를 '_'로 바꾼다. null은 빈 문자열.</summary>
        public static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++) if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_') chars[i] = '_';
            return new string(chars);
        }

        /// <summary>기본 플레이어는 "quest-chain-progress.json"을 같이 쓰고, 다른 플레이어는 id의 UTF-8 바이트를 모두 16진으로 적어 겹치지 않는다.</summary>
        public static string QuestChain(string playerId)
        {
            if (IsDefaultPlayer(playerId)) return "quest-chain-progress.json";
            var bytes = Encoding.UTF8.GetBytes(playerId);
            var name = new StringBuilder("quest-chain-progress-", 21 + bytes.Length * 2 + 5);
            for (int i = 0; i < bytes.Length; i++) name.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
            return name.Append(".json").ToString();
        }

        /// <summary>Unity AdultCareerSnapshotPersistence.FileNameFor와 같다: 빈 id는 "player", UTF-16 글자마다 4자리 16진.</summary>
        public static string AdultCareerSnapshot(string playerId)
        {
            var value = string.IsNullOrEmpty(playerId) ? LegacyPlayerId : playerId;
            var encoded = new StringBuilder("adult-career-snapshot-", 22 + value.Length * 4 + 5);
            for (int i = 0; i < value.Length; i++) encoded.Append(((int)value[i]).ToString("x4", CultureInfo.InvariantCulture));
            return encoded.Append(".json").ToString();
        }

        /// <summary>Unity FurniturePlacementLayoutPersistence.GetHouseLayoutFileNameForStage와 같다. 음수 단계는 0단계다.</summary>
        public static string HouseFurnitureLayout(int stageIndex)
            => "house-furniture-layout-stage-" + System.Math.Max(0, stageIndex).ToString(CultureInfo.InvariantCulture) + ".json";

        /// <summary>집 상태(Unity HouseStatePersistence): 지금 집 단계 번호. 슬롯마다 하나.</summary>
        public const string HouseState = "house-state.json";

        /// <summary>짓는 집의 꾸밈 기록(Core Building HouseBuildSaveData): 바닥 칸마다 벽지·바닥. 슬롯마다 하나. MonoGame 판이 더한 파일이다.</summary>
        public const string HouseBuild = "house-build.json";

        /// <summary>집 단계가 생기기 전의 배치 파일. 0단계 파일이 없을 때만 읽는다.</summary>
        public const string HouseFurnitureLayoutBeforeStages = "house-furniture-layout.json";

        /// <summary>Unity FurnitureUnlockProgressPersistence.FileNameFor와 같다: 빈 id·"player"·"local-player"는 공용 파일.</summary>
        public static string FurnitureUnlocks(string playerId)
            => IsDefaultPlayer(playerId) ? "furniture-unlocks.json" : "furniture-unlocks-" + Sanitize(playerId) + ".json";

        /// <summary>Unity ResourceRecipeProgressPersistence.FileNameFor와 같다: 빈 id와 "player"만 공용 파일이고 "local-player"는 따로 쓴다.</summary>
        public static string ResourceRecipe(string playerId)
            => string.IsNullOrEmpty(playerId) || playerId == LegacyPlayerId ? "resource-recipe-progress.json" : "resource-recipe-progress-" + Sanitize(playerId) + ".json";

        // 기본 플레이어가 공용 파일 하나를 쓰는 진행 파일들.
        public static string Campaign(string playerId) => SharedForDefault("campaign-progress", playerId);
        public static string LocationState(string playerId) => SharedForDefault("location-state-progress", playerId);
        public static string Milestone(string playerId) => SharedForDefault("milestone-progress", playerId);
        public static string WorldState(string playerId) => SharedForDefault("world-state-progress", playerId);
        public static string Exploration(string playerId) => SharedForDefault("exploration-progress", playerId);

        /// <summary>Unity CareerInterestProgressPersistence.FileNameFor와 같다: 빈 id와 "local-player"만 공용 파일이고 "player"는 따로 쓴다.</summary>
        public static string CareerInterest(string playerId)
            => string.IsNullOrEmpty(playerId) || playerId == DefaultPlayerId ? "career-interest-progress.json" : "career-interest-progress-" + Sanitize(playerId) + ".json";

        /// <summary>Unity DailyEventProgressPersistence.FileNameFor와 같다: 빈 id와 "local-player"만 공용 파일이고 "player"는 따로 쓴다.</summary>
        public static string DailyEvent(string playerId)
            => string.IsNullOrEmpty(playerId) || playerId == DefaultPlayerId ? "daily-event-progress.json" : "daily-event-progress-" + Sanitize(playerId) + ".json";

        // ---- 스냅샷 이전 Unity 판 영역 파일(읽기만 한다. 스냅샷이 생기면 그쪽이 원본이다) ----

        /// <summary>Unity StudentLifeProgressPersistence.FileNameFor: 빈 id와 "local-player"는 공용 파일.</summary>
        public static string UnityStudentLife(string playerId)
            => string.IsNullOrEmpty(playerId) || playerId == DefaultPlayerId ? "student-life-progress.json" : "student-life-progress-" + Sanitize(playerId) + ".json";

        /// <summary>Unity AdultCareerRuntimeComponent.LoadLegacyInventory: "local-player"만 공용 파일(빈 id도 공용으로 본다).</summary>
        public static string UnityInventory(string playerId)
            => string.IsNullOrEmpty(playerId) || playerId == DefaultPlayerId ? "inventory.json" : "inventory-" + Sanitize(playerId) + ".json";

        /// <summary>Unity CareerCandidateProgressPersistence.FileNameFor: 빈 id와 "local-player"는 공용 파일.</summary>
        public static string UnityCareerCandidates(string playerId)
            => string.IsNullOrEmpty(playerId) || playerId == DefaultPlayerId ? "career-candidate-progress.json" : "career-candidate-progress-" + Sanitize(playerId) + ".json";

        /// <summary>Unity DailyPlanProgressPersistence.FileNameFor: 빈 id와 "player"만 공용 파일, 그 밖은 UTF-16 글자마다 4자리 16진.</summary>
        public static string UnityDailyPlan(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == LegacyPlayerId) return "daily-plan-progress.json";
            var encoded = new StringBuilder("daily-plan-progress-", 20 + playerId.Length * 4 + 5);
            for (int i = 0; i < playerId.Length; i++) encoded.Append(((int)playerId[i]).ToString("x4", CultureInfo.InvariantCulture));
            return encoded.Append(".json").ToString();
        }

        /// <summary>Unity DailyPlanProgressPersistence.LegacyFileNameFor: 위 이름이 없을 때 찾는 더 옛 이름(정리한 id).</summary>
        public static string UnityDailyPlanSanitized(string playerId)
            => string.IsNullOrEmpty(playerId) || playerId == LegacyPlayerId ? "daily-plan-progress.json" : "daily-plan-progress-" + Sanitize(playerId) + ".json";

        /// <summary>Unity 판 탐색 파일 이름(플레이어 id를 정리하지 않았다).</summary>
        public static string LegacyExploration(string playerId)
            => IsDefaultPlayer(playerId) ? "exploration-progress.json" : "exploration-progress-" + playerId + ".json";

        // 플레이어마다 파일 하나. 빈 id는 "player".
        public static string WorldStateUsage(string playerId) => PerPlayer("world-state-usage-", playerId);
        public static string Encyclopedia(string playerId) => PerPlayer("encyclopedia-", playerId);
        public static string DiscoveryClues(string playerId) => PerPlayer("discovery-clues-", playerId);
        public static string ClueInterpretations(string playerId) => PerPlayer("clue-interpretations-", playerId);

        /// <summary>흐르는 시계의 개인 상태(지침, 밤에 깎은 시간, 하는 중). MonoGame 판이 더한 파일이다.</summary>
        public static string StudentClock(string playerId) => PerPlayer("student-clock-", playerId);

        /// <summary>일터 진행(쓴 템플릿 요청, 기본 템플릿을 깐 일터, 꾸미며 배운 횟수). MonoGame 판이 더한 파일이다.</summary>
        public static string WorkspaceProgress(string playerId) => PerPlayer("workspace-progress-", playerId);

        /// <summary>일터 면 하나의 가구 배치(모양은 집 배치 파일과 같다). MonoGame 판이 더한 파일이다. 빈 플레이어 id는 "player".</summary>
        public static string WorkspaceLayout(string surfaceId, string playerId)
            => "workspace-layout-" + Sanitize(surfaceId) + "-" + Sanitize(string.IsNullOrEmpty(playerId) ? LegacyPlayerId : playerId) + ".json";

        // Unity 판이 플레이어 id를 정리하지 않고 쓰던 이름.
        public static string LegacyEncyclopedia(string playerId) => LegacyPerPlayer("encyclopedia-", playerId);
        public static string LegacyDiscoveryClues(string playerId) => LegacyPerPlayer("discovery-clues-", playerId);
        public static string LegacyClueInterpretations(string playerId) => LegacyPerPlayer("clue-interpretations-", playerId);

        private static string SharedForDefault(string stem, string playerId)
            => IsDefaultPlayer(playerId) ? stem + ".json" : stem + "-" + Sanitize(playerId) + ".json";

        private static string PerPlayer(string prefix, string playerId)
            => prefix + Sanitize(string.IsNullOrEmpty(playerId) ? LegacyPlayerId : playerId) + ".json";

        private static string LegacyPerPlayer(string prefix, string playerId)
            => prefix + (string.IsNullOrEmpty(playerId) ? LegacyPlayerId : playerId) + ".json";
    }
}
