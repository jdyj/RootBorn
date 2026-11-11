using System.Linq;
using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Screens.Records;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Records
{
    public sealed class LocationStateLogScreenTests
    {
        private static TestSession Open(string language = "ko", bool visited = false)
        {
            var t = new TestSession(language);
            if (visited) t.Session.VisitLocation("location.library");
            t.Stack.OpenHud();
            Assert.That(t.Stack.Open(ScreenCatalog.LocationStateLog), Is.Not.Null);
            t.Emit();
            return t;
        }

        [Test]
        public void IsAPanelThatDoesNotBlockMovement_WithUnitySize()
        {
            using TestSession t = Open();
            Assert.That(t.Stack.MovementBlocked, Is.False, "Unity: 장소 기록 패널은 입력을 잠그지 않았다");
            Assert.That(t.Node(LocationStateLogScreen.PanelId).Rect, Is.EqualTo(new UiRect(960 - 190, 540 - 210, 380, 420)));
        }

        [Test]
        public void NothingDiscovered_ShowsTheEmptyMessage()
        {
            using TestSession t = Open();
            var screen = t.Screen<LocationStateLogScreen>(ScreenCatalog.LocationStateLog);
            Assert.That(screen.Model.Rows, Is.Empty);
            Assert.That(t.Node(LocationStateLogScreen.EmptyId).Visible, Is.True);
            Assert.That(((UiLabel)t.Node(LocationStateLogScreen.EmptyId)).Text, Is.EqualTo(t.Text.Get("ui.location-log.empty")));
            Assert.That(((UiLabel)t.Node(LocationStateLogScreen.TitleId)).Text, Is.EqualTo(t.Text.Get("ui.location-log.title")));
        }

        [Test]
        public void VisitedLocation_ShowsTodaysStateAsACard_WithDayPlaceAndTime()
        {
            using TestSession t = Open(visited: true);
            var screen = t.Screen<LocationStateLogScreen>(ScreenCatalog.LocationStateLog);
            LocationStateTodaySummarySaveData[] expected = t.Session.LocationStates.TodaySummary;
            Assert.That(expected.Length, Is.GreaterThan(0), "장소에 들어가면 오늘 본 상태가 남는다");
            Assert.That(screen.Model.Rows.Count, Is.EqualTo(expected.Length));
            Assert.That(t.Node(LocationStateLogScreen.EmptyId).Visible, Is.False);
            string drawn = string.Join("\n", t.Texts(t.Emit()));
            LocationLogRow row = screen.Model.Rows[0];
            Assert.That(drawn, Does.Contain(t.Text.Get(row.TitleKey)));
            string detail = t.Text.Format("life.location-state.record.detail", row.Day, t.Text.Get(row.LocationNameKey), t.Text.Get(row.TimeNameKey));
            Assert.That(drawn, Does.Contain(detail));
        }

        [Test]
        public void HiddenState_ShowsTheHiddenTitle()
        {
            using TestSession t = Open();
            var screen = t.Screen<LocationStateLogScreen>(ScreenCatalog.LocationStateLog);
            LocationLogRow row = LocationStateLogModel.RowFor(new LocationStateTodaySummarySaveData { Hidden = true, StateId = "x", Day = 1 }, t.Session, t.Services.Names);
            Assert.That(row.TitleKey, Is.EqualTo("dream.location-state.record.hidden"));
            Assert.That(screen, Is.Not.Null);
        }

        [Test]
        public void UnknownState_ShowsTheGenericTitle_AndNoDayUsesTheNoDayFormat()
        {
            using var t = new TestSession();
            LocationLogRow row = LocationStateLogModel.RowFor(new LocationStateTodaySummarySaveData { StateId = "no-such", Day = 0, LocationId = "no-such" }, t.Session, t.Services.Names);
            Assert.That(row.TitleKey, Is.EqualTo("life.location-state.record.unknown"));
            Assert.That(row.LocationNameKey, Is.EqualTo("life.location-state.record.location"));
            Assert.That(row.TimeNameKey, Is.EqualTo("life.location-state.record.time"));
            Assert.That(row.Day, Is.Zero);
        }

        [Test]
        public void ManyCards_AreVirtualizedAndScroll()
        {
            using TestSession t = Open(visited: true);
            var grid = (UiCardGrid)t.Node(LocationStateLogScreen.ListId);
            grid.ItemCount = 300;
            Assert.That(grid.RowCount, Is.EqualTo(300));
            t.Stack.Pointer(grid.Rect.CenterX, grid.Rect.CenterY, false, -3f);
            Assert.That(grid.ScrollOffset, Is.GreaterThan(0f));
        }

        [Test]
        public void Escape_ClosesIt()
        {
            using TestSession t = Open();
            Assert.That(t.Stack.Key(ScreenKey.Escape, false), Is.True);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.LocationStateLog), Is.False);
        }

        [Test]
        public void OpeningAnotherPanel_ReplacesIt()
        {
            using TestSession t = Open();
            t.Stack.Open(ScreenCatalog.Wardrobe);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.LocationStateLog), Is.False);
        }

        [Test]
        public void ReopeningAfterAVisit_ShowsItWithoutNewNodes()
        {
            using TestSession t = Open();
            int nodes = Count(t.Stack.Tree.Root);
            t.Stack.Close(ScreenCatalog.LocationStateLog);
            t.Session.VisitLocation("location.library");
            t.Stack.Open(ScreenCatalog.LocationStateLog);
            t.Emit();
            Assert.That(t.Screen<LocationStateLogScreen>(ScreenCatalog.LocationStateLog).Model.Rows.Count, Is.GreaterThan(0));
            Assert.That(Count(t.Stack.Tree.Root), Is.EqualTo(nodes));
        }

        private static int Count(UiNode node) => 1 + node.Children.Sum(Count);

        [TestCase("ko")]
        [TestCase("en")]
        public void Text_ExistsInBothLanguages(string language)
        {
            using TestSession t = Open(language, visited: true);
            t.Emit();
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, t.Screen<LocationStateLogScreen>(ScreenCatalog.LocationStateLog).TextKeys);
        }

        [Test]
        public void ChangingTheLanguage_ChangesTheShownText()
        {
            using TestSession t = Open(visited: true);
            string before = string.Join("|", t.Texts(t.Emit()));
            t.Text.Language = "en";
            t.Stack.NotifyChanged(ChangeTopics.Language);
            Assert.That(string.Join("|", t.Texts(t.Emit())), Is.Not.EqualTo(before));
        }
    }
}
