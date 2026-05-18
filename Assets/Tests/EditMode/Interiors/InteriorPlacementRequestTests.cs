using NUnit.Framework;
using Rootborn.Game.Interiors;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class InteriorPlacementRequestTests
    {
        [Test]
        public void Generate_PlacesRequestedSofaCountBeforeOptionalFurniture()
        {
            var profile = InteriorGenerationProfile.CreateDefaultOfficeForTests();
            profile.ConfigurePlacementRequestsForTests(new[]
            {
                InteriorFurniturePlacementRequest.CreateForTests(InteriorObjectKind.Sofa, 2, InteriorPlacementPreference.NearWall, InteriorFacingDirection.None, true)
            });

            var map = InteriorGenerator.Generate(profile, 2718);

            Assert.GreaterOrEqual(map.CountObjects(InteriorObjectKind.Sofa), 2);
            foreach (var placed in map.PlacedObjects)
            {
                if (placed.ObjectKind == InteriorObjectKind.Sofa)
                {
                    var front = InteriorDirectionUtility.ToVector(placed.FacingDirection);
                    Assert.AreNotEqual(Vector2Int.zero, front);
                    Assert.IsTrue(map.IsWalkable(placed.Cell + front));
                }
            }
        }

        [Test]
        public void Generate_PlacesRequestedDeskClustersWithChairsAndComputers()
        {
            var profile = InteriorGenerationProfile.CreateDefaultOfficeForTests();
            profile.ConfigurePlacementRequestsForTests(new[]
            {
                InteriorFurniturePlacementRequest.CreateForTests(InteriorObjectKind.Desk, 2, InteriorPlacementPreference.AvoidCorridor, InteriorFacingDirection.East, true)
            });

            var map = InteriorGenerator.Generate(profile, 3141);

            Assert.GreaterOrEqual(map.CountObjects(InteriorObjectKind.Desk), 2);
            Assert.AreEqual(map.CountObjects(InteriorObjectKind.Desk), map.CountObjects(InteriorObjectKind.Chair));
            Assert.AreEqual(map.CountObjects(InteriorObjectKind.Desk), map.CountObjects(InteriorObjectKind.Computer));
            foreach (var placed in map.PlacedObjects)
            {
                if (placed.ObjectKind != InteriorObjectKind.Desk)
                {
                    continue;
                }

                var front = InteriorDirectionUtility.ToVector(placed.FacingDirection);
                Assert.AreEqual(InteriorObjectKind.Chair, map.GetObject(placed.Cell + front));
                Assert.AreEqual(InteriorObjectKind.Computer, map.GetObject(placed.Cell - front));
            }
        }

        [Test]
        public void Generate_ThrowsWhenRequiredPlacementCannotBeSatisfied()
        {
            var profile = InteriorGenerationProfile.CreateDefaultOfficeForTests();
            profile.ConfigurePlacementRequestsForTests(new[]
            {
                InteriorFurniturePlacementRequest.CreateForTests(InteriorObjectKind.Sofa, 100, InteriorPlacementPreference.NearWall, InteriorFacingDirection.None, true)
            });

            Assert.Throws<InteriorPlacementException>(() => InteriorGenerator.Generate(profile, 999));
        }

        [Test]
        public void Generate_SkipsUnsatisfiedOptionalPlacementRequest()
        {
            var profile = InteriorGenerationProfile.CreateDefaultOfficeForTests();
            profile.ConfigurePlacementRequestsForTests(new[]
            {
                InteriorFurniturePlacementRequest.CreateForTests(InteriorObjectKind.Sofa, 100, InteriorPlacementPreference.NearWall, InteriorFacingDirection.None, false)
            });

            var map = InteriorGenerator.Generate(profile, 999);

            Assert.Greater(map.CountCells(InteriorCellKind.Floor), 0);
            Assert.IsTrue(InteriorPathValidator.CanReachAnyDoor(map, map.SpawnCell));
        }
    }
}
