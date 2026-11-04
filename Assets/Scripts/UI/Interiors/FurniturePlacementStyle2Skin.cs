using System.Collections.Generic;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.Interiors
{
    [DefaultExecutionOrder(-19000)]
    public sealed class FurniturePlacementStyle2Skin : MonoBehaviour
    {
        private static readonly Vector2 TileSize = new Vector2(16f, 16f);
        private static readonly Color PanelTint = new Color(0.10f, 0.12f, 0.18f, 0.94f);
        private const int FramesToReapply = 12;
        private int _framesApplied;
        private readonly List<GameObject> _temporarilyHiddenControls = new List<GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoaded()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureAfterInitialSceneLoad()
        {
            Ensure(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Ensure(scene);
        }

        private static void Ensure(Scene scene)
        {
            if (scene.name != "House" || FindFirstObjectByType<FurniturePlacementStyle2Skin>() != null)
            {
                return;
            }

            var go = new GameObject("[FurniturePlacementStyle2Skin]", typeof(FurniturePlacementStyle2Skin));
            SceneManager.MoveGameObjectToScene(go, scene);
        }

        private void LateUpdate()
        {
            var panel = FindFirstObjectByType<InteriorFurniturePlacementPanel>();
            SetUtilityMenusHidden(panel != null);
            if (panel == null)
            {
                return;
            }

            ClearEmptyPreviewPlaceholder(panel.transform);

            if (_framesApplied >= FramesToReapply)
            {
                return;
            }

            var tileImages = panel.GetComponentsInChildren<ModernUiTileImage>(true);
            for (int i = 0; i < tileImages.Length; i++)
            {
                tileImages[i].SetRecipe(ModernUiRecipes.CommonPanel);
                tileImages[i].SetTileSize(TileSize);
                tileImages[i].Rebuild();
                TintGeneratedTiles(tileImages[i].transform);
            }

            if (tileImages.Length > 0)
            {
                _framesApplied++;
            }
        }

        private void SetUtilityMenusHidden(bool hidden)
        {
            if (!hidden)
            {
                foreach (var control in _temporarilyHiddenControls)
                    if (control != null) control.SetActive(true);
                _temporarilyHiddenControls.Clear();
                return;
            }
            // These are real gameplay entry points. Preserve their owner and callbacks.
            foreach (string name in new[] { "WorldStateLogButton", "EncyclopediaButton" })
            {
                var control = GameObject.Find(name);
                if (control == null || control.scene != gameObject.scene) continue;
                _temporarilyHiddenControls.Add(control);
                control.SetActive(false);
            }
        }

        private void OnDisable() => SetUtilityMenusHidden(false);

        private static void ClearEmptyPreviewPlaceholder(Transform root)
        {
            var preview = FindChild(root, "SelectedFurniturePreviewIcon");
            if (preview == null)
            {
                return;
            }

            var image = preview.GetComponent<Image>();
            if (image != null && image.sprite == null)
            {
                image.color = Color.clear;
            }
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }

                var found = FindChild(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void TintGeneratedTiles(Transform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (!child.name.StartsWith("Tile_"))
                {
                    continue;
                }

                var image = child.GetComponent<Image>();
                if (image != null)
                {
                    image.color = PanelTint;
                }
            }
        }
    }
}
