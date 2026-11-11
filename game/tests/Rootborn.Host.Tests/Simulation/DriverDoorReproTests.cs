using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.World;
using Rootborn.Simulation;

namespace Rootborn.Host.Tests.Simulation
{
    /// <summary>
    /// 장치 이벤트 주입(OS 입력 계층 미확인): 시뮬레이션이 찾은 테스트 도우미 결함의 재현. 고칠 곳(PlayerDriver.cs)이 이 작업 범위 밖이라
    /// 지금 동작을 고정하고, 시뮬레이션의 실제 창 몸(DriverBody)은 자기 문 지나기를 쓴다. 고쳐지면 첫 테스트가 실패하므로 그때 지운다.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class DriverDoorReproTests
    {
        /// <summary>마을에서 해변 문 위쪽 줄의 왼쪽 끝(문 가운데에서 먼 곳)까지 걸어간 플레이어.</summary>
        private static PlayerDriver AtBeachDoorLeftEnd(out DoorArea beach)
        {
            string root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
            HostOptions options = HostOptions.Resolve(new[] { "--world", "Town", "--spawn", "town-kitchen-entry", "--size", "1920x1080", "--evidence", Path.Combine(root, "output", "host-sim-evidence") });
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            beach = player.World.Map.Doors.First(d => d.Target == "beach");
            int size = player.World.Map.Grid.CellSize;
            int doorTop = CollisionInput.CellsUnder(beach.Area, size).Min(c => c.Y);
            var reach = PlayObserver.Reachable(player.World.Map, player.PlayerCell);
            int left = (int)(beach.Area.X / size) + 1;
            CellPos end = reach.Where(c => c.Y == doorTop - 1 && c.X >= left).OrderBy(c => c.X).First();
            player.WalkTo(end);
            return player;
        }

        /// <summary>
        /// SIM-002: PlayerDriver.WalkThroughDoor는 문 가까이 가서 문 "가운데" 쪽으로 키를 누른다. 맵 아래쪽을 따라 긴 해변 문(1408x45px)의
        /// 끝 쪽에 서 있으면 가운데가 옆쪽이라 옆으로만 걷고 문에 닿지 않는다. 원인: game/tests/Rootborn.Host.Tests/PlayerDriver.cs WalkThroughDoor의 방향 고르기.
        /// </summary>
        [Test]
        public void SIM002_PlayerDriverWalkThroughDoor_MissesTheWideBeachDoorFromItsEnd()
        {
            using PlayerDriver player = AtBeachDoorLeftEnd(out _);
            Assert.That(() => player.WalkThroughDoor("beach"), Throws.InvalidOperationException, "고쳐졌다면 이 테스트를 지운다");
            Assert.That(player.World.Map.Id, Is.EqualTo("Town"));
        }

        /// <summary>같은 자리에서 시뮬레이션의 방법(문 사각형의 가장 가까운 점 쪽으로 누른다)은 해변에 간다.</summary>
        [Test]
        public void FromTheSameSpot_PressingTowardTheNearestEdge_ReachesTheBeach()
        {
            using PlayerDriver player = AtBeachDoorLeftEnd(out DoorArea beach);
            (int dx, int dy) = DoorDirection.Toward(beach.Area, player.World.Walker.X, player.World.Walker.Y);
            Assert.That((dx, dy), Is.EqualTo((0, 1)), "아래로");
            for (int i = 0; i < 4 && player.World.Map.Id == "Town"; i++) player.HoldKey(TestKey.Down, 60);
            player.Frames(60);
            Assert.That(player.World.Map.Id, Is.EqualTo("Beach"));
        }
    }
}
