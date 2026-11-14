using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Workspaces;

namespace Rootborn.Core.Tests
{
    /// <summary>새 게임의 집에 처음 놓이는 가구 묶음(HouseStarterLayout): 기록 모양과 올바르지 않은 값(자동 테스트).</summary>
    public sealed class HouseStarterLayoutTests
    {
        [Test]
        public void Records_HaveFurnitureAnchorDirection()
        {
            var starter = new HouseStarterLayout("house-starter.x", "house", new[]
            {
                new TemplatePlacement("desk", new GridPoint(2, 13), FurniturePlacementDirection.North),
                new TemplatePlacement("chair", new GridPoint(2, 12), FurniturePlacementDirection.East),
            });

            Assert.That(starter.IsValid, Is.True);
            Assert.That(starter.Placements.Count, Is.EqualTo(2));
            var records = starter.Records();
            Assert.That(records.Length, Is.EqualTo(2));
            Assert.That((records[0].FurnitureId, records[0].SurfaceId, records[0].AnchorX, records[0].AnchorY, records[0].Direction),
                Is.EqualTo(("desk", "house", 2, 13, FurniturePlacementDirection.North)));
            Assert.That((records[1].FurnitureId, records[1].AnchorX, records[1].AnchorY, records[1].Direction),
                Is.EqualTo(("chair", 2, 12, FurniturePlacementDirection.East)));
            Assert.That((records[0].StateJson, records[0].TileName, records[0].Storage), Is.EqualTo((string.Empty, string.Empty, (FurnitureStorageSaveData)null)));

            // 부를 때마다 새 기록이다(부르는 쪽이 고쳐도 다음 기록은 그대로).
            records[0].AnchorX = 99;
            Assert.That(starter.Records()[0].AnchorX, Is.EqualTo(2));
        }

        [Test]
        public void Invalid_WhenEmptyIdsOrBadDirection()
        {
            var ok = new TemplatePlacement("desk", new GridPoint(0, 0), FurniturePlacementDirection.North);
            Assert.That(new HouseStarterLayout("", "house", new[] { ok }).IsValid, Is.False, "빈 id");
            Assert.That(new HouseStarterLayout("s", "", new[] { ok }).IsValid, Is.False, "빈 면");
            Assert.That(new HouseStarterLayout("s", "house", new[] { new TemplatePlacement("", new GridPoint(0, 0), FurniturePlacementDirection.North) }).IsValid,
                Is.False, "빈 가구 id");
            Assert.That(new HouseStarterLayout("s", "house", new[] { new TemplatePlacement("desk", new GridPoint(0, 0), (FurniturePlacementDirection)99) }).IsValid,
                Is.False, "모르는 방향");
            Assert.That(new HouseStarterLayout("s", "house", null).Placements, Is.Empty, "null 목록은 빈 목록");
            Assert.That(new HouseStarterLayout(null, null, null).Id, Is.EqualTo(string.Empty));
        }
    }
}
