using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.World;
using Rootborn.World;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 건물 부지의 열리는 문을 실제 창과 게임 루프로(장치 이벤트 주입, OS 입력 계층 미확인). 고정 맵 game/tests/Rootborn.World.Tests/Fixtures/buildings
    /// (부지 하나, 2단계 건물, 문 프레임 셋)를 --maps·--buildings로 띄우고 방향 키(위)만 눌러 문으로 걸어 들어간다.
    /// 문 프레임 번호를 프레임마다 읽는다 — 그림의 픽셀에는 기대지 않는다. 스크린샷: output/building-door-evidence/*.png.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class BuildingDoorHostTests
    {
        private const string FixtureDir = "game/tests/Rootborn.World.Tests/Fixtures/buildings";

        [Test]
        public void BUILDING_DOOR_001_WalkingIntoTheBuildingDoorShowsItsFramesFromClosedToOpenThenArrivesInside()
        {
            string root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--world", "Yard", "--size", "1280x720",
                "--maps", Path.Combine(root, FixtureDir, "maps.json"),
                "--buildings", Path.Combine(root, FixtureDir, "buildings.json"),
                "--evidence", Path.Combine(root, "output", "building-door-evidence"),
            });
            options.TestMode = true;
            using var player = new PlayerDriver(options);
            player.Frames(3);
            Assert.That(player.World.Map.Id, Is.EqualTo("Yard"));
            LoadedMap yard = player.Game.World.Current;
            Assert.That(yard.LotProblems, Is.Empty, string.Join("\n", yard.LotProblems));
            int last = yard.DoorArt.Single().Frames.Count - 1;
            Assert.That(player.Game.World.DoorFrameShown, Is.EqualTo(-1), "닫혀 있을 때는 프레임을 그리지 않는다(건물 그림의 닫힌 문)");

            var shown = new List<int>();
            bool midShot = false;
            int blockedFrames = 0;
            player.KeyDown(TestKey.Up);
            for (int i = 0; i < 240 && player.World.Map.Id == "Yard"; i++)
            {
                player.Frames(1);
                if (player.World.Map.Collision.AnyBlocked(player.World.Walker.Box)) blockedFrames++;
                int frame = player.Game.World.DoorFrameShown;
                if (frame < 0) continue;
                shown.Add(frame);
                Assert.That(player.Game.World.Feel.Transition.Kind, Is.EqualTo(Rootborn.World.WorldTransitionKind.Door));
                if (!midShot && frame == 1) { player.Screenshot("01-door-opening"); midShot = true; }
            }
            player.KeyUp(TestKey.Up);

            Assert.That(player.World.Map.Id, Is.EqualTo("Inside"), "문을 지나 실내의 도착 지점으로");
            Assert.That(shown, Is.Not.Empty, "열리는 동안 문 프레임을 그렸다");
            Assert.That(shown.First(), Is.EqualTo(0), "닫힘부터");
            Assert.That(shown, Is.Ordered, "열림 쪽으로만 간다");
            Assert.That(shown.Last(), Is.EqualTo(last), "넘어가기 전에 마지막(열림) 프레임");
            Assert.That(shown.Distinct().Count(), Is.EqualTo(last + 1), "모든 프레임을 지난다");
            Assert.That(player.Game.World.DoorFrameShown, Is.EqualTo(-1));
            Assert.That(blockedFrames, Is.EqualTo(0), "몸이 건물의 막힌 칸에 들어가지 않았다");
        }
    }
}
