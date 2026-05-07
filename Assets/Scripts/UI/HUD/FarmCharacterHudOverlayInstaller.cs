using System.Collections;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Family;
using Rootborn.Game.Save;
using Rootborn.Game.Time;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.HUD
{
    public sealed class FarmCharacterHudOverlayInstaller : MonoBehaviour
    {
        private const string HudName = "TopLeftCharacterHud";
        private float _nextRefreshTime;

        private IEnumerator Start()
        {
            while (true)
            {
                EnsureOverlay();
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefreshTime) return;
            _nextRefreshTime = Time.unscaledTime + 0.5f;
            EnsureOverlay();
        }

        private static void EnsureOverlay()
        {
            var canvas = FindFarmCanvas();
            if (canvas == null) return;
            canvas.gameObject.SetActive(true);
            HideDefaultBlockingPanels(canvas.transform);
            var hud = canvas.transform.Find(HudName);
            if (hud == null) hud = BuildHud(canvas.transform);
            hud.gameObject.SetActive(true);
            hud.SetAsLastSibling();
            RefreshHudData(hud);
            BuildCharacterThumbnail(hud);
        }

        private static Canvas FindFarmCanvas()
        {
            var farmCanvas = GameObject.Find("[FarmCanvas]");
            if (farmCanvas != null && farmCanvas.TryGetComponent(out Canvas canvas)) return canvas;
            foreach (var candidate in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (candidate != null && candidate.name == "[FarmCanvas]") return candidate;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return canvases.Length > 0 ? canvases[0] : null;
        }

        private static void HideDefaultBlockingPanels(Transform canvasRoot)
        {
            HideChild(canvasRoot, "HUD");
            HideChild(canvasRoot, "HotkeyHint");
            HideChild(canvasRoot, "QuestLogPanel");
            HideChild(canvasRoot, "BookPanel");
        }

        private static void HideChild(Transform canvasRoot, string childName)
        {
            var child = canvasRoot.Find(childName);
            if (child != null) child.gameObject.SetActive(false);
        }

        private static Transform BuildHud(Transform canvasRoot)
        {
            var root = TileBox(canvasRoot, HudName, new Vector2(18f, -18f), new Vector2(248f, 158f));
            root.SetAsLastSibling();
            var topBackplate = Box(root, "CharacterThumbnailBackplate", new Vector2(-18f, 18f), new Vector2(170f, 10f), new Color(0.23f, 0.23f, 0.31f, 1f));
            var leftBackplate = Box(root, "CharacterThumbnailLeftBackplate", new Vector2(-18f, -6f), new Vector2(24f, 76f), new Color(0.23f, 0.23f, 0.31f, 1f));
            topBackplate.SetAsFirstSibling();
            leftBackplate.SetSiblingIndex(1);
            topBackplate.GetComponent<Image>().raycastTarget = false;
            leftBackplate.GetComponent<Image>().raycastTarget = false;
            Box(root, "CharacterThumbnailMidBackplateA", new Vector2(0f, -34f), new Vector2(104f, 4f), new Color(0.23f, 0.23f, 0.31f, 1f)).GetComponent<Image>().raycastTarget = false;
            Box(root, "CharacterThumbnailMidBackplateB", new Vector2(0f, -48f), new Vector2(104f, 4f), new Color(0.23f, 0.23f, 0.31f, 1f)).GetComponent<Image>().raycastTarget = false;
            Box(root, "CharacterThumbnailBottomBackplate", new Vector2(-18f, -70f), new Vector2(180f, 12f), new Color(0.23f, 0.23f, 0.31f, 1f)).GetComponent<Image>().raycastTarget = false;

            var frame = TileBox(root, "CharacterThumbnailFrame", new Vector2(14f, -20f), new Vector2(78f, 78f));
            var thumbnail = new GameObject("CharacterThumbnail", typeof(RectTransform));
            thumbnail.transform.SetParent(frame, false);
            var trt = (RectTransform)thumbnail.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8f, 8f); trt.offsetMax = new Vector2(-8f, -8f);

            MakeHudText(root, "TimeLabel", "00:00", new Vector2(110f, -18f), new Vector2(110f, 28f), 18, TextAnchor.MiddleLeft);
            MakeHudText(root, "CurrencyLabel", "0G", new Vector2(110f, -50f), new Vector2(110f, 28f), 18, TextAnchor.MiddleLeft);
            MakeHudText(root, "DayLabel", "DAY 1", new Vector2(110f, -82f), new Vector2(118f, 24f), 16, TextAnchor.MiddleLeft);
            BuildHudSlot(root, "HudSlot_Inventory", new Vector2(50f, -110f), new Color(0.2f, 0.58f, 0.38f, 1f), "I", new Vector2(36f, 36f));
            BuildHudSlot(root, "HudSlot_Health", new Vector2(96f, -110f), new Color(0.78f, 0.14f, 0.18f, 1f), string.Empty, new Vector2(36f, 36f));
            BuildHudSlot(root, "HudSlot_Tool", new Vector2(160f, -100f), new Color(0.52f, 0.34f, 0.18f, 1f), string.Empty, new Vector2(54f, 54f), 14f);
            return root;
        }

        private static void RefreshHudData(Transform hudRoot)
        {
            var clock = GameClock.Instance;
            if (clock != null)
            {
                SetText(hudRoot, "TimeLabel", FormatClockTime(clock.DayProgress01));
                SetText(hudRoot, "DayLabel", "DAY " + clock.Day);
            }
            SetText(hudRoot, "CurrencyLabel", "0G");
        }

        private static string FormatClockTime(float dayProgress01)
        {
            int totalMinutes = Mathf.FloorToInt(Mathf.Repeat(dayProgress01, 1f) * 24f * 60f);
            int hour = totalMinutes / 60;
            int minute = totalMinutes % 60;
            return hour.ToString("00") + ":" + minute.ToString("00");
        }

        private static void SetText(Transform root, string childName, string value)
        {
            var label = root.Find(childName)?.GetComponent<Text>();
            if (label != null) label.text = value;
        }

        private static RectTransform TileBox(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(ModernUiTileImage));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var tileImage = go.GetComponent<ModernUiTileImage>();
            tileImage.SetRecipe(ModernUiRecipes.CommonPanel);
            tileImage.Rebuild();
            return rt;
        }

        private static RectTransform Box(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return rt;
        }

        private static Text MakeHudText(Transform parent, string name, string value, Vector2 pos, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = HudFonts.Pixel;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.2f, 0.13f, 0.09f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private static void BuildHudSlot(Transform parent, string name, Vector2 pos, Color fillColor, string label, Vector2? sizeOverride = null, float rotation = 0f)
        {
            var size = sizeOverride ?? new Vector2(20f, 20f);
            var frame = TileBox(parent, name, pos, size);
            frame.localEulerAngles = new Vector3(0f, 0f, rotation);
            var fill = Box(frame, "Fill", new Vector2(4f, -4f), size - new Vector2(8f, 8f), fillColor);
            fill.GetComponent<Image>().raycastTarget = false;
            if (!string.IsNullOrEmpty(label))
            {
                var text = MakeHudText(frame, "Label", label, new Vector2(0f, -1f), size, 9, TextAnchor.MiddleCenter);
                text.color = Color.white;
            }
        }

        private static void BuildCharacterThumbnail(Transform hudRoot)
        {
            var thumbnailRoot = hudRoot.Find("CharacterThumbnailFrame/CharacterThumbnail");
            if (thumbnailRoot == null) return;
            var registry = Rootborn.Game.Managers.Managers.Data?.Registry ?? Resources.Load<GameDataRegistry>("GameDataRegistry");
            if (registry == null || registry.CharacterParts == null) return;
            var saved = ActiveSaveContext.Metadata != null ? ActiveSaveContext.Metadata.Appearance : null;
            var appearance = CharacterAppearance.ResolveWithDefaults(saved, registry.CharacterParts);
            var parts = ResolveSelectedParts(appearance, registry.CharacterParts);
            parts.Sort((a, b) => a.LayerOrder.CompareTo(b.LayerOrder));
            for (int i = thumbnailRoot.childCount - 1; i >= 0; i--) Destroy(thumbnailRoot.GetChild(i).gameObject);
            foreach (var part in parts)
            {
                if (part == null || part.PreviewSprite == null) continue;
                var image = Box(thumbnailRoot, "HudPart_" + part.CategoryId, Vector2.zero, Vector2.zero, Color.white).GetComponent<Image>();
                var rt = (RectTransform)image.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                image.sprite = part.PreviewSprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
        }

        private static List<CharacterPartDefinition> ResolveSelectedParts(CharacterAppearance appearance, CharacterPartDefinition[] definitions)
        {
            var selected = new List<CharacterPartDefinition>();
            var categories = new HashSet<string>();
            foreach (var definition in definitions)
            {
                if (definition == null || string.IsNullOrEmpty(definition.CategoryId) || !categories.Add(definition.CategoryId)) continue;
                string id = appearance.GetSelectedPartId(definition.CategoryId);
                var part = Find(definitions, id, definition.CategoryId) ?? Default(definitions, definition.CategoryId);
                if (part != null) selected.Add(part);
            }
            return selected;
        }

        private static CharacterPartDefinition Find(CharacterPartDefinition[] definitions, string id, string categoryId)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var definition in definitions) if (definition != null && definition.Id == id && definition.CategoryId == categoryId) return definition;
            return null;
        }

        private static CharacterPartDefinition Default(CharacterPartDefinition[] definitions, string categoryId)
        {
            foreach (var definition in definitions) if (definition != null && definition.CategoryId == categoryId && definition.IsDefault) return definition;
            return null;
        }
    }
}
