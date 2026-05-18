using Rootborn.Game.DiscoveryClues;
using Rootborn.Game.Encyclopedia;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.UI.DiscoveryClues
{
    [DisallowMultipleComponent]
    public sealed class ClueInterpretationInteractor : MonoBehaviour, IPlayerInteractable, IPrioritizedPlayerInteractable
    {
        private const int InterpretationPriority = 59;

        [SerializeField] private ClueInterpretationDefinition[] _interpretations = System.Array.Empty<ClueInterpretationDefinition>();
        [SerializeField] private ClueInterpretationSourceKind _sourceKind = ClueInterpretationSourceKind.NpcDialogue;
        [SerializeField] private string _sourceTargetId;

        private QuestLog _questLog;

        public ClueInterpretationResult LastResult { get; private set; }
        public int InteractionPriority => InterpretationPriority;
        public string InteractionPrompt => "[E] Interpret Clue";
        public Vector3 InteractionPromptOffset => new Vector3(0f, 1.2f, 0f);
        public Transform InteractionTransform => transform;

        public void Bind(ClueInterpretationDefinition[] interpretations, ClueInterpretationSourceKind sourceKind, string sourceTargetId, QuestLog questLog)
        {
            _interpretations = interpretations ?? System.Array.Empty<ClueInterpretationDefinition>();
            _sourceKind = sourceKind;
            _sourceTargetId = string.IsNullOrEmpty(sourceTargetId) ? string.Empty : sourceTargetId;
            _questLog = questLog;
        }

        public bool CanInteract(GameObject player)
        {
            if (player == null || _interpretations == null || _interpretations.Length == 0) return false;
            var student = player.GetComponent<StudentLifeProgressComponent>();
            return student != null && student.EnsureProgress() != null;
        }

        public bool TryInteract(GameObject player)
        {
            if (!CanInteract(player)) return false;
            var studentComponent = player.GetComponent<StudentLifeProgressComponent>();
            var student = studentComponent.EnsureProgress();
            string saveSlot = student.SaveSlot;
            string playerId = student.PlayerId;
            var clueProgress = DiscoveryClueProgressPersistence.LoadOrCreate(saveSlot, playerId);
            var interpretationProgress = ClueInterpretationProgressPersistence.LoadOrCreate(saveSlot, playerId);
            var world = WorldStateProgressPersistence.LoadOrCreate(saveSlot, playerId);
            var encyclopedia = EncyclopediaProgressPersistence.LoadOrCreate(saveSlot, playerId);
            var context = new ClueInterpretationContext(world, clueProgress, interpretationProgress, student, _questLog, encyclopedia, student.CurrentDay);
            var summaries = ClueInterpretationSummaryBuilder.BuildForSource(new ClueInterpretationLookupCache(_interpretations), _sourceKind, _sourceTargetId, context, SurfaceFor(_sourceKind));
            if (summaries.Length == 0) return false;
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                var canvasObject = new GameObject("ClueInterpretationCanvas", typeof(Canvas));
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            var panel = ClueInterpretationChoicePanel.EnsureInScene(canvas);
            panel.Show(summaries, selected => CompleteSelected(selected, studentComponent, saveSlot, playerId));
            return true;
        }

        private void CompleteSelected(ClueInterpretationSummaryModel selected, StudentLifeProgressComponent studentComponent, string saveSlot, string playerId)
        {
            var definition = FindInterpretation(selected.InterpretationId);
            if (definition == null) return;
            var student = studentComponent.EnsureProgress();
            var clueProgress = DiscoveryClueProgressPersistence.LoadOrCreate(saveSlot, playerId);
            var interpretationProgress = ClueInterpretationProgressPersistence.LoadOrCreate(saveSlot, playerId);
            var world = WorldStateProgressPersistence.LoadOrCreate(saveSlot, playerId);
            var encyclopedia = EncyclopediaProgressPersistence.LoadOrCreate(saveSlot, playerId);
            var context = new ClueInterpretationContext(world, clueProgress, interpretationProgress, student, _questLog, encyclopedia, student.CurrentDay);
            new ClueInterpretationRunner().TryComplete(definition, context, out var result);
            LastResult = result;
            StudentLifeProgressPersistence.Save(studentComponent);
            WorldStateProgressPersistence.Save(world);
            DiscoveryClueProgressPersistence.Save(clueProgress);
            ClueInterpretationProgressPersistence.Save(interpretationProgress);
            EncyclopediaProgressPersistence.Save(encyclopedia);
        }

        private ClueInterpretationDefinition FindInterpretation(string interpretationId)
        {
            if (_interpretations == null) return null;
            for (int i = 0; i < _interpretations.Length; i++)
            {
                var interpretation = _interpretations[i];
                if (interpretation != null && interpretation.Id == interpretationId) return interpretation;
            }
            return null;
        }

        private static ClueInterpretationSummarySurface SurfaceFor(ClueInterpretationSourceKind sourceKind)
        {
            if (sourceKind == ClueInterpretationSourceKind.NpcDialogue) return ClueInterpretationSummarySurface.NpcDialogue;
            if (sourceKind == ClueInterpretationSourceKind.LocationInvestigation) return ClueInterpretationSummarySurface.LocationPanel;
            if (sourceKind == ClueInterpretationSourceKind.ObjectInteraction) return ClueInterpretationSummarySurface.ObjectPanel;
            if (sourceKind == ClueInterpretationSourceKind.EncyclopediaReview) return ClueInterpretationSummarySurface.Encyclopedia;
            return ClueInterpretationSummarySurface.ClueDetail;
        }
    }
}
