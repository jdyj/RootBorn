using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Interaction;
using Rootborn.Core.StudentLife;
using Rootborn.Screens.Discoveries;
using Rootborn.Screens.Hud;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Discoveries
{
    /// <summary>
    /// 발견 지점(상호작용 종류 "discovery") → 알림(창 없음, 자동 테스트, 화면 근거 아님). 실제 data/의 발견 정의 전부를 쓴다
    /// (지금 하나: discovery.clocktower-notice — 관찰력 +2). 한 번만 적용되는지는 Core가 정하고, 여기서는 화면이 그 결과를 보이는지 본다.
    /// </summary>
    public sealed class DiscoveryNoticeScreenTests
    {
        private static Discovery FirstDiscovery()
        {
            Discovery discovery = TestSession.Definitions.All<Discovery>().FirstOrDefault(d => d.Outcomes.Count > 0);
            Assert.That(discovery, Is.Not.Null, "data/에 결과가 있는 발견 정의가 있어야 한다");
            return discovery;
        }

        private static InteractionEvent At(Discovery d) => new InteractionEvent("Town", DiscoveryNoticeScreen.InteractionKind, d.Id);

        [Test]
        public void Interaction_OpensTheNoticeOnTheNoticeLayer_AppliesTheDiscovery_AndShowsWhatChanged()
        {
            using var t = new TestSession();
            Discovery discovery = FirstDiscovery();
            var trait = (DiscoveryTraitDeltaOutcome)discovery.Outcomes.First(o => o is DiscoveryTraitDeltaOutcome);
            int before = t.Session.Student.GetTraitValue(trait.TraitId);

            Assert.That(t.Stack.Interaction(At(discovery)), Is.True, "등록 표가 discovery 종류로 알림을 연다");
            t.Emit();
            var notice = t.Screen<DiscoveryNoticeScreen>(ScreenCatalog.DiscoveryNotice);
            Assert.That(notice.Layer, Is.EqualTo(ScreenLayer.Notice));
            Assert.That(t.Stack.MovementBlocked, Is.False, "알림이 떠 있어도 걷는다");
            Assert.That(notice.Model.Applied, Is.True);
            Assert.That(t.Session.Student.GetTraitValue(trait.TraitId), Is.EqualTo(before + trait.Delta));
            Assert.That(((UiLabel)t.Node(DiscoveryNoticeScreen.NameId)).Text, Is.EqualTo(t.Text.Get(discovery.DisplayNameKey)));
            Assert.That(((UiLabel)t.Node(DiscoveryNoticeScreen.MessageId)).Text, Is.EqualTo(t.Text.Get("ui.discovery.found")));
            Assert.That(((UiLabel)t.Node(DiscoveryNoticeScreen.ChangeId(0))).Text,
                Is.EqualTo(t.Text.Format("ui.discovery.change", t.Text.Get(trait.TraitId), "+" + trait.Delta)));
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, notice.TextKeys);
        }

        [Test]
        public void SecondLook_SaysAlreadyLooked_AndChangesNothing_EvenAfterReloading()
        {
            using var t = new TestSession();
            Discovery discovery = FirstDiscovery();
            t.Stack.Interaction(At(discovery));
            var trait = (DiscoveryTraitDeltaOutcome)discovery.Outcomes.First(o => o is DiscoveryTraitDeltaOutcome);
            int once = t.Session.Student.GetTraitValue(trait.TraitId);

            t.Stack.Interaction(At(discovery));
            var notice = t.Screen<DiscoveryNoticeScreen>(ScreenCatalog.DiscoveryNotice);
            Assert.That(notice.Model.Applied, Is.False);
            Assert.That(((UiLabel)t.Node(DiscoveryNoticeScreen.MessageId)).Text, Is.EqualTo(t.Text.Get("ui.discovery.already")));
            Assert.That(t.Node(DiscoveryNoticeScreen.ChangeId(0)).Visible, Is.False);
            Assert.That(t.Session.Student.GetTraitValue(trait.TraitId), Is.EqualTo(once));

            using TestSession again = t.Reopen();
            Assert.That(again.Session.Student.GetTraitValue(trait.TraitId), Is.EqualTo(once), "저장 뒤 다시 읽어도 한 번만");
            again.Stack.Interaction(At(discovery));
            Assert.That(again.Screen<DiscoveryNoticeScreen>(ScreenCatalog.DiscoveryNotice).Model.MessageKey, Is.EqualTo("ui.discovery.already"));
            Assert.That(again.Session.Student.GetTraitValue(trait.TraitId), Is.EqualTo(once));
        }

        [Test]
        public void Notice_ClosesByItselfAfterItsTime_OnlyThroughTick()
        {
            using var t = new TestSession();
            t.Stack.Interaction(At(FirstDiscovery()));
            var notice = t.Screen<DiscoveryNoticeScreen>(ScreenCatalog.DiscoveryNotice);
            float total = notice.RemainingSeconds;
            Assert.That(total, Is.GreaterThan(0f));
            t.Stack.Tick(total / 2f);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.DiscoveryNotice), Is.True);
            t.Stack.Tick(total);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.DiscoveryNotice), Is.False);
        }

        [Test]
        public void Notice_DoesNotTakePointerInput()
        {
            using var t = new TestSession();
            t.Stack.Interaction(At(FirstDiscovery()));
            t.Emit();
            UiRect box = t.Node(DiscoveryNoticeScreen.BoxId).Rect;
            UiNode root = t.Node(DiscoveryNoticeScreen.RootId);
            for (UiNode hit = t.Stack.Tree.HitTest(box.CenterX, box.CenterY); hit != null; hit = hit.Parent)
                Assert.That(hit, Is.Not.SameAs(root), "알림은 포인터를 가져가지 않는다(아래 화면·월드가 받는다)");
        }

        [Test]
        public void English_HasEveryKey_AndTheTextFollowsALanguageChange()
        {
            using var t = new TestSession("en");
            t.Stack.Interaction(At(FirstDiscovery()));
            var notice = t.Screen<DiscoveryNoticeScreen>(ScreenCatalog.DiscoveryNotice);
            string english = ((UiLabel)t.Node(DiscoveryNoticeScreen.MessageId)).Text;
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, notice.TextKeys);
            t.Text.Language = "ko";
            t.Stack.NotifyChanged(ChangeTopics.Language);
            Assert.That(((UiLabel)t.Node(DiscoveryNoticeScreen.MessageId)).Text, Is.Not.EqualTo(english));
        }

        [Test]
        public void Prompt_InFrontOfADiscoveryPoint_ShowsTheDiscoveryName()
        {
            using var t = new TestSession();
            Discovery discovery = FirstDiscovery();
            t.Stack.OpenHud();
            t.Stack.SetInteractionTarget(new InteractionTargetView(DiscoveryNoticeScreen.InteractionKind, discovery.Id, 960f, 540f, 32f));
            var prompt = t.Screen<InteractionPromptScreen>(ScreenCatalog.HudPrompt);
            Assert.That(prompt.Text, Does.Contain(t.Text.Get(discovery.DisplayNameKey)));
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, prompt.TextKeys);
        }

        [Test]
        public void OpenedWithoutAnInteraction_ChangesNothing()
        {
            using var t = new TestSession();
            Discovery discovery = FirstDiscovery();
            t.Stack.Open(ScreenCatalog.DiscoveryNotice);
            Assert.That(t.Session.Student.HasAppliedRequest(DiscoveryRunner.RequestIdFor(discovery)), Is.False);
        }
    }
}
