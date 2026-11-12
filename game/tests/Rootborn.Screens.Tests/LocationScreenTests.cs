using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Placement;
using Rootborn.Core.StudentLife;
using Rootborn.Screens.Day;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests
{
    /// <summary>장소 화면(Unity LocationIdentityPanel, 장소 지점 상호작용으로 열림). 자동 테스트, 창 없음.</summary>
    public sealed class LocationScreenTests
    {
        private const string School = "location.school";
        private const string Store = "location.corner-store";
        private DayScreensFixture _f;

        [SetUp] public void Make() => _f = new DayScreensFixture();
        [TearDown] public void Clean() => _f.Dispose();

        private LocationScreen Open(string locationId)
        {
            Assert.That(_f.Interact("School", LocationScreen.InteractionKind, locationId), Is.True);
            Assert.That(_f.Stack.IsOpen(ScreenCatalog.Location), Is.True, locationId + "를 열지 못했다");
            return (LocationScreen)_f.Stack.Find(ScreenCatalog.Location);
        }

        private int FirstEnabledActivity(LocationScreen screen)
        {
            for (int i = 0; i < screen.Model.Rows.Count; i++)
                if (screen.Model.Rows[i].Kind == LocationRowKind.Activity && screen.Model.Rows[i].Enabled) return i;
            return -1;
        }

        private void PressRow(LocationScreen screen, int index)
        {
            UiRect r = screen.RowRect(index);
            _f.ClickAt(r.CenterX, r.CenterY, LocationScreen.RowsId);
        }

        [Test]
        public void Interaction_OpensA1120x800ModalThatBlocksWalking_AndRecordsTheVisit()
        {
            Assert.That(new LocationVisitProgress(_f.Session.Student).HasVisited(School), Is.False);
            Open(School);
            Assert.That(_f.Node(LocationScreen.RootId).Rect, Is.EqualTo(new UiRect(400, 140, 1120, 800)));
            Assert.That(_f.Stack.MovementBlocked, Is.True, "Unity 판 장소 화면은 이동을 막았다(UiGameplayInputLock)");
            Assert.That(new LocationVisitProgress(_f.Session.Student).HasVisited(School), Is.True, "열 때 방문을 기록한다");
        }

        [Test]
        public void Model_ComesFromTheLocationIdentity_InItsOrder()
        {
            LocationScreen screen = Open(School);
            LocationIdentity identity = LocationModel.FindIdentity(_f.Session, School);
            Assert.That(screen.Model.Rows.Where(r => r.Kind == LocationRowKind.Activity).Select(r => r.Id),
                Is.EqualTo(identity.AvailableActivities.Select(a => a.Id)));
            Assert.That(((UiLabel)_f.Node(LocationScreen.TitleId)).Text, Is.EqualTo(_f.Text.Get(_f.Session.Definitions.Get<Location>(School).DisplayNameKey)));
            Assert.That(((UiTextBlock)_f.Node(LocationScreen.DescriptionId)).Text, Is.EqualTo(_f.Text.Get(identity.DescriptionKey)));
            Assert.That(((UiTextBlock)_f.Node(LocationScreen.HintsId)).Text, Does.StartWith(_f.Text.Get("life.location.hints")));
        }

        [Test]
        public void PressingAnActivity_PerformsIt_AndShowsTheResult()
        {
            LocationScreen screen = Open(School);
            int index = FirstEnabledActivity(screen);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "첫날 학교에서 할 수 있는 활동이 있다");
            string activity = screen.Model.Rows[index].Id;
            int before = _f.Session.Student.GetActivityLogIds().Length;
            PressRow(screen, index);
            Assert.That(_f.Session.Student.GetActivityLogIds().Length, Is.EqualTo(before + 1));
            string result = ((UiTextBlock)_f.Node(LocationScreen.ResultId)).Text;
            int clock = _f.Session.ClockMinutes;
            Assert.That(result, Does.StartWith(string.Format(_f.Text.Get("life.time.applied"), clock / 60, clock % 60)), "끝난 시각을 보인다");
            Assert.That(((UiLabel)_f.Node(LocationScreen.TimeBudgetId)).Text, Does.EndWith((clock / 60).ToString("00") + ":" + (clock % 60).ToString("00")), "시간 줄이 바로 바뀐다");
            Assert.That(activity, Is.Not.Empty);
        }

        /// <summary>활동 줄이 걸리는 시간과 함께 끝나는 시각을 보인다(취침 직전에 시작해 밤 체력 감소를 맞지 않게 — 흐르는 시간 틈, 2026-11-12).</summary>
        [Test]
        public void ActivityRows_ShowWhenTheyEnd()
        {
            LocationScreen screen = Open(School);
            LocationRow row = screen.Model.Rows.First(r => r.Kind == LocationRowKind.Activity && r.TimeCostMinutes > 0);
            int end = StudentTimeText.ClockMinutes(_f.Session.Student.TimeMinutes + row.TimeCostMinutes);
            string expected = string.Format(_f.Text.Get("life.time.cost-ends"), string.Format(_f.Text.Get("life.time.cost"), row.TimeCostMinutes), end / 60, end % 60);
            Assert.That(_f.EmitTexts(), Does.Contain(expected));
        }

        /// <summary>끝나는 시각이 취침 시각을 넘기면 다른 문구로 보인다(밤에는 체력이 준다).</summary>
        [Test]
        public void ActivityRows_PastBedtime_SayItEndsAfterBedtime()
        {
            for (int guard = 0; guard < 200 && _f.Session.MinutesUntilBedtime > 30; guard++) _f.Session.AdvanceTime(25f / 4f, false); // 15분씩
            Assert.That(_f.Session.MinutesUntilBedtime, Is.InRange(1, 30), "취침 시각 30분 안쪽");
            LocationScreen screen = Open(School);
            LocationRow row = screen.Model.Rows.First(r => r.Kind == LocationRowKind.Activity && r.TimeCostMinutes > _f.Session.MinutesUntilBedtime);
            int end = StudentTimeText.ClockMinutes(_f.Session.Student.TimeMinutes + row.TimeCostMinutes);
            string expected = string.Format(_f.Text.Get("life.time.cost-ends-late"), string.Format(_f.Text.Get("life.time.cost"), row.TimeCostMinutes), end / 60, end % 60);
            Assert.That(_f.EmitTexts(), Does.Contain(expected));
        }

        [Test]
        public void Refused_WhenTimeRunsOut_RowsShowWhy_AndPressingChangesNothing()
        {
            LocationScreen screen = Open(School);
            for (int guard = 0; guard < 20; guard++)
            {
                int index = FirstEnabledActivity(screen);
                if (index < 0) break;
                PressRow(screen, index);
            }
            LocationRow refused = screen.Model.Rows.First(r => r.Kind == LocationRowKind.Activity);
            Assert.That(refused.Enabled, Is.False, "시간·기력을 다 쓰면 활동을 못 한다");
            Assert.That(refused.Refusal, Is.Not.EqualTo(StudentActivityRefusalReason.None));

            int logs = _f.Session.Student.GetActivityLogIds().Length;
            int tp = _f.Session.Student.TimePointsRemaining, energy = _f.Session.Student.Energy;
            PressRow(screen, 0);
            Assert.That(_f.Session.Student.GetActivityLogIds().Length, Is.EqualTo(logs));
            Assert.That((_f.Session.Student.TimePointsRemaining, _f.Session.Student.Energy), Is.EqualTo((tp, energy)));
            Assert.That(_f.EmitTexts(), Does.Contain(_f.Text.Get(StudentActivityRefusal.LocalizationKey(refused.Refusal))), "못 하는 이유가 줄에 보인다");
        }

        [Test]
        public void Store_ListsFurnitureOffersAfterTheActivities_WithTheAllowance()
        {
            if (!LocationModel.CanOpen(_f.Session, Store)) Assert.Ignore("첫날엔 가게에 못 들어간다");
            LocationScreen screen = Open(Store);
            var offers = _f.Session.FurnitureForSaleAt(Store);
            Assert.That(offers, Is.Not.Empty);
            Assert.That(screen.Model.Rows.Where(r => r.Kind == LocationRowKind.Offer).Select(r => r.Id), Is.EqualTo(offers.Select(o => o.StableId)));
            Assert.That(screen.Model.Rows.SkipWhile(r => r.Kind == LocationRowKind.Activity).All(r => r.Kind == LocationRowKind.Offer), Is.True, "활동 다음에 물건");
            int balance = _f.Session.Inventory.CountOf(offers[0].PriceItem);
            Assert.That(((UiLabel)_f.Node(LocationScreen.TimeBudgetId)).Text, Is.EqualTo(string.Format(_f.Text.Get("life.location.time-with-balance"), string.Format(_f.Text.Get("life.time.summary"), 1, _f.Text.Get("life.weekday." + _f.Session.Student.Calendar.GetWeekdayIndex(1)), 7, 0), balance)));
        }

        [Test]
        public void Store_NotEnoughMoney_TheOfferCannotBePressed_AndNothingChanges()
        {
            if (!LocationModel.CanOpen(_f.Session, Store)) Assert.Ignore("첫날엔 가게에 못 들어간다");
            LocationScreen screen = Open(Store);
            int index = screen.Model.Rows.ToList().FindIndex(r => r.Kind == LocationRowKind.Offer && !r.Affordable && !r.Owned);
            if (index < 0) Assert.Ignore("새 게임 용돈으로 살 수 없는 물건이 없다");
            FurnitureDefinition offer = _f.Session.Definitions.Get<FurnitureDefinition>(screen.Model.Rows[index].Id);
            int money = _f.Session.Inventory.CountOf(offer.PriceItem);
            PressRow(screen, index);
            Assert.That(_f.Session.FurnitureUnlocks.IsUnlocked(offer.UnlockToken), Is.False);
            Assert.That(_f.Session.Inventory.CountOf(offer.PriceItem), Is.EqualTo(money));
        }

        [Test]
        public void CloseButton_ClosesTheModal_AndWalkingIsFreeAgain()
        {
            Open(School);
            _f.Click(LocationScreen.CloseId);
            Assert.That(_f.Stack.IsOpen(ScreenCatalog.Location), Is.False);
            Assert.That(_f.Stack.MovementBlocked, Is.False);
        }

        [Test]
        public void UnknownLocation_DoesNotOpen()
        {
            _f.Interact("School", LocationScreen.InteractionKind, "location.no-such-place");
            Assert.That(_f.Stack.IsOpen(ScreenCatalog.Location), Is.False);
            Assert.That(_f.Stack.MovementBlocked, Is.False);
        }

        [Test]
        public void AllTextKeys_ExistInBothLanguages_ExceptTheSharedKnownMissingOnes()
        {
            LocationScreen screen = Open(School);
            int index = FirstEnabledActivity(screen);
            if (index >= 0) PressRow(screen, index);
            _f.EmitTexts();
            _f.Text.Language = "en";
            _f.EmitTexts();
            KnownMissingKeys.AssertOnlyKnownMissing(_f.Text, screen.TextKeys);
        }
    }
}
