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
                AssertLegacyTilemapEmptyWithRenderer("GroundTilemap", 0);
                AssertLegacyTilemapEmptyWithRenderer("TownDecorationTilemap", 10);
                AssertTown2OperationalLdtkMapStored();
                AssertSavedTownCameraZoomedOut();
                AssertSavedLevel1TransitionTrigger();
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

        private static void AssertLegacyTilemapEmptyWithRenderer(string name, int sortingOrder)
        {
            GameObject tilemapObject = GameObject.Find(name);
            Assert.IsNotNull(tilemapObject, name + " must remain as a named compatibility Tilemap layer in the saved Town scene.");
            Tilemap tilemap = tilemapObject.GetComponent<Tilemap>();
            Assert.IsNotNull(tilemap, name + " must contain a Tilemap component.");
            Assert.AreEqual(0, CountTiles(tilemap), name + " must not keep the pre-LDtk sample tiles in Town.unity.");
            TilemapRenderer renderer = tilemapObject.GetComponent<TilemapRenderer>();
            Assert.IsNotNull(renderer, name + " must contain a TilemapRenderer component.");
            Assert.IsTrue(renderer.enabled, name + " renderer must remain available for runtime compatibility.");
            Assert.AreEqual(sortingOrder, renderer.sortingOrder, name + " must keep the expected rendering order.");
        }

        private static void AssertTown2OperationalLdtkMapStored()
        {
            GameObject root = GameObject.Find("Town2LdtkTilemaps");
            Assert.IsNotNull(root, "Town must store operational LDtk Tilemap layers in the saved scene.");
            AssertLdtkLayer(root, "Town2LdtkGroundTilemap", 5050, 0);
            AssertLdtkLayer(root, "Town2LdtkGround2Tilemap", 96, 100);
            AssertLdtkLayer(root, "Town2LdtkBeachTilemap", 0, 900);
            AssertLdtkLayer(root, "Town2LdtkFenseTilemap", 96, 300);
            AssertLdtkLayer(root, "Town2LdtkPropsTilemap", 687, 200);
            AssertLdtkLayer(root, "Town2LdtkShopTilemap", 237, 400);
            AssertLdtkLayer(root, "Town2LdtkHospitalTilemap", 605, 500);
            AssertLdtkLayer(root, "Town2LdtkPoliceTilemap", 141, 600);
            AssertLdtkLayer(root, "Town2LdtkBuildingTilemap", 279, 700);
            AssertLdtkLayer(root, "Town2LdtkSchoolTilemap", 47, 800);
        }

        private static void AssertLdtkLayer(GameObject root, string name, int expectedTiles, int sortingOrder)
        {
            Transform child = root.transform.Find(name);
            Assert.IsNotNull(child, "Missing saved LDtk Tilemap layer: " + name);
            Tilemap tilemap = child.GetComponent<Tilemap>();
            Assert.IsNotNull(tilemap, name + " must have a Tilemap component.");
            Assert.AreEqual(expectedTiles, CountTiles(tilemap), name + " must keep its saved LDtk tile count.");
            TilemapRenderer renderer = child.GetComponent<TilemapRenderer>();
            Assert.IsNotNull(renderer, name + " must have a TilemapRenderer component.");
            Assert.IsTrue(renderer.enabled, name + " renderer must be enabled.");
            Assert.AreEqual(sortingOrder, renderer.sortingOrder, name + " must keep the expected LDtk draw order.");
        }

        private static void AssertSavedTownCameraZoomedOut()
        {
            Camera camera = Camera.main;
            Assert.IsNotNull(camera, "Town.unity must store a Main Camera for player-facing visual framing.");
            Assert.IsTrue(camera.orthographic, "Town camera must stay orthographic for tilemap play.");
            Assert.AreEqual(9f, camera.orthographicSize, 0.01f, "Town camera must be slightly zoomed out in the saved scene.");
        }


                private static void AssertSavedLevel1TransitionTrigger()
                {
                    GameObject trigger = GameObject.Find("TownLevelTransition_Level0ToLevel1");
                    Assert.IsNotNull(trigger, "Town.unity must store the player-facing Level_0 to Level_1 transition trigger.");
                    Assert.AreEqual(new Vector3(37.5f, -62.6f, 0f), trigger.transform.position, "Level transition trigger must sit on the lower Level_0 boundary.");
            var transition = trigger.GetComponent<Rootborn.Game.World.TownLevelTransitionTrigger>();
            Assert.IsNotNull(transition, "Level transition trigger must use TownLevelTransitionTrigger.");
            Assert.AreEqual("Beach", transition.DestinationScene, "Town lower boundary must transition to the Beach scene.");
            Assert.AreEqual("beach-top-entry", transition.DestinationSpawnId, "Town lower boundary must target the Beach top entry spawn.");
                    Collider2D collider = trigger.GetComponent<Collider2D>();
                    Assert.IsNotNull(collider, "Level transition trigger must have a Collider2D.");
                    Assert.IsTrue(collider.isTrigger, "Level transition collider must be a trigger.");
                    
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
