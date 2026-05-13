using Rootborn.UI.Objectives;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rootborn.UI.Modern
{
    [DisallowMultipleComponent]
    public sealed class ModernUiPanelInputRouter : MonoBehaviour
    {
        [SerializeField] private ModernUiInventoryPanel _inventoryPanel;
        [SerializeField] private ModernUiStatusPanel _statusPanel;
        [SerializeField] private SettingsPanel _settingsPanel;
        [SerializeField] private ObjectiveJournalPanel _objectiveJournalPanel;

        private bool _wasInventoryKeyPressed;
        private bool _wasStatusKeyPressed;
        private bool _wasSettingsKeyPressed;
        private bool _wasObjectiveJournalKeyPressed;

        public void Bind(ModernUiInventoryPanel inventoryPanel, ModernUiStatusPanel statusPanel)
        {
            Bind(inventoryPanel, statusPanel, null, null);
        }

        public void Bind(ModernUiInventoryPanel inventoryPanel, ModernUiStatusPanel statusPanel, SettingsPanel settingsPanel)
        {
            Bind(inventoryPanel, statusPanel, settingsPanel, null);
        }

        public void Bind(ModernUiInventoryPanel inventoryPanel, ModernUiStatusPanel statusPanel, SettingsPanel settingsPanel, ObjectiveJournalPanel objectiveJournalPanel)
        {
            _inventoryPanel = inventoryPanel;
            _statusPanel = statusPanel;
            _settingsPanel = settingsPanel;
            _objectiveJournalPanel = objectiveJournalPanel;
        }

        private void OnEnable()
        {
            _wasInventoryKeyPressed = false;
            _wasStatusKeyPressed = false;
            _wasSettingsKeyPressed = false;
            _wasObjectiveJournalKeyPressed = false;
        }

        private void Update()
        {
            bool inventoryPressed = IsAnyKeyboardPressed(KeyboardKey.Inventory);
            bool statusPressed = IsAnyKeyboardPressed(KeyboardKey.Status);
            bool settingsPressed = IsAnyKeyboardPressed(KeyboardKey.Settings);
            bool objectiveJournalPressed = IsAnyKeyboardPressed(KeyboardKey.ObjectiveJournal);

            if (inventoryPressed && !_wasInventoryKeyPressed)
            {
                ToggleInventoryPanel();
            }
            if (statusPressed && !_wasStatusKeyPressed)
            {
                ToggleStatusPanel();
            }
            if (settingsPressed && !_wasSettingsKeyPressed)
            {
                ToggleSettingsPanel();
            }
            if (objectiveJournalPressed && !_wasObjectiveJournalKeyPressed)
            {
                ToggleObjectiveJournalPanel();
            }

            _wasInventoryKeyPressed = inventoryPressed;
            _wasStatusKeyPressed = statusPressed;
            _wasSettingsKeyPressed = settingsPressed;
            _wasObjectiveJournalKeyPressed = objectiveJournalPressed;
        }

        public void ToggleInventoryPanel()
        {
            if (_inventoryPanel == null)
            {
                return;
            }

            bool willShow = !_inventoryPanel.IsVisible;
            HideOtherFullPanels(_inventoryPanel);

            if (willShow)
            {
                _inventoryPanel.Show();
            }
            else
            {
                _inventoryPanel.Hide();
            }
        }

        public void ToggleStatusPanel()
        {
            if (_statusPanel == null)
            {
                return;
            }

            bool willShow = !_statusPanel.IsVisible;
            HideOtherFullPanels(_statusPanel);

            if (willShow)
            {
                _statusPanel.Show();
            }
            else
            {
                _statusPanel.Hide();
            }
        }

        public void ToggleSettingsPanel()
        {
            if (_settingsPanel == null)
            {
                return;
            }

            bool willShow = !_settingsPanel.IsVisible;
            HideOtherFullPanels(_settingsPanel);

            if (willShow)
            {
                _settingsPanel.Show();
            }
            else
            {
                _settingsPanel.Hide();
            }
        }

        public void ToggleObjectiveJournalPanel()
        {
            if (_objectiveJournalPanel == null)
            {
                return;
            }

            bool willShow = !_objectiveJournalPanel.IsVisible;
            HideOtherFullPanels(_objectiveJournalPanel);

            if (willShow)
            {
                _objectiveJournalPanel.Show();
            }
            else
            {
                _objectiveJournalPanel.Hide();
            }
        }

        private void HideOtherFullPanels(Component except)
        {
            if (_inventoryPanel != null && _inventoryPanel != except && _inventoryPanel.IsVisible)
            {
                _inventoryPanel.Hide();
            }
            if (_statusPanel != null && _statusPanel != except && _statusPanel.IsVisible)
            {
                _statusPanel.Hide();
            }
            if (_settingsPanel != null && _settingsPanel != except && _settingsPanel.IsVisible)
            {
                _settingsPanel.Hide();
            }
            if (_objectiveJournalPanel != null && _objectiveJournalPanel != except && _objectiveJournalPanel.IsVisible)
            {
                _objectiveJournalPanel.Hide();
            }
        }

        private static bool IsAnyKeyboardPressed(KeyboardKey key)
        {
            var keyboards = InputSystem.devices;
            for (int i = 0; i < keyboards.Count; i++)
            {
                var keyboard = keyboards[i] as Keyboard;
                if (keyboard == null)
                {
                    continue;
                }

                if (key == KeyboardKey.Inventory && keyboard.iKey.isPressed)
                {
                    return true;
                }
                if (key == KeyboardKey.Status && keyboard.cKey.isPressed)
                {
                    return true;
                }
                if (key == KeyboardKey.Settings && keyboard.escapeKey.isPressed)
                {
                    return true;
                }
                if (key == KeyboardKey.ObjectiveJournal && keyboard.tabKey.isPressed)
                {
                    return true;
                }
            }

            return false;
        }

        private enum KeyboardKey
        {
            Inventory,
            Status,
            Settings,
            ObjectiveJournal
        }
    }
}
