using System;
using Rootborn.Core.DiscoveryClues;
using Rootborn.Core.Encyclopedia;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game
{
    /// <summary>
    /// 따로 파일을 쓰는 영역: 탐색, 발견 단서, 단서 해석, 도감, 하루 이벤트. Unity 판에서 이 명령들은 퀘스트 이벤트를 내지 않았다.
    /// 하루 이벤트 진행은 SaveFileNames에 파일 이름이 없어 아직 저장하지 않는다(같은 요청은 학생 진행의 적용 요청이 막는다).
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
            DailyEvents = new DailyEventProgress(SaveSlot, PlayerId);
        }

        private void SaveOptionalDomains()
        {
            ExplorationProgressPersistence.Save(_store, Exploration);
            DiscoveryClueSaveFiles.Save(_store, DiscoveryClues);
            ClueInterpretationSaveFiles.Save(_store, ClueInterpretations);
            EncyclopediaSaveFiles.Save(_store, Encyclopedia);
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
            return Saved(result, applied);
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
            return Saved(result, applied);
        }

        /// <summary>단서를 해석한다. 순서·조건 판정은 Core가 한다.</summary>
        public ClueInterpretationResult InterpretClue(string interpretationId)
        {
            _definitions.TryGet<ClueInterpretation>(interpretationId, out var interpretation);
            var context = new ClueInterpretationContext(World, DiscoveryClues, ClueInterpretations, Student, QuestLog, Encyclopedia, Student.CurrentDay,
                new ClueInterpretationLookupCache(_definitions.All<ClueInterpretation>()), _utcNowTicks());
            bool applied = new ClueInterpretationRunner().TryComplete(interpretation, in context, out var result);
            return Saved(result, applied);
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
            return Saved(result, applied);
        }
    }
}
