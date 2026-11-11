using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game;
using Rootborn.Screens.Start;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Start
{
    /// <summary>
    /// 손상된 슬롯 알림(창 없음, 자동 테스트): 시작이 SaveSlotDamagedException으로 거절되면 Host가 슬롯 선택 위에 이 모달을 연다.
    /// 문제 종류마다 현지화 키, 파일 이름은 자리 표시 값. 단추는 돌아가기 하나(새 게임으로 덮기·사본에서 되살리기는 기획 결정 대기).
    /// </summary>
    public sealed class SaveSlotDamagedScreenTests
    {
        private static SaveSlotDamagedException Damage(params SaveSlotProblem[] problems)
            => new SaveSlotDamagedException("slot-1", problems, new[] { "metadata.json" });

        private static SaveSlotDamagedScreen Open(StartMenuFixture t, SaveSlotDamagedException error)
        {
            t.OpenSlots();
            t.Stack.Open(ScreenCatalog.SaveSlotDamaged, argument: error);
            t.Emit();
            return t.Screen<SaveSlotDamagedScreen>(ScreenCatalog.SaveSlotDamaged);
        }

        [Test]
        public void Model_GivesOneLinePerProblem_WithAKeyForItsKind_AndTheFileNameAsAValue()
        {
            var error = Damage(new SaveSlotProblem("adult-career-snapshot.json", SaveSlotProblemKind.Missing),
                new SaveSlotProblem("campaign.json", SaveSlotProblemKind.Unreadable, new IOException("깨짐")));
            SaveSlotDamagedModel model = SaveSlotDamagedModel.From(error);
            Assert.That(model.SlotId, Is.EqualTo("slot-1"));
            Assert.That(model.Problems.Select(p => p.Key), Is.EqualTo(new[] { "ui.save.damaged.missing", "ui.save.damaged.unreadable" }));
            Assert.That(model.Problems.Select(p => p.FileName), Is.EqualTo(new[] { "adult-career-snapshot.json", "campaign.json" }));
        }

        [Test]
        public void Screen_IsAModalOverTheSlotSelect_ListsTheProblems_AndOnlyOffersGoingBack()
        {
            using var t = new StartMenuFixture();
            t.WriteStory("slot-1", "하늘");
            Open(t, Damage(new SaveSlotProblem("adult-career-snapshot.json", SaveSlotProblemKind.Missing),
                new SaveSlotProblem("campaign.json", SaveSlotProblemKind.Unreadable, new IOException("깨짐"))));

            Assert.That(t.Stack.Top.Id, Is.EqualTo(ScreenCatalog.SaveSlotDamaged));
            Assert.That(t.Stack.Top.Layer, Is.EqualTo(ScreenLayer.Modal));
            Assert.That(t.Stack.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True, "아래에 슬롯 선택이 남는다");
            Assert.That(t.LabelText(SaveSlotDamagedScreen.TitleId), Is.EqualTo(t.Text.Get("ui.save.damaged.title")));
            Assert.That(((UiTextBlock)t.Node(SaveSlotDamagedScreen.BodyId)).Text, Is.EqualTo(t.Text.Format("ui.save.damaged.body", "slot-1")));
            Assert.That(t.LabelText(SaveSlotDamagedScreen.ProblemId(0)), Is.EqualTo(t.Text.Format("ui.save.damaged.missing", "adult-career-snapshot.json")));
            Assert.That(t.LabelText(SaveSlotDamagedScreen.ProblemId(1)), Is.EqualTo(t.Text.Format("ui.save.damaged.unreadable", "campaign.json")));
            Assert.That(t.Node(SaveSlotDamagedScreen.ProblemId(2)).Visible, Is.False);
            Assert.That(t.Node(SaveSlotDamagedScreen.MoreId).Visible, Is.False);

            var buttons = Walk(t.Node(SaveSlotDamagedScreen.RootId)).OfType<UiButton>().Where(b => b.IsActive).ToList();
            Assert.That(buttons.Select(b => b.Id), Is.EqualTo(new[] { SaveSlotDamagedScreen.BackId }), "단추는 돌아가기 하나뿐");
            Assert.That(buttons[0].Text, Is.EqualTo(t.Text.Get("ui.save.damaged.back")));
            Assert.That(t.Stack.Tree.Focused, Is.SameAs(buttons[0]), "열면 돌아가기에 포커스");
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, t.Stack.Top.TextKeys);
        }

        [Test]
        public void Back_ClosesTheNotice_AndTheSlotSelectTakesInputAgain()
        {
            using var t = new StartMenuFixture();
            t.WriteStory("slot-1", "하늘");
            Open(t, Damage(new SaveSlotProblem("campaign.json", SaveSlotProblemKind.Unreadable)));
            t.Click(SaveSlotDamagedScreen.BackId);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.SaveSlotDamaged), Is.False);
            Assert.That(t.Stack.Top.Id, Is.EqualTo(ScreenCatalog.SaveSlotSelect));
            t.Click(SaveSlotScreen.ContinueId(1));
            Assert.That(t.Entries.Single().SlotId, Is.EqualTo("slot-1"), "닫은 뒤 슬롯 칸이 다시 눌린다");
        }

        [Test]
        public void ManyProblems_ShowTheFirstFew_AndHowManyMore()
        {
            using var t = new StartMenuFixture();
            var problems = Enumerable.Range(0, SaveSlotDamagedScreen.MaxLines + 3)
                .Select(i => new SaveSlotProblem("file-" + i + ".json", SaveSlotProblemKind.Unreadable)).ToArray();
            Open(t, Damage(problems));
            for (int i = 0; i < SaveSlotDamagedScreen.MaxLines; i++)
                Assert.That(t.LabelText(SaveSlotDamagedScreen.ProblemId(i)), Does.Contain("file-" + i + ".json"));
            Assert.That(t.LabelText(SaveSlotDamagedScreen.MoreId), Is.EqualTo(t.Text.Format("ui.save.damaged.more", 3)));
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, t.Stack.Top.TextKeys);
        }

        [Test]
        public void English_HasEveryKey_AndChangingLanguageRewritesTheNotice()
        {
            using var t = new StartMenuFixture("en");
            Open(t, Damage(new SaveSlotProblem("campaign.json", SaveSlotProblemKind.Missing)));
            string english = t.LabelText(SaveSlotDamagedScreen.TitleId);
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, t.Stack.Top.TextKeys);
            t.Text.Language = "ko";
            t.Stack.NotifyChanged(ChangeTopics.Language);
            Assert.That(t.LabelText(SaveSlotDamagedScreen.TitleId), Is.Not.EqualTo(english));
            Assert.That(t.LabelText(SaveSlotDamagedScreen.TitleId), Is.EqualTo(t.Text.Get("ui.save.damaged.title")));
        }

        [Test]
        public void ContinueThatOpensTheNotice_DoesNotAlsoShowTheGenericEntryFailure()
        {
            using var t = new StartMenuFixture();
            t.WriteStory("slot-1", "하늘");
            var error = Damage(new SaveSlotProblem("campaign.json", SaveSlotProblemKind.Unreadable));
            // Host처럼: 들어가기가 손상으로 거절되면 알림을 열고 false를 돌려준다.
            var menu = new StartMenu(t.Menu.Slots, t.Looks, t.Art, e => { t.Stack.Open(ScreenCatalog.SaveSlotDamaged, argument: error); return false; });
            t.Stack.Open(ScreenCatalog.SaveSlotSelect, argument: menu);
            t.Emit();
            t.Click(SaveSlotScreen.ContinueId(1));
            Assert.That(t.Stack.IsOpen(ScreenCatalog.SaveSlotDamaged), Is.True);
            Assert.That(t.Node(SaveSlotScreen.ErrorId).Visible, Is.False, "알림이 문제를 말하므로 일반 실패 문구는 겹쳐 보이지 않는다");
            t.Click(SaveSlotDamagedScreen.BackId);
            Assert.That(t.Node(SaveSlotScreen.ErrorId).Visible, Is.False);
        }

        [Test]
        public void OpenedWithoutADamageReport_ShowsNoProblemLines_AndDoesNotThrow()
        {
            using var t = new StartMenuFixture();
            Assert.DoesNotThrow(() => Open(t, null));
            Assert.That(t.Node(SaveSlotDamagedScreen.ProblemId(0)).Visible, Is.False);
        }

        private static System.Collections.Generic.IEnumerable<UiNode> Walk(UiNode root)
        {
            yield return root;
            foreach (UiNode child in root.Children)
                foreach (UiNode n in Walk(child)) yield return n;
        }
    }
}
