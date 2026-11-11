using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Screens;
using Rootborn.Screens.Day;
using Rootborn.Screens.Hud;
using Rootborn.Ui;
using Rootborn.World;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 낮밤 빛은 HUD 시계가 보이는 시각을 따른다(혼자 하기에서는 흐르는 시계 — NPC 자리·장소 상태의 개인 시각과 같다. 같이 하는 방은 때의 대표 시각).
    /// 장치 이벤트 주입(OS 입력 계층 미확인): 걷기·문·상호작용·단추 누르기는 PlayerDriver로.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class DayNightRoomTimeHostTests
    {
        private string _saves;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-room-light-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            _root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        private PlayerDriver Start(string timeScale = "1")
        {
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--save-root", _saves, "--slot", "slot-1", "--size", "1280x720",
                "--evidence", Path.Combine(_root, "output", "host-room-light-evidence"), "--time-scale", timeScale,
            });
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        /// <summary>HUD 시계 글자("1일  14:00 · …")의 시각이 가리키는 때.</summary>
        private static string HudSlot(PlayerDriver player)
        {
            string text = ((UiLabel)player.Node(ClockHudScreen.LabelId)).Text;
            int colon = text.IndexOf(':');
            Assert.That(colon, Is.GreaterThan(1), "HUD 시계 글자: " + text);
            int hour = int.Parse(text.Substring(colon - 2, 2).Trim()), minute = int.Parse(text.Substring(colon + 1, 2));
            return LocationStateTimeSlots.FromMinutes(hour * 60 + minute);
        }

        private static LightColor Ambient(PlayerDriver player, string slot)
        {
            Assert.That(player.Game.World.LightingData.TryGetAmbient(slot, out LightColor tint), Is.True, slot);
            return tint;
        }

        private static void AssertTint(PlayerDriver player, LightColor expected, string why)
        {
            LightColor t = player.Game.World.Tint;
            Assert.That(new[] { t.R, t.G, t.B }, Is.EqualTo(new[] { expected.R, expected.G, expected.B }).Within(0.02f), why + ": 지금 " + t);
        }

        [Test]
        public void ROOM_LIGHT_001_NewGame_HudAndBrightnessShowTheSamePartOfDay_NpcTimeStaysPersonal()
        {
            using PlayerDriver player = Start();
            string hud = HudSlot(player);
            Assert.That(player.Game.World.LightTimeSlot, Is.EqualTo(hud), "새 게임 첫 화면: 밝기는 HUD의 때를 따른다");
            // NPC 자리·상호작용의 시간대는 지금처럼 개인 시각(분)이다.
            Assert.That(player.Game.World.TimeSlot, Is.EqualTo(LocationStateTimeSlots.FromMinutes(player.Game.Session.Student.TimeMinutes)));

            Assert.That(player.WalkThroughDoor("town"), Is.EqualTo("Town"));
            player.Frames(2);
            AssertTint(player, Ambient(player, HudSlot(player)), "마을에 나오면 HUD의 때(" + HudSlot(player) + ")의 밝기");
            player.Screenshot("room-light-01-town-first-part");
        }

        [Test]
        public void ROOM_LIGHT_002_TheClockFlowsIntoTheNextPart_HudAndBrightnessChangeTogether_IndoorsStaysWhite()
        {
            // 혼자 하기에는 넘기기가 없다(흐르는 시간 — 시계가 흐른다). 배속 60(게임 1시간 = 25프레임)으로 오전에서 오후로 흘러가게 둔다.
            using PlayerDriver player = Start("60");
            string first = HudSlot(player);
            for (int i = 0; i < 400 && HudSlot(player) == first; i++) player.Frames(1);

            string next = HudSlot(player);
            Assert.That(next, Is.Not.EqualTo(first), "HUD의 때가 넘어갔다");
            Assert.That(player.Game.World.LightTimeSlot, Is.EqualTo(next), "밝기도 같은 때로");
            AssertTint(player, LightColor.White, "실내는 그대로 흰 빛");
            player.Screenshot("room-light-02-house-after-skip");

            Assert.That(player.WalkThroughDoor("town"), Is.EqualTo("Town"));
            player.Frames(2);
            string now = HudSlot(player); // 문까지 걷는 동안에도 시계가 흐른다
            Assert.That(player.Game.World.LightTimeSlot, Is.EqualTo(now));
            AssertTint(player, Ambient(player, now), "마을은 지금 때(" + now + ")의 밝기");
            player.Screenshot("room-light-03-town-after-skip");
        }
    }
}
