using System.Collections;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Family;
using Rootborn.Game.Save;
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
            HideLegacyHud(canvas.transform);
            var hud = canvas.transform.Find(HudName);
            if (hud == null) hud = BuildHud(canvas.transform);
            hud.gameObject.SetActive(true);
            hud.SetAsLastSibling();
            BuildCharacterThumbnail(hud);
        }

        private static Canvas FindFarmCanvas()
        {
            var farmCanvas = GameObject.Find("[FarmCanvas]");
            if (farmCanvas != null && farmCanvas.TryGetComponent(out Canvas canvas)) return canvas;

            foreach (var candidate in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate != null && candidate.name == "[FarmCanvas]") return candidate;
            }

            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return canvases.Length > 0 ? canvases[0] : null;
        }

        private static void HideLegacyHud(Transform canvasRoot)
        {
            var legacy = canvasRoot.Find("HUD");
            if (legacy != null) legacy.gameObject.SetActive(false);
        }

        private static Transform BuildHud(Transform canvasRoot)
        {
            var root = Box(canvasRoot, HudName, new Vector2(24f, -24f), new Vector2(228f, 96f), new Color(0.13f, 0.09f, 0.06f, 0.78f));
            root.SetAsLastSibling();
            var frame = Box(root, "CharacterThumbnailFrame", new Vector2(12f, -12f), new Vector2(72f, 72f), new Color(0.78f, 0.62f, 0.42f, 0.95f));
            var thumbnail = new GameObject("CharacterThumbnail", typeof(RectTransform));
            thumbnail.transform.SetParent(frame, false);
            var trt = (RectTransform)thumbnail.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8f, 8f); trt.offsetMax = new Vector2(-8f, -8f);
            Gauge(root, "HealthGauge", new Vector2(100f, -18f), new Color(0.72f, 0.18f, 0.16f, 1f));
            Gauge(root, "EnergyGauge", new Vector2(100f, -42f), new Color(0.28f, 0.62f, 0.28f, 1f));
            Gauge(root, "ToolGauge", new Vector2(100f, -66f), new Color(0.82f, 0.58f, 0.24f, 1f));
            return root;
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

        private static void Gauge(Transform parent, string name, Vector2 pos, Color color)
        {
            var frame = Box(parent, name, pos, new Vector2(106f, 14f), new Color(0.22f, 0.16f, 0.1f, 0.9f));
            var fill = Box(frame, "Fill", new Vector2(4f, -4f), new Vector2(98f, 6f), color).GetComponent<Image>();
            fill.raycastTarget = false;
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
