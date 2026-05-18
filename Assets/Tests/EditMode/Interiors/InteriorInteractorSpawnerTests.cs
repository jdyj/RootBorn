using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Interiors;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class InteriorInteractorSpawnerTests
    {
        [Test]
        public void Generate_RecordsPlacedObjectsForInteractiveFurniture()
        {
            var profile = InteriorGenerationProfile.CreateDefaultOfficeForTests();
            var map = InteriorGenerator.Generate(profile, 9090);

            var placedComputers = map.PlacedObjects.Where(placed => placed.ObjectKind == InteriorObjectKind.Computer).ToArray();

            Assert.AreEqual(map.CountObjects(InteriorObjectKind.Computer), placedComputers.Length);
            Assert.Greater(placedComputers.Length, 0, "Generated computer props should be recorded so object interacters can be spawned later.");
            Assert.IsTrue(placedComputers.All(placed => map.GetObject(placed.Cell) == InteriorObjectKind.Computer));
        }

        [Test]
        public void SpawnInteractables_CreatesComputerInteractorAtTileCenter()
        {
            var profile = InteriorGenerationProfile.CreateDefaultOfficeForTests();
            var map = InteriorGenerator.Generate(profile, 9090);
            var computerDefinition = InteriorInteractionDefinition.CreateForTests(InteriorObjectKind.Computer, "[E] Use Computer");
            var gridObject = new GameObject("Grid");
            var tilemapObject = new GameObject("Floor");
            var root = new GameObject("Interactors");

            try
            {
                gridObject.AddComponent<Grid>();
                tilemapObject.transform.SetParent(gridObject.transform, false);
                var tilemap = tilemapObject.AddComponent<Tilemap>();
                tilemapObject.AddComponent<TilemapRenderer>();
                var offset = new Vector3Int(-map.Width / 2, -map.Height / 2, 0);

                var spawned = InteriorInteractorSpawner.SpawnInteractables(map, tilemap, offset, root.transform, new[] { computerDefinition });

                Assert.AreEqual(map.CountObjects(InteriorObjectKind.Computer), spawned.Count);
                Assert.Greater(spawned.Count, 0);

                var interactor = spawned[0].GetComponent<InteriorObjectInteractor>();
                Assert.IsNotNull(interactor);
                Assert.AreEqual("[E] Use Computer", interactor.InteractionPrompt);
                Assert.AreEqual(InteriorObjectKind.Computer, interactor.ObjectKind);

                var expectedCell = new Vector3Int(interactor.Cell.x, interactor.Cell.y, 0) + offset;
                Assert.AreEqual(tilemap.GetCellCenterWorld(expectedCell), interactor.transform.position);

                var collider = spawned[0].GetComponent<BoxCollider2D>();
                Assert.IsNotNull(collider);
                Assert.IsTrue(collider.isTrigger);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tilemapObject);
                Object.DestroyImmediate(gridObject);
                Object.DestroyImmediate(computerDefinition);
            }
        }
    }
}
