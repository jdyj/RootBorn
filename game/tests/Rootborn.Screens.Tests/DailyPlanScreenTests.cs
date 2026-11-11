using System.Linq;
using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;
using Rootborn.Screens.Day;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests
{
    /// <summary>일과 화면(Unity DailyPlanPanel.BindSchedule, 계획판 상호작용으로 열림). 자동 테스트, 창 없음.</summary>
    public sealed class DailyPlanScreenTests
    {
        private DayScreensFixture _f;

        [SetUp] public void Make() => _f = new DayScreensFixture();
        [TearDown] public void Clean() => _f.Dispose();

        private DailyPlanScreen Open()
        {
            Assert.That(_f.Interact("House", DailyPlanScreen.InteractionKind, "house.daily-plan-board"), Is.True);
            return (DailyPlanScreen)_f.Stack.Find(ScreenCatalog.DailyPlan);
        }

        [Test]
        public void BoardInteraction_OpensA1120x800PanelInTheMiddle_ThatDoesNotBlockWalking()
        {
            Open();
            Assert.That(_f.Stack.IsOpen(ScreenCatalog.DailyPlan), Is.True);
            Assert.That(_f.Node(DailyPlanScreen.RootId).Rect, Is.EqualTo(new UiRect(400, 140, 1120, 800)));
            Assert.That(_f.Stack.MovementBlocked, Is.False, "Unity 판 일과 화면은 이동을 막지 않았다");
        }

        [Test]
        public void Model_ComesFromTheSession_RoutesSlotsAndTime()
        {
            DailyPlanScreen screen = Open();
            DailyPlanModel model = screen.Model;
            DailyPlanRoute[] routes = _f.Session.AvailableDailyPlanRoutes();
            Assert.That(routes, Is.Not.Empty, "새 게임 첫날에도 고를 일과 길이 있다");
            Assert.That(model.Routes.Select(r => r.RouteId), Is.EqualTo(routes.Select(r => r.Id)));
            Assert.That(model.Slots.Count, Is.EqualTo(_f.Session.DailyPlan.SlotCount));
            Assert.That(model.SelectedSlot, Is.EqualTo(0), "처음엔 비어 있는 첫 칸을 고른다");
            Assert.That(model.IsCurrentDay && model.CanAssign, Is.True);
            Assert.That(model.CanGoPrevious || model.CanGoNext, Is.False, "첫날은 지난 일기가 없다");
            Assert.That(((UiLabel)_f.Node(DailyPlanScreen.TimeBudgetId)).Text,
                Is.EqualTo(string.Format(_f.Text.Get("life.time.summary"), 1, _f.Text.Get("life.weekday." + _f.Session.Student.Calendar.GetWeekdayIndex(1)), 7, 0)),
                "날·요일·지금 시각(시간 포인트는 보이지 않는다)");
        }

        [Test]
        public void SkipButton_IsHiddenWhenAlone_TheClockFlowsInstead()
        {
            Open();
            Assert.That(_f.Node(DailyPlanScreen.SkipId).Visible, Is.False, "혼자 하기에서는 넘기기가 없다(흐르는 시간 — 4단계에서 지운다)");
        }

        [Test]
        public void SkipButton_InARoom_EndsThisPartWithoutAnActivity_AndIsOffWhenNothingIsLeft()
        {
            var clock = new RoomClock(_f.Session.Student.Calendar);
            clock.Join("me");
            using var room = new DayScreensFixture(room: clock, playerId: "me");
            Assert.That(room.Interact("House", DailyPlanScreen.InteractionKind, "house.daily-plan-board"), Is.True);
            Assert.That(room.Node(DailyPlanScreen.SkipId).Visible && room.Node(DailyPlanScreen.SkipId).Enabled, Is.True, "같이 하는 방에는 아직 넘기기가 있다");
            room.Click(DailyPlanScreen.SkipId);
            Assert.That(room.Session.Student.TimePointsSpentToday, Is.EqualTo(1), "방의 때 하나를 쓴다");
            Assert.That(room.Session.Room.PartIndex, Is.EqualTo(1), "나 혼자인 방이면 바로 다음 때");
            Assert.That(room.Session.Student.GetTodayActivityIds(), Is.Empty);

            room.Click(DailyPlanScreen.SkipId);
            Assert.That(room.Session.Room.IsDayOver, Is.True);
            Assert.That(room.Node(DailyPlanScreen.SkipId).Enabled, Is.False, "넘길 때가 없다");
        }

        [Test]
        public void TheSlotOfTheCurrentTimeSlot_IsMarkedNow_AndChosenFirst()
        {
            // 일과 칸은 시간대에 묶인다(GameSession.CurrentDailyPlanSlotIndex). 지금 시간대의 칸이 없으면 표시도 없다.
            DailyPlanScreen screen = Open();
            int now = _f.Session.CurrentDailyPlanSlotIndex;
            for (int i = 0; i < screen.Model.Slots.Count; i++)
                Assert.That(screen.Model.Slots[i].Now, Is.EqualTo(i == now), "칸 " + i);
            _f.Click(DailyPlanScreen.CloseTopId);

            // 시계가 흘러 칸이 묶인 시간대에 들어가면 그 칸이 "지금"이 되고 처음 고르는 칸이 된다.
            int target = -1;
            for (int guard = 0; guard < 24 * 60 && target < 0; guard++)
            {
                _f.Session.AdvanceTime(25f / 60f, false);
                if (_f.Session.CurrentDailyPlanSlotIndex >= 0) target = _f.Session.CurrentDailyPlanSlotIndex;
            }
            Assert.That(target, Is.GreaterThanOrEqualTo(0), "그날의 어느 시간대에는 칸이 있다");
            screen = Open();
            Assert.That(screen.Model.Slots[target].Now, Is.True);
            Assert.That(screen.Model.SelectedSlot, Is.EqualTo(target), "지금 시간대의 칸을 먼저 고른다");
            string label = ((UiCenteredText)_f.Node(DailyPlanScreen.SlotTextId(target))).Text;
            Assert.That(label, Does.Contain(_f.Text.Format("daily-plan.period-now", _f.Text.Get(screen.Model.Slots[target].PeriodKey))));
        }

        [Test]
        public void SkipButton_WhileWaitingForOthers_IsOff()
        {
            var clock = new RoomClock(_f.Session.Student.Calendar);
            clock.Join("me");
            clock.Join("friend");
            using var shared = new DayScreensFixture(room: clock, playerId: "me");
            Assert.That(shared.Interact("House", DailyPlanScreen.InteractionKind, "house.daily-plan-board"), Is.True);
            shared.Click(DailyPlanScreen.SkipId);
            Assert.That(clock.HasFinishedPart("me"), Is.True);
            Assert.That(clock.PartIndex, Is.EqualTo(0), "friend를 기다린다");
            Assert.That(shared.Node(DailyPlanScreen.SkipId).Enabled, Is.False, "기다리는 동안은 넘기지 못한다");
        }

        [Test]
        public void ClickingARouteCard_FillsTheSelectedSlot_AndTheSlotShowsIt()
        {
            DailyPlanScreen screen = Open();
            DailyPlanModel before = screen.Model;
            string route = before.Routes[0].RouteId;
            UiRect card = screen.RouteCardRect(0);
            _f.ClickAt(card.CenterX, card.CenterY, DailyPlanScreen.RouteGridId);

            Assert.That(_f.Session.DailyPlan.Slots[0].RouteId, Is.EqualTo(route));
            Assert.That(screen.Model.Slots[0].RouteId, Is.EqualTo(route));
            string label = ((UiCenteredText)_f.Node(DailyPlanScreen.SlotTextId(0))).Text;
            Assert.That(label, Does.StartWith("> "), "고른 칸 표시");
            Assert.That(label, Does.Contain(_f.Text.Get(before.Routes[0].NameKey)));
            Assert.That(label, Does.Contain(_f.Text.Get("daily-plan.planned")));
            Assert.That(((UiLabel)_f.Node(DailyPlanScreen.RefusalId)).Text, Is.Empty);
        }

        [Test]
        public void ClickingASlot_SelectsIt_ThenARouteGoesThere()
        {
            DailyPlanScreen screen = Open();
            if (screen.Model.Slots.Count < 2) Assert.Ignore("칸이 하나뿐인 날");
            _f.Click(DailyPlanScreen.SlotId(1));
            Assert.That(screen.Model.SelectedSlot, Is.EqualTo(1));
            UiRect card = screen.RouteCardRect(0);
            _f.ClickAt(card.CenterX, card.CenterY);
            Assert.That(_f.Session.DailyPlan.Slots[1].RouteId, Is.EqualTo(screen.Model.Routes[0].RouteId));
            Assert.That(_f.Session.DailyPlan.Slots[0].RouteId, Is.Empty);
        }

        [Test]
        public void Refused_SameRouteAgainInTheSameSlot_ChangesNothing_AndShowsWhy()
        {
            DailyPlanScreen screen = Open();
            UiRect card = screen.RouteCardRect(0);
            _f.ClickAt(card.CenterX, card.CenterY);
            string saved = _f.Session.DailyPlan.Slots[0].RouteId;
            int remaining = _f.Session.Student.TimePointsRemaining;

            _f.ClickAt(card.CenterX, card.CenterY);
            Assert.That(_f.Session.DailyPlan.Slots[0].RouteId, Is.EqualTo(saved));
            Assert.That(_f.Session.Student.TimePointsRemaining, Is.EqualTo(remaining));
            Assert.That(((UiLabel)_f.Node(DailyPlanScreen.RefusalId)).Text, Is.EqualTo(_f.Text.Get("daily-plan.assign-refused")));
        }

        [Test]
        public void Refused_RouteWithAPerDayLimit_CannotFillMoreSlotsThanTheLimit()
        {
            DailyPlanScreen screen = Open();
            int limited = -1;
            for (int i = 0; i < screen.Model.Routes.Count; i++)
            {
                DailyPlanRoute route = _f.Session.AvailableDailyPlanRoutes()[i];
                if (route.MaxSelectionsPerDay < _f.Session.DailyPlan.SlotCount) { limited = i; break; }
            }
            if (limited < 0) Assert.Ignore("하루 제한이 칸 수보다 작은 일과 길이 데이터에 없다");
            DailyPlanRoute target = _f.Session.AvailableDailyPlanRoutes()[limited];
            UiRect card = screen.RouteCardRect(limited);
            for (int slot = 0; slot < _f.Session.DailyPlan.SlotCount; slot++)
            {
                _f.Click(DailyPlanScreen.SlotId(slot));
                _f.ClickAt(card.CenterX, card.CenterY);
            }
            int used = _f.Session.DailyPlan.Slots.Count(s => s.RouteId == target.Id);
            Assert.That(used, Is.EqualTo(target.MaxSelectionsPerDay));
            Assert.That(((UiLabel)_f.Node(DailyPlanScreen.RefusalId)).Text, Is.EqualTo(_f.Text.Get("daily-plan.assign-refused")));
        }

        [Test]
        public void CloseButtons_CloseTheScreen()
        {
            Open();
            _f.Click(DailyPlanScreen.CloseTopId);
            Assert.That(_f.Stack.IsOpen(ScreenCatalog.DailyPlan), Is.False);
            Open();
            _f.Click(DailyPlanScreen.CloseBottomId);
            Assert.That(_f.Stack.IsOpen(ScreenCatalog.DailyPlan), Is.False);
        }

        [Test]
        public void EndedDay_SlotsAndRoutesAreDisabled()
        {
            _f.Session.EndDay();
            DailyPlanScreen screen = Open();
            Assert.That(screen.Model.CanAssign, Is.False);
            Assert.That(_f.Node(DailyPlanScreen.SlotId(0)).Enabled, Is.False);
            UiRect card = screen.RouteCardRect(0);
            _f.ClickAt(card.CenterX, card.CenterY);
            Assert.That(_f.Session.DailyPlan.Slots[0].RouteId, Is.Empty);
        }

        [Test]
        public void WithoutAGameSession_TheBoardDoesNotLeaveTheScreenOpen()
        {
            var stack = new ScreenStack(ScreenCatalog.Create(), new ScreenServices(TestData.Text(), null));
            stack.Layout(1920, 1080);
            stack.Interaction(new Rootborn.Core.Interaction.InteractionEvent("House", DailyPlanScreen.InteractionKind, "house.daily-plan-board"));
            Assert.That(stack.IsOpen(ScreenCatalog.DailyPlan), Is.False, "--world처럼 게임 한 판이 없으면 일과가 없다");
        }

        [Test]
        public void AllTextKeys_ExistInBothLanguages_ExceptTheSharedKnownMissingOnes()
        {
            DailyPlanScreen screen = Open();
            UiRect card = screen.RouteCardRect(0);
            _f.ClickAt(card.CenterX, card.CenterY);
            _f.ClickAt(card.CenterX, card.CenterY); // 거절 문구까지
            _f.EmitTexts();
            _f.Text.Language = "en";
            _f.EmitTexts();
            KnownMissingKeys.AssertOnlyKnownMissing(_f.Text, screen.TextKeys);
        }
    }
}
