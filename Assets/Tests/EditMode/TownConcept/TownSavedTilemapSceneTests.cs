using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.TownConcept
{
    public sealed class TownSavedTilemapSceneTests
    {
        [Test]
        public void TOWN_VISUAL_EDIT_001_TownSceneStoresVisibleTilemapLayout()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Town.unity", OpenSceneMode.Single);

            try
            {
                Assert.AreEqual("Town", scene.name);
                Tilemap ground = AssertTilemapHasTiles("GroundTilemap", 900);
                Assert.IsNotNull(ground.GetTile(new Vector3Int(5, -4, 0)), "GroundTilemap must store a tile under the default Town player spawn cell.");
                AssertTilemapExistsWithRenderer("TownDecorationTilemap", 10);
                AssertNoVisibleRuntimeCue("TownApartment");
                AssertNoVisibleRuntimeCue("TownStreet");
                AssertNoVisibleRuntimeCue("TownShop");
                AssertNoVisibleRuntimeCue("TownCommunityBoard");
            }
            finally
            {
                if (previous.IsValid() && !string.IsNullOrEmpty(previous.path))
                {
                    EditorSceneManager.OpenScene(previous.path, OpenSceneMode.Single);
                }
            }
        }

        private static Tilemap AssertTilemapHasTiles(string name, int minimumTiles)
        {
            Tilemap tilemap = GameObject.Find(name)?.GetComponent<Tilemap>();
            Assert.IsNotNull(tilemap, name + " must exist in the saved Town scene.");
            Assert.GreaterOrEqual(CountTiles(tilemap), minimumTiles, name + " must contain saved visible tile data.");
            return tilemap;
        }

        private static void AssertTilemapExistsWithRenderer(string name, int sortingOrder)
        {
            GameObject tilemapObject = GameObject.Find(name);
            Assert.IsNotNull(tilemapObject, name + " must exist in the saved Town scene as a named visual layer.");
            Assert.IsNotNull(tilemapObject.GetComponent<Tilemap>(), name + " must contain a Tilemap component.");
            TilemapRenderer renderer = tilemapObject.GetComponent<TilemapRenderer>();
            Assert.IsNotNull(renderer, name + " must contain a TilemapRenderer component.");
            Assert.IsTrue(renderer.enabled, name + " renderer must be enabled.");
            Assert.AreEqual(sortingOrder, renderer.sortingOrder, name + " must keep the expected rendering order.");
        }

        private static void AssertNoVisibleRuntimeCue(string name)
        {
            GameObject cue = GameObject.Find(name);
            if (cue == null)
            {
                return;
            }

            SpriteRenderer renderer = cue.GetComponent<SpriteRenderer>();
            Assert.IsTrue(renderer == null || !renderer.enabled || !cue.activeInHierarchy, name + " must not be a visible saved placeholder cue in Town.unity.");
        }

        private static int CountTiles(Tilemap tilemap)
        {
            int count = 0;
            BoundsInt bounds = tilemap.cellBounds;
            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (tilemap.GetTile(cell) != null)
                {
                    count++;
                }
            }

            return count;
        }
    }
}