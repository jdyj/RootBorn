using System.Linq;
using NUnit.Framework;
using Rootborn.Screens.Hud;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Hud
{
    public sealed class ClockVitalsHudTests
    {
        [Test]
        public void OpenHud_ShowsClockAndVitals_InTheTopRowPositions()
        {
            using var t = new TestSession();
            t.Stack.OpenHud();
            Assert.That(t.Stack.IsOpen(ScreenCatalog.HudClock) && t.Stack.IsOpen(ScreenCatalog.HudVitals), Is.True);
            Assert.That(t.Stack.MovementBlocked, Is.False, "HUD는 이동을 막지 않는다");
            // Unity UrbanGameplayHudLayout: 시계 오른쪽 끝에서 936 들어온 420x144, 체력 500, 집중 344(140x144), 위 36.
            Assert.That(t.Node(ClockHudScreen.PanelId).Rect, Is.EqualTo(new UiRect(1920 - 936, 36, 420, 144)));
            Assert.That(t.Node("hud.energy").Rect, Is.EqualTo(new UiRect(1920 - 500, 36, 140, 144)));
            Assert.That(t.Node("hud.focus").Rect, Is.EqualTo(new UiRect(1920 - 344, 36, 140, 144)));
        }

        [Test]
        public void NewGame_ShowsDayOneEightOClock_AndTheStartingVitals()
        {
            using var t = new TestSession();
            t.Stack.OpenHud();
            var clock = t.Screen<ClockHudScreen>(ScreenCatalog.HudClock);
            Assert.That((clock.Model.Day, clock.Model.Hour, clock.Model.Minute), Is.EqualTo((1, 8, 0)));
            Assert.That(clock.Model.Allowance, Is.EqualTo(0), "가게 화폐를 알고 가방에 없다");
            Assert.That(((UiLabel)t.Node(ClockHudScreen.LabelId)).Text, Is.EqualTo("1일  08:00 · 용돈 0"));

            var vitals = t.Screen<VitalsHudScreen>(ScreenCatalog.HudVitals);
            Assert.That((vitals.Model.Energy, vitals.Model.Focus), Is.EqualTo((t.Session.Student.Energy, t.Session.Student.Focus)));
            Assert.That(((UiLabel)t.Node(VitalsHudScreen.ValueId("hud.energy"))).Text, Is.EqualTo(t.Session.Student.Energy.ToString()));
        }

        [Test]
        public void Command_UpdatesTimeAndVitals_OnlyThroughTheChangeNotice()
        {
            using var t = new TestSession();
            t.Stack.OpenHud();
            int energy = t.Session.Student.Energy;
            // 게임 명령을 감싸개 밖에서 부르면 HUD는 모른다(매 프레임 읽지 않는다).
            t.Session.PerformLocationActivity("location.activity.school-class", "req-direct");
            Assert.That(t.Screen<VitalsHudScreen>(ScreenCatalog.HudVitals).Model.Energy, Is.EqualTo(energy));
            // 감싸개로 부르면 알림을 받아 고친다.
            t.Commands.Run(s => s.PerformLocationActivity("location.activity.library-hard-book", "req-1"), ChangeTopics.Student);
            Assert.That(t.Screen<VitalsHudScreen>(ScreenCatalog.HudVitals).Model.Energy, Is.EqualTo(t.Session.Student.Energy));
            Assert.That(t.Session.Student.Energy, Is.LessThan(energy));
            var clock = t.Screen<ClockHudScreen>(ScreenCatalog.HudClock);
            Assert.That(clock.Model.Hour * 60 + clock.Model.Minute, Is.EqualTo(t.Session.Student.TimeMinutes % 1440));
            Assert.That(clock.Model.Hour * 60 + clock.Model.Minute, Is.GreaterThan(8 * 60));
        }

        [Test]
        public void WithoutAGame_ClockSaysLoading_AndVitalsAreHidden()
        {
            var stack = new ScreenStack(ScreenCatalog.Create(), new ScreenServices(TestData.Text(), null));
            stack.Layout(1920, 1080);
            stack.OpenHud();
            Assert.That(((UiLabel)stack.Tree.Root.Find(ClockHudScreen.LabelId)).Text, Is.EqualTo("시간표를 불러오는 중…"));
            Assert.That(stack.Tree.Root.Find("hud.energy").Visible, Is.False);
        }

        [Test]
        public void English_RedrawsTheText_AndOnlyKnownKeysAreMissing()
        {
            using var t = new TestSession();
            t.Stack.OpenHud();
            t.Text.Language = "en";
            Assert.That(((UiLabel)t.Node(ClockHudScreen.LabelId)).Text, Is.EqualTo("Day 1  08:00 · Allowance 0"));
            t.Emit();
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text,
                t.Stack.Find(ScreenCatalog.HudClock).TextKeys.Concat(t.Stack.Find(ScreenCatalog.HudVitals).TextKeys));
        }
    }
}
