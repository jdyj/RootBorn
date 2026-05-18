using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Game.Interiors;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class InteriorGenerationTests
    {
        [Test]
        public void Generate_SameSeed_ProducesSameSignature()
        {
            var profile = InteriorGenerationProfile.CreateDefaultOfficeForTests();

            var a = InteriorGenerator.Generate(profile, 12345);
            var b = InteriorGenerator.Generate(profile, 12345);

            Assert.AreEqual(a.Signature(), b.Signature());
        }

        [Test]
        public void Generate_DifferentSeeds_ProduceDifferentFurnitureLayout()
        {
            var profile = InteriorGenerationProfile.CreateDefaultOfficeForTests();

            var a = InteriorGenerator.Generate(profile, 12345);
            var b = InteriorGenerator.Generate(profile, 12346);

            Assert.AreNotEqual(a.Signature(), b.Signature());
        }

        [Test]
        public void Generate_CreatesConnectedOfficeWithRoomsCorridorsAndDoor()
        {
            var profile = InteriorGenerationProfile.CreateDefaultOfficeForTests();
            var map = InteriorGenerator.Generate(profile, 3021);

            Assert.GreaterOrEqual(map.RoomCount, 2, "The generated House interior should contain multiple usable rooms.");
            Assert.Greater(map.CountCells(InteriorCellKind.Corridor), 0, "The generated House interior should include corridor cells.");
            Assert.Greater(map.CountCells(InteriorCellKind.Door), 0, "The generated House interior should include at least one door.");
            Assert.IsTrue(InteriorPathValidator.CanReachAnyDoor(map, map.SpawnCell), "The spawn cell must be connected to a door through walkable space.");
        }

        [Test]
        public void Generate_LeavesDoorFrontAndDeskFrontWalkable()
        {
            var profile = InteriorGenerationProfile.CreateDefaultOfficeForTests();
            var map = InteriorGenerator.Generate(profile, 4040);

            foreach (var cell in map.Cells)
            {
                if (cell.Kind == InteriorCellKind.Door)
                {
                    Assert.IsTrue(map.IsWalkable(cell.Position + Vector2Int.up) || map.IsWalkable(cell.Position + Vector2Int.down) || map.IsWalkable(cell.Position + Vector2Int.left) || map.IsWalkable(cell.Position + Vector2Int.right), $"Door at {cell.Position} should have adjacent walkable clearance.");
                }

                if (cell.ObjectKind == InteriorObjectKind.Desk)
                {
                    var front = InteriorDirectionUtility.ToVector(cell.FacingDirection);
                    Assert.AreNotEqual(Vector2Int.zero, front, $"Desk at {cell.Position} should record a facing direction.");
                    Assert.IsTrue(map.IsWalkable(cell.Position + front), $"Desk at {cell.Position} should keep its facing/front cell walkable.");
                }
            }
        }

        [Test]
        public void Generate_PlacesOfficeFurnitureWithRequiredRelationships()
        {
            var profile = InteriorGenerationProfile.CreateDefaultOfficeForTests();
            var map = InteriorGenerator.Generate(profile, 9090);

            Assert.Greater(map.CountObjects(InteriorObjectKind.Desk), 0, "Office generation should place at least one desk.");
            Assert.AreEqual(map.CountObjects(InteriorObjectKind.Desk), map.CountObjects(InteriorObjectKind.Chair), "Each desk should receive a matching chair.");
            Assert.AreEqual(map.CountObjects(InteriorObjectKind.Desk), map.CountObjects(InteriorObjectKind.Computer), "Each desk should receive a computer prop on the desk.");
            Assert.Greater(map.CountObjects(InteriorObjectKind.Sofa), 0, "Office generation should place a sofa near a wall when space allows.");
            Assert.Greater(map.CountObjects(InteriorObjectKind.Plant), 0, "Office generation should use remaining free cells for plants/decorations.");
        }

        [Test]
        public void Generate_RecordsDeskChairAndComputerDirections()
        {
            var profile = InteriorGenerationProfile.CreateDefaultOfficeForTests();
            var map = InteriorGenerator.Generate(profile, 9090);

            foreach (var placed in map.PlacedObjects)
            {
                if (placed.ObjectKind != InteriorObjectKind.Desk)
                {
                    continue;
                }

                var front = InteriorDirectionUtility.ToVector(placed.FacingDirection);
                Assert.AreNotEqual(Vector2Int.zero, front, $"Desk at {placed.Cell} should have a facing direction.");
                Assert.AreEqual(InteriorObjectKind.Chair, map.GetObject(placed.Cell + front), $"Desk at {placed.Cell} should place its chair in front.");
                Assert.AreEqual(InteriorObjectKind.Computer, map.GetObject(placed.Cell - front), $"Desk at {placed.Cell} should place its computer behind/on the desk.");
                Assert.AreEqual(placed.FacingDirection, map.GetFacingDirection(placed.Cell + front), $"Chair at {placed.Cell + front} should face the same desk direction metadata.");
            }
        }

        [Test]
        public void Generate_UsesMultipleDeskFacingDirectionsAcrossSeeds()
        {
            var profile = InteriorGenerationProfile.CreateDefaultOfficeForTests();
            var directions = new HashSet<InteriorFacingDirection>();

            for (int seed = 9000; seed < 9030; seed++)
            {
                var map = InteriorGenerator.Generate(profile, seed);
                foreach (var placed in map.PlacedObjects)
                {
                    if (placed.ObjectKind == InteriorObjectKind.Desk)
                    {
                        directions.Add(placed.FacingDirection);
                    }
                }
            }

            Assert.GreaterOrEqual(directions.Count, 2, "Office desks should not all face the same direction across random seeds.");
        }

        [Test]
        public void Generate_SofaRecordsDirectionTowardOpenRoom()
        {
            var profile = InteriorGenerationProfile.CreateDefaultOfficeForTests();
            var map = InteriorGenerator.Generate(profile, 777);

            foreach (var placed in map.PlacedObjects)
            {
                if (placed.ObjectKind != InteriorObjectKind.Sofa)
                {
                    continue;
                }

                var front = InteriorDirectionUtility.ToVector(placed.FacingDirection);
                Assert.AreNotEqual(Vector2Int.zero, front, $"Sofa at {placed.Cell} should record a room-facing direction.");
                Assert.IsTrue(map.IsWalkable(placed.Cell + front), $"Sofa at {placed.Cell} should face open walkable space.");
                return;
            }

            Assert.Fail("Generated office should contain a sofa to validate wall-facing placement metadata.");
        }

        [Test]
        public void Generate_CollisionMatchesBlockingCells()
        {
            var profile = InteriorGenerationProfile.CreateDefaultOfficeForTests();
            var map = InteriorGenerator.Generate(profile, 777);

            foreach (var cell in map.Cells)
            {
                bool expectedBlocked = cell.Kind == InteriorCellKind.Wall || cell.BlocksMovement;
                Assert.AreEqual(expectedBlocked, map.IsCollision(cell.Position), $"Collision mismatch at {cell.Position}.");
            }
        }

        [Test]
        public void ClearObject_RemovesPlacedObjectAndReleasesCollision()
        {
            var map = new InteriorGeneratedMap(4, 4);
            var cell = new Vector2Int(1, 1);
            map.SetKind(cell, InteriorCellKind.Floor);
            Assert.IsTrue(map.TryPlaceObject(cell, InteriorObjectKind.Desk, true, InteriorFacingDirection.East));
            Assert.AreEqual(InteriorObjectKind.Desk, map.GetObject(cell));
            Assert.IsTrue(map.IsCollision(cell));

            Assert.IsTrue(map.ClearObject(cell));

            Assert.AreEqual(InteriorObjectKind.None, map.GetObject(cell));
            Assert.AreEqual(InteriorFacingDirection.None, map.GetFacingDirection(cell));
            Assert.IsFalse(map.IsCollision(cell));
            Assert.IsTrue(map.IsWalkable(cell));
            Assert.AreEqual(0, map.CountObjects(InteriorObjectKind.Desk));
        }
    }
}
