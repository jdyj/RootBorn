using Rootborn.Game.Quests;
using UnityEngine;

namespace Rootborn.Game.Dialogue
{
    [CreateAssetMenu(fileName = "Npc_New", menuName = "Rootborn/Dialogue/NPC Definition")]
    public sealed class NpcDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayNameKey;
        [SerializeField] private DialogueDefinition _defaultDialogue;
        [SerializeField] private QuestDefinition[] _quests = System.Array.Empty<QuestDefinition>();

        public string Id => _id;
        public string DisplayNameKey => _displayNameKey;
        public DialogueDefinition DefaultDialogue => _defaultDialogue;
        public DialogueDefinition Dialogue => _defaultDialogue;
        public QuestDefinition[] Quests => _quests;
    }
}
