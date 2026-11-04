using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rootborn.Game.Common
{
    public static class UiTextInputFocus
    {
        public static bool HasFocus
        {
            get
            {
                var eventSystem = EventSystem.current;
                var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
                if (selected == null) return false;
                var input = selected.GetComponentInParent<InputField>();
                // A selected field owns keyboard input until selection leaves it, including the frame
                // in which Escape/Submit ends editing. This avoids update-order leakage to game hotkeys.
                return input != null && input.isActiveAndEnabled && input.IsInteractable();
            }
        }
    }
}
