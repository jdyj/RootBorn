using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Interaction;
using Rootborn.Core.StudentLife;
using Rootborn.Screens.Career;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Career
{
    public sealed class ExplorationScreenTests
    {
        private const string PointId = "exploration.test.alley-crate";
        private static readonly InteractionEvent Crate = new InteractionEvent("Town", "exploration", PointId);

        private static TestSession OpenAtPoint()
        {
            var t = new TestSession();
            t.Stack.OpenHud();
            Assert.That(t.Stack.Interaction(Crate), Is.True, "탐색 지점 상호작용이 화면을 연다");
            t.Emit();
            return t;
        }

        [Test]
        public void ExplorationPoint_OpensTheScreen_ByInteractionKind_AndBlocksMovement()
        {
            using TestSession t = OpenAtPoint();
            var screen = t.Screen<ExplorationScreen>(ScreenCatalog.Exploration);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Exploration), Is.True);
            Assert.That(screen.Model.InteractionId, Is.EqualTo(PointId));
            Assert.That(screen.Model.Choices.Select(c => c.ChoiceId), Is.EqualTo(new[] { "choice.test.inspect", "choice.test.careful", "choice.test.discovery", "choice.test.leave" }));
            Assert.That(t.Stack.MovementBlocked, Is.True, "Unity 판: 탐색 패널은 이동을 막는다");
            Assert.That(((UiLabel)t.Node(ExplorationScreen.TitleId)).Text, Is.EqualTo(t.Text.Get("Exploration Test Alley Crate")), "지점 이름");
            Assert.That(screen.ResultText, Is.Empty);
        }

        [Test]
        public void Layout_FollowsUnity_PanelCenteredChoicesStackedAboveTheResult()
        {
            using TestSession t = OpenAtPoint();
            UiRect panel = t.Node(ExplorationScreen.PanelId).Rect;
            Assert.That(panel, Is.EqualTo(new UiRect(960 - 280, 540 - 180, 560, 360)));
            UiRect title = t.Node(ExplorationScreen.TitleId).Rect, close = t.Node(ExplorationScreen.CloseId).Rect, result = t.Node(ExplorationScreen.ResultId).Rect;
            UiRect c0 = t.Node(ExplorationScreen.ChoiceId(0)).Rect, c1 = t.Node(ExplorationScreen.ChoiceId(1)).Rect, c3 = t.Node(ExplorationScreen.ChoiceId(3)).Rect;
            Assert.That((c0.W, c0.H, c1.Y - c0.Y), Is.EqualTo((480f, 38f, 46f)));
            Assert.That(title.Bottom, Is.LessThanOrEqualTo(c0.Y));
            Assert.That(c3.Bottom, Is.LessThanOrEqualTo(result.Y), "선택지 아래에 결과 글");
            foreach (UiRect r in new[] { title, close, result, c0, c3 })
                Assert.That(r.X >= panel.X && r.Right <= panel.Right && r.Y >= panel.Y && r.Bottom <= panel.Bottom, Is.True);
            Assert.That(close.X, Is.GreaterThanOrEqualTo(title.Right - 1), "닫기는 제목 옆");
        }

        [Test]
        public void ChoosingAnAvailableChoice_RunsTheCoreRunner_AndShowsTheResultBelow()
        {
            using TestSession t = OpenAtPoint();
            var screen = t.Screen<ExplorationScreen>(ScreenCatalog.Exploration);
            int index = System.Array.FindIndex(screen.Model.Choices, c => c.Available);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "조건이 없는 선택지가 하나는 있다");
            int energyBefore = t.Session.Student.Energy;
            t.Click(ExplorationScreen.ChoiceId(index));
            Assert.That(screen.LastResult, Is.Not.Null);
            Assert.That(screen.ResultText, Is.Not.Empty);
            Assert.That(screen.ResultText, Does.StartWith(t.Text.Get("exploration.result." + screen.LastResult.Value.Kind.ToString().ToLowerInvariant())));
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Exploration), Is.True, "결과를 보이고 열려 있다");
            Assert.That(t.Session.Exploration, Is.Not.Null);
            _ = energyBefore;
        }

        [Test]
        public void ALockedChoice_CannotBeClicked_AndShowsItsLockedReason()
        {
            using TestSession t = OpenAtPoint();
            var screen = t.Screen<ExplorationScreen>(ScreenCatalog.Exploration);
            int locked = System.Array.FindIndex(screen.Model.Choices, c => !c.Available);
            if (locked < 0) Assert.Ignore("시험 지점에 잠긴 선택지가 없다");
            UiNode button = t.Node(ExplorationScreen.ChoiceId(locked));
            Assert.That(button.IsActive, Is.False, "조건이 안 되면 누를 수 없다");
            Assert.That(((UiMultilineButton)button).Text, Does.Contain(" — "));
            UiRect r = button.Rect;
            t.Stack.Pointer(r.CenterX, r.CenterY, false, 0);
            t.Stack.Pointer(r.CenterX, r.CenterY, true, 0);
            t.Stack.Pointer(r.CenterX, r.CenterY, false, 0);
            Assert.That(screen.LastResult, Is.Null, "누르지 못했다");
        }

        [Test]
        public void EscapeAndTheCloseButton_BothClose()
        {
            using (TestSession t = OpenAtPoint())
            {
                Assert.That(t.Stack.Key(ScreenKey.Escape, false), Is.True);
                Assert.That(t.Stack.IsOpen(ScreenCatalog.Exploration), Is.False);
                Assert.That(t.Stack.MovementBlocked, Is.False);
            }
            using (TestSession t = OpenAtPoint())
            {
                t.Click(ExplorationScreen.CloseId);
                Assert.That(t.Stack.IsOpen(ScreenCatalog.Exploration), Is.False);
            }
        }

        [Test]
        public void ResultModel_CountsGainsAndVitals_LikeUnity()
        {
            var collector = new ExplorationOutcomeCollector();
            collector.AddItemGrant("item.apple:2");
            collector.AddItemGrant("bad-item");
            collector.AddItemSpend("item.coin:5");
            collector.AddClue("clue.a"); collector.AddClue("clue.b");
            collector.AddEncyclopediaEntry("entry.x");
            collector.AddCareerHint("hint.y");
            collector.AddFollowUpQuest("quest.z");
            collector.AddOutcome("trait:focus"); collector.AddOutcome("skill:talk"); collector.AddOutcome("item-grant:item.apple"); collector.AddOutcome("mystery:1");
            collector.AddFatigueStatus("energy:-3"); collector.AddFatigueStatus("focus:0"); collector.AddFatigueStatus("stress:2"); collector.AddFatigueStatus("odd:9");
            var result = new ExplorationChoiceResult(ExplorationChoiceResultKind.Applied, "i", "c", null, collector);
            var model = ExplorationResultModel.From(result, id => id == "item.apple" ? "name.apple" : null);

            Assert.That(model.StatusKey, Is.EqualTo("exploration.result.applied"));
            Assert.That(model.Rewards.Select(e => (e.Key, e.NameKey, e.Number)), Is.EqualTo(new[]
            {
                ("exploration.effect.item-gain", "name.apple", 2),
                ("exploration.effect.item-spent", "", 5),
                ("exploration.effect.clues", (string)null, 2),
                ("exploration.effect.entries", (string)null, 1),
                ("exploration.effect.careers", (string)null, 1),
                ("exploration.effect.quests", (string)null, 1),
                ("exploration.effect.growth", (string)null, 2),
                ("exploration.effect.other", (string)null, 2), // 모양이 틀린 물건 1 + 설명할 수 없는 변화 1
            }));
            Assert.That(model.Vitals.Select(e => (e.Key, e.Number)), Is.EqualTo(new[] { ("exploration.effect.energy", -3), ("exploration.effect.stress", 2), ("exploration.effect.status", 1) }));
            Assert.That(model.RewardFacts, Is.EqualTo(1 + 1 + 2 + 1 + 1 + 1 + 2 + 2));
        }

        [Test]
        public void ResultModel_ForAFailedChoice_ShowsNoEffects()
        {
            var collector = new ExplorationOutcomeCollector();
            collector.AddItemGrant("item.apple:2");
            var failed = ExplorationResultModel.From(new ExplorationChoiceResult(ExplorationChoiceResultKind.ConditionFailed, "i", "c", "exploration.condition.trait", collector), _ => null);
            Assert.That(failed.StatusKey, Is.EqualTo("exploration.result.conditionfailed"));
            Assert.That(failed.ShowsLockedReason && failed.LockedReasonKey == "exploration.condition.trait", Is.True);
            Assert.That(failed.Rewards, Is.Empty, "적용되지 않은 결과는 보상을 보이지 않는다");
            var duplicate = ExplorationResultModel.From(new ExplorationChoiceResult(ExplorationChoiceResultKind.DuplicateRequest, "i", "c", null, collector), _ => null);
            Assert.That(duplicate.ShowsLockedReason, Is.False);
        }

        [Test]
        public void LongResult_FallsBackToTheSummary_SoItStaysInsideTheBox()
        {
            using TestSession t = OpenAtPoint();
            t.Text.Language = "en";
            var screen = t.Screen<ExplorationScreen>(ScreenCatalog.Exploration);
            t.Emit();
            int index = System.Array.FindIndex(screen.Model.Choices, c => c.Available);
            t.Click(ExplorationScreen.ChoiceId(index));
            t.Emit();
            UiRect box = t.Node(ExplorationScreen.ResultId).Rect;
            var text = new TestMeasurer();
            Assert.That(((UiTextBlock)t.Node(ExplorationScreen.ResultId)).ContentHeight(text), Is.LessThanOrEqualTo(box.H + 0.5f), "결과 글이 칸 안에 들어간다");
        }

        [Test]
        public void SwitchingLanguage_ChangesTheOpenScreenText()
        {
            using TestSession t = OpenAtPoint();
            t.Text.Language = "en";
            t.Emit();
            Assert.That(((UiLabel)t.Node(ExplorationScreen.TitleId)).Text, Is.EqualTo(t.Text.Get("Exploration Test Alley Crate")));
            Assert.That(((UiLabel)t.Node(ExplorationScreen.TitleId)).Text, Is.Not.EqualTo("골목의 낡은 상자"));
            Assert.That(((UiButton)t.Node(ExplorationScreen.CloseId)).Text, Is.EqualTo("Close"));
        }

        [Test]
        public void Text_ExistsInBothLanguages_OrIsKnownMissing()
        {
            using TestSession t = OpenAtPoint();
            var screen = t.Screen<ExplorationScreen>(ScreenCatalog.Exploration);
            foreach (string language in new[] { "ko", "en" })
            {
                t.Text.Language = language;
                t.Emit();
                for (int i = 0; i < screen.Model.Choices.Length; i++)
                    if (screen.Model.Choices[i].Available && t.Node(ExplorationScreen.ChoiceId(i)).IsActive) t.Click(ExplorationScreen.ChoiceId(i));
                if (!t.Stack.IsOpen(ScreenCatalog.Exploration)) t.Stack.Interaction(Crate);
            }
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, t.Stack.Find(ScreenCatalog.Exploration).TextKeys);
        }
    }
}
