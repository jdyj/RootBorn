using System;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Core.World;
using Rootborn.Game;
using Rootborn.Screens;
using Rootborn.Screens.Day;
using Rootborn.Screens.Hud;
using Rootborn.Ui;
using Rootborn.World;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 흐르는 시간(설계 2026-11-12-flowing-time-design.md 6절 3단계)을 실제 창과 게임 루프로: 07:00 시작, 프레임마다 시계가 흐르고 패널이 열리면 멈춤,
    /// 취침 시각 경고, 잠자리에서 자기, 밤샘 뒤 알림, 지침 표시, 일과 계획판의 지금 칸, 낮밤 빛, 이어 하기.
    /// 입력은 장치 이벤트 주입(OS 입력 계층 미확인). 시간은 테스트 모드의 고정 프레임(1/60초) × 실행 인자 --time-scale로만 흐른다(실시간을 기다리지 않는다).
    /// 지침을 만드는 준비만 GameSession 명령(이웃 돕기 되풀이)으로 한다 — 보고에 "준비"로 적는다.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class FlowingTimeHostTests
    {
        private string _saves;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-flow-time-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        /// <summary>슬롯 하나로 띄운다. scale은 학생 시계 배속(--time-scale): 하루 600초 ÷ 배속 = 하루의 실제 초.</summary>
        private PlayerDriver Start(float scale)
        {
            HostOptions options = HostOptions.Resolve(new[] { "--save-root", _saves, "--slot", "slot-1", "--size", "1280x720",
                "--time-scale", scale.ToString(CultureInfo.InvariantCulture) });
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        private static string ClockText(PlayerDriver p) => ((UiLabel)p.Node(ClockHudScreen.LabelId)).Text;
        private static string AlertText(PlayerDriver p) => ((UiLabel)p.Node(ClockHudScreen.RoomLabelId)).Text;
        private static ClockModel Clock(PlayerDriver p) => ((ClockHudScreen)p.Screens.Find(ScreenCatalog.HudClock)).Model;

        /// <summary>게임 시각(그날 0시부터 센 분)이 minutes가 될 때까지 프레임을 보낸다. 날이 바뀌어도 멈춘다.</summary>
        private static void RunUntil(PlayerDriver p, int minutes, int maxFrames = 20000)
        {
            GameSession s = p.Game.Session;
            int day = s.StudentDay;
            for (int i = 0; i < maxFrames && s.StudentTimeMinutes < minutes && s.StudentDay == day; i++) p.Frames(1);
            Assert.That(s.StudentTimeMinutes >= minutes || s.StudentDay != day, Is.True, "시계가 " + minutes + "분까지 가지 않았다: " + s.StudentTimeMinutes);
        }

        private static void WalkToDayEnd(PlayerDriver p)
        {
            InteractionThing bed = p.Game.World.HereData.Things.FirstOrDefault(t => t.Kind == DayResultScreen.InteractionKind);
            Assert.That(bed, Is.Not.Null, p.World.Map.Id + "에 잠자리(하루 마감 지점)가 없다");
            p.WalkNear(bed.X, bed.Y, p.World.Map.Grid.CellSize * 1.25f);
            var target = p.Game.World.Interactions.Target;
            Assert.That((target?.Kind, target?.Id), Is.EqualTo((bed.Kind, bed.Id)), "잠자리 앞에서 E의 대상");
        }

        /// <summary>준비(플레이어 경로가 아니다): 이웃 돕기(체력 1)를 되풀이해 지치게 한다.</summary>
        private static void ExhaustByCommands(GameSession s)
        {
            for (int n = 0; !s.IsExhausted && n < 40; n++) s.PerformOutsideSchoolActivity("outside.help-neighbor", "flow-host-exhaust:" + n);
            Assert.That(s.IsExhausted, Is.True, "준비: 지침");
        }

        [Test]
        public void FLOW_HOST_001_NewGameStartsAtSeven_TheClockRuns_AndStopsWhileAPanelIsOpen()
        {
            using PlayerDriver p = Start(10f); // 게임 1시간 = 2.5초 = 150프레임
            Assert.That(ClockText(p), Does.StartWith("1일  07:0"), "하루는 07:00에 시작한다(사용자 결정 2026-11-12) — 띄운 뒤 3프레임은 흘렀다");
            Assert.That(p.Game.Session.StudentTimeMinutes, Is.LessThan(7 * 60 + 2));
            Assert.That(AlertText(p), Is.Empty, "혼자 하기에는 시간 포인트 줄이 없다");
            p.Screenshot("flow-01-new-game-0700");

            int start = p.Game.Session.StudentTimeMinutes;
            p.Frames(150);
            Assert.That(p.Game.Session.StudentTimeMinutes - start, Is.InRange(59, 61), "게임 루프가 프레임마다 시계를 돌린다(150프레임 = 게임 1시간)");
            Assert.That(ClockText(p), Does.StartWith("1일  08:0"), "HUD가 따라온다");

            p.OpenScreenWithKey(TestKey.I, ScreenCatalog.Inventory);
            int paused = p.Game.Session.StudentTimeMinutes;
            p.Frames(300);
            Assert.That(p.Game.Session.StudentTimeMinutes, Is.EqualTo(paused), "혼자 하기에서 패널이 열려 있으면 멈춘다");
            p.CloseScreenWithKey(TestKey.I, ScreenCatalog.Inventory);
            p.Frames(150);
            Assert.That(p.Game.Session.StudentTimeMinutes, Is.GreaterThanOrEqualTo(paused + 59), "닫으면 다시 흐른다");

            // 움직이는 동안에도 흐른다(걷기는 시계를 멈추지 않는다).
            int walking = p.Game.Session.StudentTimeMinutes;
            p.WalkAway(3);
            Assert.That(p.Game.Session.StudentTimeMinutes, Is.GreaterThan(walking));
        }

        [Test]
        public void FLOW_HOST_002_BedtimeWarning_ThenSleepingAtTheBed_WakesTheNextDayAtSeven()
        {
            using PlayerDriver p = Start(60f); // 게임 1시간 = 25프레임
            int bedtime = p.Game.Session.TimeRules.BedtimeMinutes;
            WalkToDayEnd(p); // 먼저 잠자리 앞에 가 서 있는다(걷는 동안에도 시간이 흐른다)
            RunUntil(p, bedtime - 30);
            Assert.That(Clock(p).Alert, Is.EqualTo(ClockAlert.BedtimeSoon));
            Assert.That(AlertText(p), Is.EqualTo(p.Game.Text.Get("ui.hud.bedtime-soon")));
            Assert.That(((Rootborn.Ui.UiImage)p.Node(ClockHudScreen.IconId)).Image, Is.EqualTo(ClockHudScreen.BedtimeIcon));
            p.Screenshot("flow-02-near-bedtime");

            Assert.That(((InteractionPromptScreen)p.Screens.Find(ScreenCatalog.HudPrompt)).Text, Is.EqualTo("[E] 자기"));
            p.PressKey(TestKey.E);
            p.WaitForScreen(ScreenCatalog.DayResult);
            Assert.That(p.Game.Session.Student.LastNight.HasSlept, Is.True);
            p.PressButton(DayResultScreen.NextDayId);
            p.WaitForScreenClosed(ScreenCatalog.DayResult);
            Assert.That(ClockText(p), Does.StartWith("2일  07:0"), "자면 다음 날 07:00");
            Assert.That(p.Game.Session.Student.Energy, Is.EqualTo(12), "취침 시각 전에 자면 체력이 전부 돌아온다");
            Assert.That(p.Screens.IsOpen(ScreenCatalog.StayedUpNotice), Is.False);
        }

        [Test]
        public void FLOW_HOST_003_StayingUpPastSeven_OpensTheNextDayWithANotice_AndNoRest()
        {
            using PlayerDriver p = Start(60f); // 하루 = 10초 = 600프레임
            int energy = 0;
            for (int i = 0; i < 800 && p.Game.Session.StudentDay == 1; i++)
            {
                energy = p.Game.Session.Student.Energy;
                p.Frames(1);
            }
            Assert.That(p.Game.Session.StudentDay, Is.EqualTo(2), "자지 않아도 07:00에 날이 넘어간다");
            Assert.That(p.Screens.IsOpen(ScreenCatalog.DayResult), Is.False, "하루 결과 화면 없이");
            Assert.That(p.Screens.IsOpen(ScreenCatalog.StayedUpNotice), Is.True, "무슨 일이 났는지 알린다");
            Assert.That(p.Game.Session.Student.Energy, Is.LessThanOrEqualTo(energy), "회복이 없다");
            Assert.That(p.Game.Session.Student.LastNight.HasSlept, Is.False);
            p.Screenshot("flow-04-morning-after-staying-up");
            p.Frames((int)(StayedUpNoticeScreen.ShowSeconds * 60) + 5);
            Assert.That(p.Screens.IsOpen(ScreenCatalog.StayedUpNotice), Is.False, "알림은 저절로 닫힌다");
        }

        [Test]
        public void FLOW_HOST_004_Exhausted_TheHudSaysSo_AndItIsStillExhaustedAfterContinue()
        {
            using (PlayerDriver p = Start(0f)) // 시계가 서 있다(배속 0)
            {
                ExhaustByCommands(p.Game.Session);
                p.Frames(2);
                Assert.That(Clock(p).Alert, Is.EqualTo(ClockAlert.Exhausted));
                Assert.That(AlertText(p), Is.EqualTo(p.Game.Text.Get("ui.hud.exhausted")));
                Assert.That(((Rootborn.Ui.UiImage)p.Node(ClockHudScreen.IconId)).Image, Is.EqualTo(ClockHudScreen.ExhaustedIcon));
                p.Screenshot("flow-03-exhausted");
            }
            using (PlayerDriver again = Start(0f))
            {
                Assert.That(again.Game.Session.IsExhausted, Is.True, "지친 채 저장하고 다시 읽어도 지쳐 있다");
                Assert.That(Clock(again).Alert, Is.EqualTo(ClockAlert.Exhausted));
            }
        }

        [Test]
        public void FLOW_HOST_005_Continue_ResumesAtTheLastSavedHour_TheMinutesSinceThenAreLost()
        {
            int savedHour;
            using (PlayerDriver p = Start(10f))
            {
                RunUntil(p, 9 * 60 + 30); // 09:30 — 저장은 게임 1시간마다(09:00)
                savedHour = 9;
                Assert.That(ClockText(p), Does.Contain("09:3"));
            }
            using (PlayerDriver again = Start(0f))
            {
                Assert.That(again.Game.Session.StudentTimeMinutes / 60, Is.EqualTo(savedHour), "그 시각에서 이어진다");
                Assert.That(again.Game.Session.StudentTimeMinutes % 60, Is.LessThan(30), "마지막 저장(정시) 뒤의 분은 잃는다 — 게임 1시간마다 저장");
            }
        }

        [Test]
        public void FLOW_HOST_006_DailyPlanBoard_MarksTheSlotOfTheCurrentTimeSlot()
        {
            using PlayerDriver p = Start(60f);
            GameSession s = p.Game.Session;
            for (int i = 0; i < 2000 && s.CurrentDailyPlanSlotIndex < 0; i++) p.Frames(1);
            int now = s.CurrentDailyPlanSlotIndex;
            Assert.That(now, Is.GreaterThanOrEqualTo(0), "그날의 어느 시간대에는 일과 칸이 있다");
            InteractionThing board = p.Game.World.HereData.Things.FirstOrDefault(t => t.Kind == DailyPlanScreen.InteractionKind);
            Assert.That(board, Is.Not.Null, "집에 일과 계획판이 있다");
            p.WalkNear(board.X, board.Y, p.World.Map.Grid.CellSize * 1.25f);
            p.PressKey(TestKey.E);
            p.WaitForScreen(ScreenCatalog.DailyPlan);
            var screen = (DailyPlanScreen)p.Screens.Find(ScreenCatalog.DailyPlan);
            Assert.That(screen.Model.Slots[s.CurrentDailyPlanSlotIndex].Now, Is.True, "지금 시간대의 칸");
            Assert.That(screen.Model.SelectedSlot, Is.EqualTo(s.CurrentDailyPlanSlotIndex), "지금 칸을 먼저 고른다");
            Assert.That(p.Screens.Tree.Root.Find("daily-plan.skip"), Is.Null, "넘기기는 없다(흐르는 시간 4단계)");
            int paused = s.StudentTimeMinutes;
            p.Frames(100);
            Assert.That(s.StudentTimeMinutes, Is.EqualTo(paused), "일과 계획판(패널)이 열려 있으면 시계가 멈춘다");
            p.ScreenshotScreen(ScreenCatalog.DailyPlan, "flow-05-daily-plan");
        }

        [Test]
        public void FLOW_HOST_007_TownLight_FollowsTheFlowingClock_MorningDayEveningNight()
        {
            using PlayerDriver p = Start(20f); // 게임 1시간 = 75프레임
            p.WalkThroughDoor("town");
            Assert.That(p.Game.World.Current.Entry.DayNight, Is.True, "준비: 바깥 맵");
            GameSession s = p.Game.Session;
            (int Minutes, string Name)[] moments = { (s.StudentTimeMinutes, "morning"), (13 * 60, "day"), (18 * 60 + 30, "evening"), (22 * 60, "night") };
            foreach ((int minutes, string name) in moments)
            {
                RunUntil(p, minutes);
                p.Frames(60); // 빛이 따라오는 시간(1초 — 게임 20분)
                string slot = Rootborn.Core.StudentLife.LocationStateTimeSlots.FromMinutes(s.ClockMinutes);
                Assert.That(p.Game.World.TimeSlot, Is.EqualTo(slot), "NPC 자리·장소 상태가 학생 시계 하나를 본다(" + name + ")");
                Assert.That(p.Game.World.LightTimeSlot, Is.EqualTo(slot), "낮밤 빛도 같은 시각을 본다(혼자 하기 — " + name + ")");
                p.Screenshot("flow-06-town-" + name);
            }
            Assert.That(p.Game.World.Tint.R, Is.LessThan(0.5f), "밤은 어둡다");
        }
    }
}
