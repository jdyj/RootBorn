using System.Globalization;
using Rootborn.Game.Common;
using Rootborn.Game.DiscoveryClues;
using Rootborn.Game.Encyclopedia;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using UnityEngine;

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
        public string InteractionPrompt => "[E] " + Humanize(_interaction != null ? _interaction.DisplayNameKey : string.Empty);
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
            var questLog = Object.FindFirstObjectByType<Rootborn.UI.Quests.QuestLogPanel>(FindObjectsInactive.Include)?.QuestLog;
            var clueProgress = DiscoveryClueProgressPersistence.LoadOrCreate(student.SaveSlot, student.PlayerId);
            var encyclopedia = EncyclopediaProgressPersistence.LoadOrCreate(student.SaveSlot, student.PlayerId);
            var cache = new ExplorationLookupCache(new[] { _interaction });
            var summary = ExplorationSummaryBuilder.Build(_interaction.Id, cache, exploration, student, inventory != null ? inventory.Inventory : null, questLog, clueProgress, student.CurrentDay, student.TimeMinutes);
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            var panel = ExplorationChoicePanel.EnsureInScene(canvas);
            panel.Open(summary, exploration, choiceId => Choose(player, studentComponent, exploration, clueProgress, encyclopedia, questLog, choiceId, panel));
            return true;
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

        private static string Humanize(string key)
        {
            if (string.IsNullOrEmpty(key)) return "Inspect";
            string text = key.Replace("exploration.", string.Empty).Replace("choice.", string.Empty).Replace('-', ' ').Replace('_', ' ');
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text);
        }
    }
}
