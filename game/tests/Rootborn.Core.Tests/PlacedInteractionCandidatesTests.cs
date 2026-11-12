using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Interaction;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.World;

namespace Rootborn.Core.Tests
{
    /// <summary>놓인 가구마다 상호작용 후보를 만드는 함수(Host와 시험 세계가 함께 쓴다).</summary>
    public sealed class PlacedInteractionCandidatesTests
    {
        private static GridPoint P(int x, int y) => new GridPoint(x, y);

        private static FurnitureDefinition Furniture(string id, params GridPoint[] cells)
            => new FurnitureDefinition(id, id, null, "Storage", new[] { "house" }, cells.Select(c => new FurnitureTilePart(c, "s.png", "t")).ToArray(), null, true,
                FurniturePlacementPreference.Any);

        private static HouseFurnitureLayout Layout()
        {
            var floor = new List<GridPoint>();
            for (int x = 0; x < 6; x++) for (int y = 0; y < 6; y++) floor.Add(P(x, y));
            return new HouseFurnitureLayout(new HousePlacementModel(new PlacementGrid(floor, null, P(0, 0), new[] { P(0, 0) })), "house");
        }

        // 칸 (x,y)의 가운데가 (x*10+5, y*10+5)인 방
        private static AreaRect Cell(GridPoint c) => new AreaRect(c.X * 10, c.Y * 10, 10, 10);

        [Test]
        public void Fill_MakesOneCandidatePerMatchingFurniture_AtTheAverageOfItsCellCenters()
        {
            var crate = Furniture("crate", P(0, 0));
            var wide = Furniture("wide", P(0, 0), P(1, 0));
            var chair = Furniture("chair", P(0, 0));
            var layout = Layout();
            Assert.That(layout.TryPlace(crate, P(1, 1), FurniturePlacementDirection.North), Is.True);
            Assert.That(layout.TryPlace(wide, P(3, 3), FurniturePlacementDirection.North), Is.True);
            Assert.That(layout.TryPlace(chair, P(5, 5), FurniturePlacementDirection.North), Is.True);

            var result = new List<InteractionCandidate>();
            PlacedInteractionCandidates.Fill(layout.Placed, d => d != chair, Cell, "kind-x", result);

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result.All(c => c.Kind == "kind-x" && c.Acts && c.Priority == PlacedInteractionCandidates.Priority), Is.True);
            Assert.That(result[0].Id, Is.EqualTo(layout.Placed[0].InstanceId));
            Assert.That((result[0].X, result[0].Y), Is.EqualTo((15f, 15f)));
            Assert.That(result[1].Id, Is.EqualTo(layout.Placed[1].InstanceId));
            Assert.That((result[1].X, result[1].Y), Is.EqualTo((40f, 35f)), "차지한 칸 둘의 가운데 평균");
        }

        [Test]
        public void Fill_AddsToTheGivenList_AndDoesNotClearIt()
        {
            var layout = Layout();
            layout.TryPlace(Furniture("crate", P(0, 0)), P(1, 1), FurniturePlacementDirection.North);
            var result = new List<InteractionCandidate> { new InteractionCandidate("old", "o", 0, 0, 0, true) };
            PlacedInteractionCandidates.Fill(layout.Placed, d => true, Cell, "k", result);
            Assert.That(result.Select(c => c.Kind), Is.EqualTo(new[] { "old", "k" }));
        }

        [Test]
        public void Fill_SkipsFurnitureThatOccupiesNoCells()
        {
            var ghost = Furniture("ghost");
            var placed = new[] { new PlacedFurniture("ghost#1", ghost, new FurniturePlacementSaveData { FurnitureId = "ghost", Direction = FurniturePlacementDirection.North }) };
            Assert.That(placed[0].OccupiedCells.Count, Is.EqualTo(0));
            var result = new List<InteractionCandidate>();
            PlacedInteractionCandidates.Fill(placed, d => true, Cell, "k", result);
            Assert.That(result, Is.Empty);
        }
    }
}
