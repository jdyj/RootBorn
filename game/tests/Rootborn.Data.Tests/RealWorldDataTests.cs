using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Dialogue;
using Rootborn.Core.DiscoveryClues;
using Rootborn.Core.Encyclopedia;
using Rootborn.Core.StudentLife;
using Rootborn.Core.WorldState;
using Rootborn.Data.Builders;

namespace Rootborn.Data.Tests
{
    /// <summary>
    /// 월드 영역(월드 상태, 사용, 도감, 발견 단서, 단서 해석, 대화, NPC, 일정)의 실제 엔티티 하나씩이
    /// data/의 값대로 만들어지는지 본다. 기대값은 data/ JSON에서 옮겨 적었다.
    /// </summary>
    public sealed class RealWorldDataTests
    {
        private LoadResult _result;

        [OneTimeSetUp]
        public void LoadOnce()
        {
            _result = GameDataLoader.Load(RepoPaths.DataDirectory, RootbornBuilders.Register);
        }

        [Test]
        public void AWorldStateFlag_CarriesItsLocationScopeSurfacesAndBadges()
        {
            var flag = _result.Definitions.Get<WorldStateFlag>("world.library.archive-open");

            Assert.That(flag.DisplayNameKey, Is.EqualTo("world.library.archive-open.name"));
            Assert.That(flag.RelatedLocationId, Is.EqualTo("location.library"));
            Assert.That(flag.Scope, Is.EqualTo(WorldStateScopeKind.Shared));
            Assert.That(flag.ChangeKind, Is.EqualTo(WorldStateChangeKind.LocationUnlocked));
            Assert.That(flag.NextActionKey, Is.EqualTo("world.library.archive-open.next"));
            // visibleSurfaces "000000000200000001000000" = DayResult, WorldLog, LocationPanel
            Assert.That(flag.IsVisibleOn(WorldStateSummarySurface.DayResult), Is.True);
            Assert.That(flag.IsVisibleOn(WorldStateSummarySurface.WorldLog), Is.True);
            Assert.That(flag.IsVisibleOn(WorldStateSummarySurface.LocationPanel), Is.True);
            // badges "0200000004000000" = New, Shared
            Assert.That(flag.Badges, Is.EqualTo(new[] { WorldStateBadgeKind.New, WorldStateBadgeKind.Shared }));
            Assert.That(flag.SortPriority, Is.EqualTo(100));
            Assert.That(flag.EffectVersion, Is.EqualTo(1));
        }

        [Test]
        public void AFlagWithoutSurfaces_IsVisibleEverywhere()
        {
            var flag = _result.Definitions.Get<WorldStateFlag>("world.chef.level2");

            Assert.That(flag.RelatedLocationId, Is.EqualTo("location.restaurant"));
            Assert.That(flag.Scope, Is.EqualTo(WorldStateScopeKind.Personal));
            Assert.That(flag.Badges, Is.Empty);
            Assert.That(flag.IsVisibleOn(WorldStateSummarySurface.WorldLog), Is.True);
        }

        [Test]
        public void AWorldStateScope_CarriesItsKind()
        {
            var scope = _result.Definitions.Get<WorldStateScope>("scope.shared-world");

            Assert.That(scope.Kind, Is.EqualTo(WorldStateScopeKind.Shared));
            Assert.That(scope.DisplayNameKey, Is.EqualTo("Shared World"));
        }

        [Test]
        public void AWorldStateUsage_CarriesItsFlagConditionsOutcomesAndPolicy()
        {
            var usage = _result.Definitions.Get<WorldStateUsage>("usage.library.archive-table");

            Assert.That(usage.SourceFlagId, Is.EqualTo("world.library.archive-open"));
            Assert.That(usage.RelatedLocationId, Is.Empty);
            Assert.That(usage.InteractionPromptKey, Is.EqualTo("Browse the job shelf"));
            Assert.That(usage.Conditions.Single(), Is.InstanceOf<WorldStateUsageFlagActiveCondition>());
            Assert.That(usage.Outcomes.Select(o => o.GetType()), Is.EqualTo(new[]
            {
                typeof(WorldStateUsageQuestUnlockOutcome), typeof(WorldStateUsageEncyclopediaOutcome), typeof(WorldStateUsageCareerHintOutcome),
            }));
            var quest = (WorldStateUsageQuestUnlockOutcome)usage.Outcomes[0];
            Assert.That(quest.Quests.Single().Id, Is.EqualTo("quest.career.interior"));
            var entry = (WorldStateUsageEncyclopediaOutcome)usage.Outcomes[1];
            Assert.That(entry.Entries.Single().Id, Is.EqualTo("ency.location.locked-archive"));
            var career = (WorldStateUsageCareerHintOutcome)usage.Outcomes[2];
            Assert.That(career.CareerIds, Is.EqualTo(new[] { "career.interior" }));
            Assert.That(usage.RepeatPolicy.Mode, Is.EqualTo(WorldStateUsageRepeatMode.Once));
            Assert.That(usage.RepeatPolicy.MaxUses, Is.EqualTo(1));
            Assert.That(usage.SortPriority, Is.EqualTo(10));
        }

