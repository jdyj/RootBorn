using NUnit.Framework;
using Rootborn.Game.Interiors;
using Rootborn.UI.Interiors;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class InteriorFurniturePlacementUiModelTests
    {
        [Test]
        public void BuildPlacementRequests_ConvertsConfiguredFurnitureRows()
        {
            var model = new InteriorFurniturePlacementUiModel();
            model.SetFurniture(InteriorObjectKind.Desk, count: 2, InteriorFacingDirection.East, InteriorPlacementPreference.AvoidCorridor, required: true, interactivePreview: true);
            model.SetFurniture(InteriorObjectKind.Sofa, count: 1, InteriorFacingDirection.North, InteriorPlacementPreference.NearWall, required: false, interactivePreview: false);
            model.SetFurniture(InteriorObjectKind.Plant, count: 0, InteriorFacingDirection.None, InteriorPlacementPreference.Any, required: true, interactivePreview: false);

            var requests = model.BuildPlacementRequests();

            Assert.AreEqual(2, requests.Length);
            Assert.AreEqual(InteriorObjectKind.Desk, requests[0].ObjectKind);
            Assert.AreEqual(2, requests[0].Count);
            Assert.AreEqual(InteriorFacingDirection.East, requests[0].FacingDirection);
            Assert.AreEqual(InteriorPlacementPreference.AvoidCorridor, requests[0].Preference);
            Assert.IsTrue(requests[0].Required);
            Assert.AreEqual(InteriorObjectKind.Sofa, requests[1].ObjectKind);
            Assert.AreEqual(1, requests[1].Count);
            Assert.AreEqual(InteriorFacingDirection.North, requests[1].FacingDirection);
            Assert.AreEqual(InteriorPlacementPreference.NearWall, requests[1].Preference);
            Assert.IsFalse(requests[1].Required);
        }

        [Test]
        public void Regenerate_DefaultManualPlacementMapStartsWithoutAutoFurniture()
        {
            var model = new InteriorFurniturePlacementUiModel();
            model.SetSeed("1205");
            model.SetFurniture(InteriorObjectKind.Desk, count: 0, InteriorFacingDirection.None, InteriorPlacementPreference.AvoidCorridor, required: false, interactivePreview: true);

            var result = model.Regenerate(null);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.Map.CountObjects(InteriorObjectKind.Desk));
            Assert.AreEqual(0, result.Map.CountObjects(InteriorObjectKind.Chair));
            Assert.AreEqual(0, result.Map.CountObjects(InteriorObjectKind.Computer));
            Assert.AreEqual(0, result.Map.CountObjects(InteriorObjectKind.Sofa));
            Assert.AreEqual(0, result.Map.CountObjects(InteriorObjectKind.Plant));
        }

        [Test]
        public void Regenerate_RequiredFailureReportsStatusAndPreservesPreviousMap()
        {
            var model = new InteriorFurniturePlacementUiModel();
            model.SetSeed("17");
            model.Regenerate(InteriorGenerationProfile.CreateDefaultOfficeForTests());
            var previous = model.CurrentMap;

            model.SetFurniture(InteriorObjectKind.Sofa, count: 100, InteriorFacingDirection.None, InteriorPlacementPreference.NearWall, required: true, interactivePreview: false);

            var result = model.Regenerate(InteriorGenerationProfile.CreateDefaultOfficeForTests());

            Assert.IsFalse(result.Success);
            StringAssert.Contains("Could not satisfy required furniture placement request", result.Message);
            Assert.AreSame(previous, model.CurrentMap, "Failed required placement must not replace the last successful map.");
        }

        [Test]
        public void Regenerate_OptionalFailureKeepsGeneratedMapAndSummary()
        {
            var model = new InteriorFurniturePlacementUiModel();
            model.SetSeed("23");
            model.SetFurniture(InteriorObjectKind.Sofa, count: 100, InteriorFacingDirection.None, InteriorPlacementPreference.NearWall, required: false, interactivePreview: false);

            var result = model.Regenerate(InteriorGenerationProfile.CreateDefaultOfficeForTests());

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(model.CurrentMap);
            StringAssert.Contains("Generated", result.Message);
            StringAssert.Contains("Desk", result.Summary);
            StringAssert.Contains("Sofa", result.Summary);
            StringAssert.Contains("Plant", result.Summary);
        }
    }
}
