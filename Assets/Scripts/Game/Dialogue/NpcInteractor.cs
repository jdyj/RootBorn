using System;
using Rootborn.Game.Quests;
using UnityEngine;

namespace Rootborn.Game.Dialogue
{
    [DisallowMultipleComponent]
    public sealed class NpcInteractor : MonoBehaviour
    {
        [SerializeField] private NpcDefinition _npc;

        private readonly DialogueSession _session = new DialogueSession();
        private IQuestEventSink _questEvents;

        public DialogueSession Session => _session;
        public NpcDefinition Npc => _npc;
        public event Action<NpcInteractor> OnInteracted;

        public void Bind(NpcDefinition npc)
        {
            _npc = npc;
        }

        public void BindQuestEvents(IQuestEventSink questEvents)
        {
            _questEvents = questEvents;
        }

        public void Interact()
        {
            if (_npc == null)
            {
                return;
            }

            _session.Open(_npc.DefaultDialogue);
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
    }
}
