using System;
using NUnit.Framework;
using Rootborn.Core.Dialogue;
using Rootborn.Core.DiscoveryClues;
using Rootborn.Core.Inventory;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.StudentLife;
using Rootborn.Core.WorldState;
using Rootborn.Data.Builders;

namespace Rootborn.Data.Tests
{
    /// <summary>
    /// data/의 열거형은 Unity가 쓴 숫자다. 만들기 함수가 읽는 열거형마다 Core(또는 Data의 대응표) 순서가
    /// 원본 Assets 소스의 선언 순서와 같은지 고정한다. 기대 배열은 원본 소스를 그대로 옮겨 적은 것이다.
    /// </summary>
    public sealed class EnumOrderTests
    {
        [Test]
        public void LifeActivityCategory_HasTheUnityOrder()
        {
            // Assets/Scripts/Game/StudentLife/StudentLifeCore.cs
            Assert.That(Enum.GetNames(typeof(LifeActivityCategory)),
                Is.EqualTo(new[] { "School", "Work", "Hobby", "Errand", "Social", "Rest", "SelfStudy" }));
        }

        [Test]
        public void FurniturePlacementEnums_HaveTheUnityOrder()
        {
            // Assets/Scripts/Game/Placement/FurniturePlacementTypes.cs. 방향은 세이브에도 숫자로 들어 있다.
            Assert.That(Enum.GetNames(typeof(FurniturePlacementDirection)), Is.EqualTo(new[] { "North", "East", "South", "West" }));
            Assert.That(Enum.GetNames(typeof(FurniturePlacementPreference)), Is.EqualTo(new[] { "Any", "AvoidBlockedCells", "NearWall", "NearWindow" }));
        }

        [Test]
        public void ItemCategory_HasTheUnityOrder()
        {
            // Assets/Scripts/Game/Common/GameDataRegistry.cs
            Assert.That(Enum.GetNames(typeof(ItemCategory)), Is.EqualTo(new[] { "Resource", "Tool", "Misc", "Seed" }));
        }

        [Test]
        public void CareerCandidateState_HasTheUnityOrder()
        {
            // Assets/Scripts/Game/StudentLife/CareerCandidateCore.cs
            Assert.That(Enum.GetNames(typeof(CareerCandidateState)),
                Is.EqualTo(new[] { "Locked", "Hinted", "Revealed", "Deepening", "ReadyForChoice" }));
        }

        [Test]
        public void MinigameBonusTarget_HasTheUnityOrder()
        {
            // Assets/Scripts/Game/Minigames/GrowthMinigameBonusRule.cs
            Assert.That(Enum.GetNames(typeof(MinigameBonusTarget)),
                Is.EqualTo(new[] { "TimingWindow", "TimeLimit", "MistakeAllowance", "HintCount", "Harmony", "Steadiness" }));
        }

        [Test]
        public void DiscoveryScope_HasTheUnityOrder()
        {
            // Assets/Scripts/Game/StudentLife/DiscoveryDefinition.cs
            Assert.That(Enum.GetNames(typeof(DiscoveryScope)), Is.EqualTo(new[] { "Personal", "SharedWorld" }));
        }

        [Test]
        public void LocationGrowthRoute_NumbersMapToTheCoreNames_InTheUnityOrder()
        {
            // Assets/Scripts/Game/StudentLife/LocationActivityDefinition.cs enum LocationGrowthRoute.
            // Core는 문자열(LocationGrowthRoutes)로 받으므로 Data의 대응표를 고정한다.
            Assert.That(LocationBuilders.GrowthRouteNames, Is.EqualTo(new[]
            {
                LocationGrowthRoutes.FormalStudy, LocationGrowthRoutes.SelfStudy, LocationGrowthRoutes.Work,
                LocationGrowthRoutes.Commerce, LocationGrowthRoutes.SocialHelp, LocationGrowthRoutes.Rest,
                LocationGrowthRoutes.Exploration,
            }));
            Assert.That(LocationBuilders.GrowthRouteNames,
                Is.EqualTo(new[] { "FormalStudy", "SelfStudy", "Work", "Commerce", "SocialHelp", "Rest", "Exploration" }));
        }

