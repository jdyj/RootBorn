using System.Linq;
using NUnit.Framework;
using Rootborn.Core.StudentLife;
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
                Is.EqualTo(string.Format(_f.Text.Get("life.tp.summary"), 1, _f.Text.Get("life.weekday." + _f.Session.Student.Calendar.GetWeekdayIndex(1)),
                    _f.Session.Student.TimePointsRemaining, _f.Session.Student.DailyTimePointBudget)));
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
            string label = ((CenteredText)_f.Node(DailyPlanScreen.SlotTextId(0))).Text;
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
