using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.PlayMode
{
    public sealed class HouseSceneBootTests
    {
        [UnityTest]
        public IEnumerator HouseScene_LoadsPlayableTilePaintingBaseline()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;

            Scene active = SceneManager.GetActiveScene();
            Assert.AreEqual("House", active.name);

            var player = GameObject.Find("Player");
            Assert.IsNotNull(player, "House scene should contain a Player start object.");
            Assert.IsNotNull(player.GetComponent<PlayerController>(), "House Player should support normal movement input.");
            Assert.IsNotNull(player.GetComponent<Rigidbody2D>(), "House Player should have 2D physics for movement.");
            Assert.IsNotNull(player.GetComponent<Collider2D>(), "House Player should have a 2D collider for collision.");

            var spawn = GameObject.Find("HouseSpawnPoint");
            Assert.IsNotNull(spawn, "House scene should expose a named spawn point for the start location.");
            Assert.Less(Vector3.Distance(player.transform.position, spawn.transform.position), 0.05f, "Player should start at the House spawn point within normal physics settling tolerance.");

            Assert.IsNotNull(Camera.main, "House scene should contain a Main Camera.");
            Assert.IsTrue(Camera.main.orthographic, "House camera should be orthographic for tile painting and play.");
            Assert.IsNotNull(Camera.main.GetComponent<CameraFollow>(), "House camera should follow the Player.");

            var bounds = GameObject.Find("HouseCameraBounds");
            Assert.IsNotNull(bounds, "House scene should define a finite camera bounds object.");
            Assert.IsNotNull(bounds.GetComponent<BoxCollider2D>(), "House camera bounds should be editable as a BoxCollider2D.");

            var grid = GameObject.Find("Grid");
            Assert.IsNotNull(grid, "House scene should contain a Grid root for painting tiles.");
            Assert.IsNotNull(GameObject.Find("HouseGroundTilemap")?.GetComponent<Tilemap>(), "House should expose a ground tilemap layer.");
            Assert.IsNotNull(GameObject.Find("HouseWallTilemap")?.GetComponent<Tilemap>(), "House should expose a wall tilemap layer.");
            Assert.IsNotNull(GameObject.Find("HouseDecorationTilemap")?.GetComponent<Tilemap>(), "House should expose a decoration tilemap layer.");
            Assert.IsNotNull(GameObject.Find("HouseCollisionTilemap")?.GetComponent<Tilemap>(), "House should expose a collision tilemap layer.");
        }

        [UnityTest]
        public IEnumerator HouseCamera_StopsAtConfiguredBoundsWhenPlayerLeavesEdge()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;

            var player = GameObject.Find("Player");
            var camera = Camera.main;
            var bounds = GameObject.Find("HouseCameraBounds")?.GetComponent<BoxCollider2D>();
            Assert.IsNotNull(player, "Player");
            Assert.IsNotNull(camera, "Main Camera");
            Assert.IsNotNull(bounds, "HouseCameraBounds");

            player.transform.position = new Vector3(100f, 0f, 0f);
            for (int i = 0; i < 180; i++)
            {
                yield return null;
            }

            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * camera.aspect;
            float maxCameraX = bounds.bounds.max.x - halfWidth;
            Assert.LessOrEqual(camera.transform.position.x, maxCameraX + 0.05f, "Camera should stop at the right edge of HouseCameraBounds instead of following the Player forever.");
        }
    }
}
