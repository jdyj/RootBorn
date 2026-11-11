using System.Linq;
using NUnit.Framework;
using Rootborn.Screens.Hud;
using Rootborn.Screens.Objectives;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Hud
{
    /// <summary>
    /// 추적 목표 HUD: Unity TrackedObjectiveHud처럼 목표 수첩에서 "추적"한 항목 하나만 보인다. 추적 지정이 없으면(메인 체인이 시작돼도) 숨는다.
    /// </summary>
    public sealed class TrackedObjectiveHudTests
    {
        private sealed class Blocker : GameScreen
        {
            public Blocker(ScreenServices s) : base(s) { }
            protected override UiNode Build() => new UiPanel { Id = "blocker" };
        }

        private static TestSession Start() =>
            new TestSession(extra: r => r.Add("blocker", ScreenLayer.Modal, true, s => new Blocker(s)));

        /// <summary>Tab으로 수첩을 열고, 심부름 분류에서 메인 체인 줄을 포인터로 골라 추적 단추를 누르고 Tab으로 닫는다.</summary>
        internal static JournalItem TrackMainChainFromJournal(TestSession t)
        {
            t.Stack.Key(ScreenKey.Tab, false);
            var journal = t.Screen<ObjectiveJournalScreen>(ScreenCatalog.ObjectiveJournal);
            t.Click(ObjectiveJournalScreen.CategoryButtonId(JournalCategories.Quests));
            int row = journal.Items.ToList().FindIndex(i => i.StableKey.StartsWith("questchain.") && i.IsTracked);
            Assert.That(row, Is.GreaterThanOrEqualTo(0), "메인 체인 줄");
            ObjectiveJournalScreenTests.ClickRow(t, journal, row);
            t.Click(ObjectiveJournalScreen.TrackId);
            t.Stack.Key(ScreenKey.Tab, false);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.ObjectiveJournal), Is.False);
            return journal.Items[row];
        }

        [Test]
        public void NewGame_NothingTracked_HudIsHidden()
        {
            using TestSession t = Start();
            t.Stack.OpenHud();
            var hud = t.Screen<TrackedObjectiveHudScreen>(ScreenCatalog.HudTracked);
            Assert.That(hud.Model.HasItem, Is.False);
            Assert.That(hud.IsShown, Is.False);
            Assert.That(t.Node(TrackedObjectiveHudScreen.PanelId).Rect, Is.EqualTo(new UiRect(1920 - 204 - 640, 196, 640, 200)), "Unity PositionTracked");
        }

        [Test]
        public void MainChainStarted_WithoutJournalTracking_HudStaysHidden()
        {
            using TestSession t = Start();
            t.Stack.OpenHud();
            t.Commands.Run(s => s.TalkTo("npc.first-guide"), ChangeTopics.Dialogue);
            Assert.That(t.Session.TrackedObjective().HasChain, Is.True, "Core에서는 체인이 추적 중이다");
            Assert.That(t.Screen<TrackedObjectiveHudScreen>(ScreenCatalog.HudTracked).IsShown, Is.False, "Unity: 수첩에서 추적하기 전에는 HUD가 없다");
        }

        [Test]
        public void TrackedInTheJournal_HudShowsThatItem()
        {
            using TestSession t = Start();
            t.Stack.OpenHud();
            t.Commands.Run(s => s.TalkTo("npc.first-guide"), ChangeTopics.Dialogue);
            JournalItem tracked = TrackMainChainFromJournal(t);
            var hud = t.Screen<TrackedObjectiveHudScreen>(ScreenCatalog.HudTracked);
            Assert.That(hud.Model.StableKey, Is.EqualTo(tracked.StableKey));
            Assert.That(hud.IsShown, Is.True);
            Assert.That(((UiLabel)t.Node(TrackedObjectiveHudScreen.TitleId)).Text, Is.EqualTo(t.Text.Get(t.Session.TrackedObjective().StepNameKey)), "제목은 지금 단계 이름");
            Assert.That(((UiLabel)t.Node(TrackedObjectiveHudScreen.ProgressId)).Text, Does.Contain(" / "));
            Assert.That(((UiTextBlock)t.Node(TrackedObjectiveHudScreen.HintId)).Text, Does.StartWith("다음: "));
            t.Emit();
            t.Text.Language = "en";
            t.Emit();
            Assert.That(((UiTextBlock)t.Node(TrackedObjectiveHudScreen.HintId)).Text, Does.StartWith("Next: "), "언어를 바꾸면 다시 받는다");
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, t.Stack.Find(ScreenCatalog.HudTracked).TextKeys);
        }

        [Test]
        public void BlockingScreen_HidesTheHud_UntilItCloses()
        {
            using TestSession t = Start();
            t.Stack.OpenHud();
            t.Commands.Run(s => s.TalkTo("npc.first-guide"), ChangeTopics.Dialogue);
            TrackMainChainFromJournal(t);
            var hud = t.Screen<TrackedObjectiveHudScreen>(ScreenCatalog.HudTracked);
            t.Stack.Open("blocker");
            Assert.That(hud.IsShown, Is.False, "Unity: 입력 잠금 중에는 투명");
            t.Stack.Close("blocker");
            Assert.That(hud.IsShown, Is.True);
        }

        [Test]
        public void ProgressChange_IsShownAfterTheNotice()
        {
            using TestSession t = Start();
            t.Stack.OpenHud();
            t.Commands.Run(s => s.TalkTo("npc.first-guide"), ChangeTopics.Dialogue);
            TrackMainChainFromJournal(t);
            var hud = t.Screen<TrackedObjectiveHudScreen>(ScreenCatalog.HudTracked);
            string before = ((UiLabel)t.Node(TrackedObjectiveHudScreen.ProgressId)).Text;
            var routes = t.Session.AvailableDailyPlanRoutes();
            Assume.That(routes.Length, Is.GreaterThan(0));
            t.Commands.Run(s => s.SelectDailyPlanRoute(routes[0].Id), ChangeTopics.Quests);
            Assert.That(((UiLabel)t.Node(TrackedObjectiveHudScreen.ProgressId)).Text, Is.Not.EqualTo(before), "일과를 고르면 BackToSchool 목표 하나가 찬다");
            Assert.That(hud.IsShown, Is.True);
        }
    }
}
