using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.Modern
{
    public static class ModernUiPanelBuilder
    {
        public static readonly Vector2 CommonPanel48TileSize = new Vector2(48f, 48f);

        public static GameObject CreateCommonPanel48(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(ModernUiTileImage));
            go.transform.SetParent(parent, false);
            ConfigureCenteredRect(go.GetComponent<RectTransform>(), position, size);
            ApplyCommonPanel48(go);
            return go;
        }

        public static GameObject CreatePlainContainer(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            ConfigureCenteredRect(go.GetComponent<RectTransform>(), position, size);
            return go;
        }

        public static GameObject CreatePlainButton(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            ConfigureCenteredRect(go.GetComponent<RectTransform>(), position, size);

            var image = go.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            return go;
        }

        public static GameObject CreateCommonPanel48Button(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(ModernUiTileImage));
            go.transform.SetParent(parent, false);
            ConfigureCenteredRect(go.GetComponent<RectTransform>(), position, size);

            var image = go.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            ApplyCommonPanel48(go);
            return go;
        }

        public static ModernUiTileImage ApplyCommonPanel48(GameObject go)
        {
            var tiles = go.GetComponent<ModernUiTileImage>();
            if (tiles == null)
            {
                tiles = go.AddComponent<ModernUiTileImage>();
            }

            tiles.SetRecipe(ModernUiRecipes.CommonPanel48);
            tiles.SetTileSize(CommonPanel48TileSize);
            tiles.Rebuild();
            return tiles;
        }

        private static void ConfigureCenteredRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
