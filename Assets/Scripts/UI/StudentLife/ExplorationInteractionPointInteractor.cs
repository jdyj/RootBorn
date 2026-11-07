using Rootborn.Game.Common;
using Rootborn.Game.DiscoveryClues;
using Rootborn.Game.Encyclopedia;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.SceneManagement;
using Rootborn.UI.Modern;

namespace Rootborn.UI.StudentLife
{
    [DisallowMultipleComponent]
    public sealed class ExplorationInteractionPointInteractor : MonoBehaviour, IPlayerInteractable, IPrioritizedPlayerInteractable
    {
        private const string ExplorationProgressFileName = "exploration-progress.json";
        private const int ExplorationInteractionPriority = 50;

        [SerializeField] private ExplorationInteractionDefinition _interaction;

        public ExplorationInteractionDefinition Interaction => _interaction;
        public int InteractionPriority => ExplorationInteractionPriority;
        public string InteractionPrompt => ExplorationTextPresentation.Prompt(_interaction != null ? _interaction.DisplayNameKey : null);
        public Vector3 InteractionPromptOffset => new Vector3(0f, 1.15f, 0f);
        public Transform InteractionTransform => transform;
        public ExplorationChoiceResult LastResult { get; private set; }

        public void Bind(ExplorationInteractionDefinition interaction)
        {
            _interaction = interaction;
        }

        public bool CanInteract(GameObject player)
        {
            return _interaction != null && player != null && player.GetComponent<StudentLifeProgressComponent>() != null;
        }

        public bool TryInteract(GameObject player)
        {
            if (!CanInteract(player)) return false;
            var studentComponent = player.GetComponent<StudentLifeProgressComponent>();
            var student = studentComponent.EnsureProgress();
            var exploration = LoadProgress(student.SaveSlot, student.PlayerId);
            var inventory = player.GetComponent<PlayerInventory>();
            var questLog = Rootborn.Game.Player.PlayerQuestRuntime.TryGet(student.SaveSlot, student.PlayerId, out var quests) ? quests.QuestLog : null;
            var clueProgress = DiscoveryClueProgressPersistence.LoadOrCreate(student.SaveSlot, student.PlayerId);
            var encyclopedia = EncyclopediaProgressPersistence.LoadOrCreate(student.SaveSlot, student.PlayerId);
            var cache = new ExplorationLookupCache(new[] { _interaction });
            var summary = ExplorationSummaryBuilder.Build(_interaction.Id, cache, exploration, student, inventory != null ? inventory.Inventory : null, questLog, clueProgress, student.CurrentDay, student.TimeMinutes);
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var canvas = SelectInteractionCanvas(canvases, gameObject.scene);
            if (canvas == null) return false;
            var panel = ExplorationChoicePanel.EnsureInScene(canvas);
            if (panel == null) return false;
            panel.Open(summary, exploration, choiceId => Choose(player, studentComponent, exploration, clueProgress, encyclopedia, questLog, choiceId, panel));
            return panel.IsOpen && panel.gameObject.activeInHierarchy;
        }

        private void Choose(GameObject player, StudentLifeProgressComponent studentComponent, ExplorationProgress exploration, DiscoveryClueProgress clueProgress, EncyclopediaProgress encyclopedia, QuestLog questLog, string choiceId, ExplorationChoicePanel panel)
        {
            var choice = FindChoice(choiceId);
            var inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
            var student = studentComponent != null ? studentComponent.EnsureProgress() : null;
            var context = new ExplorationInteractionContext(exploration, student, inventory != null ? inventory.Inventory : null, questLog, clueProgress, encyclopedia, student != null ? student.CurrentDay : 1, student != null ? student.TimeMinutes : 0);
            var runner = new ExplorationInteractionRunner();
            runner.TryChoose(_interaction, choice, context, out var result);
            LastResult = result;
            if (result.Kind == ExplorationChoiceResultKind.Dismissed)
            {
                panel.Close();
                return;
            }
            panel.ShowResult(result);
            if (result.Kind == ExplorationChoiceResultKind.Applied && studentComponent != null)
            {
                StudentLifeProgressPersistence.Save(studentComponent);
                SaveProgress(exploration);
                DiscoveryClueProgressPersistence.Save(clueProgress);
                EncyclopediaProgressPersistence.Save(encyclopedia);
            }
        }

        private ExplorationChoiceDefinition FindChoice(string choiceId)
        {
            if (_interaction == null || _interaction.Choices == null) return null;
            for (int i = 0; i < _interaction.Choices.Count; i++)
            {
                var choice = _interaction.Choices[i];
                if (choice != null && choice.Id == choiceId) return choice;
            }

            return null;
        }

        private static ExplorationProgress LoadProgress(string saveSlot, string playerId)
        {
            string json = new SaveService(saveSlot).ReadJson(FileNameFor(playerId));
            if (string.IsNullOrEmpty(json)) return new ExplorationProgress(saveSlot, playerId);
            try
            {
                var data = JsonUtility.FromJson<ExplorationProgressSaveData>(json);
                return ExplorationProgress.FromSaveData(data);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[ROOTBORN] Failed to load exploration progress: " + ex.Message);
                return new ExplorationProgress(saveSlot, playerId);
            }
        }

        private static void SaveProgress(ExplorationProgress progress)
        {
            if (progress == null) return;
            string json = JsonUtility.ToJson(progress.ToSaveData(), true);
            new SaveService(progress.SaveSlot).WriteJson(FileNameFor(progress.PlayerId), json);
        }

        private static string FileNameFor(string playerId)
        {
            return string.IsNullOrEmpty(playerId) || playerId == "player" || playerId == PlayerIdentity.DefaultPlayerId ? ExplorationProgressFileName : "exploration-progress-" + playerId + ".json";
        }

        private static Canvas SelectInteractionCanvas(Canvas[] candidates, Scene scene)
        {
            Canvas selected = null;
            bool selectedShared = false;
            if (candidates == null) return null;
            for (int i = 0; i < candidates.Length; i++)
            {
                var canvas = candidates[i];
                if (canvas == null || !canvas.isActiveAndEnabled || !canvas.gameObject.activeInHierarchy
                    || canvas.gameObject.scene != scene || canvas.rootCanvas != canvas
                    || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                bool shared = canvas.GetComponentInChildren<ModernUiInventoryPanel>(true) != null
                    || canvas.GetComponentInChildren<ModernUiPanelInputRouter>(true) != null;
                if (selected == null || (shared && !selectedShared)
                    || (shared == selectedShared && canvas.sortingOrder > selected.sortingOrder))
                {
                    selected = canvas;
                    selectedShared = shared;
                }
            }
            return selected;
        }


    }
}