        [Test]
        public void AnEncyclopediaEntry_CarriesItsCategoryLinesAndConditions()
        {
            var entry = _result.Definitions.Get<EncyclopediaEntry>("ency.location.first-discovery");

            Assert.That(entry.Category.Id, Is.EqualTo("ency.category.locations"));
            Assert.That(entry.DisplayName, Is.EqualTo("ui.encyclopedia.entry.clocktower-note.name"));
            Assert.That(entry.LockedDisplayName, Is.EqualTo("ui.encyclopedia.locked.name"));
            Assert.That(entry.DetailLines, Is.EqualTo(new[] { "dream.encyclopedia.first-discovery.detail" }));
            var condition = (EncyclopediaDiscoveryCompletedCondition)entry.UnlockConditions.Single();
            Assert.That(condition.DiscoveryId, Is.EqualTo("discovery.clocktower-notice"));
            Assert.That(entry.RevealStage, Is.EqualTo(1));
        }

        [Test]
        public void AnEncyclopediaCategory_CarriesItsOrder()
        {
            var category = _result.Definitions.Get<EncyclopediaCategory>("ency.category.activities");

            Assert.That(category.DisplayName, Is.EqualTo("ui.encyclopedia.category.activities"));
            Assert.That(category.SortOrder, Is.EqualTo(4));
        }

        [Test]
        public void ADiscoveryClue_CarriesItsSourcesStrategiesAndSurfaces()
        {
            var clue = _result.Definitions.Get<DiscoveryClue>("clue.discovery.library-archive-rumor");

            Assert.That(clue.RelatedWorldState.Id, Is.EqualTo("WorldState_DiscoveryClue_LibraryArchive"));
            Assert.That(clue.RelatedQuestId, Is.EqualTo("Quest_DiscoveryClue_LibraryFollowUp"));
            Assert.That(clue.RelatedLocationId, Is.Empty);
            Assert.That(clue.Strength, Is.EqualTo(3));
            Assert.That(clue.HiddenTextKey, Is.EqualTo("??? book"));
            Assert.That(clue.Sources.Select(s => s.Id), Is.EqualTo(new[] { "source.discovery.librarian-rumor", "source.discovery.board-rumor" }));
            var board = clue.Sources[1];
            Assert.That(board.Kind, Is.EqualTo(DiscoveryClueSourceKind.BoardPost));
            Assert.That(board.RelatedWorldState.Id, Is.EqualTo("WorldState_DiscoveryClue_LibraryArchive"));
            Assert.That(board.Strength, Is.EqualTo(1));
            Assert.That(clue.Conditions.Single(), Is.InstanceOf<DiscoveryClueWorldStateActiveCondition>());
            var visit = (DiscoveryClueLocationVisitCompletion)clue.Completions.Single();
            Assert.That(visit.LocationId, Is.EqualTo("location.library.archive-room"));
            Assert.That(clue.Outcomes.Select(o => o.GetType()), Is.EqualTo(new[]
            {
                typeof(DiscoveryClueQuestUnlockOutcome), typeof(DiscoveryClueCareerHintOutcome),
                typeof(DiscoveryClueEncyclopediaOutcome), typeof(DiscoveryClueWorldStateRevealOutcome),
            }));
            // visibleSurfaces "00000000010000000500000003000000" = NpcDialogue, Board, WorldLog, Encyclopedia
            Assert.That(clue.IsVisibleOn(DiscoveryClueSummarySurface.Board), Is.True);
            Assert.That(clue.IsVisibleOn(DiscoveryClueSummarySurface.WorldLog), Is.True);
            Assert.That(clue.IsVisibleOn(DiscoveryClueSummarySurface.Map), Is.False);
            Assert.That(clue.SortPriority, Is.EqualTo(10));
        }

        [Test]
        public void AClueInterpretation_CarriesItsClueSourcesConditionsAndPolicy()
        {
            var interpretation = _result.Definitions.Get<ClueInterpretation>("interpret.ask-librarian");

            Assert.That(interpretation.ClueId, Is.EqualTo("clue.discovery.play-loop"));
            Assert.That(interpretation.MethodKey, Is.EqualTo("Ask"));
            var source = interpretation.Sources.Single();
            Assert.That(source.Kind, Is.EqualTo(ClueInterpretationSourceKind.NpcDialogue));
            Assert.That(source.TargetId, Is.EqualTo("npc.librarian"));
            Assert.That(source.Strength, Is.EqualTo(3));
            Assert.That(interpretation.Conditions[0], Is.InstanceOf<ClueInterpretationClueSeenCondition>());
            var relationship = (ClueInterpretationRelationshipThresholdCondition)interpretation.Conditions[1];
            Assert.That(relationship.RelationshipId, Is.EqualTo("relationship.first-guide.trust"));
            Assert.That(relationship.MinimumValue, Is.EqualTo(0));
            Assert.That(relationship.LockedReasonKey, Is.EqualTo("condition.missing"));
            Assert.That(interpretation.Outcomes.Select(o => o.GetType()), Is.EqualTo(new[]
            {
                typeof(ClueInterpretationEncyclopediaOutcome), typeof(ClueInterpretationCareerHintOutcome),
            }));
            Assert.That(interpretation.Policy.Id, Is.EqualTo("policy.interpret.nonexclusive"));
            Assert.That(interpretation.Policy.Kind, Is.EqualTo(ClueInterpretationPolicyKind.NonExclusive));
            Assert.That(interpretation.Policy.GroupId, Is.EqualTo("policy.group.play-loop.multi"));
            Assert.That(interpretation.SortPriority, Is.EqualTo(30));
        }

