using System;
using Rootborn.Game.Managers;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using Rootborn.Network.Time;
using Rootborn.UI.Quests;
using Rootborn.UI.WorldState;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rootborn.UI.StudentLife
{
    [DisallowMultipleComponent]
    public sealed class StudentDayEndInteractor : MonoBehaviour, IPlayerInteractable, IPrioritizedPlayerInteractable
    {
        private const int DayEndInteractionPriority = 80;
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";

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

            var campaignSummary = CampaignRuntimeInstaller.BuildDayEndSummary(player, progress, endedCurrentDay);
            var relationshipConditionSummary = BuildRelationshipConditionSummary(progress, summary.ResultLogIds);
            var worldStateSummaries = BuildWorldStateSummary(player, progress, registry);
            var locationStateSummaries = BuildLocationStateSummary(player, progress);
            _resultPanel.Show(progressComponent, summary, questInventorySummary, relationshipConditionSummary, milestoneSummary);
            _resultPanel.ShowWorldStateChanges(worldStateSummaries);
            _resultPanel.ShowLocationStates(locationStateSummaries);
            AppendCampaignSummary(_resultPanel.transform, campaignSummary);
            AppendQuestChainSummary(_resultPanel.transform, QuestChainRuntimeInstaller.BuildDayEndSummary());
            return true;
        }

        private static WorldStateSummaryModel[] BuildWorldStateSummary(GameObject player, StudentLifeProgress progress, Rootborn.Game.Common.GameDataRegistry registry)
        {
            if (progress == null)
            {
                return Array.Empty<WorldStateSummaryModel>();
            }

            string playerId = ResolvePlayerId(player, progress.PlayerId);
            string saveSlot = ResolveActiveSaveSlot(progress.SaveSlot);
            var worldStateProgress = WorldStateProgressPersistence.LoadOrCreate(saveSlot, playerId);
            return WorldStateSummaryBuilder.BuildForSurface(ResolveWorldStateFlags(registry), worldStateProgress, WorldStateSummarySurface.DayResult);
        }

        private static LocationStateTodaySummarySaveData[] BuildLocationStateSummary(GameObject player, StudentLifeProgress progress)
        {
            if (progress == null) return Array.Empty<LocationStateTodaySummarySaveData>();
            string playerId = ResolvePlayerId(player, progress.PlayerId);
            string saveSlot = ResolveActiveSaveSlot(progress.SaveSlot);
            var locationStateProgress = LocationStateProgressPersistence.LoadOrCreate(saveSlot, playerId);
            return locationStateProgress.TodaySummary;
        }

        private static WorldStateFlagDefinition[] ResolveWorldStateFlags(Rootborn.Game.Common.GameDataRegistry registry)
        {
            if (registry != null && registry.WorldStateFlags != null && registry.WorldStateFlags.Length > 0) return registry.WorldStateFlags;
#if UNITY_EDITOR
            var editorRegistry = AssetDatabase.LoadAssetAtPath<Rootborn.Game.Common.GameDataRegistry>(RegistryPath);
            if (editorRegistry != null && editorRegistry.WorldStateFlags != null) return editorRegistry.WorldStateFlags;
#endif
            return Array.Empty<WorldStateFlagDefinition>();
        }

        private static string ResolveActiveSaveSlot(string fallback)
        {
            var metadata = ActiveSaveContext.Metadata;
            if (metadata != null && !string.IsNullOrEmpty(metadata.SlotId)) return metadata.SlotId;
            return string.IsNullOrEmpty(fallback) ? "default" : fallback;
        }

        private static string ResolvePlayerId(GameObject player, string fallback)
        {
            var identity = player != null ? player.GetComponent<PlayerIdentity>() : null;
            if (identity != null && !string.IsNullOrEmpty(identity.PlayerId)) return identity.PlayerId;
            return string.IsNullOrEmpty(fallback) ? PlayerIdentity.DefaultPlayerId : fallback;
        }

        private static void AppendCampaignSummary(Transform panel, CampaignDayResultSummary summary)
        {
            if (panel == null || string.IsNullOrEmpty(summary.ProgressText)) return;
            var existing = FindChild(panel, "CampaignResultSummary");
            GameObject go;
            if (existing != null) go = existing.gameObject;
            else
            {
                go = new GameObject("CampaignResultSummary", typeof(RectTransform), typeof(Text));
                go.transform.SetParent(panel, false);
            }

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -336f);
            rt.sizeDelta = new Vector2(760f, 56f);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.25f, 0.18f, 0.12f, 1f);
            text.raycastTarget = false;
            text.text = "Campaign\n" + summary.SelectedRouteText + "\n" + summary.ProgressText + "\n" + summary.NextGuideText;
        }

        private static void AppendQuestChainSummary(Transform panel, string summaryText)
        {
            if (panel == null || string.IsNullOrEmpty(summaryText)) return;
            var existing = FindChild(panel, "QuestChainResultSummary");
            GameObject go;
            if (existing != null) go = existing.gameObject;
            else
            {
                go = new GameObject("QuestChainResultSummary", typeof(RectTransform), typeof(Text));
                go.transform.SetParent(panel, false);
            }

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -400f);
            rt.sizeDelta = new Vector2(760f, 58f);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.25f, 0.18f, 0.12f, 1f);
            text.raycastTarget = false;
            text.text = "Quest Chains\n" + summaryText;
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindChild(root.GetChild(i), name);
                if (match != null) return match;
            }
            return null;
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
