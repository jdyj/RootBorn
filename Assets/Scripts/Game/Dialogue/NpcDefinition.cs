using Rootborn.Game.Quests;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.Dialogue
{
    [CreateAssetMenu(fileName = "Npc_New", menuName = "Rootborn/Dialogue/NPC Definition")]
    public sealed class NpcDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayNameKey;
        [SerializeField] private DialogueDefinition _defaultDialogue;
        [SerializeField] private DialogueDefinition[] _stageDialogues = System.Array.Empty<DialogueDefinition>();
        [SerializeField] private QuestDefinition[] _quests = System.Array.Empty<QuestDefinition>();
        [SerializeField] private Texture2D _worldTexture;
        [SerializeField] private Rect _worldSpriteRect = new Rect(0f, 0f, 16f, 16f);
        [SerializeField] private float _worldSpritePixelsPerUnit = 16f;

        public string Id => _id;
        public string DisplayNameKey => _displayNameKey;
        public DialogueDefinition DefaultDialogue => _defaultDialogue;
        public DialogueDefinition Dialogue => _defaultDialogue;
        public DialogueDefinition[] StageDialogues => _stageDialogues;
        public QuestDefinition[] Quests => _quests;
        public Texture2D WorldTexture => _worldTexture;
        public Rect WorldSpriteRect => _worldSpriteRect;
        public float WorldSpritePixelsPerUnit => _worldSpritePixelsPerUnit;

        public DialogueDefinition ResolveDialogue(StudentLifeProgress progress)
        {
            for (int i = 0; i < _stageDialogues.Length; i++)
            {
                var dialogue = _stageDialogues[i];
                if (dialogue != null && dialogue.IsAvailable(progress))
                {
                    return dialogue;
                }
            }

            return _defaultDialogue;
        }
    }
}
