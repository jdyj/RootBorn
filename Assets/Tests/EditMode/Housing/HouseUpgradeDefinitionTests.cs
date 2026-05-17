using NUnit.Framework;
using Rootborn.Game.Housing;
using Rootborn.Game.Interiors;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Housing
{
    public sealed class HouseUpgradeDefinitionTests
    {
        [Test]
        public void HOUSE_UPGRADE_010_StageDefinitionClampsCostsAndRequiresPositiveStage()
        {
            var profile = InteriorGenerationProfile.CreateDefaultOfficeForTests();
            var stage = HouseUpgradeStageDefinition.CreateForTests("house.stage.1", 1, 300, 120, profile, null, null);

            Assert.AreEqual("house.stage.1", stage.Id);
            Assert.AreEqual(1, stage.StageIndex);
            Assert.AreEqual(300, stage.HireCost);
            Assert.AreEqual(120, stage.DirectCost);
            Assert.AreSame(profile, stage.Profile);
        }

        [Test]
        public void HOUSE_UPGRADE_011_BlueprintRejectsMissingRequiredCells()
        {
            var blueprint = HouseConstructionBlueprintDefinition.CreateForTests(
                "blueprint.stage.1",
                new RectInt(0, 0, 4, 4),
                new[] { HouseConstructionCellRequirement.Floor(1, 1), HouseConstructionCellRequirement.Wall(1, 2), HouseConstructionCellRequirement.Door(2, 1) });

            Assert.AreEqual(3, blueprint.RequiredCells.Count);
            Assert.IsFalse(blueprint.IsCellAllowed(new Vector2Int(5, 5), HouseConstructionCellKind.Floor));
            Assert.IsTrue(blueprint.IsCellAllowed(new Vector2Int(1, 1), HouseConstructionCellKind.Floor));
        }
    }
}
