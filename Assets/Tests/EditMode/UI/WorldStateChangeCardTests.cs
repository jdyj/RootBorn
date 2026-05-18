using NUnit.Framework;
using Rootborn.Game.WorldState;
using Rootborn.UI.WorldState;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.UI
{
    public sealed class WorldStateChangeCardTests
    {
        [Test]
        public void WORLD_STATE_UI_001_CommonCardRendersSummaryModelWithoutFlagIdBranching()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            var card = WorldStateChangeCard.Create((RectTransform)canvasObject.transform, "WorldStateCard");
            var model = new WorldStateSummaryModel(
                "world.library.archive-open",
                "world.library.archive-open.name",
                "world.library.archive-open.desc",
                "location.library",
                "npc.librarian",
                "Check the archive shelf",
                WorldStateChangeKind.LocationUnlocked,
                WorldStateScopeKind.Shared,
                new[] { WorldStateBadgeKind.New, WorldStateBadgeKind.Shared },
                1,
                false);

            card.Bind(model);

            Assert.AreEqual("name", card.TitleTextForTests);
            StringAssert.Contains("desc", card.BodyTextForTests);
            StringAssert.Contains("library", card.BodyTextForTests);
            StringAssert.Contains("librarian", card.BodyTextForTests);
            StringAssert.Contains("Check the archive shelf", card.BodyTextForTests);
            StringAssert.Contains("New", card.BadgeTextForTests);
            StringAssert.Contains("Shared", card.BadgeTextForTests);

            Object.DestroyImmediate(canvasObject);
        }
    }
}
