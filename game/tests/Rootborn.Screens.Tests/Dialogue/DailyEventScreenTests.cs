using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Interaction;
using Rootborn.Core.StudentLife;
using Rootborn.Screens.Dialogue;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Dialogue
{
    public sealed class DailyEventScreenTests
    {
        private static readonly InteractionEvent Library = new InteractionEvent("Town", "location-activity", "location.library");

        private sealed class LocationProbe : GameScreen
        {
            public LocationProbe(ScreenServices s) : base(s) { }
            protected override UiNode Build() => new UiPanel { Id = "location-probe" }.Centered(0, 0, 400, 300);
        }

        private static TestSession OpenAtLibrary()
        {
            var t = new TestSession();
            t.Stack.OpenHud();
            t.Stack.Interaction(Library);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.DailyEvent), Is.True, "장소 지점 상호작용 뒤 그 장소의 하루 이벤트가 열린다");
            t.Emit();
            return t;
        }

        [Test]
        public void LocationInteraction_OpensTheLibraryEvent_AsAMovementBlockingModal()
        {
            using TestSession t = OpenAtLibrary();
            var screen = t.Screen<DailyEventScreen>(ScreenCatalog.DailyEvent);
            Assert.That(screen.Model.EventId, Is.EqualTo("daily-event.library-hard-book"));
            Assert.That(t.Stack.MovementBlocked, Is.True, "Unity: 하루 이벤트는 입력을 잠갔다");
            Assert.That(t.Node(DailyEventScreen.PanelId).Rect, Is.EqualTo(new UiRect(960 - 560, 540 - 400, 1120, 800)));
            Assert.That(((UiLabel)t.Node(DailyEventScreen.TitleId)).Text, Is.EqualTo("끝까지 읽어 볼까?"));
            Assert.That(((UiLabel)t.Node(DailyEventScreen.DeadlineId)).Text, Is.EqualTo(t.Text.Get("life.event.ui.deferrable")));
            Assert.That(screen.Model.Choices.Select(c => c.Id), Is.EqualTo(new[] { "daily-event-choice.library-read-to-end", "daily-event-choice.library-ask-guide" }));
            Assert.That(t.Node(DailyEventScreen.ChoiceId(2)).Visible, Is.False);
            Assert.That(t.Node(DailyEventScreen.DeferId).Visible, Is.True, "미룰 수 있는 종류");
        }

        [Test]
        public void OtherLocations_AndOtherKinds_DoNotOpenIt()
        {
            using var t = new TestSession();
            t.Stack.Interaction(new InteractionEvent("School", "location-activity", "location.school"));
            t.Stack.Interaction(new InteractionEvent("Town", "location-visit", "location.library"));
            Assert.That(t.Stack.IsOpen(ScreenCatalog.DailyEvent), Is.False);
        }

        [Test]
        public void WaitsForTheScreenTheLocationInteractionOpened_ThenOpens()
        {
            using var t = new TestSession(extra: r => r.Add("location-probe", ScreenLayer.Panel, false, s => new LocationProbe(s)).OpenedByInteraction("location-activity"));
            t.Stack.Interaction(Library);
            Assert.That(t.Stack.IsOpen("location-probe"), Is.True);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.DailyEvent), Is.False, "장소 화면이 떠 있는 동안은 기다린다");
            t.Stack.Close("location-probe");
            Assert.That(t.Stack.IsOpen(ScreenCatalog.DailyEvent), Is.True);
        }

        [Test]
        public void ChoosingAChoice_AppliesItOnce_AndShowsTheResult_PanelStaysOpen()
        {
            using TestSession t = OpenAtLibrary();
            var screen = t.Screen<DailyEventScreen>(ScreenCatalog.DailyEvent);
            int minutes = t.Session.Student.TimeMinutes;
            t.Click(DailyEventScreen.ChoiceId(0));
            Assert.That(screen.LastResult?.Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(t.Session.Student.TimeMinutes, Is.GreaterThan(minutes), "선택지 비용이 들었다");
            Assert.That(screen.LastRecord.State, Is.EqualTo(DailyEventStates.Completed));
            Assert.That(((UiTextBlock)t.Node(DailyEventScreen.ResultId)).Text, Does.StartWith(t.Text.Get("life.event.ui.applied")));
            Assert.That(t.Stack.IsOpen(ScreenCatalog.DailyEvent), Is.True);

            int after = t.Session.Student.TimeMinutes;
            t.Click(DailyEventScreen.ChoiceId(0));
            Assert.That(screen.LastResult?.Kind, Is.EqualTo(LifeActivityResultKind.DuplicateRequest), "같은 선택은 두 번 적용되지 않는다");
            Assert.That(t.Session.Student.TimeMinutes, Is.EqualTo(after));
        }

        [Test]
        public void Defer_PushesItToTomorrow_AndClose_JustCloses()
        {
            using TestSession t = OpenAtLibrary();
            var screen = t.Screen<DailyEventScreen>(ScreenCatalog.DailyEvent);
            t.Click(DailyEventScreen.DeferId);
            Assert.That(screen.LastRecord.State, Is.EqualTo(DailyEventStates.Deferred));
            Assert.That(screen.LastRecord.DueDay, Is.EqualTo(2));
            Assert.That(((UiTextBlock)t.Node(DailyEventScreen.ResultId)).Text, Is.EqualTo("2일차에 다시 만나요."));
            t.Click(DailyEventScreen.CloseId);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.DailyEvent), Is.False);
            Assert.That(t.Stack.MovementBlocked, Is.False);
            t.Stack.Interaction(Library);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.DailyEvent), Is.False, "미룬 이벤트는 오늘 다시 나오지 않는다");
        }

        [Test]
        public void Decline_RecordsIt()
        {
            using TestSession t = OpenAtLibrary();
            t.Click(DailyEventScreen.DeclineId);
            Assert.That(t.Screen<DailyEventScreen>(ScreenCatalog.DailyEvent).LastRecord.State, Is.EqualTo(DailyEventStates.Declined));
            Assert.That(((UiTextBlock)t.Node(DailyEventScreen.ResultId)).Text, Is.EqualTo(t.Text.Get("life.event.ui.declined")));
        }

        [Test]
        public void ArrowKeysMoveBetweenButtons_InThisBlockingScreen()
        {
            using TestSession t = OpenAtLibrary();
            Assert.That(t.Stack.Key(ScreenKey.Down, false), Is.True);
            Assert.That(t.Stack.Tree.Focused, Is.Not.Null, "이동을 막는 화면은 방향 키만으로 버튼을 옮긴다");
        }

        [Test]
        public void Text_ExistsInBothLanguages()
        {
            using TestSession t = OpenAtLibrary();
            t.Click(DailyEventScreen.ChoiceId(0));
            t.Text.Language = "en";
            t.Emit();
            Assert.That(((UiLabel)t.Node(DailyEventScreen.TitleId)).Text, Is.EqualTo(t.Text.Get("life.event.library.name")));
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, t.Stack.Find(ScreenCatalog.DailyEvent).TextKeys);
        }
    }
}
