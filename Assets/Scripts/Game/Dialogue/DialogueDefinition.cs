using UnityEngine;

namespace Rootborn.Game.Dialogue
{
    [CreateAssetMenu(fileName = "Dialogue_New", menuName = "Rootborn/Dialogue/Dialogue Definition")]
    public sealed class DialogueDefinition : ScriptableObject
    {
        [SerializeField] private string[] _lineKeys = System.Array.Empty<string>();
        [SerializeField] private DialogueChoiceDefinition[] _choices = System.Array.Empty<DialogueChoiceDefinition>();

        public string[] LineKeys => _lineKeys;
        public DialogueChoiceDefinition[] Choices => _choices;
    }
}
