using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Core.Time;
using Rootborn.Data.Save;
using Rootborn.Screens;
using Rootborn.Screens.Day;
using Rootborn.Screens.Hud;
using Rootborn.Ui;

namespace Rootborn.Host.Tests.TwoGames
{
    /// <summary>같이 자기와 하는 중(흐르는 시간 설계 3.2·3.3절)을 두 게임의 입력으로. 근거와 준비는 <see cref="TwoGamesHostTests"/> 머리말.</summary>
    public sealed partial class TwoGamesHostTests
    {
        /// <summary>
        /// 방장이 잠자리에서 E로 자면 "자는 중"과 깨어 있는 손님(캐릭터 이름으로)이 보이고 다음 날 단추가 꺼져 있다. 손님도 잠자리에서 E로 자면 곧바로 둘 다 다음 날 07:00 —
        /// 둘 다 단추를 눌러 2일째를 연다. 준비: 이야기가 있는 슬롯 파일(양쪽).
        /// </summary>
        [Test]
        public void TWO_GAMES_005_HostSleeps_SeesTheGuestAwake_GuestSleepsToo_BothWakeAtSevenNextDay()
        {
            WriteStory(_hostSaves, "slot-1", "방장", 101);
            WriteStory(_guestSaves, "slot-1", "새벽", 202); // 기본 말("손님")과 다른 이름이라야 이름이 보이는지 가린다
            _host = LaunchHost();
            HostOpensFromStartMenu(null);
            _guest = StartGuest();
            GuestJoinsByAddress(_host.Game.Hosting.ListenAddress, null);
            string me = _host.Game.Room.PlayerId, guestId = GuestId;
            var clock = _host.Game.Hosting.Room.Server.Clock;

            DayResultScreen result = _host.GoToBed();
            Assert.That(_guest.Until(() => result.Model.Sleeping && result.Model.AwakeOthers.SequenceEqual(new[] { guestId }) && clock.IsAsleep(me)), Is.True,
                "자는 중 — 손님은 깨어 있다(방이 내 잠을 셌다)");
            string sleeping = result.Body.Sections[0];
            Assert.That(sleeping, Does.Contain("새벽"), "깨어 있는 사람은 캐릭터 이름으로 보인다");
            Assert.That(sleeping, Does.Not.Contain(guestId), "방의 신원 id를 보이지 않는다");
            Assert.That(_host.Game.Hosting.Room.Server.Players.Single(p => p.PlayerId == guestId).Name, Is.EqualTo("새벽"), "방이 받은 이름");
            var next = (UiButton)_host.Node(DayResultScreen.NextDayId);
            Assert.That(next.Enabled, Is.False, "자면 07:00까지 깨어날 수 없다");
            // 방장의 잠은 UDP로 따로 손님에게 간다 — 도착을 기다린 뒤에 본다(도착 전에는 방장이 아직 깨어 있는 것으로 보인다).
            Assert.That(_guest.Until(() => _guest.Query("awake-others").Length == 0), Is.True, "손님 쪽: 깨어 있는 다른 사람 없음(손님 자신은 세지 않는다)");
            _host.MovePointer(-50, -50);
            TestContext.WriteLine("스크린샷: " + _host.ScreenshotScreen(ScreenCatalog.DayResult, "two-05-host-sleeping-guest-awake"));
            TestContext.WriteLine("스크린샷(손님): " + _guest.Screenshot("two-05-guest-awake-while-host-sleeps"));

            _guest.Call("go-to-bed");
            Assert.That(_guest.Until(() => next.Enabled && _guest.QueryBool("can-start-next-day")), Is.True, "모두 잠들면 곧바로 다음 날");
            Assert.That(clock.Day, Is.EqualTo(2));
            Assert.That(TimeRules.ClockMinutes(clock.Minutes) / 60, Is.EqualTo(7), "07:00");
            TestContext.WriteLine("스크린샷(손님): " + _guest.ScreenshotScreen(ScreenCatalog.DayResult, "two-05-guest-both-asleep-next-day"));
            _host.PressButton(DayResultScreen.NextDayId);
            _host.WaitForScreenClosed(ScreenCatalog.DayResult);
            _guest.PressButton(DayResultScreen.NextDayId);
            _guest.WaitForScreenClosed(ScreenCatalog.DayResult);
            Assert.That(_host.Game.Session.Student.CurrentDay, Is.EqualTo(2));
            Assert.That(_guest.QueryInt("day"), Is.EqualTo(2));
            Assert.That(((UiLabel)_host.Node(ClockHudScreen.LabelId)).Text, Does.StartWith("2일  07:"));
            Assert.That(_guest.Query("text", ClockHudScreen.LabelId), Does.StartWith("2일  07:"));
            Assert.That(_guest.QueryBool("slept-last-night"), Is.True);
        }

