using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Interaction;
using Rootborn.Core.Quests;
using Rootborn.Screens.Dialogue;
using Rootborn.Screens.Hud;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Dialogue
{
    public sealed class DialogueScreenTests
    {
        private static readonly InteractionEvent Mom = new InteractionEvent("House", "npc", "npc.first-guide");

        private static TestSession StartAndTalkToMom()
        {
            var t = new TestSession();
            t.Stack.OpenHud();
            Assert.That(t.Stack.Interaction(Mom), Is.True, "NPC 상호작용이 대화를 연다");
            t.Emit();
            return t;
        }

        [Test]
        public void TalkingToMom_OpensTheDialogue_WithHerLinesAndTheOneChoiceSheOffers()
        {
            using TestSession t = StartAndTalkToMom();
            var screen = t.Screen<DialogueScreen>(ScreenCatalog.Dialogue);
            Assert.That(t.Session.CurrentDialogueNpcId, Is.EqualTo("npc.first-guide"), "열 때 GameSession.TalkTo로 말을 건다");
            Assert.That(screen.Model.DialogueId, Is.EqualTo("Dialogue_FirstNpc"));
            Assert.That(screen.Model.LineKeys, Is.EqualTo(new[] { "opening.dialogue.guide", "dialogue.firstNpc.greeting" }));
            Assert.That(screen.Model.Choices.Select(c => c.LabelKey), Is.EqualTo(new[] { "dialogue.choice.acceptGatherWood" }),
                "보상 받기는 아직 고를 수 없어 숨긴다");
            Assert.That(((UiTextBlock)t.Node(DialogueScreen.LinesId)).Text, Does.StartWith(t.Text.Get("opening.dialogue.guide")));
            Assert.That(((UiButton)t.Node(DialogueScreen.ChoiceId(0))).Text, Is.EqualTo("나무 모으기를 맡을게요"));
            Assert.That(t.Stack.MovementBlocked, Is.False, "대화는 이동을 막지 않는다(Unity 판과 같음)");
        }

        [Test]
        public void Panel_IsBottomCenter760Wide_AndTallEnoughForTheLinesAndChoices()
        {
            using TestSession t = StartAndTalkToMom();
            UiRect panel = t.Node(DialogueScreen.PanelId).Rect;
            UiRect lines = t.Node(DialogueScreen.LinesId).Rect;
            UiRect choice = t.Node(DialogueScreen.ChoiceId(0)).Rect;
            UiRect close = t.Node(DialogueScreen.CloseId).Rect;
            Assert.That((panel.X, panel.W, panel.Bottom), Is.EqualTo((960f - 380f, 760f, 1080f - 36f)));
            Assert.That(lines.H, Is.GreaterThanOrEqualTo(84f));
            Assert.That(panel.H, Is.EqualTo(42 * 2 + 48 + 24 + lines.H).Within(0.01f));
            Assert.That(choice.Y, Is.EqualTo(lines.Bottom + 24).Within(0.01f), "대사 아래 24 띄워 선택지");
            Assert.That(choice.Bottom, Is.EqualTo(panel.Bottom - 42).Within(0.01f));
            Assert.That((close.Right, close.Y, close.W), Is.EqualTo((panel.Right - 42, panel.Y + 42, 48f)));
            Assert.That(lines.Right, Is.LessThanOrEqualTo(close.X), "대사가 닫기 버튼과 겹치지 않는다");
        }

        [Test]
        public void CloseButton_ClosesTheDialogue_AndTheSession()
        {
            using TestSession t = StartAndTalkToMom();
            t.Click(DialogueScreen.CloseId);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Dialogue), Is.False);
            Assert.That(t.Session.CurrentDialogue, Is.Null);
        }

        [Test]
        public void ClickingTheChoice_AcceptsTheQuest_AndCloses()
        {
            using TestSession t = StartAndTalkToMom();
            QuestDefinition quest = t.Session.Definitions.Get<QuestDefinition>("quest.gatherWood");
            t.Click(DialogueScreen.ChoiceId(0));
            Assert.That(t.Session.QuestLog.GetState(quest), Is.Not.EqualTo(QuestStage.NotStarted), "퀘스트를 받았다");
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Dialogue), Is.False, "퀘스트 받기를 고르면 닫힌다");
        }

        [Test]
        public void InteractKey_PicksTheFirstAvailableChoice_ThenTalkingAgainShowsNoChoice()
        {
            using TestSession t = StartAndTalkToMom();
            Assert.That(t.Stack.Key(ScreenKey.Interact, false), Is.True, "E는 첫 선택지를 고른다");
            Assert.That(t.Screen<DialogueScreen>(ScreenCatalog.Dialogue).LastChoiceApplied, Is.True);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Dialogue), Is.False);

            t.Stack.Interaction(Mom);
            var screen = t.Screen<DialogueScreen>(ScreenCatalog.Dialogue);
            Assert.That(screen.Model.Choices, Is.Empty, "받은 퀘스트는 다시 고를 수 없다");
            Assert.That(t.Stack.Key(ScreenKey.Interact, false), Is.False, "고를 것이 없으면 E는 월드로 간다");
            Assert.That(t.Node(DialogueScreen.ChoiceId(0)).Visible, Is.False);
        }

        [Test]
        public void TalkingToMom_StartsTheMainChain_SoTheTrackedHudChanges()
        {
            using TestSession t = StartAndTalkToMom();
            var hud = t.Screen<TrackedObjectiveHudScreen>(ScreenCatalog.HudTracked);
            Assert.That(hud.Model.StepId, Is.EqualTo("mainquest.BackToSchool"));
            Assert.That(hud.IsShown, Is.True);
        }

        [Test]
        public void Text_ExistsInBothLanguages_OrIsKnownMissing()
        {
            using TestSession t = StartAndTalkToMom();
            t.Text.Language = "en";
            t.Emit();
            Assert.That(((UiButton)t.Node(DialogueScreen.ChoiceId(0))).Text, Is.EqualTo("I'll gather the wood"));
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, t.Stack.Find(ScreenCatalog.Dialogue).TextKeys);
        }
    }
}
