using System;
using System.Collections.Generic;
using Rootborn.Game.Characters;
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
        [SerializeField] private string _introductionKey;
        [SerializeField] private LocationDefinition _homeLocation;
        [SerializeField] private NpcRoleDefinition[] _roles = Array.Empty<NpcRoleDefinition>();
        [SerializeField] private DialogueDefinition _defaultDialogue;
        [SerializeField] private NpcDialogueConditionBase[] _defaultDialogueConditions = Array.Empty<NpcDialogueConditionBase>();
        [SerializeField] private DialogueDefinition[] _stageDialogues = Array.Empty<DialogueDefinition>();
        [SerializeField] private QuestDefinition[] _quests = Array.Empty<QuestDefinition>();
        [SerializeField] private Texture2D _worldTexture;
        [SerializeField] private Rect _worldSpriteRect = new Rect(0f, 0f, 16f, 16f);
        [SerializeField] private float _worldSpritePixelsPerUnit = 16f;
        [SerializeField] private CharacterAppearanceDefinition _characterAppearance;

        public string Id => _id;
        public string DisplayNameKey => _displayNameKey;
        public string IntroductionKey => string.IsNullOrEmpty(_introductionKey) ? _displayNameKey : _introductionKey;
        public LocationDefinition HomeLocation => _homeLocation;
        public IReadOnlyList<NpcRoleDefinition> Roles => _roles;
        public DialogueDefinition DefaultDialogue => _defaultDialogue;
        public DialogueDefinition Dialogue => _defaultDialogue;
        public IReadOnlyList<NpcDialogueConditionBase> DefaultDialogueConditions => _defaultDialogueConditions;
        public DialogueDefinition[] StageDialogues => _stageDialogues;
        public QuestDefinition[] Quests => _quests;
        public Texture2D WorldTexture => _worldTexture;
        public Rect WorldSpriteRect => _worldSpriteRect;
        public float WorldSpritePixelsPerUnit => _worldSpritePixelsPerUnit;
        public CharacterAppearanceDefinition CharacterAppearance => _characterAppearance;

        public bool CanUseDefaultDialogue(StudentLifeProgress progress)
        {
            for (int i = 0; i < _defaultDialogueConditions.Length; i++)
            {
                var condition = _defaultDialogueConditions[i];
                if (condition != null && !condition.IsSatisfied(progress)) return false;
            }

            return true;
        }

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

            return CanUseDefaultDialogue(progress) ? _defaultDialogue : null;
        }

        public void ConfigureForTests(
            string id,
            string displayNameKey,
            string introductionKey,
            LocationDefinition homeLocation,
            NpcRoleDefinition[] roles,
            DialogueDefinition defaultDialogue,
            NpcDialogueConditionBase[] defaultDialogueConditions,
            CharacterAppearanceDefinition characterAppearance = null)
        {
            _id = id;
            _displayNameKey = displayNameKey;
            _introductionKey = string.IsNullOrEmpty(introductionKey) ? displayNameKey : introductionKey;
            _homeLocation = homeLocation;
            _roles = roles ?? Array.Empty<NpcRoleDefinition>();
            _defaultDialogue = defaultDialogue;
            _defaultDialogueConditions = defaultDialogueConditions ?? Array.Empty<NpcDialogueConditionBase>();
            _characterAppearance = characterAppearance;
        }
    }

    public abstract class NpcDialogueConditionBase : ScriptableObject
    {
        public abstract bool IsSatisfied(StudentLifeProgress progress);
    }
}
