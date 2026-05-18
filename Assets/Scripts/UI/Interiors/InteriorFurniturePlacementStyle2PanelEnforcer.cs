using System.Collections;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.Interiors
{
    public sealed class InteriorFurniturePlacementStyle2PanelEnforcer : MonoBehaviour
    {
        private static readonly string[] PanelNames =
        {
            "FurniturePalette",
            "SelectedFurnitureSettings",
            "InteriorPlacementBottomBar"
        };

        private static readonly Color PanelTileTint = new Color(0.08f, 0.1f, 0.12f, 0.92f);
        private static readonly Color PanelTextColor = new Color(0.95f, 0.93f, 0.84f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoaded()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyAfterInitialSceneLoad()
        {
            if (SceneManager.GetActiveScene().name == "House")
            {
                Apply();
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "House")
            {
                return;
            }

            var go = new GameObject("[HouseInteriorPlacementStyle2PanelEnforcer]");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<InteriorFurniturePlacementStyle2PanelEnforcer>();
        }

        private IEnumerator Start()
        {
            yield return null;
            Apply();
            Destroy(gameObject);
        }

        public static void Apply()
        {
            for (int i = 0; i < PanelNames.Length; i++)
            {
                var go = GameObject.Find(PanelNames[i]);
                var tileImage = go != null ? go.GetComponent<ModernUiTileImage>() : null;
                if (tileImage == null)
                {
                    continue;
                }

                tileImage.SetRecipe(ModernUiRecipes.CommonPanel);
                tileImage.SetTileSize(new Vector2(16f, 16f));
                tileImage.Rebuild();
                TintGeneratedTiles(go.transform);
                TintLabels(go.transform);
            }
        }

        private static void TintGeneratedTiles(Transform root)
        {
            var images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i].gameObject.name.StartsWith("Tile_"))
                {
                    images[i].color = PanelTileTint;
                }
            }
        }

        private static void TintLabels(Transform root)
        {
            var labels = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i].color = PanelTextColor;
            }
        }
    }
}
