using System.Collections;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.Story;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.UI.Quests
{
    public static class QuestDialogueChoiceRuntimeRestorer
    {
        private const string RunnerName = "[QuestDialogueChoiceRuntimeRestorer]";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForActiveScene();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Ensure(scene);
        }

        private static void EnsureForActiveScene()
        {
            Ensure(SceneManager.GetActiveScene());
        }

        private static void Ensure(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || FindRoot(scene, RunnerName) != null)
            {
                return;
            }

            var go = new GameObject(RunnerName);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private readonly StoryFlagSet _storyFlags = new StoryFlagSet();
            private DialoguePanel _pendingDialoguePanel;
            private System.Action<bool> _pendingChoiceHandler;

            private void OnEnable()
            {
                NpcInteractor.OnAnyInteracted += HandleNpcInteracted;
            }

            private void OnDisable()
            {
                NpcInteractor.OnAnyInteracted -= HandleNpcInteracted;
                ClearPendingChoiceHandler();
            }

            private void HandleNpcInteracted(NpcInteractor npc, StudentLifeProgress progress)
            {
                var provider = npc != null ? npc.GetComponent<QuestProvider>() : null;
                if (provider == null || provider.Quests == null || provider.Quests.Length == 0)
                {
                    return;
                }

                StartCoroutine(RestoreQuestDialogueNextFrame(npc, progress));
            }

            private IEnumerator RestoreQuestDialogueNextFrame(NpcInteractor npc, StudentLifeProgress progress)
            {
                yield return null;
                if (npc == null || npc.Npc == null)
                {
                    yield break;
                }

                var dialoguePanel = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
                var questPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
                if (dialoguePanel == null || questPanel == null || questPanel.QuestLog == null)
                {
                    yield break;
                }

                var player = FindPlayer();
                var inventoryComponent = player != null ? player.GetComponent<PlayerInventory>() : null;
                var studentComponent = player != null ? player.GetComponent<StudentLifeProgressComponent>() : null;
                var studentProgress = progress ?? (studentComponent != null ? studentComponent.EnsureProgress() : null);
                string playerId = ResolvePlayerId(inventoryComponent);
                var metadata = ActiveSaveContext.Metadata;
                var worldStateProgress = metadata != null && !string.IsNullOrEmpty(metadata.SlotId)
                    ? WorldStateProgressPersistence.LoadOrCreate(metadata.SlotId, playerId)
                    : new WorldStateProgress("default", playerId);
                var context = new RewardRuntimeContext(
                    questPanel.QuestLog,
                    inventoryComponent != null ? inventoryComponent.Inventory : null,
                    null,
                    _storyFlags,
                    studentProgress,
                    worldStateProgress);

                RegisterPendingChoiceSave(dialoguePanel, worldStateProgress);
                dialoguePanel.Open(npc.Npc.ResolveDialogue(studentProgress), new DialogueChoiceContext(questPanel.QuestLog, context));
            }

            private void RegisterPendingChoiceSave(DialoguePanel panel, WorldStateProgress progress)
            {
                ClearPendingChoiceHandler();
                if (panel == null || progress == null)
                {
                    return;
                }

                _pendingDialoguePanel = panel;
                _pendingChoiceHandler = success =>
                {
                    if (_pendingDialoguePanel != null && _pendingChoiceHandler != null)
                    {
                        _pendingDialoguePanel.OnChoiceExecuted -= _pendingChoiceHandler;
                    }

                    _pendingDialoguePanel = null;
                    _pendingChoiceHandler = null;
                    if (success)
                    {
                        WorldStateProgressPersistence.Save(progress);
                    }
                };
                panel.OnChoiceExecuted += _pendingChoiceHandler;
            }

            private void ClearPendingChoiceHandler()
            {
                if (_pendingDialoguePanel != null && _pendingChoiceHandler != null)
                {
                    _pendingDialoguePanel.OnChoiceExecuted -= _pendingChoiceHandler;
                }

                _pendingDialoguePanel = null;
                _pendingChoiceHandler = null;
            }
        }

        private static GameObject FindPlayer()
        {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var player = FindPlayerInChildren(roots[i].transform);
                if (player != null) return player;
            }

            return null;
        }

        private static GameObject FindPlayerInChildren(Transform root)
        {
            if (root.GetComponent<PlayerInventory>() != null || root.GetComponent<PlayerIdentity>() != null)
            {
                return root.gameObject;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var player = FindPlayerInChildren(root.GetChild(i));
                if (player != null) return player;
            }

            return null;
        }

        private static string ResolvePlayerId(PlayerInventory inventory)
        {
            if (inventory == null) return PlayerIdentity.DefaultPlayerId;
            var identity = inventory.GetComponent<PlayerIdentity>();
            return identity != null ? identity.PlayerId : PlayerIdentity.DefaultPlayerId;
        }

        private static GameObject FindRoot(Scene scene, string rootName)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++) if (roots[i].name == rootName) return roots[i];
            return null;
        }
    }
}
