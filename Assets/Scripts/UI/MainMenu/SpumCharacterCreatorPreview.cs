using System.Collections.Generic;
using Rootborn.Game.Characters;
using Rootborn.Game.Characters.Spum;
using Rootborn.Game.Tools;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.MainMenu
{
    public sealed class SpumCharacterCreatorPreview : MonoBehaviour, ICharacterVisualView
    {
        private readonly List<GameObject> _selectionRows = new List<GameObject>();

        public Vector2 LastMotionInput { get; private set; }
        public Vector2 LastFacing { get; private set; }
        public bool LastFlipX { get; private set; }
        public CharacterVisualAction LastAction { get; private set; }
        public ToolVisualMappingDefinition LastToolVisualMapping { get; private set; }

        public void Build(IReadOnlyList<SpumPartDefinition> weaponParts)
        {
            ClearGeneratedChildren();
            _selectionRows.Clear();
            if (weaponParts == null || weaponParts.Count == 0)
                return;

            var weaponPreview = new GameObject("WeaponPreview", typeof(RectTransform), typeof(Image));
            weaponPreview.transform.SetParent(transform, false);
            var rect = (RectTransform)weaponPreview.transform;
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-24f, 0f);
            rect.sizeDelta = new Vector2(96f, 96f);

            var image = weaponPreview.GetComponent<Image>();
            image.sprite = weaponParts[0] != null ? weaponParts[0].PreviewSprite : null;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        public void ApplySelectedParts(IReadOnlyDictionary<string, string> selectedPartIds)
        {
            for (int i = 0; i < _selectionRows.Count; i++)
            {
                if (_selectionRows[i] != null)
                    Object.DestroyImmediate(_selectionRows[i]);
            }

            _selectionRows.Clear();
            if (selectedPartIds == null)
                return;

            int row = 0;
            foreach (KeyValuePair<string, string> selected in selectedPartIds)
            {
                if (string.IsNullOrWhiteSpace(selected.Key) || string.IsNullOrWhiteSpace(selected.Value))
                    continue;

                GameObject labelObject = new GameObject("PreviewSelected_" + SanitizeName(selected.Key), typeof(RectTransform), typeof(Text));
                labelObject.transform.SetParent(transform, false);
                var rect = (RectTransform)labelObject.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, 120f - row * 28f);
                rect.sizeDelta = new Vector2(240f, 24f);

                var text = labelObject.GetComponent<Text>();
                text.text = selected.Key + ": " + selected.Value;
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 14;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.raycastTarget = false;
                _selectionRows.Add(labelObject);
                row++;
            }
        }

        public void SetMotion(Vector2 input, Vector2 facing)
        {
            LastMotionInput = input;
            LastFacing = facing;
        }

        public void SetFlipX(bool flipX)
        {
            LastFlipX = flipX;
        }

        public void PlayAction(CharacterVisualAction action)
        {
            LastAction = action;
        }

        public void ApplyEquippedToolVisual(ToolVisualMappingDefinition mapping)
        {
            LastToolVisualMapping = mapping;
        }

        private void ClearGeneratedChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(transform.GetChild(i).gameObject);
            }
        }

        private static string SanitizeName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "empty" : value.Replace('.', '_').Replace('/', '_').Replace('\\', '_');
        }
    }
}
