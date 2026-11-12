using System.Linq;
using NUnit.Framework;
using Rootborn.Core.World;

namespace Rootborn.Host.Tests
{
    /// <summary>마을 Level_0 밖의 길을 실제 창과 게임 루프로 걷는다(장치 이벤트 주입, OS 입력 계층 미확인).</summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class MissingPathScreenTests
    {
        private static PlayerDriver Start(params string[] args)
        {
            HostOptions options = HostOptions.Resolve(args.Concat(new[] { "--size", "1280x720" }).ToArray());
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        private static void AssertAt(PlayerDriver player, string mapId, string spawnId)
        {
            Assert.That(player.World.Map.Id, Is.EqualTo(mapId));
            Assert.That(player.World.Map.TryGetSpawn(spawnId, out var spawn), Is.True);
            Assert.That((player.World.Walker.X, player.World.Walker.Y), Is.EqualTo((spawn.X, spawn.Y)));
        }

        [Test]
        public void PATH_PLAY_001_WalkOffTheTownBottomToTheBeachAndBack()
        {
            using PlayerDriver player = Start("--world", "Town", "--spawn", "town-bottom-entry");
            Assert.That(player.WalkThroughDoor("beach"), Is.EqualTo("Beach"));
            AssertAt(player, "Beach", "beach-top-entry");
            player.Screenshot("path-01-beach");
            Assert.That(player.WalkThroughDoor("town"), Is.EqualTo("Town"));
            AssertAt(player, "Town", "town-bottom-entry");
            Assert.That(player.CollisionViolations, Is.EqualTo(0));
            player.Screenshot("path-02-town-bottom");
        }

        [Test]
        public void PATH_PLAY_002_SchoolOpensWithTheTeacherInTheMorning()
        {
            using PlayerDriver player = Start("--world", "School", "--hour", "9", "--time-scale", "0");
            AssertAt(player, "School", "school-entry");
            Assert.That(Enumerable.Range(0, player.Game.World.Npcs.Count).Select(i => player.Game.World.Npcs[i].NpcId), Is.EqualTo(new[] { "npc.teacher" }));
            player.Screenshot("path-03-school-morning");
        }
    }
}