        /// <summary>
        /// 방장만 자고 손님은 깨어 있다가 07:00이 되면 둘 다 다음 날이다. 안 잔 손님은 잔 밤이 없다(회복 없음).
        /// 준비: 방의 시계 파일(1일째 06:30 — 방은 실제 시간으로 흐르고 하루가 실제 600초라 밤을 기다리지 않으려고 이어 여는 방의 시각을 늦은 밤으로 둔다)과
        /// 이야기가 있는 슬롯 파일. 방을 연 뒤의 진행은 입력뿐이다.
        /// </summary>
        [Test]
        public void TWO_GAMES_006_OnlyTheHostSleeps_AtSevenBothAreInTheNextDay_TheAwakeGuestDidNotSleep()
        {
            WriteStory(_hostSaves, "slot-1", "방장", 101);
            WriteStory(_guestSaves, "slot-1", "손님", 202);
            new FileSaveStore(_hostSaves).Write("slot-1", SaveFileNames.RoomClock, new RoomClockSaveData { Version = RoomClockSaveData.KnownVersion, Day = 1, Minutes = 24 * 60 + 6 * 60 + 30 }); // 그날 0시부터 센 분: 다음 날 새벽 06:30
            _host = LaunchHost();
            HostOpensFromStartMenu(null);
            var clock = _host.Game.Hosting.Room.Server.Clock;
            TestContext.WriteLine("[방 시계] " + clock.Day + "일 " + TimeRules.ClockMinutes(clock.Minutes) / 60 + ":" + TimeRules.ClockMinutes(clock.Minutes) % 60);
            Assert.That(clock.Day, Is.EqualTo(1));
            _guest = StartGuest();
            GuestJoinsByAddress(_host.Game.Hosting.ListenAddress, null);
            int guestEnergy = _guest.QueryInt("energy");

            DayResultScreen result = _host.GoToBed();
            Assert.That(_guest.Until(() => result.Model.Sleeping && clock.IsAsleep(_host.Game.Room.PlayerId)), Is.True, "방장만 잔다");
            TestContext.WriteLine("스크린샷: " + _host.ScreenshotScreen(ScreenCatalog.DayResult, "two-06-host-sleeping-alone"));

            // 07:00까지 두 게임을 같이 돌린다. 방의 시계는 실제 시간으로 흐른다(게임 30분 ≈ 실제 12.5초) — 프레임 한도는 멈췄을 때 끝내는 안전장치라 넉넉히.
            Assert.That(_guest.Until(() => clock.Day == 2 && _guest.QueryInt("day") == 2, 200000), Is.True,
                "07:00이 되면 손님도 다음 날: 방 " + clock.Day + "일, 손님 " + _guest.Query("day") + "일");
            Assert.That(_guest.Until(() => ((UiButton)_host.Node(DayResultScreen.NextDayId)).Enabled), Is.True);
            _host.PressButton(DayResultScreen.NextDayId);
            _host.WaitForScreenClosed(ScreenCatalog.DayResult);
            Assert.That(_host.Game.Session.Student.CurrentDay, Is.EqualTo(2));
            Assert.That(_host.Game.Session.Student.LastNight.HasSlept, Is.True, "방장은 잤다");
            Assert.That(_guest.QueryBool("slept-last-night"), Is.False, "손님은 자지 않고 넘겼다");
            TestContext.WriteLine("[07:00 뒤] 손님 체력 " + guestEnergy + " → " + _guest.Query("energy") + ", 지침 " + _guest.Query("exhausted")
                + " / 방장 체력 " + _host.Game.Session.Student.Energy + ", 지침 " + _host.Game.Session.IsExhausted + ", 손님 화면 " + _guest.Query("screens"));
            Assert.That(_guest.QueryInt("energy"), Is.LessThanOrEqualTo(guestEnergy), "안 잔 쪽은 회복이 없다");
            TestContext.WriteLine("스크린샷(손님): " + _guest.Screenshot("two-06-guest-next-day-without-sleep"));
            TestContext.WriteLine("스크린샷: " + _host.Screenshot("two-06-host-next-day"));
        }

        /// <summary>
        /// 같이 할 때 손님이 알바를 하면 끝나는 시각까지 "하는 중"(HUD)이고, 그동안 다른 알바 자리에서 E를 누르면 거절 안내가 보인다.
        /// 방장은 그동안 알바를 할 수 있다. 준비: 이야기가 있는 슬롯 파일(양쪽).
        /// </summary>
        [Test]
        public void TWO_GAMES_007_GuestIsBusyAfterAShift_AnotherActivityIsRefusedWithANotice_TheHostIsNotBusy()
        {
            WriteStory(_hostSaves, "slot-1", "방장", 101);
            WriteStory(_guestSaves, "slot-1", "손님", 202);
            _host = LaunchHost();
            HostOpensFromStartMenu(null);
            _guest = StartGuest();
            GuestJoinsByAddress(_host.Game.Hosting.ListenAddress, null);

            _guest.GoTo("Town");
            Assert.That(_guest.Call("work-shift", ShopWork), Is.EqualTo("worked"));
            Assert.That(_guest.QueryBool("busy"), Is.True, "같이 할 때 알바는 시계를 건너뛰지 않고 하는 중");
            int ends = _guest.QueryInt("busy-ends");
            string busyText = _guest.Query("ui-text", "ui.hud.busy-until", (ends / 60).ToString(), (ends % 60).ToString());
            Assert.That(_guest.Query("text", ClockHudScreen.RoomLabelId), Does.Contain(busyText), "손님 HUD에 하는 중과 끝나는 시각");
            TestContext.WriteLine("스크린샷(손님): " + _guest.Screenshot("two-07-guest-busy-hud"));

            Assert.That(_guest.Call("work-shift", "work.clinic-helper"), Is.EqualTo("refused"), "하는 중에는 다른 알바가 열리지 않는다");
            Assert.That(_guest.Query("refusal-key"), Is.EqualTo("ui.prompt.busy"), "거절 안내: 하는 중");
            TestContext.WriteLine("스크린샷(손님): " + _guest.Screenshot("two-07-guest-busy-refused"));

            Assert.That(_host.Game.Session.IsBusy, Is.False, "방장은 하는 중이 아니다");
            _host.GoTo("Town");
            Assert.That(_host.WorkShift(ShopWork), Is.True, "손님이 하는 중이어도 방장은 알바를 한다");
            Assert.That(_guest.QueryBool("busy"), Is.True);
        }
    }
}
