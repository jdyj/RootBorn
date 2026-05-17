using NUnit.Framework;
using Rootborn.Game.Housing;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Housing
{
    public sealed class HouseConstructionSessionTests
    {
        [Test]
        public void HOUSE_UPGRADE_020_DirectConstructionCompletesOnlyAfterAllRequiredCellsArePlaced()
        {
            var blueprint = HouseConstructionBlueprintDefinition.CreateForTests(
                "blueprint.stage.1",
                new RectInt(0, 0, 4, 4),
                new[] { HouseConstructionCellRequirement.Floor(1, 1), HouseConstructionCellRequirement.Wall(1, 2), HouseConstructionCellRequirement.Door(2, 1) });

            var session = new HouseConstructionSession(blueprint);

            Assert.IsFalse(session.IsComplete);
            Assert.IsTrue(session.TryPlace(new Vector2Int(1, 1), HouseConstructionCellKind.Floor));
            Assert.IsTrue(session.TryPlace(new Vector2Int(1, 2), HouseConstructionCellKind.Wall));
            Assert.IsFalse(session.IsComplete);
            Assert.IsTrue(session.TryPlace(new Vector2Int(2, 1), HouseConstructionCellKind.Door));
            Assert.IsTrue(session.IsComplete);
        }

        [Test]
        public void HOUSE_UPGRADE_021_DirectConstructionRejectsWrongTileKind()
        {
            var blueprint = HouseConstructionBlueprintDefinition.CreateForTests(
                "blueprint.stage.1",
                new RectInt(0, 0, 4, 4),
                new[] { HouseConstructionCellRequirement.Wall(1, 2) });

            var session = new HouseConstructionSession(blueprint);

            Assert.IsFalse(session.TryPlace(new Vector2Int(1, 2), HouseConstructionCellKind.Floor));
            Assert.IsFalse(session.IsComplete);
        }
    }
}
