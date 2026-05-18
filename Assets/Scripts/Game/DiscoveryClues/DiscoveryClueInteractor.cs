using System.Globalization;
using Rootborn.Game.Encyclopedia;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
{
    [DisallowMultipleComponent]
    public sealed class DiscoveryClueInteractor : MonoBehaviour, IPlayerInteractable, IPrioritizedPlayerInteractable
    {
        private const int CluePriority = 58;

        [SerializeField] private DiscoveryClueDefinition _clue;
        [SerializeField] private DiscoveryClueSourceDefinition _source;
        [SerializeField] private DiscoveryClueCompletionKind _completionKind = DiscoveryClueCompletionKind.LocationVisited;
        [SerializeField] private string _completionTargetId;

        private readonly DiscoveryClueRunner _runner = new DiscoveryClueRunner();
        private QuestLog _questLog;

        public DiscoveryClueResult LastRevealResult { get; private set; }
        public DiscoveryClueResult LastCompleteResult { get; private set; }
        public int InteractionPriority => CluePriority;
        public string InteractionPrompt => "[E] " + Humanize(_source != null ? _source.HintTextKey : _clue != null ? _clue.PublicTextKey : string.Empty);
        public Vector3 InteractionPromptOffset => new Vector3(0f, 1.2f, 0f);
        public Transform InteractionTransform => transform;

        public void Bind(DiscoveryClueDefinition clue, DiscoveryClueSourceDefinition source, QuestLog questLog, DiscoveryClueCompletionKind completionKind, string completionTargetId)
        {
            _clue = clue;
            _source = source;
            _questLog = questLog;
            _completionKind = completionKind;
            _completionTargetId = string.IsNullOrEmpty(completionTargetId) ? string.Empty : completionTargetId;
        }

        public bool CanInteract(GameObject player)
        {
            if (_clue == null || _source == null || player == null) return false;
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
            var world = WorldStateProgressPersistence.LoadOrCreate(saveSlot, playerId);
            var clueProgress = DiscoveryClueProgressPersistence.LoadOrCreate(saveSlot, playerId);
            var encyclopedia = EncyclopediaProgressPersistence.LoadOrCreate(saveSlot, playerId);
            var context = new DiscoveryClueContext(world, clueProgress, student, _questLog, encyclopedia, student.CurrentDay);
            bool revealed = _runner.TryRevealSource(_clue, _source, context, out var revealResult);
            LastRevealResult = revealResult;
            bool completed = _runner.TryComplete(_clue, new DiscoveryClueCompletionEvent(_completionKind, _completionTargetId), context, out var completeResult);
            LastCompleteResult = completeResult;
            if (revealed || completed)
            {
                StudentLifeProgressPersistence.Save(studentComponent);
                WorldStateProgressPersistence.Save(world);
                DiscoveryClueProgressPersistence.Save(clueProgress);
                EncyclopediaProgressPersistence.Save(encyclopedia);
            }

            Debug.Log("[ROOTBORN] Discovery clue interact reveal=" + revealResult.Kind + " complete=" + completeResult.Kind + " clue=" + (_clue != null ? _clue.Id : string.Empty) + " player=" + playerId);
            return revealed || completed;
        }

        private static string Humanize(string key)
        {
            if (string.IsNullOrEmpty(key)) return "Inspect clue";
            string text = key.Replace("clue.", string.Empty).Replace("source.", string.Empty).Replace('-', ' ').Replace('_', ' ');
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text);
        }
    }
}
