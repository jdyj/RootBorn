using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Interiors;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.PlayMode.Interiors
{
    public sealed class HouseInteriorGenerationPlayModeTests
    {
        private const string DoorTileName = "tile_r03_c07";
        private const string VerticalWallAccentTileName = "tile_r02_c09";

        [UnityTest]
        public IEnumerator HouseScene_GeneratesOfficeInteriorLayersFromNewPalette()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return WaitForGeneratedHouse();

            var applier = Object.FindFirstObjectByType<InteriorTilemapApplier>();
            Assert.IsNotNull(applier, "House should install the interior Tilemap applier.");

            var floor = GameObject.Find("HouseGroundTilemap")?.GetComponent<Tilemap>();
            var walls = GameObject.Find("HouseWallTilemap")?.GetComponent<Tilemap>();
            var doors = GameObject.Find("HouseDoorTilemap")?.GetComponent<Tilemap>();
            var decorations = GameObject.Find("HouseDecorationTilemap")?.GetComponent<Tilemap>();
            var collision = GameObject.Find("HouseCollisionTilemap")?.GetComponent<Tilemap>();
            Assert.IsNotNull(floor);
            Assert.IsNotNull(walls);
            Assert.IsNotNull(doors, "Generated House should expose a dedicated door Tilemap for visual/runtime verification.");
            Assert.IsNotNull(decorations);
            Assert.IsNotNull(collision);

            Assert.Greater(CountTiles(floor), 100, "Generated House floor should cover a playable interior area.");
            Assert.Greater(CountTiles(walls), 20, "Generated House should include wall structure.");
            Assert.Greater(CountTiles(doors), 0, "Generated House should render door tiles on the dedicated door layer.");
            Assert.Greater(CountTiles(decorations), 5, "Generated House should include office furniture/decorations.");
            Assert.Greater(CountTiles(collision), 20, "Generated House should build collision from walls and blocking furniture.");

            var collisionRenderer = collision.GetComponent<TilemapRenderer>();
            Assert.IsNotNull(collisionRenderer);
            Assert.IsFalse(collisionRenderer.enabled, "Collision Tilemap must not be rendered visibly.");

            var player = GameObject.Find("Player");
            var spawn = GameObject.Find("HouseSpawnPoint");
            Assert.IsNotNull(player);
            Assert.IsNotNull(spawn);
            Assert.Less(Vector3.Distance(player.transform.position, spawn.transform.position), 0.05f, "Player should start at the generated interior spawn within normal physics settling tolerance.");
        }

        [UnityTest]
        public IEnumerator HouseScene_UsesTileR02C09ForVerticalWallAccentsAndDoorFrames()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return WaitForGeneratedHouse();

            var walls = GameObject.Find("HouseWallTilemap")?.GetComponent<Tilemap>();
            var doors = GameObject.Find("HouseDoorTilemap")?.GetComponent<Tilemap>();
            Assert.IsNotNull(walls);
            Assert.IsNotNull(doors);

            Assert.Greater(CountTilesNamed(walls, VerticalWallAccentTileName), 2, "tile_r02_c09 should be used beyond the two window cells as vertical wall accents.");
            Assert.Greater(CountTilesNamed(doors, DoorTileName), 0, "Door tile should be rendered on HouseDoorTilemap, not collapsed into HouseWallTilemap.");
            Assert.IsTrue(HasDoorAdjacentAccent(doors, walls), "Door cells should be visually framed by tile_r02_c09 on adjacent wall cells.");
        }

        private static IEnumerator WaitForGeneratedHouse()
        {
            for (int i = 0; i < 60; i++)
            {
                var floor = GameObject.Find("HouseGroundTilemap")?.GetComponent<Tilemap>();
                var walls = GameObject.Find("HouseWallTilemap")?.GetComponent<Tilemap>();
                var doors = GameObject.Find("HouseDoorTilemap")?.GetComponent<Tilemap>();
                var collision = GameObject.Find("HouseCollisionTilemap")?.GetComponent<Tilemap>();
                if (floor != null && walls != null && doors != null && collision != null
                    && CountTiles(floor) > 0 && CountTiles(walls) > 0 && CountTiles(doors) > 0 && CountTiles(collision) > 0)
                {
                    yield break;
                }

                yield return null;
            }
        }
        private static int CountTiles(Tilemap tilemap)
        {
            int count = 0;
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.GetTile(position) != null)
                {
                    count++;
                }
            }
            return count;
        }

        private static int CountTilesNamed(Tilemap tilemap, string tileName)
        {
            int count = 0;
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                var tile = tilemap.GetTile(position);
                if (tile != null && tile.name == tileName)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool HasDoorAdjacentAccent(Tilemap doors, Tilemap walls)
        {
            foreach (var position in doors.cellBounds.allPositionsWithin)
            {
                var tile = doors.GetTile(position);
                if (tile == null || tile.name != DoorTileName)
                {
                    continue;
                }

                if (IsTileNamed(walls, position + Vector3Int.left, VerticalWallAccentTileName)
                    || IsTileNamed(walls, position + Vector3Int.right, VerticalWallAccentTileName)
                    || IsTileNamed(walls, position + Vector3Int.up, VerticalWallAccentTileName)
                    || IsTileNamed(walls, position + Vector3Int.down, VerticalWallAccentTileName))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsTileNamed(Tilemap tilemap, Vector3Int position, string tileName)
        {
            var tile = tilemap.GetTile(position);
            return tile != null && tile.name == tileName;
        }
    }
}
