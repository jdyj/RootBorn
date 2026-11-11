using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/Interiors/QuestFurniturePlacementEventTests.cs
    // 퀘스트에 알리는 "가구를 놓았다"는 새로 놓아 저장된 것뿐이다. 거절한 배치, 저장 기록 되살리기, 옮기기, 치우기는 알리지 않는다.
    // (GameSession은 이 이벤트로만 배치 퀘스트 이벤트를 내고, 이미 놓인 가구는 재생 이벤트로 따로 다시 알린다.)
    public sealed class FurniturePlacementEventTests
    {
        private static GridPoint P(int x, int y) => new GridPoint(x, y);

        private static readonly FurnitureDefinition Chair = new FurnitureDefinition("chair", "chair", null, "Seating", new[] { "house" },
            new[] { new FurnitureTilePart(P(0, 0), "sprite.png", "tile") }, null, true, FurniturePlacementPreference.Any);

        private static IEnumerable<GridPoint> Floor()
        {
            for (int x = 0; x <= 4; x++)
                for (int y = 0; y <= 2; y++)
                    yield return P(x, y);
        }

        private static HouseFurnitureLayout Layout(List<FurniturePlacementSaveData[]> commits, bool commitSucceeds = true)
            => new HouseFurnitureLayout(new HousePlacementModel(new PlacementGrid(Floor(), new[] { P(2, 1) }, P(0, 0), new[] { P(4, 0) })), "house",
                records => { commits.Add(records); return commitSucceeds; });

        [Test]
        public void ASuccessfulPlacement_AnnouncesThatPieceOnce_AfterTheLayoutIsSaved()
        {
            var commits = new List<FurniturePlacementSaveData[]>();
            var layout = Layout(commits);
            var announced = new List<string>();
            layout.PlacementCommitted += f => { Assert.That(commits.Count, Is.EqualTo(1), "저장한 뒤에 알린다"); announced.Add(f.StableId); };

            Assert.That(layout.TryPlace(Chair, P(1, 1), FurniturePlacementDirection.North), Is.True);
            Assert.That(announced, Is.EqualTo(new[] { "chair" }));
        }

        [Test]
        public void ARejectedPlacement_NeitherSavesNorAnnounces()
        {
            var commits = new List<FurniturePlacementSaveData[]>();
            var layout = Layout(commits);
            int announced = 0;
            layout.PlacementCommitted += _ => announced++;

            Assert.That(layout.TryPlace(Chair, P(2, 1), FurniturePlacementDirection.North), Is.False, "고정 소품 칸");
            Assert.That(layout.TryPlace(Chair, P(9, 9), FurniturePlacementDirection.North), Is.False, "방 밖");
            Assert.That(commits, Is.Empty);
            Assert.That(announced, Is.Zero);
        }

        [Test]
        public void RestoringMovingAndClearing_DoNotAnnounceANewPlacement()
        {
            var commits = new List<FurniturePlacementSaveData[]>();
            var layout = Layout(commits);
            int announced = 0;
            layout.PlacementCommitted += _ => announced++;

            layout.Restore(new[] { new FurniturePlacementSaveData { FurnitureId = "chair", SurfaceId = "house", AnchorX = 1, AnchorY = 1, Direction = FurniturePlacementDirection.North } },
                new[] { Chair });
            Assert.That(layout.Placed.Count, Is.EqualTo(1));
            string id = layout.Placed.Single().InstanceId;
            Assert.That(layout.TryMove(id, P(3, 1), FurniturePlacementDirection.East), Is.True);
            Assert.That(layout.ClearUserFurniture(), Is.True);

            Assert.That(announced, Is.Zero);
        }
    }
}
