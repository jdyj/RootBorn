using System;
using System.Collections.Generic;
using Rootborn.Game.Common;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.Modern
{
    [ExecuteAlways]
    public sealed class ModernUiTileImage : MonoBehaviour
    {
        private static readonly Vector2 DefaultTileSize = new Vector2(16f, 16f);
        private static Sprite s_fallbackSprite;

        [SerializeField] private bool _rebuildOnEnable = true;
        [SerializeField] private Vector2 _tileSize = DefaultTileSize;

        private ModernUiTileRecipe _recipe = ModernUiRecipes.CommonPanel;
        private IModernUiSpriteResolver _resolver = new ModernUiSpriteResolver();
        private int _tileCount;
        private int _cornerTileCount;
        private bool _hasStretchedCornerTiles;

        private UrbanUiThemeDefinition _themeOverride;
        private Image _skin;
        private Image _rootImage;
        private Color _originalRootColor;
        private bool _rootHidden;
        private Button _button;
        private Graphic _originalTarget;
        private Color _tint = Color.white;
        private readonly Dictionary<Text, Color> _originalLabelColors = new Dictionary<Text, Color>();

        public void SetTheme(UrbanUiThemeDefinition theme)
        {
            _themeOverride = theme;
        }

        public void SetTint(Color tint)
        {
            _tint = tint;
            if (_skin != null) _skin.color = tint;
        }

        public int TileCount => _tileCount;
        public int CornerTileCount => _cornerTileCount;
        public Vector2 TileSize => _tileSize;
        public ModernUiTileRecipe Recipe => _recipe;
        public bool HasStretchedCornerTiles => _hasStretchedCornerTiles;

        public void SetRecipe(ModernUiTileRecipe recipe)
        {
            _recipe = recipe ?? ModernUiRecipes.CommonPanel;
        }

        public void SetTileSize(Vector2 size)
        {
            _tileSize = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
        }

        public void SetResolver(IModernUiSpriteResolver resolver)
        {
            _resolver = resolver ?? new ModernUiSpriteResolver();
        }

        private void OnEnable()
        {
            UrbanUiThemeRuntime.Changed -= OnThemeChanged;
            UrbanUiThemeRuntime.Changed += OnThemeChanged;
            if (_rebuildOnEnable)
            {
                Rebuild();
            }
        }

        private void Start()
        {
            // Runtime factories configure labels after AddComponent/OnEnable.
            if (_themeOverride != null || UrbanUiThemeRuntime.Current != null) Rebuild();
        }

        private void OnDisable()
        {
            UrbanUiThemeRuntime.Changed -= OnThemeChanged;
        }

        private void OnDestroy()
        {
            UrbanUiThemeRuntime.Changed -= OnThemeChanged;
        }

        private void OnThemeChanged()
        {
            if (isActiveAndEnabled) Rebuild();
        }

        private void OnCanvasHierarchyChanged()
        {
            // Reparenting under a different Canvas changes Image's reference PPU.
            if (isActiveAndEnabled && _skin != null && _skin.gameObject.activeSelf) Rebuild();
        }

        public void Rebuild()
        {
            if (TryRenderUrbanTheme()) return;
            RestoreLegacyGraphics();
            ClearGeneratedTiles();

            var rect = (RectTransform)transform;
            Rect bounds = rect.rect;
            int columns = Mathf.Max(3, Mathf.CeilToInt(bounds.width / _tileSize.x));
            int rows = Mathf.Max(3, Mathf.CeilToInt(bounds.height / _tileSize.y));
            _tileCount = 0;
            _cornerTileCount = 0;
            _hasStretchedCornerTiles = false;
            var spriteByRole = new Dictionary<string, Sprite>();

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    string role = RoleFor(x, y, columns, rows);
                    bool isCorner = role.StartsWith("corner");
                    if (!spriteByRole.TryGetValue(role, out var resolvedSprite))
                    {
                        resolvedSprite = ResolveSprite(_recipe.FindByRole(role));
                        spriteByRole[role] = resolvedSprite;
                    }

                    var tile = CreateTile(rect.pivot, bounds, x, y, role, resolvedSprite);
                    tile.transform.SetSiblingIndex(_tileCount);
                    if (isCorner)
                    {
                        _cornerTileCount++;
                        var tileRect = (RectTransform)tile.transform;
                        if (tileRect.sizeDelta != _tileSize)
                        {
                            _hasStretchedCornerTiles = true;
                        }
                    }

                    _tileCount++;
                }
            }
        }

        private bool TryRenderUrbanTheme()
        {
            var theme = _themeOverride != null ? _themeOverride : UrbanUiThemeRuntime.Current;
            if (theme == null) return false;
            _button = GetComponent<Button>();
            bool isSlot = ReferenceEquals(_recipe, ModernUiRecipes.SlotPanel);
            bool isButton = !isSlot && _button != null;
            Sprite sprite = isSlot ? theme.SlotSprite : isButton ? theme.ButtonSprite : theme.PanelSprite;
            if (sprite == null) return false;
            ClearGeneratedTiles();
            if (_skin == null)
            {
                var existing = transform.Find("[UrbanSkin]");
                if (existing != null) _skin = existing.GetComponent<Image>();
                if (_skin == null)
                {
                    var child = new GameObject("[UrbanSkin]", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                    child.transform.SetParent(transform, false);
                    _skin = child.GetComponent<Image>();
                }
            }
            var layout = _skin.GetComponent<LayoutElement>();
            if (layout == null) layout = _skin.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            _skin.gameObject.SetActive(true);
            _skin.transform.SetAsFirstSibling();
            var rect = _skin.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _skin.sprite = sprite;
            _skin.type = Image.Type.Sliced;
            var canvas = GetComponentInParent<Canvas>();
            float referencePpu = canvas != null ? canvas.referencePixelsPerUnit : 16f;
            _skin.pixelsPerUnitMultiplier = .5f * referencePpu / 16f;
            _skin.preserveAspect = false;
            _skin.raycastTarget = false;
            _skin.color = _tint;
            if (!_rootHidden)
            {
                _rootImage = GetComponent<Image>();
                if (_rootImage != null)
                {
                    _originalRootColor = _rootImage.color;
                    _rootImage.color = Color.clear;
                    _rootHidden = true;
                }
            }
            if (_button != null && _button.targetGraphic != _skin)
            {
                _originalTarget = _button.targetGraphic;
                _button.targetGraphic = _skin;
            }
            RestoreLabelColors();
            if (isButton)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    var label = transform.GetChild(i).GetComponent<Text>();
                    if (label == null) continue;
                    _originalLabelColors[label] = label.color;
                    label.color = theme.ButtonTextColor;
                }
            }
            _tileCount = 1;
            _cornerTileCount = 0;
            _hasStretchedCornerTiles = false;
            return true;
        }

        private void RestoreLabelColors()
        {
            foreach (var pair in _originalLabelColors)
                if (pair.Key != null) pair.Key.color = pair.Value;
            _originalLabelColors.Clear();
        }

        private void RestoreLegacyGraphics()
        {
            if (_skin != null) _skin.gameObject.SetActive(false);
            if (_rootHidden && _rootImage != null) _rootImage.color = _originalRootColor;
            _rootHidden = false;
            if (_button != null && _button.targetGraphic == _skin) _button.targetGraphic = _originalTarget;
            RestoreLabelColors();
        }

        private GameObject CreateTile(Vector2 parentPivot, Rect parentBounds, int column, int row, string role, Sprite sprite)
        {
            var go = new GameObject($"Tile_{row}_{column}_{role}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = parentPivot;
            rect.anchorMax = parentPivot;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(
                parentBounds.xMin + column * _tileSize.x,
                parentBounds.yMax - row * _tileSize.y);
            rect.sizeDelta = _tileSize;

            var image = go.GetComponent<Image>();
            image.sprite = sprite != null ? sprite : EnsureFallbackSprite();
            image.color = Color.white;
            image.preserveAspect = false;
            image.raycastTarget = false;
            return go;
        }

        private Sprite ResolveSprite(ModernUiSpriteKey spriteKey)
        {
            try
            {
                var sprite = _resolver.Resolve(spriteKey);
                return sprite != null ? sprite : EnsureFallbackSprite();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ROOTBORN] Modern UI Style2 sprite could not be resolved; using fallback tile. " + exception.Message, this);
                return EnsureFallbackSprite();
            }
        }

        private static Sprite EnsureFallbackSprite()
        {
            if (s_fallbackSprite != null) return s_fallbackSprite;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            s_fallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return s_fallbackSprite;
        }

        private void ClearGeneratedTiles()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name.StartsWith("Tile_"))
                {
                    child.gameObject.SetActive(false);
                    if (Application.isPlaying)
                    {
                        Destroy(child.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
            }
        }

        private static string RoleFor(int column, int row, int columns, int rows)
        {
            bool left = column == 0;
            bool right = column == columns - 1;
            bool top = row == 0;
            bool bottom = row == rows - 1;

            if (top && left) return "corner-tl";
            if (top && right) return "corner-tr";
            if (bottom && left) return "corner-bl";
            if (bottom && right) return "corner-br";
            if (top) return "edge-t";
            if (bottom) return "edge-b";
            if (left) return "edge-l";
            if (right) return "edge-r";
            return "fill";
        }
    }
}
