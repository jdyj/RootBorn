using System;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Rewards;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.Quests
{
    [DisallowMultipleComponent]
    public sealed class QuestLogPanel : MonoBehaviour
    {
        private static readonly string[] GeneratedChildNames =
        {
            "QuestTitleTab",
            "QuestList",
            "QuestDetail",
            "ObjectiveProgress",
            "RewardRow",
            "QuestScrollbar",
            "ClaimButton"
        };

        private QuestLog _questLog;
        private QuestDefinition[] _quests = Array.Empty<QuestDefinition>();
        private RewardRuntimeContext _rewardContext;

        public QuestLog QuestLog => _questLog;
        public QuestDefinition[] Quests => _quests;

        private void OnDestroy()
        {
            UnsubscribeQuestLog();
        }

        public void Bind(QuestLog questLog)
        {
            SetQuestLog(questLog);
            _quests = Array.Empty<QuestDefinition>();
            _rewardContext = default;
            RebuildModernUiStructure();
            UpdateLegacySummaryText(null);
        }

        public void Bind(QuestLog questLog, QuestDefinition[] quests, RewardRuntimeContext context)
        {
            SetQuestLog(questLog);
            _quests = quests ?? Array.Empty<QuestDefinition>();
            _rewardContext = context;
            RebuildModernUiStructure();
            PopulateQuestContent();
        }

        private void SetQuestLog(QuestLog questLog)
        {
            if (_questLog == questLog)
            {
                return;
            }

            UnsubscribeQuestLog();
            _questLog = questLog;
            if (_questLog != null)
            {
                _questLog.OnStateChanged += HandleQuestStateChanged;
            }
        }

        private void UnsubscribeQuestLog()
        {
            if (_questLog != null)
            {
                _questLog.OnStateChanged -= HandleQuestStateChanged;
            }
        }

        private void HandleQuestStateChanged(QuestStateChange change)
        {
            RefreshQuestContent();
        }

        private void RebuildModernUiStructure()
        {
            ClearGeneratedChildren();

            var root = transform as RectTransform;
            if (root != null)
            {
                if (root.sizeDelta == Vector2.zero)
                {
                    root.sizeDelta = new Vector2(520f, 360f);
                }
            }

            MakeTilePanel("QuestTitleTab", new Vector2(0f, 154f), new Vector2(192f, 40f));
            MakeTilePanel("QuestList", new Vector2(-146f, 24f), new Vector2(208f, 256f));
            MakeTilePanel("QuestDetail", new Vector2(120f, 58f), new Vector2(256f, 188f));
            MakeTilePanel("ObjectiveProgress", new Vector2(120f, -62f), new Vector2(256f, 48f));
            MakeTilePanel("RewardRow", new Vector2(72f, -124f), new Vector2(160f, 48f));
            MakeTilePanel("QuestScrollbar", new Vector2(-22f, 24f), new Vector2(24f, 256f));
            MakeClaimButton(new Vector2(196f, -124f), new Vector2(96f, 48f), null);
        }

        private void PopulateQuestContent()
        {
            QuestDefinition selectedQuest = SelectQuestForDetail();
            for (int i = 0; i < _quests.Length; i++)
            {
                var quest = _quests[i];
                if (quest == null)
                {
                    continue;
                }

                MakeQuestRow(quest, i);
            }

            PopulateQuestDetail(selectedQuest);
            BindClaimButton(selectedQuest);
            UpdateLegacySummaryText(selectedQuest);
        }

        private void RefreshQuestContent()
        {
            QuestDefinition selectedQuest = SelectQuestForDetail();
            PopulateQuestDetail(selectedQuest);
            BindClaimButton(selectedQuest);
            UpdateLegacySummaryText(selectedQuest);
        }

        private QuestDefinition SelectQuestForDetail()
        {
            QuestDefinition fallback = null;
            for (int i = 0; i < _quests.Length; i++)
            {
                var quest = _quests[i];
                if (quest == null)
                {
                    continue;
                }

                fallback ??= quest;
                if (_questLog == null)
                {
                    continue;
                }

                var state = _questLog.GetState(quest);
                if (state == QuestState.Completed || state == QuestState.Active || state == QuestState.RewardClaimed)
                {
                    return quest;
                }
            }

            return fallback;
        }

        private void MakeQuestRow(QuestDefinition quest, int index)
        {
            var list = transform.Find("QuestList");
            if (list == null)
            {
                return;
            }

            var row = new GameObject("QuestRow_" + quest.Id, typeof(RectTransform), typeof(ModernUiTileImage));
            row.transform.SetParent(list, false);
            var rect = (RectTransform)row.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -12f - index * 40f);
            rect.sizeDelta = new Vector2(184f, 32f);

            var tiles = row.GetComponent<ModernUiTileImage>();
            tiles.SetRecipe(ModernUiRecipes.CommonPanel);
            tiles.Rebuild();
            DisableGeneratedTileSprites(row.transform);

            MakeText(row.transform, "QuestRowText", quest.DisplayNameKey, Vector2.zero, rect.sizeDelta - new Vector2(16f, 8f));
        }

        private void PopulateQuestDetail(QuestDefinition quest)
        {
            var detail = transform.Find("QuestDetail");
            if (detail != null)
            {
                string detailText = quest == null ? string.Empty : BuildQuestDetailText(quest);
                SetOrMakeText(detail, "DetailText", detailText, Vector2.zero, new Vector2(224f, 156f));
            }

            var objective = transform.Find("ObjectiveProgress");
            if (objective != null)
            {
                string objectiveText = quest == null || _questLog == null ? string.Empty : BuildObjectiveText(quest);
                SetOrMakeText(objective, "ObjectiveText", objectiveText, Vector2.zero, new Vector2(224f, 32f));
            }

            var reward = transform.Find("RewardRow");
            if (reward != null)
            {
                string rewardText = quest == null ? string.Empty : BuildRewardText(quest);
                SetOrMakeText(reward, "RewardText", rewardText, Vector2.zero, new Vector2(128f, 32f));
            }
        }

        private void UpdateLegacySummaryText(QuestDefinition quest)
        {
            var legacy = transform.Find("ActiveQuest");
            if (legacy == null || !legacy.TryGetComponent<Text>(out var text))
            {
                return;
            }

            if (quest == null || _questLog == null)
            {
                text.text = "No active quests";
                return;
            }

            text.text = SafeText(quest.DisplayNameKey, quest.Id) + "\n" + BuildObjectiveText(quest) + "\n" + BuildRewardText(quest);
        }

        private string BuildQuestDetailText(QuestDefinition quest)
        {
            return SafeText(quest.DisplayNameKey, quest.Id) + "\n" + SafeText(quest.DescriptionKey, string.Empty);
        }

        private string BuildObjectiveText(QuestDefinition quest)
        {
            var state = _questLog.GetState(quest);
            if (quest.Objectives == null || quest.Objectives.Length == 0 || quest.Objectives[0] == null)
            {
                return state.ToString();
            }

            var objective = quest.Objectives[0];
            int current = _questLog.GetObjectiveCount(quest, 0);
            int required = objective.RequiredCount;
            string label = SafeText(objective.DisplayKey, objective.GetType().Name);
            return state + " | " + label + " " + current + " / " + required;
        }

        private string BuildRewardText(QuestDefinition quest)
        {
            string prefix = _questLog != null && _questLog.GetState(quest) == QuestState.RewardClaimed ? "RewardClaimed: " : "Rewards: ";
            if (quest.Rewards == null || quest.Rewards.Length == 0)
            {
                return prefix + "0";
            }

            for (int i = 0; i < quest.Rewards.Length; i++)
            {
                if (quest.Rewards[i] is ItemQuestReward itemReward && itemReward.Item != null)
                {
                    return prefix + SafeText(itemReward.Item.DisplayKey, itemReward.Item.Id) + " x" + itemReward.Count;
                }
            }

            return prefix + quest.Rewards.Length;
        }

        private void BindClaimButton(QuestDefinition quest)
        {
            var rewardButton = transform.Find("ClaimButton")?.GetComponent<QuestRewardButton>();
            if (rewardButton != null)
            {
                rewardButton.Bind(_questLog, quest, _rewardContext);
            }
        }

        private void ClearGeneratedChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (!IsGeneratedChild(child.name))
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private static bool IsGeneratedChild(string childName)
        {
            for (int i = 0; i < GeneratedChildNames.Length; i++)
            {
                if (GeneratedChildNames[i] == childName)
                {
                    return true;
                }
            }

            return false;
        }

        private GameObject MakeTilePanel(string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(ModernUiTileImage));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var tiles = go.GetComponent<ModernUiTileImage>();
            tiles.SetRecipe(ModernUiRecipes.CommonPanel);
            tiles.Rebuild();
            DisableGeneratedTileSprites(go.transform);
            return go;
        }

        private void MakeClaimButton(Vector2 position, Vector2 size, QuestDefinition quest)
        {
            var go = new GameObject("ClaimButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(ModernUiTileImage), typeof(QuestRewardButton));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;

            var tiles = go.GetComponent<ModernUiTileImage>();
            tiles.SetRecipe(ModernUiRecipes.CommonPanel);
            tiles.Rebuild();
            DisableGeneratedTileSprites(go.transform);

            go.GetComponent<QuestRewardButton>().Bind(_questLog, quest, _rewardContext);
        }

        private static Text SetOrMakeText(Transform parent, string name, string text, Vector2 position, Vector2 size)
        {
            var existing = parent.Find(name);
            if (existing != null && existing.TryGetComponent<Text>(out var existingText))
            {
                existingText.text = text ?? string.Empty;
                return existingText;
            }

            return MakeText(parent, name, text, position, size);
        }

        private static Text MakeText(Transform parent, string name, string text, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var label = go.GetComponent<Text>();
            label.text = text ?? string.Empty;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.fontSize = 14;
            return label;
        }

        private static void DisableGeneratedTileSprites(Transform root)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name.StartsWith("Tile_", StringComparison.Ordinal) && child.TryGetComponent<Image>(out var image))
                {
                    image.enabled = false;
                }
            }
        }

        private static string SafeText(string primary, string fallback)
        {
            return string.IsNullOrEmpty(primary) ? fallback : primary;
        }
    }
}
