using System;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.UI.StudentLife
{
    [DisallowMultipleComponent]
    public sealed class StudentDayQuestInventoryTracker : MonoBehaviour
    {
        private StudentDayQuestInventorySnapshot _baseline;
        private QuestLogPanel _questPanel;
        private PlayerInventory _inventory;

        public bool HasBaseline => _baseline != null;

        public void CaptureBaseline(QuestLogPanel questPanel, PlayerInventory inventory)
        {
            _questPanel = questPanel;
            _inventory = inventory;
            _baseline = CaptureCurrent();
        }

        public StudentDayQuestInventorySummary BuildSummary()
        {
            var before = _baseline ?? StudentDayQuestInventorySnapshot.Capture(null, null, null);
            var after = CaptureCurrent();
            return StudentDayQuestInventorySummaryBuilder.Build(before, after);
        }

        private StudentDayQuestInventorySnapshot CaptureCurrent()
        {
            return StudentDayQuestInventorySnapshot.Capture(
                _questPanel != null ? _questPanel.QuestLog : null,
                _questPanel != null ? _questPanel.Quests : null,
                _inventory != null ? _inventory.Inventory : null);
        }
    }

    public static class StudentDayQuestInventoryTrackerInstaller
    {
        private const string TownSceneName = "Town";
        private const string RunnerName = "[StudentDayQuestInventoryTrackerInstaller]";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForActiveScene();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TownSceneName)
            {
                StartRunner(scene);
            }
        }

        private static void EnsureForActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name == TownSceneName)
            {
                StartRunner(scene);
            }
        }

        private static void StartRunner(Scene scene)
        {
            if (FindRoot(scene, RunnerName) != null)
            {
                return;
            }

            var go = new GameObject(RunnerName);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private System.Collections.IEnumerator Start()
            {
                float elapsed = 0f;
                while (elapsed < 5f)
                {
                    var player = GameObject.Find("Player");
                    var questPanel = UnityEngine.Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
                    var inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
                    if (player != null && questPanel != null && questPanel.QuestLog != null && inventory != null)
                    {
                        var tracker = player.GetComponent<StudentDayQuestInventoryTracker>();
                        if (tracker == null)
                        {
                            tracker = player.AddComponent<StudentDayQuestInventoryTracker>();
                        }

                        if (!tracker.HasBaseline)
                        {
                            tracker.CaptureBaseline(questPanel, inventory);
                        }

                        yield break;
                    }

                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name)
                {
                    return roots[i];
                }
            }

            return null;
        }
    }

    [Serializable]
    public sealed class StudentDayQuestInventorySummarySaveData
    {
        public QuestSummaryEntrySaveData[] QuestEntries = Array.Empty<QuestSummaryEntrySaveData>();
        public InventoryDeltaEntrySaveData[] InventoryDeltas = Array.Empty<InventoryDeltaEntrySaveData>();
        public RewardSummaryEntrySaveData[] RewardEntries = Array.Empty<RewardSummaryEntrySaveData>();
    }

    [Serializable]
    public struct QuestSummaryEntrySaveData
    {
        public string QuestId;
        public string DisplayName;
        public string BeforeState;
        public string AfterState;
    }

    [Serializable]
    public struct InventoryDeltaEntrySaveData
    {
        public string ItemId;
        public string DisplayName;
        public int BeforeCount;
        public int AfterCount;
        public int Delta;
        public bool FromReward;
    }

    [Serializable]
    public struct RewardSummaryEntrySaveData
    {
        public string QuestId;
        public string QuestDisplayName;
        public string ItemId;
        public string ItemDisplayName;
        public int Count;
    }

    public static class StudentDayQuestInventorySummaryPersistence
    {
        private const string FileName = "student-day-result-quest-inventory-summary.json";

        public static void Save(string slotId, StudentDayQuestInventorySummary summary)
        {
            if (string.IsNullOrEmpty(slotId))
            {
                return;
            }

            new SaveService(slotId).WriteJson(FileName, JsonUtility.ToJson(ToSaveData(summary), true));
        }

        public static StudentDayQuestInventorySummary Load(string slotId)
        {
            if (string.IsNullOrEmpty(slotId))
            {
                return Empty();
            }

            string json = new SaveService(slotId).ReadJson(FileName);
            if (string.IsNullOrEmpty(json))
            {
                return Empty();
            }

            return FromSaveData(JsonUtility.FromJson<StudentDayQuestInventorySummarySaveData>(json));
        }

        private static StudentDayQuestInventorySummary Empty()
        {
            return new StudentDayQuestInventorySummary(null, null, null);
        }

        private static StudentDayQuestInventorySummarySaveData ToSaveData(StudentDayQuestInventorySummary summary)
        {
            var data = new StudentDayQuestInventorySummarySaveData();
            data.QuestEntries = new QuestSummaryEntrySaveData[summary.QuestEntries.Length];
            for (int i = 0; i < summary.QuestEntries.Length; i++)
            {
                var entry = summary.QuestEntries[i];
                data.QuestEntries[i] = new QuestSummaryEntrySaveData
                {
                    QuestId = entry.QuestId,
                    DisplayName = entry.DisplayName,
                    BeforeState = entry.BeforeState.ToString(),
                    AfterState = entry.AfterState.ToString(),
                };
            }

            data.InventoryDeltas = new InventoryDeltaEntrySaveData[summary.InventoryDeltas.Length];
            for (int i = 0; i < summary.InventoryDeltas.Length; i++)
            {
                var entry = summary.InventoryDeltas[i];
                data.InventoryDeltas[i] = new InventoryDeltaEntrySaveData
                {
                    ItemId = entry.ItemId,
                    DisplayName = entry.DisplayName,
                    BeforeCount = entry.BeforeCount,
                    AfterCount = entry.AfterCount,
                    Delta = entry.Delta,
                    FromReward = entry.FromReward,
                };
            }

            data.RewardEntries = new RewardSummaryEntrySaveData[summary.RewardEntries.Length];
            for (int i = 0; i < summary.RewardEntries.Length; i++)
            {
                var entry = summary.RewardEntries[i];
                data.RewardEntries[i] = new RewardSummaryEntrySaveData
                {
                    QuestId = entry.QuestId,
                    QuestDisplayName = entry.QuestDisplayName,
                    ItemId = entry.ItemId,
                    ItemDisplayName = entry.ItemDisplayName,
                    Count = entry.Count,
                };
            }

            return data;
        }

        private static StudentDayQuestInventorySummary FromSaveData(StudentDayQuestInventorySummarySaveData data)
        {
            if (data == null)
            {
                return Empty();
            }

            var quests = new QuestSummaryEntry[data.QuestEntries != null ? data.QuestEntries.Length : 0];
            for (int i = 0; i < quests.Length; i++)
            {
                var entry = data.QuestEntries[i];
                quests[i] = new QuestSummaryEntry(entry.QuestId, entry.DisplayName, ParseState(entry.BeforeState), ParseState(entry.AfterState));
            }

            var inventory = new InventoryDeltaEntry[data.InventoryDeltas != null ? data.InventoryDeltas.Length : 0];
            for (int i = 0; i < inventory.Length; i++)
            {
                var entry = data.InventoryDeltas[i];
                inventory[i] = new InventoryDeltaEntry(entry.ItemId, entry.DisplayName, entry.BeforeCount, entry.AfterCount, entry.FromReward);
            }

            var rewards = new RewardSummaryEntry[data.RewardEntries != null ? data.RewardEntries.Length : 0];
            for (int i = 0; i < rewards.Length; i++)
            {
                var entry = data.RewardEntries[i];
                rewards[i] = new RewardSummaryEntry(entry.QuestId, entry.QuestDisplayName, entry.ItemId, entry.ItemDisplayName, entry.Count);
            }

            return new StudentDayQuestInventorySummary(quests, inventory, rewards);
        }

        private static QuestState ParseState(string value)
        {
            return Enum.TryParse(value, out QuestState state) ? state : QuestState.NotStarted;
        }
    }
}
