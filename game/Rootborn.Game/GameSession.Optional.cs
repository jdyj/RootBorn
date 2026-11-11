using System;
using Rootborn.Core.DiscoveryClues;
using Rootborn.Core.Encyclopedia;
using Rootborn.Core.Objectives;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game
{
    /// <summary>
    /// 따로 파일을 쓰는 영역: 탐색, 발견 단서, 단서 해석, 도감, 하루 이벤트(daily-event-progress*.json). Unity 판에서 이 명령들은 퀘스트 이벤트를 내지 않았다.
    /// </summary>
    public sealed partial class GameSession
    {
        private Func<long> _utcNowTicks = () => 0L;

        public ExplorationProgress Exploration { get; private set; }
        public DiscoveryClueProgress DiscoveryClues { get; private set; }
        public ClueInterpretationProgress ClueInterpretations { get; private set; }
        public EncyclopediaProgress Encyclopedia { get; private set; }
        public DailyEventProgress DailyEvents { get; private set; }

        /// <summary>도감 발견 시각 등에 쓰는 지금 시각(UTC 틱). Host가 넘긴다. 넘기지 않으면 0이다.</summary>
        public void UseClock(Func<long> utcNowTicks) => _utcNowTicks = utcNowTicks ?? (() => 0L);

        private void LoadOptionalDomains()
        {
            Exploration = ExplorationProgressPersistence.LoadOrCreate(_store, SaveSlot, PlayerId);
            DiscoveryClues = DiscoveryClueSaveFiles.LoadOrCreate(_store, SaveSlot, PlayerId);
            ClueInterpretations = ClueInterpretationSaveFiles.LoadOrCreate(_store, SaveSlot, PlayerId);
            Encyclopedia = EncyclopediaSaveFiles.LoadOrCreate(_store, SaveSlot, PlayerId);
            DailyEvents = DailyEventSaveFiles.LoadOrCreate(_store, SaveSlot, PlayerId);
            ObjectiveJournalTrackingSaveFiles.TryLoad(_store, SaveSlot, out var tracking);
            ObjectiveTracking = tracking;
        }

        private void SaveOptionalDomains()
        {
            ExplorationProgressPersistence.Save(_store, Exploration);
            DiscoveryClueSaveFiles.Save(_store, DiscoveryClues);
            ClueInterpretationSaveFiles.Save(_store, ClueInterpretations);
            EncyclopediaSaveFiles.Save(_store, Encyclopedia);
            DailyEventSaveFiles.Save(_store, DailyEvents);
        }

        private DiscoveryClueLookupCache ClueCache => new DiscoveryClueLookupCache(_definitions.All<DiscoveryClue>());

        /// <summary>탐색 지점의 선택지를 고른다. 비용·위험·물건 지급과 이어지는 퀘스트는 Core 실행기가 한 번에 처리한다.</summary>
        public ExplorationChoiceResult ChooseExploration(string interactionId, string choiceId)
        {
            _definitions.TryGet<ExplorationInteraction>(interactionId, out var interaction);
            ExplorationChoice choice = null;
            if (interaction != null)
                foreach (var candidate in interaction.Choices)
                    if (candidate != null && candidate.Id == choiceId) choice = candidate;
            var context = new ExplorationInteractionContext(Exploration, Student, Inventory, QuestLog, new DiscoveryClueAccess(DiscoveryClues, ClueCache),
                new EncyclopediaAccess(Encyclopedia, _utcNowTicks), Student.CurrentDay, Student.TimeMinutes);
            bool applied = new ExplorationInteractionRunner().TryChoose(interaction, choice, in context, out var result);
            return Saved(result, applied, RewardTopics, GameChangeTopics.Discovery);
        }

        /// <summary>단서의 출처 하나를 드러낸다(대화·조사 등으로 출처를 봤을 때).</summary>
        public DiscoveryClueResult RevealClueSource(string clueId, string sourceId)
        {
            _definitions.TryGet<DiscoveryClue>(clueId, out var clue);
            DiscoveryClueSource source = null;
            if (clue != null)
                foreach (var candidate in clue.Sources)
                    if (candidate != null && candidate.Id == sourceId) source = candidate;
            var context = new DiscoveryClueContext(World, DiscoveryClues, Student, QuestLog, Encyclopedia, Student.CurrentDay, _utcNowTicks());
            bool applied = new DiscoveryClueRunner().TryRevealSource(clue, source, in context, out var result);
            return Saved(result, applied, RewardTopics, GameChangeTopics.Discovery);
        }

        /// <summary>단서를 해석한다. 순서·조건 판정은 Core가 한다.</summary>
        public ClueInterpretationResult InterpretClue(string interpretationId)
        {
            _definitions.TryGet<ClueInterpretation>(interpretationId, out var interpretation);
            var context = new ClueInterpretationContext(World, DiscoveryClues, ClueInterpretations, Student, QuestLog, Encyclopedia, Student.CurrentDay,
                new ClueInterpretationLookupCache(_definitions.All<ClueInterpretation>()), _utcNowTicks());
            bool applied = new ClueInterpretationRunner().TryComplete(interpretation, in context, out var result);
            return Saved(result, applied, RewardTopics, GameChangeTopics.Discovery);
        }

        /// <summary>지금 장소에서 고를 수 있는 하루 이벤트.</summary>
        public DailyEvent[] AvailableDailyEvents(string locationId)
            => new DailyEventResolver(_definitions.All<DailyEvent>()).GetAvailableEvents(new DailyEventContext(Student, DailyEvents, locationId, Student.CurrentDay, Student.TimeMinutes));

        /// <summary>하루 이벤트의 선택지를 고른다(Unity 판처럼 캠페인을 "campaign-daily-event:&lt;날&gt;"로 다시 평가한다).</summary>
        public LifeActivityResult ChooseDailyEvent(string eventId, string choiceId, string requestId, string locationId)
        {
            _definitions.TryGet<DailyEvent>(eventId, out var dailyEvent);
            DailyEventChoice choice = null;
            if (dailyEvent != null)
                foreach (var candidate in dailyEvent.Choices)
                    if (candidate != null && candidate.Id == choiceId) choice = candidate;
            bool applied = new DailyEventRunner().TryChoose(dailyEvent, choice, Student, DailyEvents, requestId, locationId, out var result);
            if (applied) ApplyCampaign("campaign-daily-event:" + Student.CurrentDay);
            return Saved(result, applied, null, GameChangeTopics.Student, GameChangeTopics.DailyEvents, GameChangeTopics.Quests);
        }

        /// <summary>
        /// 하루 이벤트를 미룬다(Unity DailyEventPanel의 미루기). 미룰 수 있는 종류만 되고, 다시 나오는 날은 Core DailyEventRunner가 정한다.
        /// 적용되면 저장한다. record는 그 이벤트의 기록(이벤트가 없으면 null).
        /// </summary>
        public bool DeferDailyEvent(string eventId, out DailyEventRecord record)
        {
            _definitions.TryGet<DailyEvent>(eventId, out var dailyEvent);
            bool applied = new DailyEventRunner().TryDefer(dailyEvent, DailyEvents, Student.CurrentDay, out record);
            return Saved(applied, applied, null, GameChangeTopics.DailyEvents);
        }

        /// <summary>하루 이벤트를 지나간다(거절, Unity DailyEventPanel의 지나가기). 선택지 결과는 주지 않는다. 적용되면 저장한다.</summary>
        public bool DeclineDailyEvent(string eventId, out DailyEventRecord record)
        {
            _definitions.TryGet<DailyEvent>(eventId, out var dailyEvent);
            bool applied = new DailyEventRunner().TryDecline(dailyEvent, DailyEvents, Student.CurrentDay, out record);
            return Saved(applied, applied, null, GameChangeTopics.DailyEvents);
        }

        // ---------- 목표 수첩 추적 지정 ----------

        /// <summary>
        /// 목표 수첩에서 추적하는 항목(분류 id, 안정 키). 파일이 없으면 아무것도 추적하지 않는다(Unity 판과 같음 — 추적 HUD는 비어 있다).
        /// 안정 키는 목표 수첩 항목이 정한다. 이 값은 그 키를 저장해 둘 뿐이고 그 항목이 지금 있는지는 보지 않는다.
        /// </summary>
        public ObjectiveTracking ObjectiveTracking { get; private set; }

        /// <summary>
        /// 추적할 항목을 바꾸고 바로 저장한다(슬롯마다 파일 하나, Unity ObjectiveJournalTrackingStore). 안정 키가 비면 아무것도 바꾸지 않는다.
        /// 다른 명령의 저장은 이 파일을 쓰지 않는다(Unity 판처럼 추적을 고를 때만 쓴다).
        /// </summary>
        public bool TrackObjective(string categoryId, string stableKey)
        {
            if (!ObjectiveJournalTrackingSaveFiles.Save(_store, SaveSlot, categoryId, stableKey)) return false;
            ObjectiveTracking = new ObjectiveTracking(categoryId, stableKey);
            Notify(null, GameChangeTopics.Objectives);
            return true;
        }
    }
}
