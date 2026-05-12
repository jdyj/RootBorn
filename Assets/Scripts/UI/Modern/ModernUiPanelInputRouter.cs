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

        private bool _wasInventoryKeyPressed;
        private bool _wasStatusKeyPressed;
        private bool _wasSettingsKeyPressed;

        public void Bind(ModernUiInventoryPanel inventoryPanel, ModernUiStatusPanel statusPanel)
        {
            Bind(inventoryPanel, statusPanel, null);
        }

        public void Bind(ModernUiInventoryPanel inventoryPanel, ModernUiStatusPanel statusPanel, SettingsPanel settingsPanel)
        {
            _inventoryPanel = inventoryPanel;
            _statusPanel = statusPanel;
            _settingsPanel = settingsPanel;
        }

        private void OnEnable()
        {
            _wasInventoryKeyPressed = false;
            _wasStatusKeyPressed = false;
            _wasSettingsKeyPressed = false;
        }

        private void Update()
        {
            bool inventoryPressed = IsAnyKeyboardPressed(KeyboardKey.Inventory);
            bool statusPressed = IsAnyKeyboardPressed(KeyboardKey.Status);
            bool settingsPressed = IsAnyKeyboardPressed(KeyboardKey.Settings);

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

            _wasInventoryKeyPressed = inventoryPressed;
            _wasStatusKeyPressed = statusPressed;
            _wasSettingsKeyPressed = settingsPressed;
        }

        public void ToggleInventoryPanel()
        {
            if (_inventoryPanel == null)
            {
                return;
            }

            bool willShow = !_inventoryPanel.IsVisible;
            if (_statusPanel != null && _statusPanel.IsVisible)
            {
                _statusPanel.Hide();
            }
            if (_settingsPanel != null && _settingsPanel.IsVisible)
            {
                _settingsPanel.Hide();
            }

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
            if (_inventoryPanel != null && _inventoryPanel.IsVisible)
            {
                _inventoryPanel.Hide();
            }
            if (_settingsPanel != null && _settingsPanel.IsVisible)
            {
                _settingsPanel.Hide();
            }

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
            if (_inventoryPanel != null && _inventoryPanel.IsVisible)
            {
                _inventoryPanel.Hide();
            }
            if (_statusPanel != null && _statusPanel.IsVisible)
            {
                _statusPanel.Hide();
            }

            if (willShow)
            {
                _settingsPanel.Show();
            }
            else
            {
                _settingsPanel.Hide();
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
                if (key == KeyboardKey.Status && keyboard.tabKey.isPressed)
                {
                    return true;
                }
                if (key == KeyboardKey.Settings && keyboard.escapeKey.isPressed)
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
            Settings
        }
    }
}