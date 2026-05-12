using System.Collections;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Managers;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.Story;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.Quests
{
    public static class QuestHudAutoFiller
    {
        private const string FarmSceneName = "Farm";
        private const string TownSceneName = "Town";
        private const string RunnerName = "[QuestHudAutoFiller]";
        private const string QuestLogFileName = "quest-log.json";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForActiveScene();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!SupportsScene(scene.name))
            {
                return;
            }

            StartRunner(scene);
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
            go.AddComponent<QuestHudAutoFillRunner>();
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

        private sealed class QuestHudAutoFillRunner : MonoBehaviour
        {
            private readonly StoryFlagSet _storyFlags = new StoryFlagSet();

            private IEnumerator Start()
            {
                GameDataRegistry registry = null;
                float elapsed = 0f;
                while (registry == null && elapsed < 5f)
                {
                    registry = Managers.Data != null ? Managers.Data.Registry : null;
                    if (registry == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                PlayerInventory playerInventory = null;
                StudentLifeProgressComponent studentLife = null;
                elapsed = 0f;
                while (playerInventory == null && elapsed < 5f)
                {
                    var player = FindLocalInputPlayerInScene(SceneManager.GetActiveScene()) ?? FindByName(SceneManager.GetActiveScene(), "Player");
                    if (player != null)
                    {
                        playerInventory = player.GetComponent<PlayerInventory>();
                        studentLife = player.GetComponent<StudentLifeProgressComponent>();
                    }

                    if (playerInventory == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                EnsureQuestUi(registry, gameObject, playerInventory, studentLife, _storyFlags);
            }

            private static void EnsureQuestUi(GameDataRegistry registry, GameObject runner, PlayerInventory playerInventory, StudentLifeProgressComponent studentLife, StoryFlagSet storyFlags)
            {
                EnsureEventSystem();
                var canvas = EnsureCanvas();
                string playerId = ResolvePlayerId(playerInventory);
                var metadata = ActiveSaveContext.Metadata;
                var questLog = metadata != null && !string.IsNullOrEmpty(metadata.SlotId)
                    ? PlayerGlobalState.GetQuestLog(metadata.SlotId, playerId, registry != null ? registry.Quests : null)
                    : new QuestLog(registry != null ? registry.Quests : null);
                LoadQuestLog(questLog, playerId);

                var scene = SceneManager.GetActiveScene();
                var questPanelGo = FindByName(scene, "QuestLogPanel");
                if (questPanelGo == null)
                {
                    questPanelGo = CreatePanel(canvas.transform, "QuestLogPanel", new Vector2(360f, 220f), new Vector2(-24f, -260f));
                    CreateText(questPanelGo.transform, "Title", "Quests", new Vector2(0f, -24f), new Vector2(320f, 40f), 26, TextAnchor.MiddleCenter);
                    CreateText(questPanelGo.transform, "ActiveQuest", FirstQuestLabel(registry), new Vector2(0f, -86f), new Vector2(310f, 96f), 20, TextAnchor.UpperLeft);
                }
                questPanelGo.transform.SetParent(canvas.transform, false);
                questPanelGo.transform.SetAsLastSibling();

                var questPanel = questPanelGo.GetComponent<QuestLogPanel>();
                if (questPanel == null)
                {
                    questPanel = questPanelGo.AddComponent<QuestLogPanel>();
                }
                var inventory = playerInventory != null ? playerInventory.Inventory : null;
                var rewardContext = new RewardRuntimeContext(questLog, inventory, null, storyFlags, studentLife != null ? studentLife.EnsureProgress() : null);
                questPanel.Bind(questLog, registry != null ? registry.Quests : null, rewardContext);

                var dialogueGo = FindByName(scene, "DialoguePanel");
                if (dialogueGo == null)
                {
                    dialogueGo = CreatePanel(canvas.transform, "DialoguePanel", new Vector2(760f, 190f), new Vector2(-580f, -820f));
                    CreateText(dialogueGo.transform, "Speaker", "Guide", new Vector2(0f, -24f), new Vector2(700f, 40f), 24, TextAnchor.MiddleCenter);
                    CreateText(dialogueGo.transform, "Choice", "Accept Quest", new Vector2(0f, -92f), new Vector2(700f, 72f), 20, TextAnchor.MiddleCenter);
                }
                dialogueGo.transform.SetParent(canvas.transform, false);
                dialogueGo.transform.SetAsLastSibling();

                var dialoguePanel = dialogueGo.GetComponent<DialoguePanel>();
                if (dialoguePanel == null)
                {
                    dialoguePanel = dialogueGo.AddComponent<DialoguePanel>();
                }
                dialogueGo.SetActive(false);

                var binder = runner.GetComponent<QuestDialogueUiBinder>();
                if (binder == null)
                {
                    binder = runner.AddComponent<QuestDialogueUiBinder>();
                }
                binder.Bind(dialoguePanel, questLog, playerInventory, studentLife, storyFlags, playerId);
                binder.AttachSceneNpcs();
            }

            private static void LoadQuestLog(QuestLog questLog, string playerId)
            {
                var metadata = ActiveSaveContext.Metadata;
                if (metadata == null || string.IsNullOrEmpty(metadata.SlotId))
                {
                    return;
                }

                var json = new SaveService(metadata.SlotId).ReadJson(QuestLogFileNameFor(playerId));
                if (string.IsNullOrEmpty(json))
                {
                    return;
                }

                var saveData = JsonUtility.FromJson<QuestLogSaveData>(json);
                questLog.LoadFromSaveData(saveData);
            }

            private static Canvas EnsureCanvas()
            {
                var scene = SceneManager.GetActiveScene();
                var canvas = FindComponentInScene<Canvas>(scene);
                if (canvas == null)
                {
                    var go = new GameObject("[SceneCanvas]", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                    SceneManager.MoveGameObjectToScene(go, scene);
                    canvas = go.GetComponent<Canvas>();
                    var scaler = go.GetComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920f, 1080f);
                }

                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 1000);
                if (canvas.GetComponent<GraphicRaycaster>() == null)
                {
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
                }
                return canvas;
            }

            private static void EnsureEventSystem()
            {
                var scene = SceneManager.GetActiveScene();
                var eventSystem = FindComponentInScene<EventSystem>(scene);
                if (eventSystem == null)
                {
                    var go = new GameObject("EventSystem", typeof(EventSystem));
                    AddUiInputModule(go);
                    SceneManager.MoveGameObjectToScene(go, scene);
                    return;
                }

                if (eventSystem.GetComponent<BaseInputModule>() == null)
                {
                    AddUiInputModule(eventSystem.gameObject);
                }
            }

            private static void AddUiInputModule(GameObject go)
            {
                UiInputModuleInstaller.AddPreferredInputModule(go);
            }

            private static GameObject CreatePanel(Transform parent, string name, Vector2 size, Vector2 anchoredPosition)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = anchoredPosition;
                rt.sizeDelta = size;
                go.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.12f, 0.86f);
                return go;
            }

            private static Text CreateText(Transform parent, string name, string text, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Text));
                go.transform.SetParent(parent, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = anchoredPosition;
                rt.sizeDelta = size;
                var label = go.GetComponent<Text>();
                label.text = text;
                label.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = fontSize;
                label.alignment = alignment;
                label.color = new Color(0.96f, 0.93f, 0.84f, 1f);
                return label;
            }

            private static string FirstQuestLabel(GameDataRegistry registry)
            {
                if (registry == null || registry.Quests == null || registry.Quests.Length == 0 || registry.Quests[0] == null)
                {
                    return "No active quests";
                }

                var quest = registry.Quests[0];
                return (!string.IsNullOrEmpty(quest.DisplayNameKey) ? quest.DisplayNameKey : quest.Id) + "\n0 / 1";
            }
        }

        private sealed class QuestDialogueUiBinder : MonoBehaviour
        {
            private StoryFlagSet _storyFlags = new StoryFlagSet();
            private DialoguePanel _dialoguePanel;
            private QuestLog _questLog;
            private PlayerInventory _playerInventory;
            private StudentLifeProgressComponent _studentLife;
            private string _playerId = PlayerIdentity.DefaultPlayerId;
            private NpcInteractor[] _attached = System.Array.Empty<NpcInteractor>();

            public void Bind(DialoguePanel dialoguePanel, QuestLog questLog, PlayerInventory playerInventory)
            {
                Bind(dialoguePanel, questLog, playerInventory, null, _storyFlags, ResolvePlayerId(playerInventory));
            }

            public void Bind(DialoguePanel dialoguePanel, QuestLog questLog, PlayerInventory playerInventory, StoryFlagSet storyFlags)
            {
                Bind(dialoguePanel, questLog, playerInventory, null, storyFlags, ResolvePlayerId(playerInventory));
            }

            public void Bind(DialoguePanel dialoguePanel, QuestLog questLog, PlayerInventory playerInventory, StudentLifeProgressComponent studentLife, StoryFlagSet storyFlags)
            {
                Bind(dialoguePanel, questLog, playerInventory, studentLife, storyFlags, ResolvePlayerId(playerInventory));
            }

            public void Bind(DialoguePanel dialoguePanel, QuestLog questLog, PlayerInventory playerInventory, StudentLifeProgressComponent studentLife, StoryFlagSet storyFlags, string playerId)
            {
                if (_dialoguePanel != null)
                {
                    _dialoguePanel.OnChoiceExecuted -= HandleChoiceExecuted;
                }

                _dialoguePanel = dialoguePanel;
                _questLog = questLog;
                _playerInventory = playerInventory;
                _studentLife = studentLife;
                _storyFlags = storyFlags ?? new StoryFlagSet();
                _playerId = string.IsNullOrEmpty(playerId) ? PlayerIdentity.DefaultPlayerId : playerId;

                if (_dialoguePanel != null)
                {
                    _dialoguePanel.OnChoiceExecuted += HandleChoiceExecuted;
                }
            }

            public void AttachSceneNpcs()
            {
                DetachAll();
                var npcs = new List<NpcInteractor>();
                CollectComponents(SceneManager.GetActiveScene(), npcs);
                _attached = npcs.ToArray();
                for (int i = 0; i < _attached.Length; i++)
                {
                    _attached[i].OnInteracted += HandleNpcInteracted;
                    _attached[i].BindQuestEvents(_questLog);
                }
            }

            private void OnDestroy()
            {
                if (_dialoguePanel != null)
                {
                    _dialoguePanel.OnChoiceExecuted -= HandleChoiceExecuted;
                }
                DetachAll();
            }

            private void DetachAll()
            {
                for (int i = 0; i < _attached.Length; i++)
                {
                    if (_attached[i] != null)
                    {
                        _attached[i].OnInteracted -= HandleNpcInteracted;
                    }
                }
                _attached = System.Array.Empty<NpcInteractor>();
            }

            private void HandleNpcInteracted(NpcInteractor npc)
            {
                if (_dialoguePanel == null || npc == null || npc.Npc == null)
                {
                    return;
                }

                var inventory = _playerInventory != null ? _playerInventory.Inventory : null;
                var progress = _studentLife != null ? _studentLife.EnsureProgress() : null;
                var rewardContext = new RewardRuntimeContext(_questLog, inventory, null, _storyFlags, progress);
                _dialoguePanel.Open(npc.Npc.ResolveDialogue(progress), new DialogueChoiceContext(_questLog, rewardContext));
            }

            private void HandleChoiceExecuted(bool success)
            {
                if (!success || _questLog == null)
                {
                    return;
                }

                var metadata = ActiveSaveContext.Metadata;
                if (metadata == null || string.IsNullOrEmpty(metadata.SlotId))
                {
                    return;
                }

                var json = JsonUtility.ToJson(_questLog.ToSaveData(), true);
                new SaveService(metadata.SlotId).WriteJson(QuestLogFileNameFor(_playerId), json);
            }
        }

        private static GameObject FindLocalInputPlayerInScene(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var player = FindLocalInputPlayerInChildren(roots[i].transform);
                if (player != null)
                {
                    return player;
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

        private static string ResolvePlayerId(PlayerInventory playerInventory)
        {
            if (playerInventory == null)
            {
                return PlayerIdentity.DefaultPlayerId;
            }

            var identity = playerInventory.GetComponent<PlayerIdentity>();
            return identity != null ? identity.PlayerId : PlayerIdentity.DefaultPlayerId;
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

        private static GameObject FindByName(Scene scene, string objectName)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var match = FindByName(roots[i].transform, objectName);
                if (match != null)
                {
                    return match.gameObject;
                }
            }

            return null;
        }

        private static Transform FindByName(Transform root, string objectName)
        {
            if (root.name == objectName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindByName(root.GetChild(i), objectName);
                if (match != null)
                {
                    return match;
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

        private static void CollectComponents<T>(Scene scene, List<T> results) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                CollectComponents(roots[i].transform, results);
            }
        }

        private static void CollectComponents<T>(Transform root, List<T> results) where T : Component
        {
            if (root.TryGetComponent<T>(out var component))
            {
                results.Add(component);
            }

            for (int i = 0; i < root.childCount; i++)
            {
                CollectComponents(root.GetChild(i), results);
            }
        }
    }
}
