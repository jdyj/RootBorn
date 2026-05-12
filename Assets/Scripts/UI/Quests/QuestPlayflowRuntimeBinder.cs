using System.Collections;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.UI.Quests
{
    public static class QuestPlayflowRuntimeBinder
    {
        private const string FarmSceneName = "Farm";
        private const string TownSceneName = "Town";
        private const string RunnerName = "[QuestPlayflowRuntimeBinder]";
        private const string QuestLogFileName = "quest-log.json";
        private const string InventoryFileName = "inventory.json";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForActiveScene();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (SupportsScene(scene.name))
            {
                StartRunner(scene);
            }
        }

        private static void EnsureForActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (SupportsScene(scene.name))
            {
                StartRunner(scene);
            }
        }

        private static bool SupportsScene(string sceneName)
        {
            return sceneName == FarmSceneName || sceneName == TownSceneName;
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
            private QuestLog _questLog;
            private GatherInteractor _boundGather;
            private PlayerInventory _boundInventory;
            private Inventory _subscribedInventory;
            private string _loadedInventoryKey;
            private string _playerId = PlayerIdentity.DefaultPlayerId;

            private IEnumerator Start()
            {
                while (true)
                {
                    BindCurrentSceneObjects();
                    yield return null;
                }
            }

            private void OnDestroy()
            {
                if (_boundGather != null)
                {
                    _boundGather.BindQuestEvents(null);
                    _boundGather = null;
                }

                if (_questLog != null)
                {
                    _questLog.OnStateChanged -= HandleQuestStateChanged;
                    _questLog = null;
                }
            }

            private void BindCurrentSceneObjects()
            {
                var panel = FindComponentInScene<QuestLogPanel>(gameObject.scene);
                var nextQuestLog = panel != null ? panel.QuestLog : null;
                var player = FindPlayerInScene(gameObject.scene);
                var nextGather = player != null ? player.GetComponent<GatherInteractor>() : null;
                var nextInventory = player != null ? player.GetComponent<PlayerInventory>() : null;
                var identity = player != null ? player.GetComponent<PlayerIdentity>() : null;
                var nextPlayerId = identity != null ? identity.PlayerId : PlayerIdentity.DefaultPlayerId;

                if (_questLog != nextQuestLog)
                {
                    if (_questLog != null)
                    {
                        _questLog.OnStateChanged -= HandleQuestStateChanged;
                    }

                    _questLog = nextQuestLog;

                    if (_questLog != null)
                    {
                        _questLog.OnStateChanged += HandleQuestStateChanged;
                    }
                }

                _playerId = nextPlayerId;
                BindInventory(nextInventory, nextPlayerId);

                if (_boundGather != nextGather)
                {
                    if (_boundGather != null)
                    {
                        _boundGather.BindQuestEvents(null);
                    }

                    _boundGather = nextGather;
                }

                if (_boundGather != null)
                {
                    _boundGather.BindQuestEvents(_questLog);
                }
            }

            private void BindInventory(PlayerInventory inventory, string playerId)
            {
                var metadata = ActiveSaveContext.Metadata;
                string key = metadata != null ? metadata.SlotId + ":" + playerId : string.Empty;
                if (_boundInventory == inventory && _loadedInventoryKey == key)
                {
                    return;
                }

                UnbindInventory();
                _boundInventory = inventory;
                _loadedInventoryKey = key;
                if (_boundInventory == null || metadata == null || string.IsNullOrEmpty(metadata.SlotId))
                {
                    return;
                }

                LoadInventory(_boundInventory, playerId);
                _subscribedInventory = _boundInventory.Inventory;
                _subscribedInventory.OnChanged += HandleInventoryChanged;
            }

            private void UnbindInventory()
            {
                if (_subscribedInventory != null)
                {
                    _subscribedInventory.OnChanged -= HandleInventoryChanged;
                    _subscribedInventory = null;
                }

                _boundInventory = null;
            }

            private void HandleInventoryChanged()
            {
                SaveInventory(_boundInventory, _playerId);
                Debug.Log("[ROOTBORN] Inventory changed player=" + _playerId + " " + InventorySummary(_boundInventory));
            }

            private void HandleQuestStateChanged(QuestStateChange change)
            {
                SaveQuestLog(_questLog, _playerId);
                string questId = change.Quest != null && !string.IsNullOrEmpty(change.Quest.Id) ? change.Quest.Id : string.Empty;
                Debug.Log("[ROOTBORN] Quest state changed player=" + _playerId + " quest=" + questId + " state=" + change.State + " kind=" + change.Kind + " objective=" + change.ObjectiveIndex + " count=" + change.ObjectiveCount);
            }
        }

        private static string InventorySummary(PlayerInventory inventory)
        {
            if (inventory == null || inventory.Inventory == null)
            {
                return "inventory=none";
            }

            var slots = inventory.Inventory.Slots;
            if (slots == null || slots.Count == 0)
            {
                return "inventory=empty";
            }

            string summary = "inventory=";
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.Item == null || slot.Count <= 0)
                {
                    continue;
                }

                string itemId = string.IsNullOrEmpty(slot.Item.Id) ? slot.Item.name : slot.Item.Id;
                summary += (summary == "inventory=" ? string.Empty : ",") + itemId + ":" + slot.Count;
            }

            return summary == "inventory=" ? "inventory=empty" : summary;
        }

        private static void LoadInventory(PlayerInventory inventory, string playerId)
        {
            var metadata = ActiveSaveContext.Metadata;
            if (inventory == null || metadata == null || string.IsNullOrEmpty(metadata.SlotId))
            {
                return;
            }

            string json = new SaveService(metadata.SlotId).ReadJson(InventoryFileNameFor(playerId));
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            inventory.LoadFromSaveData(JsonUtility.FromJson<PlayerInventorySaveData>(json));
        }

        private static void SaveInventory(PlayerInventory inventory, string playerId)
        {
            var metadata = ActiveSaveContext.Metadata;
            if (inventory == null || metadata == null || string.IsNullOrEmpty(metadata.SlotId))
            {
                return;
            }

            string json = JsonUtility.ToJson(inventory.ToSaveData(), true);
            new SaveService(metadata.SlotId).WriteJson(InventoryFileNameFor(playerId), json);
        }

        private static void SaveQuestLog(QuestLog questLog, string playerId)
        {
            if (questLog == null)
            {
                return;
            }

            var metadata = ActiveSaveContext.Metadata;
            if (metadata == null || string.IsNullOrEmpty(metadata.SlotId))
            {
                return;
            }

            var json = JsonUtility.ToJson(questLog.ToSaveData(), true);
            new SaveService(metadata.SlotId).WriteJson(QuestLogFileNameFor(playerId), json);
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

        private static GameObject FindPlayerInScene(Scene scene)
        {
            return FindLocalInputPlayerInScene(scene) ?? FindNamedPlayerInScene(scene);
        }

        private static GameObject FindLocalInputPlayerInScene(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var match = FindLocalInputPlayerInChildren(roots[i].transform);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static GameObject FindLocalInputPlayerInChildren(Transform root)
        {
            var controller = root.GetComponent<PlayerController>();
            var router = root.GetComponent<PlayerInteractionRouter>();
            if (controller != null && router != null && controller.enabled && router.enabled)
            {
                return root.gameObject;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindLocalInputPlayerInChildren(root.GetChild(i));
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static GameObject FindNamedPlayerInScene(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if ((root.name == "Player" || root.name == "Player(Clone)") && root.activeInHierarchy)
                {
                    return root;
                }
            }

            return null;
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var match = FindComponentInChildren<T>(roots[i].transform);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static T FindComponentInChildren<T>(Transform root) where T : Component
        {
            if (root.TryGetComponent<T>(out var component))
            {
                return component;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindComponentInChildren<T>(root.GetChild(i));
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static string InventoryFileNameFor(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == PlayerIdentity.DefaultPlayerId)
            {
                return InventoryFileName;
            }

            return "inventory-" + SanitizeFileName(playerId) + ".json";
        }

        private static string QuestLogFileNameFor(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == PlayerIdentity.DefaultPlayerId)
            {
                return QuestLogFileName;
            }

            return "quest-log-" + SanitizeFileName(playerId) + ".json";
        }

        private static string SanitizeFileName(string value)
        {
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }
    }
}
