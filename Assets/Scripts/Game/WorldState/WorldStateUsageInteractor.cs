using System.Globalization;
using Rootborn.Game.Encyclopedia;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.WorldState
{
    [DisallowMultipleComponent]
    public sealed class WorldStateUsageInteractor : MonoBehaviour, IPlayerInteractable, IPrioritizedPlayerInteractable
    {
        private const int UsagePriority = 55;

        [SerializeField] private WorldStateUsageDefinition _usage;

        private readonly WorldStateUsageRunner _runner = new WorldStateUsageRunner();
        private QuestLog _questLog;
        private int _requestSequence;

        public WorldStateUsageDefinition Usage => _usage;
        public WorldStateUsageResult LastResult { get; private set; }
        public int InteractionPriority => UsagePriority;
        public string InteractionPrompt => "[E] " + Humanize(_usage != null ? _usage.InteractionPromptKey : string.Empty);
        public Vector3 InteractionPromptOffset => new Vector3(0f, 1.2f, 0f);
        public Transform InteractionTransform => transform;

        public void Bind(WorldStateUsageDefinition usage)
        {
            Bind(usage, null);
        }

        public void Bind(WorldStateUsageDefinition usage, QuestLog questLog)
        {
            _usage = usage;
            _questLog = questLog;
        }

        public bool CanInteract(GameObject player)
        {
            if (_usage == null || player == null) return false;
            var student = player.GetComponent<StudentLifeProgressComponent>();
            return student != null && student.EnsureProgress() != null;
        }

        public bool TryInteract(GameObject player)
        {
            if (!CanInteract(player)) return false;
            var studentComponent = player.GetComponent<StudentLifeProgressComponent>();
            var student = studentComponent.EnsureProgress();
            string requestId = _usage.Id + ":" + _requestSequence++;
            string saveSlot = student.SaveSlot;
            string playerId = student.PlayerId;
            var world = WorldStateProgressPersistence.LoadOrCreate(saveSlot, playerId);
            var usageProgress = WorldStateUsageProgressPersistence.LoadOrCreate(saveSlot, playerId);
            var encyclopedia = EncyclopediaProgressPersistence.LoadOrCreate(saveSlot, playerId);
            var context = new WorldStateUsageContext(world, usageProgress, student, _questLog, encyclopedia, null, student.CurrentDay);
            bool applied = _runner.TryUse(_usage, context, out var result);
            LastResult = result;
            if (applied)
            {
                student.MarkRequestApplied(requestId);
                StudentLifeProgressPersistence.Save(studentComponent);
                WorldStateUsageProgressPersistence.Save(usageProgress);
                EncyclopediaProgressPersistence.Save(encyclopedia);
            }

            Debug.Log("[ROOTBORN] World state usage interact applied=" + applied + " result=" + result.Kind + " usage=" + result.UsageId + " player=" + playerId);
            return applied;
        }

        private static string Humanize(string key)
        {
            if (string.IsNullOrEmpty(key)) return "Use";
            string text = key.Replace("usage.", string.Empty).Replace(".prompt", string.Empty).Replace('-', ' ').Replace('_', ' ');
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text);
        }
    }
}
