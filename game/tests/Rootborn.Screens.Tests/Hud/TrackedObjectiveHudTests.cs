using NUnit.Framework;
using Rootborn.Screens.Hud;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Hud
{
    public sealed class TrackedObjectiveHudTests
    {
        private sealed class Blocker : GameScreen
        {
            public Blocker(ScreenServices s) : base(s) { }
            protected override UiNode Build() => new UiPanel { Id = "blocker" };
        }

        private static TestSession Start() =>
            new TestSession(extra: r => r.Add("blocker", ScreenLayer.Modal, true, s => new Blocker(s)));

        [Test]
        public void NewGame_NothingTracked_HudIsHidden()
        {
            using TestSession t = Start();
            t.Stack.OpenHud();
            var hud = t.Screen<TrackedObjectiveHudScreen>(ScreenCatalog.HudTracked);
            Assert.That(hud.Model.HasChain, Is.False);
            Assert.That(hud.IsShown, Is.False);
            Assert.That(t.Node(TrackedObjectiveHudScreen.PanelId).Rect, Is.EqualTo(new UiRect(1920 - 204 - 640, 196, 640, 200)), "Unity PositionTracked");
        }

        [Test]
        public void TalkingToMom_StartsTheMainChain_AndTheHudShowsItsStep()
        {
            using TestSession t = Start();
            t.Stack.OpenHud();
            var hud = t.Screen<TrackedObjectiveHudScreen>(ScreenCatalog.HudTracked);
            t.Commands.Run(s => s.TalkTo("npc.first-guide"), ChangeTopics.Dialogue);

            Assert.That(hud.Model.HasChain, Is.True);
            Assert.That(hud.Model.StepId, Is.EqualTo("mainquest.BackToSchool"));
            Assert.That(hud.IsShown, Is.True);
            Assert.That(hud.Model.Objectives.Length, Is.GreaterThan(0));
            string progress = ((UiLabel)t.Node(TrackedObjectiveHudScreen.ProgressId)).Text;
            Assert.That(progress, Does.Contain(" / "));
            Assert.That(((UiLabel)t.Node(TrackedObjectiveHudScreen.TitleId)).Text, Is.EqualTo(t.Text.Get(hud.Model.TitleKey)));
            Assert.That(hud.Model.HintKind, Is.EqualTo(TrackedHintKind.NextObjective));
            Assert.That(((UiTextBlock)t.Node(TrackedObjectiveHudScreen.HintId)).Text, Does.StartWith("다음: "));
            t.Emit();
            t.Text.Language = "en";
            t.Emit();
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, t.Stack.Find(ScreenCatalog.HudTracked).TextKeys);
        }

        [Test]
        public void BlockingScreen_HidesTheHud_UntilItCloses()
        {
            using TestSession t = Start();
            t.Stack.OpenHud();
            t.Commands.Run(s => s.TalkTo("npc.first-guide"), ChangeTopics.Dialogue);
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
            var hud = t.Screen<TrackedObjectiveHudScreen>(ScreenCatalog.HudTracked);
            int before = 0;
            foreach (var o in hud.Model.Objectives) before += o.Current;
            var routes = t.Session.AvailableDailyPlanRoutes();
            Assume.That(routes.Length, Is.GreaterThan(0));
            t.Commands.Run(s => s.SelectDailyPlanRoute(routes[0].Id), ChangeTopics.Quests);
            int after = 0;
            foreach (var o in hud.Model.Objectives) after += o.Current;
            Assert.That(after, Is.GreaterThan(before), "일과를 고르면 BackToSchool 목표 하나가 찬다");
        }
    }
}
