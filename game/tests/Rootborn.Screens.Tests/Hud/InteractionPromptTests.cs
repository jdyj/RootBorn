using NUnit.Framework;
using Rootborn.Screens.Hud;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Hud
{
    public sealed class InteractionPromptTests
    {
        private sealed class Blocker : GameScreen
        {
            public Blocker(ScreenServices s) : base(s) { }
            protected override UiNode Build() => new UiPanel { Id = "blocker" };
        }

        private static TestSession Start()
        {
            var t = new TestSession(extra: r => r.Add("blocker", ScreenLayer.Modal, true, s => new Blocker(s)));
            t.Stack.OpenHud();
            return t;
        }

        private static InteractionPromptScreen Prompt(TestSession t) => t.Screen<InteractionPromptScreen>(ScreenCatalog.HudPrompt);

        [Test]
        public void Npc_ShowsTalk_AboveTheTarget()
        {
            using TestSession t = Start();
            Assert.That(Prompt(t).Text, Is.Empty, "대상이 없으면 안내가 없다");
            t.Stack.SetInteractionTarget(new InteractionTargetView("npc", "npc.first-guide", 800, 500, 64));
            Assert.That(Prompt(t).Text, Is.EqualTo("[E] 말 걸기"));
            UiRect r = t.Node(InteractionPromptScreen.LabelId).Rect;
            Assert.That(r.CenterX, Is.EqualTo(800f));
            Assert.That(r.CenterY, Is.EqualTo(500f - 1.15f * 64f).Within(0.01f), "NpcInteractor 안내 높이 1.15칸");
        }

        [Test]
        public void LocationKinds_PutTheLocationName_IntoTheirFormat()
        {
            using TestSession t = Start();
            string school = t.Text.Get("location.school.name");
            t.Stack.SetInteractionTarget(new InteractionTargetView("location-visit", "location.school", 100, 100, 32));
            Assert.That(Prompt(t).Text, Is.EqualTo("[E] " + school + " 둘러보기"));
            t.Stack.SetInteractionTarget(new InteractionTargetView("location-activity", "location.school", 100, 100, 32));
            Assert.That(Prompt(t).Text, Is.EqualTo("[E] " + school + " 살펴보기"));
            t.Stack.SetInteractionTarget(new InteractionTargetView("daily-plan-board", "house.daily-plan-board", 100, 100, 32));
            Assert.That(Prompt(t).Text, Is.EqualTo("[E] 오늘의 계획 세우기"));
            t.Stack.SetInteractionTarget(new InteractionTargetView("student-day-end", "house.day-end", 100, 100, 32));
            Assert.That(Prompt(t).Text, Is.EqualTo("[E] 하루 마무리"));
        }

        [Test]
        public void UnknownKind_AndBlockingScreens_HideThePrompt()
        {
            using TestSession t = Start();
            t.Stack.SetInteractionTarget(new InteractionTargetView("door", "x", 100, 100, 32));
            Assert.That(Prompt(t).Text, Is.Empty);
            t.Stack.SetInteractionTarget(new InteractionTargetView("npc", "npc.first-guide", 100, 100, 32));
            t.Stack.Open("blocker");
            Assert.That(Prompt(t).Text, Is.Empty);
            t.Stack.Close("blocker");
            Assert.That(Prompt(t).Text, Is.Not.Empty);
            t.Stack.SetInteractionTarget(null);
            Assert.That(Prompt(t).Text, Is.Empty);
        }

        [Test]
        public void AllPromptKeys_ExistInBothLanguages()
        {
            using TestSession t = Start();
            foreach (InteractionPromptSpec spec in InteractionPromptScreen.Prompts)
            {
                t.Stack.SetInteractionTarget(new InteractionTargetView(spec.Kind, "location.school", 100, 100, 32));
                t.Text.Language = "en";
                t.Text.Language = "ko";
            }
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, t.Stack.Find(ScreenCatalog.HudPrompt).TextKeys);
            Assert.That(t.Stack.Find(ScreenCatalog.HudPrompt).TextKeys, Does.Contain("location.school.name"));
        }
    }
}