        [Test]
        public void ADialogue_CarriesItsLinesChoicesAndTutorialStage()
        {
            var first = _result.Definitions.Get<DialogueDefinition>("Dialogue_FirstNpc");
            Assert.That(first.LineKeys, Is.EqualTo(new[] { "opening.dialogue.guide", "dialogue.firstNpc.greeting" }));
            Assert.That(first.Choices.Count, Is.EqualTo(2));
            Assert.That(first.RequiredTutorialStageId, Is.Empty);

            var day2 = _result.Definitions.Get<DialogueDefinition>("Dialogue_FirstNpc_Day2");
            Assert.That(day2.RequiredTutorialStageId, Is.EqualTo("tutorial.day2"));
        }

        [Test]
        public void ADialogueChoice_CarriesItsActionAndQuest()
        {
            var choice = _result.Definitions.Get<DialogueChoice>("Choice_AcceptCareer_Chef");

            Assert.That(choice.LabelKey, Is.EqualTo("dialogue.choice.acceptCareer.chef"));
            Assert.That(choice.QuestAction, Is.EqualTo(DialogueQuestAction.AcceptQuest));
            Assert.That(choice.Quest.Id, Is.EqualTo("quest.career.chef"));
        }

        [Test]
        public void AnNpc_CarriesItsHomeRolesDialoguesAndQuests()
        {
            var npc = _result.Definitions.Get<NpcDefinition>("npc.first-guide");

            Assert.That(npc.DisplayNameKey, Is.EqualTo("npc.first-guide.name"));
            Assert.That(npc.IntroductionKey, Is.EqualTo("npc.first-guide.intro"));
            Assert.That(npc.HomeLocationId, Is.EqualTo("location.home"));
            var role = npc.Roles.Single();
            Assert.That(role.Id, Is.EqualTo("npc.role.guide"));
            Assert.That(role.DisplayName, Is.EqualTo("Guide"));
            Assert.That(role.SortOrder, Is.EqualTo(10));
            Assert.That(npc.DefaultDialogue.Id, Is.EqualTo("Dialogue_FirstNpc"));
            Assert.That(npc.StageDialogues.Select(d => d.Id), Is.EqualTo(new[] { "Dialogue_FirstNpc_Day2" }));
            Assert.That(npc.Quests.Select(q => q.Id), Is.EqualTo(new[]
            {
                "quest.gatherWood", "quest.career.chef", "quest.career.interior", "quest.career.soldier",
                "quest.career.emergency-care", "quest.tile-placement.interior",
            }));
        }

        [Test]
        public void AnNpcSchedule_CarriesItsEntriesAndTimeRules()
        {
            var schedule = _result.Definitions.Get<NpcSchedule>("Schedule_FirstGuide");

            Assert.That(schedule.Npc.Id, Is.EqualTo("npc.first-guide"));
            Assert.That(schedule.FallbackLocationId, Is.EqualTo("location.home"));
            Assert.That(schedule.Entries.Count, Is.EqualTo(2));
            var morning = schedule.Entries[0];
            Assert.That(morning.LocationId, Is.EqualTo("location.home"));
            Assert.That(morning.Dialogue.Id, Is.EqualTo("Npc_FirstGuide_Schedule_0"));
            Assert.That(morning.DialogueKey, Is.EqualTo("dialogue.first-guide.morning"));
            Assert.That(morning.InteractionHintKey, Is.EqualTo("npc.first-guide.hint.place-recommend"));
            Assert.That(morning.EventNoticeKey, Is.EqualTo("npc.first-guide.notice.today-route"));
            Assert.That(morning.Priority, Is.EqualTo(20));
            var rule = (TimeSlotScheduleRule)morning.Rules.Single();
            Assert.That(rule.TimeSlotIds, Is.EqualTo(new[] { "time.morning" }));
        }

        [Test]
        public void ATimeSlotScheduleRule_CarriesItsSlots()
        {
            var rule = _result.Definitions.Get<TimeSlotScheduleRule>("Rule_Time_AfternoonEvening");

            Assert.That(rule.TimeSlotIds, Is.EqualTo(new[] { "time.afternoon", "time.evening" }));
        }

        [Test]
        public void ALocationStateConflictPolicy_CarriesItsMode()
        {
            var policy = _result.Definitions.Get<LocationStateConflictPolicy>("LocationStateConflictPolicy_HighestPriority");

            Assert.That(policy.Mode, Is.EqualTo(LocationStateConflictMode.HighestPriority));
        }
    }
}
