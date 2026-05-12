using System;
using Rootborn.Game.Managers;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Network.Time;
using Rootborn.UI.Quests;
using UnityEngine;

namespace Rootborn.UI.StudentLife
{
    [DisallowMultipleComponent]
    public sealed class StudentDayEndInteractor : MonoBehaviour, IPlayerInteractable, IPrioritizedPlayerInteractable
    {
        private const int DayEndInteractionPriority = 80;

        [SerializeField] private DayEndRuleDefinition _dayEndRules;
        [SerializeField] private StudentDayResultPanel _resultPanel;

        public DayEndRuleDefinition DayEndRules => _dayEndRules;
        public int InteractionPriority => DayEndInteractionPriority;
        public string InteractionPrompt => "[E] End Day";
        public Vector3 InteractionPromptOffset => new Vector3(0f, 1.15f, 0f);
        public Transform InteractionTransform => transform;

        public void Bind(DayEndRuleDefinition dayEndRules, StudentDayResultPanel resultPanel)
        {
            _dayEndRules = dayEndRules;
            _resultPanel = resultPanel;
        }

        public bool CanInteract(GameObject player)
        {
            return player != null && player.GetComponent<StudentLifeProgressComponent>() != null && _dayEndRules != null && _resultPanel != null;
        }

        public bool TryInteract(GameObject player)
        {
            if (!CanInteract(player)) return false;

            var progressComponent = player.GetComponent<StudentLifeProgressComponent>();
            var progress = progressComponent.EnsureProgress();
            StudentDaySummary summary;
            StudentDayQuestInventorySummary questInventorySummary;
            bool endedCurrentDay = false;
            if (progress.DayState == StudentDayState.InProgress)
            {
                endedCurrentDay = progress.TryEndDay(_dayEndRules, out summary);
                questInventorySummary = BuildQuestInventorySummary(player);
                StudentLifeProgressPersistence.Save(progressComponent);
                SaveQuestInventorySummary(progress.SaveSlot, questInventorySummary);
            }
            else
            {
                summary = new StudentDaySummary(progress.CurrentDay, progress.GetPreviousDayActivityIds(), progress.GetPreviousDayResultLogIds(), progress.NextDayEntryPointId, progress.TutorialStageId, progress.NextObjectiveId, progress.NextGuideText);
                questInventorySummary = StudentDayQuestInventorySummaryPersistence.Load(progress.SaveSlot);
            }

            Debug.Log($"[ROOTBORN] Student day result player={progress.PlayerId} day={summary.DayNumber} activities={summary.CompletedActivityIds.Length} results={summary.ResultLogIds.Length} endedCurrentDay={endedCurrentDay}");

            if (endedCurrentDay && NetworkWorldTimeState.Active != null)
            {
                NetworkWorldTimeState.TryRequestDayEndReady();
            }

            var registry = Managers.Data != null ? Managers.Data.Registry : null;
            var milestones = MilestoneRegistryResolver.ResolveMilestones(registry);
            var inventory = player.GetComponent<PlayerInventory>();
            var milestoneProgress = MilestoneProgressPersistence.LoadOrCreate(progress.SaveSlot, progress.PlayerId, milestones);
            var milestoneApplyResult = milestoneProgress.ApplyDayResult(milestones, progress, inventory != null ? inventory.Inventory : null, endedCurrentDay ? "day:" + summary.DayNumber.ToString() : string.Empty);
            if (endedCurrentDay) MilestoneProgressPersistence.Save(milestoneProgress);
            var milestoneSummary = MilestoneDaySummary.FromApplyResult(milestoneApplyResult, milestones);
            var hud = UnityEngine.Object.FindFirstObjectByType<MilestoneHudPanel>(FindObjectsInactive.Include);
            if (hud != null) hud.Refresh(milestones, milestoneProgress, milestoneApplyResult);

            var relationshipConditionSummary = BuildRelationshipConditionSummary(progress, summary.ResultLogIds);
            _resultPanel.Show(progressComponent, summary, questInventorySummary, relationshipConditionSummary, milestoneSummary);
            return true;
        }

        private static StudentDayRelationshipConditionSummary BuildRelationshipConditionSummary(StudentLifeProgress progress, string[] resultLogIds)
        {
            var registry = Managers.Data != null ? Managers.Data.Registry : null;
            return StudentDayRelationshipConditionSummaryBuilder.BuildFromResultLogs(
                progress,
                resultLogIds,
                registry != null ? registry.Relationships : null,
                registry != null ? registry.StudentConditionStatuses : null);
        }

        private static StudentDayQuestInventorySummary BuildQuestInventorySummary(GameObject player)
        {
            var tracker = player.GetComponent<StudentDayQuestInventoryTracker>();
            if (tracker != null) return tracker.BuildSummary();
            var questPanel = UnityEngine.Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
            var inventory = player.GetComponent<PlayerInventory>();
            var after = StudentDayQuestInventorySnapshot.Capture(
                questPanel != null ? questPanel.QuestLog : null,
                questPanel != null ? questPanel.Quests : null,
                inventory != null ? inventory.Inventory : null);
            return StudentDayQuestInventorySummaryBuilder.Build(null, after);
        }

        private static void SaveQuestInventorySummary(string slotId, StudentDayQuestInventorySummary summary)
        {
            var metadata = ActiveSaveContext.Metadata;
            string activeSlot = metadata != null && !string.IsNullOrEmpty(metadata.SlotId) ? metadata.SlotId : slotId;
            StudentDayQuestInventorySummaryPersistence.Save(activeSlot, summary);
        }
    }
}
