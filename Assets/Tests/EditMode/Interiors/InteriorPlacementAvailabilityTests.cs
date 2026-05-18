using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Interiors;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class InteriorPlacementAvailabilityTests
    {
        [Test]
        public void CollectCandidates_DeskRequiresDeskChairAndComputerCells()
        {
            var profile = InteriorGenerationProfile.CreateDefaultOfficeForTests();
            var map = InteriorGenerator.Generate(profile, 4401);
            var request = InteriorFurniturePlacementRequest.CreateForTests(InteriorObjectKind.Desk, 1, InteriorPlacementPreference.AvoidCorridor, InteriorFacingDirection.East, true);

            var candidates = InteriorPlacementAvailability.CollectCandidates(map, request);

            Assert.Greater(candidates.Valid.Count, 0);
            foreach (var cell in candidates.Valid)
            {
                var front = InteriorDirectionUtility.ToVector(InteriorFacingDirection.East);
                Assert.IsTrue(map.IsWalkable(cell));
                Assert.IsTrue(map.IsWalkable(cell + front));
                Assert.IsTrue(map.IsWalkableBase(cell - front));
                Assert.AreEqual(InteriorObjectKind.None, map.GetObject(cell));
                Assert.AreEqual(InteriorObjectKind.None, map.GetObject(cell + front));
                Assert.AreEqual(InteriorObjectKind.None, map.GetObject(cell - front));
            }
        }

        [Test]
        public void Summarize_ReturnsCountsAndWarningWhenRequiredRequestExceedsCandidates()
        {
            var profile = InteriorGenerationProfile.CreateDefaultOfficeForTests();
            var map = InteriorGenerator.Generate(profile, 4401);
            var request = InteriorFurniturePlacementRequest.CreateForTests(InteriorObjectKind.Sofa, 100, InteriorPlacementPreference.NearWall, InteriorFacingDirection.None, true);

            var summary = InteriorPlacementAvailability.Summarize(map, request);

            Assert.Greater(summary.ValidCount, 0);
            Assert.Greater(summary.InvalidCount, 0);
            Assert.IsFalse(summary.HasEnoughCandidates);
            StringAssert.Contains("available", summary.Message);
            StringAssert.Contains("requested 100", summary.Message);
        }

        [Test]
        public void TryPlaceManual_PlacesDeskClusterAndRejectsInvalidCells()
        {
            var profile = InteriorGenerationProfile.CreateDefaultOfficeForTests();
            var map = InteriorGenerator.Generate(profile, 4401);
            var request = InteriorFurniturePlacementRequest.CreateForTests(InteriorObjectKind.Desk, 1, InteriorPlacementPreference.AvoidCorridor, InteriorFacingDirection.East, true);
            var valid = InteriorPlacementAvailability.CollectCandidates(map, request).Valid.First();
            var beforeDesk = map.CountObjects(InteriorObjectKind.Desk);

            Assert.IsTrue(InteriorPlacementAvailability.TryPlaceManual(map, valid, request, out var message));
            Assert.AreEqual("Placed Desk", message);
            Assert.AreEqual(beforeDesk + 1, map.CountObjects(InteriorObjectKind.Desk));
            Assert.AreEqual(InteriorObjectKind.Chair, map.GetObject(valid + Vector2Int.right));
            Assert.AreEqual(InteriorObjectKind.Computer, map.GetObject(valid - Vector2Int.right));

            Assert.IsFalse(InteriorPlacementAvailability.TryPlaceManual(map, valid, request, out var secondMessage));
            StringAssert.Contains("not valid", secondMessage);
        }
    }
}
