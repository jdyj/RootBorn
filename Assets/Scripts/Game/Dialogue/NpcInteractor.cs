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
        private const int NpcInteractionPriority = 50;

        [SerializeField] private NpcDefinition _npc;

        private readonly DialogueSession _session = new DialogueSession();
        private IQuestEventSink _questEvents;

        public DialogueSession Session => _session;
        public NpcDefinition Npc => _npc;
        public int InteractionPriority => NpcInteractionPriority;
        public string InteractionPrompt => "[E] Talk";
        public Vector3 InteractionPromptOffset => new Vector3(0f, 1.15f, 0f);
        public Transform InteractionTransform => transform;
        public event Action<NpcInteractor> OnInteracted;

        public void Bind(NpcDefinition npc)
        {
            _npc = npc;
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

            var progress = player != null ? player.GetComponent<StudentLifeProgressComponent>()?.EnsureProgress() : null;
            Interact(progress);
            return true;
        }

        public void Interact()
        {
            Interact(null);
        }

        public void Interact(StudentLifeProgress progress)
        {
            if (_npc == null)
            {
                return;
            }

            _session.Open(_npc.ResolveDialogue(progress));
            RecordTalkEvent();
            OnInteracted?.Invoke(this);
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
    }
}
