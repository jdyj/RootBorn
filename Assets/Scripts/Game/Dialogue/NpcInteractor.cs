using System;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.Dialogue
{
    [DisallowMultipleComponent]
    public sealed class NpcInteractor : MonoBehaviour, IPlayerInteractable, IPrioritizedPlayerInteractable
    {
        private const int NpcInteractionPriority = 30;

        [SerializeField] private NpcDefinition _npc;

        private readonly DialogueSession _session = new DialogueSession();
        private IQuestEventSink _questEvents;
        private LocationDefinition _scheduledLocation;
        private string _scheduledTimeSlotId;
        private string _scheduledEventNoticeKey;
        private DialogueDefinition _scheduledDialogue;

        public DialogueSession Session => _session;
        public NpcDefinition Npc => _npc;
        public LocationVisitResult LastVisitResult { get; private set; }
        public int InteractionPriority => NpcInteractionPriority;
        public string InteractionPrompt => "[E] Talk";
        public Vector3 InteractionPromptOffset => new Vector3(0f, 1.15f, 0f);
        public Transform InteractionTransform => transform;
        public event Action<NpcInteractor> OnInteracted;
        public static event Action<NpcInteractor, StudentLifeProgress> OnAnyInteracted;

        public void Bind(NpcDefinition npc)
        {
            _npc = npc;
        }

        public void BindSchedule(LocationDefinition location, string timeSlotId, DialogueDefinition dialogue, string dialogueKey, string eventNoticeKey)
        {
            _scheduledLocation = location;
            _scheduledTimeSlotId = string.IsNullOrEmpty(timeSlotId) ? string.Empty : timeSlotId;
            _scheduledEventNoticeKey = string.IsNullOrEmpty(eventNoticeKey) ? string.Empty : eventNoticeKey;
            _scheduledDialogue = dialogue != null ? dialogue : CreateRuntimeDialogue(dialogueKey);
        }

        public void BindQuestEvents(IQuestEventSink questEvents)
        {
            _questEvents = questEvents;
        }

        public bool CanInteract(GameObject player)
        {
            return _npc != null;
        }

        public bool TryInteract(GameObject player)
        {
            if (!CanInteract(player))
            {
                return false;
            }

            var component = player != null ? player.GetComponent<StudentLifeProgressComponent>() : null;
            var progress = component != null ? component.EnsureProgress() : null;
            Interact(progress);

            bool shouldSave = false;
            if (progress != null)
            {
                var visitProgress = new LocationVisitProgress(progress);
                bool applied = visitProgress.TryRecordNpcMeeting(_npc, out var result);
                LastVisitResult = result;
                shouldSave = applied;

                var scheduleProgress = new NpcScheduleProgress(progress);
                if (scheduleProgress.TryRecordMeeting(_npc, _scheduledLocation, _scheduledTimeSlotId, _scheduledEventNoticeKey, out _)) shouldSave = true;
                if (shouldSave) StudentLifeProgressPersistence.Save(component);
            }

            return true;
        }

        public void Interact()
        {
            Interact(null);
        }

        public void Interact(StudentLifeProgress progress)
        {
            var dialogue = _scheduledDialogue != null ? _scheduledDialogue : (_npc != null ? _npc.ResolveDialogue(progress) : null);
            _session.Open(dialogue);
            RecordTalkEvent();
            OnInteracted?.Invoke(this);
            OnAnyInteracted?.Invoke(this, progress);
        }

        public void CompleteTalk(string eventKey)
        {
            if (_npc == null)
            {
                return;
            }

            _questEvents?.Record(new QuestEvent(QuestEventKind.Talk, eventKey, npc: _npc));
        }

        public void Close()
        {
            _session.Close();
        }

        private void RecordTalkEvent()
        {
            if (_questEvents == null)
            {
                return;
            }

            string npcId = _npc != null && !string.IsNullOrEmpty(_npc.Id) ? _npc.Id : name;
            _questEvents.Record(new QuestEvent(QuestEventKind.Talk, "talk:" + npcId, npc: _npc));
        }

        private static DialogueDefinition CreateRuntimeDialogue(string dialogueKey)
        {
            var dialogue = ScriptableObject.CreateInstance<DialogueDefinition>();
            string lineKey = string.IsNullOrEmpty(dialogueKey) ? "dialogue.runtime.npc" : dialogueKey;
            dialogue.ConfigureForTests(new[] { lineKey }, Array.Empty<DialogueChoiceDefinition>(), Array.Empty<DialogueCondition>());
            return dialogue;
        }
    }
}
