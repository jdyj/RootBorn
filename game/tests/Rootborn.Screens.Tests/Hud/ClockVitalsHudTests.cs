using System.Linq;
using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;
using Rootborn.Game;
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
        public void NewGame_ShowsDayOneAndTheFirstPartsTime_TimePoints_AndTheStartingVitals()
        {
            using var t = new TestSession();
            t.Stack.OpenHud();
            var clock = t.Screen<ClockHudScreen>(ScreenCatalog.HudClock);
            // 시각 숫자는 방의 시계의 때 대표 시각(멀티플레이 설계 2.1절). 1일째는 평일(때 2개: 방과 후·저녁) — 하루의 첫 때는 07:00(사용자 결정 2026-11-12).
            Assert.That((clock.Model.Day, clock.Model.Hour * 60 + clock.Model.Minute), Is.EqualTo((1, t.Session.RoomTimeMinutes)));
            Assert.That((clock.Model.Hour, clock.Model.Minute), Is.EqualTo((7, 0)));
            Assert.That((clock.Model.TimePointsRemaining, clock.Model.TimePointsToday), Is.EqualTo((2, 2)));
            Assert.That(clock.Model.Allowance, Is.EqualTo(0), "가게 화폐를 알고 가방에 없다");
            Assert.That(((UiLabel)t.Node(ClockHudScreen.LabelId)).Text, Is.EqualTo("1일  07:00 · 용돈 0"));
            Assert.That(((UiLabel)t.Node(ClockHudScreen.RoomLabelId)).Text, Is.EqualTo("남은 TP 2/2"), "혼자면 누가 기다리는지 보이지 않는다");

            var vitals = t.Screen<VitalsHudScreen>(ScreenCatalog.HudVitals);
            Assert.That((vitals.Model.Energy, vitals.Model.Focus), Is.EqualTo((t.Session.Student.Energy, t.Session.Student.Focus)));
            Assert.That(((UiLabel)t.Node(VitalsHudScreen.ValueId("hud.energy"))).Text, Is.EqualTo(t.Session.Student.Energy.ToString()));
        }

        [Test]
        public void Command_UpdatesTimeAndVitals_ThroughTheSessionNotice()
        {
            using var t = new TestSession();
            t.Stack.OpenHud();
            int energy = t.Session.Student.Energy;
            // 누가 부르든 GameSession 명령은 Changed로 알리고, HUD는 그 알림을 받아 고친다(매 프레임 읽지 않는다).
            t.Session.PerformLocationActivity("location.activity.library-hard-book", "req-1");
            Assert.That(t.Screen<VitalsHudScreen>(ScreenCatalog.HudVitals).Model.Energy, Is.EqualTo(t.Session.Student.Energy));
            Assert.That(t.Session.Student.Energy, Is.LessThan(energy));
            var clock = t.Screen<ClockHudScreen>(ScreenCatalog.HudClock);
            Assert.That(clock.Model.Hour * 60 + clock.Model.Minute, Is.EqualTo(t.Session.RoomTimeMinutes), "다음 때의 대표 시각");
            Assert.That(t.Session.Room.PartIndex, Is.EqualTo(1));
            Assert.That(((UiLabel)t.Node(ClockHudScreen.RoomLabelId)).Text, Is.EqualTo("남은 TP 1/2"));
        }

        [Test]
        public void Together_ShowsWhoHasNotFinishedThisPart_AndUpdatesWhenTheRoomChanges()
        {
            var clock = new RoomClock(TestSession.Definitions.Get<LifeCalendar>(GameStartSettings.FromDefinitions(TestSession.Definitions).CalendarId));
            clock.Join("me");
            clock.Join("friend");
            using var t = new TestSession(room: clock, playerId: "me");
            t.Stack.OpenHud();
            var label = (UiLabel)t.Node(ClockHudScreen.RoomLabelId);
            Assert.That(label.Text, Is.EqualTo("남은 TP 2/2 · 아직: friend"));

            t.Session.PerformLocationActivity("location.activity.library-hard-book", "req-together");
            Assert.That(label.Text, Is.EqualTo("남은 TP 1/2 · 아직: friend"), "나는 끝냈고 friend를 기다린다");
            Assert.That(t.Screen<ClockHudScreen>(ScreenCatalog.HudClock).Model.Hour, Is.EqualTo(7), "때가 아직 안 넘어갔다");

            clock.Skip("friend");
            t.Session.SyncWithRoom();
            Assert.That(label.Text, Is.EqualTo("남은 TP 1/2 · 아직: friend"), "새 때에서는 둘 다 아직");
            Assert.That(t.Screen<ClockHudScreen>(ScreenCatalog.HudClock).Model.Hour, Is.EqualTo(18), "둘 다 끝내서 저녁");
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
            Assert.That(((UiLabel)t.Node(ClockHudScreen.LabelId)).Text, Is.EqualTo("Day 1  07:00 · Allowance 0"));
            Assert.That(((UiLabel)t.Node(ClockHudScreen.RoomLabelId)).Text, Is.EqualTo("TP 2/2 left"));
            t.Emit();
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text,
                t.Stack.Find(ScreenCatalog.HudClock).TextKeys.Concat(t.Stack.Find(ScreenCatalog.HudVitals).TextKeys));
        }
    }
}
