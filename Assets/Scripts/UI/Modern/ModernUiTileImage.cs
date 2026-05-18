using System;
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
            if (_rebuildOnEnable)
            {
                Rebuild();
            }
        }

        public void Rebuild()
        {
            ClearGeneratedTiles();

            var rect = (RectTransform)transform;
            Rect bounds = rect.rect;
            int columns = Mathf.Max(3, Mathf.CeilToInt(bounds.width / _tileSize.x));
            int rows = Mathf.Max(3, Mathf.CeilToInt(bounds.height / _tileSize.y));
            _tileCount = 0;
            _cornerTileCount = 0;
            _hasStretchedCornerTiles = false;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    string role = RoleFor(x, y, columns, rows);
                    bool isCorner = role.StartsWith("corner");
                    var tile = CreateTile(rect.pivot, bounds, x, y, role, _recipe.FindByRole(role));
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

        private GameObject CreateTile(Vector2 parentPivot, Rect parentBounds, int column, int row, string role, ModernUiSpriteKey spriteKey)
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
            image.sprite = ResolveSprite(spriteKey);
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
