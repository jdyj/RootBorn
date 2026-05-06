using Rootborn.Game.Dialogue;
using UnityEngine;

namespace Rootborn.UI.Quests
{
    [DisallowMultipleComponent]
    public sealed class DialoguePanel : MonoBehaviour
    {
        private DialogueDefinition _dialogue;
        private DialogueChoiceContext _context;

        public bool IsOpen { get; private set; }

        public void Open(DialogueDefinition dialogue, DialogueChoiceContext context)
        {
            _dialogue = dialogue;
            _context = context;
            IsOpen = true;
            gameObject.SetActive(true);
        }

        public void Close()
        {
            IsOpen = false;
            _dialogue = null;
            gameObject.SetActive(false);
        }

        public bool Choose(int choiceIndex)
        {
            if (_dialogue == null || _dialogue.Choices == null)
            {
                return false;
            }

            if (choiceIndex < 0 || choiceIndex >= _dialogue.Choices.Length)
            {
                return false;
            }

            return _dialogue.Choices[choiceIndex] != null
                && _dialogue.Choices[choiceIndex].TryExecute(in _context);
        }
    }
}
