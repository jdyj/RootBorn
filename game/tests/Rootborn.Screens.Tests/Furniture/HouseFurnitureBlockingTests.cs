using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.World;
using Rootborn.Screens.Furniture;

namespace Rootborn.Screens.Tests.Furniture
{
    /// <summary>
    /// 가구 방의 걷는 격자에 게임 한 판의 가구를 맞추는 곳(Host와 창 없는 판이 같이 쓴다): 플레이어가 어느 맵에 있든 면 id로 가구 방을 찾아 넣고,
    /// 막힌 칸이 실제로 바뀐 때만 알린다. 실제 data/와 GameSession, 시험용 방(10x8칸). 자동 테스트다.
    /// </summary>
    public sealed class HouseFurnitureBlockingTests
    {
        /// <summary>그 판이 놓을 수 있는 한 칸짜리 막는 가구(상자 제외).</summary>
        private static string Blocker(Rootborn.Game.GameSession session)
        {
            var storage = new HashSet<string>(session.Definitions.All<FurnitureStorageDefinition>().Select(s => s.Furniture.StableId));
            return session.AvailableFurniture().First(f => f.BlocksMovement && f.FootprintCells.Count == 1 && !storage.Contains(f.StableId)).StableId;
        }

        private static (HouseFurnitureBlocking Sync, FurnitureRoom Room, List<string> Changed) Make()
        {
            var room = new FurnitureRoom(FurniturePlacementScreenTests.TestRoomMap());
            var changed = new List<string>();
            var sync = new HouseFurnitureBlocking(id => id == room.Map.Id ? room : null, new[] { (room.Map.Id, "house") }, changed.Add);
            return (sync, room, changed);
        }

        [Test]
        public void 집_밖에서_이어도_세이브의_가구가_집_격자에_든다()
        {
            using var first = new TestSession();
            var placedRoom = new FurnitureRoom(FurniturePlacementScreenTests.TestRoomMap());
            first.Session.EnterHouse(placedRoom.Grid, "house"); // 준비: 집에서 가구를 놓아 저장한다
            var cell = new GridPoint(3, 3);
            Assert.That(first.Session.PlaceFurniture(Blocker(first.Session), cell, FurniturePlacementDirection.North), Is.True);

            using TestSession again = first.Reopen();
            Assert.That(again.Session.House, Is.Null, "이어 한 판은 아직 집에 들어가지 않았다");
            var (sync, room, changed) = Make();
            sync.Attach(again.Session, "Town");
            Assert.That(again.Session.House, Is.Not.Null, "집 밖에서 집 배치를 읽어 두었다");
            Assert.That(room.WalkMap.Collision.IsBlocked(room.ToMap(cell)), Is.True, "집에 들어가기 전에 가구 칸이 막혔다");
            Assert.That(changed, Is.EqualTo(new[] { room.Map.Id }), "막힌 칸이 바뀐 가구 방을 알린다(NPC가 길을 다시 만든다)");
        }

        [Test]
        public void 가구가_바뀌면_지금_맵과_상관없이_넣고_바뀐_때만_알린다()
        {
            using var t = new TestSession();
            var (sync, room, changed) = Make();
            sync.Attach(t.Session, "Town");
            changed.Clear();
            var cell = new GridPoint(4, 2);
            Assert.That(t.Session.PlaceFurniture(Blocker(t.Session), cell, FurniturePlacementDirection.North), Is.True, "집 배치를 읽어 두었으니 놓을 수 있다(준비)");
            sync.Apply(t.Session);
            Assert.That(room.WalkMap.Collision.IsBlocked(room.ToMap(cell)), Is.True);
            Assert.That(changed, Is.EqualTo(new[] { room.Map.Id }));
            sync.Apply(t.Session);
            Assert.That(changed, Has.Count.EqualTo(1), "같은 칸이면 다시 알리지 않는다(프레임마다 다시 계획하지 않는다)");
        }

        [Test]
        public void 이미_집에_들어간_판은_다시_읽지_않고_다른_면의_방은_건드리지_않는다()
        {
            using var t = new TestSession();
            var house = new FurnitureRoom(FurniturePlacementScreenTests.TestRoomMap());
            t.Session.EnterHouse(house.Grid, "house");
            var layout = t.Session.House;
            var other = new FurnitureRoom(new WorldMap("Shed", FurniturePlacementScreenTests.TestRoomMap().Collision, new List<DoorArea>(),
                new Dictionary<string, (float X, float Y)> { ["entry"] = (5 * 32 + 16, 6 * 32 + 16) }));
            var changed = new List<string>();
            var sync = new HouseFurnitureBlocking(id => id == "TestRoom" ? house : id == "Shed" ? other : null,
                new[] { ("Shed", "shed"), ("TestRoom", "house") }, changed.Add);
            Assert.That(t.Session.PlaceFurniture(Blocker(t.Session), new GridPoint(3, 3), FurniturePlacementDirection.North), Is.True);
            sync.Attach(t.Session, "Town");
            Assert.That(t.Session.House, Is.SameAs(layout), "집 배치가 있으면 다시 읽지 않는다");
            Assert.That(changed, Is.EqualTo(new[] { "TestRoom" }), "면이 같은 가구 방만");
            Assert.That(other.WalkMap.Collision.IsBlocked(other.ToMap(new GridPoint(3, 3))), Is.False);
        }

        [Test]
        public void 일터_면이_먼저_나와도_집_면의_배치를_읽어_둔다()
        {
            using var first = new TestSession();
            var placedRoom = new FurnitureRoom(FurniturePlacementScreenTests.TestRoomMap());
            first.Session.EnterHouse(placedRoom.Grid, "house"); // 준비: 집에서 가구를 놓아 저장한다
            var cell = new GridPoint(3, 3);
            Assert.That(first.Session.PlaceFurniture(Blocker(first.Session), cell, FurniturePlacementDirection.North), Is.True);

            using TestSession again = first.Reopen();
            var house = new FurnitureRoom(FurniturePlacementScreenTests.TestRoomMap());
            var kitchen = new FurnitureRoom(new WorldMap("Kitchen", FurniturePlacementScreenTests.TestRoomMap().Collision, new List<DoorArea>(),
                new Dictionary<string, (float X, float Y)> { ["entry"] = (5 * 32 + 16, 6 * 32 + 16) }));
            string workspaceSurface = again.Session.Definitions.All<Rootborn.Core.Workspaces.Workspace>().First().SurfaceId;
            var changed = new List<string>();
            var sync = new HouseFurnitureBlocking(id => id == "TestRoom" ? house : id == "Kitchen" ? kitchen : null,
                new[] { ("Kitchen", workspaceSurface), ("TestRoom", "house") }, changed.Add);
            sync.Attach(again.Session, "Kitchen");
            Assert.That(again.Session.House?.SurfaceId, Is.EqualTo("house"), "일터 면이 아니라 집 면으로 읽어 둔다");
            Assert.That(house.WalkMap.Collision.IsBlocked(house.ToMap(cell)), Is.True, "집 격자에 가구 칸이 막혔다");
            Assert.That(kitchen.WalkMap.Collision.IsBlocked(kitchen.ToMap(cell)), Is.False, "일터 격자에는 집 가구를 넣지 않는다");
        }
    }
}