        [Test]
        public void LocationActivityProgressTarget_HasTheUnityOrder()
        {
            // Assets/Scripts/Game/StudentLife/LocationActivityProgressDeltaOutcome.cs
            Assert.That(Enum.GetNames(typeof(LocationBuilders.ProgressTarget)),
                Is.EqualTo(new[] { "Trait", "Skill", "Relationship", "Status", "CareerHint" }));
        }

        [Test]
        public void WorldStateEnums_HaveTheUnityOrder()
        {
            // Assets/Scripts/Game/WorldState/WorldStateTypes.cs
            Assert.That(Enum.GetNames(typeof(WorldStateScopeKind)), Is.EqualTo(new[] { "Personal", "Shared", "SaveSlot" }));
            Assert.That(Enum.GetNames(typeof(WorldStateChangeKind)), Is.EqualTo(new[]
                { "LocationUnlocked", "ObjectRevealed", "PortalEnabled", "NpcDialogueChanged", "ShopStockAdded", "InteractionAdded", "EventAvailable" }));
            Assert.That(Enum.GetNames(typeof(WorldStateSummarySurface)), Is.EqualTo(new[] { "DayResult", "LocationPanel", "WorldLog" }));
            Assert.That(Enum.GetNames(typeof(WorldStateBadgeKind)), Is.EqualTo(new[] { "Blocked", "Locked", "New", "Seen", "Shared", "Personal" }));
        }

        [Test]
        public void WorldStateUsageEnums_HaveTheUnityOrder()
        {
            // Assets/Scripts/Game/WorldState/WorldStateUsageCore.cs
            Assert.That(Enum.GetNames(typeof(WorldStateUsageRepeatMode)), Is.EqualTo(new[] { "Once", "Repeatable", "OncePerDay", "DailyCooldown", "MaxUses" }));
            Assert.That(Enum.GetNames(typeof(WorldStateUsageSummarySurface)), Is.EqualTo(new[] { "LocationPanel", "WorldLog", "DayResult", "Hud" }));
        }

        [Test]
        public void DiscoveryClueEnums_HaveTheUnityOrder()
        {
            // Assets/Scripts/Game/DiscoveryClues/DiscoveryClueCore.cs
            Assert.That(Enum.GetNames(typeof(DiscoveryClueSourceKind)), Is.EqualTo(new[]
                { "NpcDialogue", "BoardPost", "MapHint", "EncyclopediaUnknown", "LocationTrace", "DailyResult" }));
            Assert.That(Enum.GetNames(typeof(DiscoveryClueSummarySurface)), Is.EqualTo(new[]
                { "NpcDialogue", "Board", "Map", "Encyclopedia", "LocationPanel", "WorldLog", "DayResult", "Hud" }));
        }

        [Test]
        public void ClueInterpretationEnums_HaveTheUnityOrder()
        {
            // Assets/Scripts/Game/DiscoveryClues/ClueInterpretationCore.cs
            Assert.That(Enum.GetNames(typeof(ClueInterpretationSourceKind)), Is.EqualTo(new[]
                { "NpcDialogue", "LocationInvestigation", "ObjectInteraction", "ActivityReview", "EncyclopediaReview", "BoardRecord", "MapCheck" }));
            Assert.That(Enum.GetNames(typeof(ClueInterpretationPolicyKind)), Is.EqualTo(new[] { "Exclusive", "NonExclusive", "Sequential", "Repeatable" }));
        }

        [Test]
        public void DialogueQuestAction_HasTheUnityOrder()
        {
            // Assets/Scripts/Game/Dialogue/DialogueChoiceDefinition.cs
            Assert.That(Enum.GetNames(typeof(DialogueQuestAction)), Is.EqualTo(new[] { "None", "AcceptQuest", "ClaimReward", "Close" }));
        }

        [Test]
        public void LocationStateConflictMode_HasTheUnityOrder()
        {
            // Assets/Scripts/Game/StudentLife/LocationStateCore.cs
            Assert.That(Enum.GetNames(typeof(LocationStateConflictMode)), Is.EqualTo(new[] { "HighestPriority", "MergeAll", "FallbackFirst" }));
        }
    }
}
