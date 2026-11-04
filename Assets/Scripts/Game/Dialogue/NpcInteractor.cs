using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
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
        private sealed class SceneAuthority { public UnityEngine.Object Owner; public Func<GameObject,bool> Allows; }
        private static readonly Dictionary<int,SceneAuthority> Authorities = new Dictionary<int,SceneAuthority>();
        public static void BindSceneAuthority(Scene scene,UnityEngine.Object owner,Func<GameObject,bool> allows)
        { if(scene.IsValid() && owner!=null && allows!=null) Authorities[scene.handle]=new SceneAuthority{Owner=owner,Allows=allows}; }
        public static void ReleaseSceneAuthority(Scene scene,UnityEngine.Object owner)
        { if(Authorities.TryGetValue(scene.handle,out var value) && ReferenceEquals(value.Owner,owner)) Authorities.Remove(scene.handle); }
        private bool HasAuthority(GameObject actor)
        {
            if(!Authorities.TryGetValue(gameObject.scene.handle,out var value)) return true;
            if(value.Owner==null){Authorities.Remove(gameObject.scene.handle);return true;}
            return value.Allows(actor);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAuthorities(){Authorities.Clear();}

        [SerializeField] private NpcDefinition _npc;

        private readonly DialogueSession _session = new DialogueSession();
        private IQuestEventSink _questEvents;
        private LocationDefinition _scheduledLocation;
        private string _scheduledTimeSlotId;
        private string _scheduledEventNoticeKey;
        private DialogueDefinition _scheduledDialogue;

        public DialogueSession Session => _session;
        // Valid only during synchronous interaction callbacks; UI must not infer another actor.
        public GameObject CurrentInteractionActor { get; private set; }
        public long InteractionSequence { get; private set; }
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
            return _npc != null && HasAuthority(player);
        }

        public bool TryInteract(GameObject player)
        {
            if (!CanInteract(player))
            {
                return false;
            }

            CurrentInteractionActor = player;
            try
            {
                var component = player != null ? player.GetComponent<StudentLifeProgressComponent>() : null;
                var progress = component != null ? component.EnsureProgress() : null;
                Interact(progress);
                // A talk event can publish a replacement aggregate before these receipts.
                progress = component != null ? component.EnsureProgress() : null;

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
            finally { CurrentInteractionActor = null; }
        }

        public void Interact()
        {
            Interact(null);
        }

        public void Interact(StudentLifeProgress progress)
        {
            InteractionSequence++;
            var dialogue = ResolveInteractionDialogue(progress);
            _session.Open(dialogue);
            RecordTalkEvent();
            if (_npc != null && progress != null)
                QuestGameplayEventBridge.Record(new QuestEvent(QuestEventKind.Talk,
                    QuestGameplayEventBridge.CreateRequestKey("npc-talk"), npc: _npc), progress);
            if(CurrentInteractionActor!=null)
            { var current=CurrentInteractionActor.GetComponent<StudentLifeProgressComponent>();if(current!=null) progress=current.EnsureProgress(); }
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

        private DialogueDefinition ResolveInteractionDialogue(StudentLifeProgress progress)
        {
            var stageDialogue = _npc != null ? _npc.ResolveDialogue(progress) : null;
            if (stageDialogue != null && _npc != null && !ReferenceEquals(stageDialogue, _npc.DefaultDialogue))
            {
                return stageDialogue;
            }

            return _scheduledDialogue != null ? _scheduledDialogue : stageDialogue;
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
