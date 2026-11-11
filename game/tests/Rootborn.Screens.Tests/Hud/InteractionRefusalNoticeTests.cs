using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Interaction;
using Rootborn.Core.StudentLife;
using Rootborn.Screens.Hud;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Hud
{
    /// <summary>
    /// 상호작용 실패 안내(Unity PlayerInteractionRouter의 실패 이유 2.5초): 일터·진로 연습 지점에서 명령이 거절되면 알림 층에 이유를 보인다.
    /// 이유는 명령의 결과 종류와 Core 판정(StudentActivityRefusal)에서 온다. 창 없음, 자동 테스트(화면 근거 아님).
    /// </summary>
    public sealed class InteractionRefusalNoticeTests
    {
        private const string Work = "work.shift.corner-store";
        private const string Practice = "practice.chef";

        private static InteractionEvent WorkPoint() => new InteractionEvent("Town", "part-time-work", Work);
        private static InteractionEvent PracticePoint() => new InteractionEvent("Town", "career-practice", Practice);

        /// <summary>
        /// 준비: 지치게 한다(이웃 돕기 되풀이 — 체력 0). 흐르는 시간에서는 시간 포인트로 막지 않는다(설계 2026-11-12-flowing-time-design.md 3.2) —
        /// 하루에 할 수 있는 것을 막는 것은 체력·지침이다. 예전 준비("오늘 시간을 다 쓴다")와 같은 것(거절 이유가 알림 층에 뜬다)을 지킨다.
        /// </summary>
        private static void UseAllTime(TestSession t)
        {
            for (int n = 0; !t.Session.IsExhausted && n < 40; n++) t.Session.PerformOutsideSchoolActivity("outside.help-neighbor", "refusal-exhaust:" + n);
            Assert.That(t.Session.IsExhausted, Is.True, "준비: 지쳤다");
        }

        private static string Message(TestSession t) => ((UiLabel)t.Node(InteractionRefusalNoticeScreen.MessageId)).Text;

        [Test]
        public void WorkPoint_WhileExhausted_ShowsWhyOnTheNoticeLayer_AndChangesNothing()
        {
            using var t = new TestSession();
            UseAllTime(t);
            int energy = t.Session.Student.Energy;

            t.Stack.Interaction(WorkPoint());

            Assert.That(t.Session.Student.HasPendingMinigame, Is.False, "거절되면 예약하지 않는다");
            Assert.That(t.Session.Student.Energy, Is.EqualTo(energy));
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Minigame), Is.False);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.InteractionRefusal), Is.True, "이유를 알린다");
            var notice = t.Screen<InteractionRefusalNoticeScreen>(ScreenCatalog.InteractionRefusal);
            Assert.That(notice.Layer, Is.EqualTo(ScreenLayer.Notice));
            Assert.That(t.Stack.MovementBlocked, Is.False, "알림이 떠 있어도 걷는다");
            Assert.That(notice.MessageKey, Is.EqualTo("ui.prompt.exhausted"));
            t.Emit();
            Assert.That(Message(t), Is.EqualTo(t.Text.Get("ui.prompt.exhausted")));
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, notice.TextKeys);
        }

        [Test]
        public void PracticePoint_WhileExhausted_ShowsWhy()
        {
            using var t = new TestSession();
            UseAllTime(t);
            t.Stack.Interaction(PracticePoint());
            Assert.That(t.Session.Student.HasPendingMinigame, Is.False);
            Assert.That(t.Screen<InteractionRefusalNoticeScreen>(ScreenCatalog.InteractionRefusal).MessageKey, Is.EqualTo("ui.prompt.exhausted"));
        }

        [Test]
        public void WorkPoint_ThatWorks_ShowsNoRefusal()
        {
            using var t = new TestSession();
            t.Stack.Interaction(WorkPoint());
            Assert.That(t.Session.Student.HasPendingMinigame, Is.True);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.InteractionRefusal), Is.False);
        }

        [Test]
        public void Notice_ClosesAfterUnitysTwoAndAHalfSeconds_AndPressingAgainRestartsIt()
        {
            using var t = new TestSession();
            UseAllTime(t);
            t.Stack.Interaction(WorkPoint());
            var notice = t.Screen<InteractionRefusalNoticeScreen>(ScreenCatalog.InteractionRefusal);
            Assert.That(notice.RemainingSeconds, Is.EqualTo(InteractionRefusalNoticeScreen.ShowSeconds).Within(1e-4f));
            Assert.That(InteractionRefusalNoticeScreen.ShowSeconds, Is.EqualTo(2.5f), "Unity FailureMessageSeconds");
            t.Stack.Tick(2f);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.InteractionRefusal), Is.True);
            t.Stack.Interaction(WorkPoint()); // 다시 누르면 처음부터 2.5초
            t.Stack.Tick(2f);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.InteractionRefusal), Is.True);
            t.Stack.Tick(0.6f);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.InteractionRefusal), Is.False);
        }

        [Test]
        public void LanguageChange_ChangesTheShownReason()
        {
            using var t = new TestSession();
            UseAllTime(t);
            t.Stack.Interaction(WorkPoint());
            t.Emit();
            string ko = Message(t);
            t.Text.Language = "en";
            t.Emit();
            Assert.That(Message(t), Is.Not.EqualTo(ko));
            Assert.That(Message(t), Is.EqualTo(t.Text.Get("ui.prompt.exhausted")));
        }

        [TestCase(AdultCareerResultKind.Applied, StudentActivityRefusalReason.None, null)]
        [TestCase(AdultCareerResultKind.DuplicateRequest, StudentActivityRefusalReason.None, null)]
        [TestCase(AdultCareerResultKind.InvalidDefinition, StudentActivityRefusalReason.None, null)]
        [TestCase(AdultCareerResultKind.InsufficientTime, StudentActivityRefusalReason.NoEnergy, "ui.prompt.no-energy")]
        [TestCase(AdultCareerResultKind.InsufficientTime, StudentActivityRefusalReason.NotToday, "ui.prompt.not-today")]
        [TestCase(AdultCareerResultKind.InsufficientResources, StudentActivityRefusalReason.Exhausted, "ui.prompt.exhausted")]
        [TestCase(AdultCareerResultKind.RequirementFailed, StudentActivityRefusalReason.None, "ui.prompt.requirements-unmet")]
        [TestCase(AdultCareerResultKind.InsufficientCapacity, StudentActivityRefusalReason.None, "ui.prompt.bag-full")]
        public void ReasonKey_ComesFromTheResultKindAndCoresRefusal(AdultCareerResultKind kind, StudentActivityRefusalReason reason, string expected)
        {
            Assert.That(InteractionRefusals.KeyFor(kind, reason), Is.EqualTo(expected));
        }

        [TestCase(LifeActivityResultKind.Applied, StudentActivityRefusalReason.None, null)]
        [TestCase(LifeActivityResultKind.InsufficientResources, StudentActivityRefusalReason.NoTime, "ui.prompt.no-time")]
        [TestCase(LifeActivityResultKind.InsufficientResources, StudentActivityRefusalReason.Exhausted, "ui.prompt.exhausted")]
        [TestCase(LifeActivityResultKind.InsufficientResources, StudentActivityRefusalReason.None, "ui.prompt.cannot-now")]
        [TestCase(LifeActivityResultKind.RequirementFailed, StudentActivityRefusalReason.None, "ui.prompt.requirements-unmet")]
        [TestCase(LifeActivityResultKind.DuplicateRequest, StudentActivityRefusalReason.None, null)]
        public void ReasonKey_ForActivitiesWithoutAMinigame(LifeActivityResultKind kind, StudentActivityRefusalReason reason, string expected)
        {
            Assert.That(InteractionRefusals.KeyFor(kind, reason), Is.EqualTo(expected));
        }
    }
}
