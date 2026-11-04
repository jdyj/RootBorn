using System.Globalization;
using Rootborn.Game.Localization;
using Rootborn.Game.StudentLife;
using Rootborn.Game.Time;
using Rootborn.UI.Modern;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.HUD
{
    // Presentation only: the clock's resolved actor owns all resource values.
    [DisallowMultipleComponent]
    public sealed class UrbanLifeVitalsHud : MonoBehaviour
    {
        private LifeTimePresentation _source;
        private StudentLifeProgressComponent _owner;
        private RectTransform _energyRoot, _focusRoot;
        private Text _energyValue, _focusValue;
        private int _shownEnergy = int.MinValue, _shownFocus = int.MinValue;

        public void Bind(LifeTimePresentation source)
        {
            if (_source == source) { Refresh(); return; }
            Unsubscribe();
            _source = source;
            if (isActiveAndEnabled && _source != null) _source.Changed += Refresh;
            Refresh();
        }

        private void OnEnable()
        {
            if (_source != null) { _source.Changed -= Refresh; _source.Changed += Refresh; }
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            DestroyWidget(_energyRoot);
            DestroyWidget(_focusRoot);
        }

        private static void DestroyWidget(RectTransform widget)
        {
            if (widget == null) return;
            if (Application.isPlaying) Destroy(widget.gameObject);
            else DestroyImmediate(widget.gameObject);
        }

        private void Unsubscribe()
        {
            if (_source != null) _source.Changed -= Refresh;
            if (_owner != null) _owner.VitalsChanged -= Refresh;
            _owner = null;
        }

        private void Refresh()
        {
            var next = isActiveAndEnabled && _source != null && _source.HasTime ? _source.Owner : null;
            if (_owner != next)
            {
                if (_owner != null) _owner.VitalsChanged -= Refresh;
                _owner = next;
                if (_owner != null) _owner.VitalsChanged += Refresh;
            }
            if (_owner == null || _owner.Progress == null) { SetVisible(false); return; }
            EnsureWidgets();
            var progress = _owner.Progress;
            // Time and vitals can change in the same command. Avoid repeated text work.
            if (_shownEnergy != progress.Energy)
            {
                _shownEnergy = progress.Energy;
                _energyValue.text = _shownEnergy.ToString(CultureInfo.CurrentCulture);
            }
            if (_shownFocus != progress.Focus)
            {
                _shownFocus = progress.Focus;
                _focusValue.text = _shownFocus.ToString(CultureInfo.CurrentCulture);
            }
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            if (_energyRoot != null && _energyRoot.gameObject.activeSelf != visible) _energyRoot.gameObject.SetActive(visible);
            if (_focusRoot != null && _focusRoot.gameObject.activeSelf != visible) _focusRoot.gameObject.SetActive(visible);
        }

        private void EnsureWidgets()
        {
            if (_energyRoot == null)
            {
                _energyRoot = CreateWidget("UrbanEnergyWidget", false, "energy", "ui.hud.energy", "\uCCB4\uB825", "Energy", out _energyValue);
                _shownEnergy = int.MinValue;
            }
            if (_focusRoot == null)
            {
                _focusRoot = CreateWidget("UrbanFocusWidget", true, "focus", "ui.hud.focus", "\uC9D1\uC911", "Focus", out _focusValue);
                _shownFocus = int.MinValue;
            }
        }

        private RectTransform CreateWidget(string name, bool isFocus, string iconKey, string labelKey, string ko, string en, out Text value)
        {
            var parent = transform.parent != null ? transform.parent : transform;
            var root = parent.Find(name) as RectTransform;
            if (root == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(ModernUiTileImage));
                go.transform.SetParent(parent, false);
                root = (RectTransform)go.transform;
            }
            UrbanGameplayHudLayout.PositionResource(root, isFocus);
            var panel = root.GetComponent<ModernUiTileImage>();
            if (panel == null) panel = root.gameObject.AddComponent<ModernUiTileImage>();
            panel.SetRecipe(ModernUiRecipes.SlotPanel);
            panel.Rebuild();

            var icon = root.Find("Icon") as RectTransform;
            if (icon == null)
            {
                var go = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(UrbanUiIcon));
                go.transform.SetParent(root, false);
                icon = (RectTransform)go.transform;
            }
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 1f);
            icon.pivot = new Vector2(0.5f, 1f);
            icon.anchoredPosition = new Vector2(0f, -20f);
            icon.sizeDelta = new Vector2(48f, 48f);
            var image = icon.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            icon.GetComponent<UrbanUiIcon>().Configure(iconKey);

            var label = CreateText(root, "Label", 68f, 28f, 18);
            var translated = GameText.Get(labelKey);
            label.text = translated != labelKey ? translated : (GameText.Language == "ko" ? ko : en);
            value = CreateText(root, "Value", 96f, 36f, 24);
            return root;
        }

        private static Text CreateText(RectTransform root, string name, float y, float height, int size)
        {
            var rect = root.Find(name) as RectTransform;
            if (rect == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Text));
                go.transform.SetParent(root, false);
                rect = (RectTransform)go.transform;
            }
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -y);
            rect.sizeDelta = new Vector2(-32f, height);
            var text = rect.GetComponent<Text>();
            text.font = LifeUiFont.Shared;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color32(48, 71, 83, 255);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }
    }
}
