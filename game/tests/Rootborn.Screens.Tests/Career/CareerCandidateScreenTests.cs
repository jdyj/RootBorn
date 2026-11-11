using System.Linq;
using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Screens.Career;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Career
{
    public sealed class CareerCandidateScreenTests
    {
        private static TestSession OpenFromHudButton(params (string candidate, string hint)[] reveal)
        {
            var t = new TestSession();
            foreach (var (candidate, hint) in reveal) CareerTestSupport.RevealCandidate(t, candidate, hint);
            t.Stack.OpenHud();
            t.Click("hud.button.careers");
            t.Emit();
            return t;
        }

        private static UiCardGrid List(TestSession t) => (UiCardGrid)t.Node(CareerCandidateScreen.ListId);
        private static UiScrollText Details(TestSession t) => (UiScrollText)t.Node(CareerCandidateScreen.DetailsId);
        private static void ClickRow(TestSession t, int index) => CareerTestSupport.ClickRect(t, List(t), List(t).CellRect(index));

        [Test]
        public void HudCareersButton_OpensTheScreen_WithAllEightCandidates_AndBlocksMovement()
        {
            using TestSession t = OpenFromHudButton();
            var screen = t.Screen<CareerCandidateScreen>(ScreenCatalog.CareerCandidates);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.CareerCandidates), Is.True);
            Assert.That(screen.Model.Rows.Length, Is.EqualTo(8));
            Assert.That(List(t).ItemCount, Is.EqualTo(8));
            Assert.That(((UiLabel)t.Node(CareerCandidateScreen.CountId)).Text, Is.EqualTo("진로 후보 8개"));
            Assert.That(((UiLabel)t.Node(CareerCandidateScreen.TitleId)).Text, Is.EqualTo("진로 후보"));
            Assert.That(t.Stack.MovementBlocked, Is.True, "Unity 판: 진로 후보 패널은 이동을 막는다");
        }

        [Test]
        public void Layout_FollowsUnity_ListOnTheLeftDetailsOnTheRight()
        {
            using TestSession t = OpenFromHudButton();
            UiRect panel = t.Node(CareerCandidateScreen.PanelId).Rect;
            Assert.That(panel, Is.EqualTo(new UiRect(960 - 450, 540 - 280, 900, 560)));
            UiRect list = List(t).Rect, details = Details(t).Rect, close = t.Node(CareerCandidateScreen.CloseId).Rect;
            Assert.That(list.Right, Is.LessThanOrEqualTo(details.X), "목록이 왼쪽, 상세가 오른쪽");
            Assert.That(details.W, Is.EqualTo(470f));
            foreach (UiRect r in new[] { list, details, close })
                Assert.That(r.X >= panel.X && r.Right <= panel.Right && r.Y >= panel.Y && r.Bottom <= panel.Bottom, Is.True);
            Assert.That(List(t).Columns, Is.EqualTo(1), "목록은 한 줄에 한 칸");
            UiRect r0 = List(t).CellRect(0), r1 = List(t).CellRect(1);
            Assert.That((r0.W, r0.H, r1.Y - r0.Y), Is.EqualTo((250f, 52f, 60f)), "버튼 250x52, 간격 8");
        }

        [Test]
        public void LockedCandidates_ShowTheTeaser_NotTheName_AndTheDiscoverHint()
        {
            using TestSession t = OpenFromHudButton();
            var screen = t.Screen<CareerCandidateScreen>(ScreenCatalog.CareerCandidates);
            Assert.That(screen.Model.Rows.All(r => r.State == CareerCandidateState.Locked), Is.True);
            Assert.That(screen.DetailIndex, Is.EqualTo(0));
            Assert.That(Details(t).Text, Does.Contain(t.Text.Get("ui.career-candidate.discover")));
            Assert.That(Details(t).Text, Does.Contain(t.Text.Get("ui.career-candidate.state.locked")));
            Assert.That(Details(t).Text, Does.Not.Contain(t.Text.Get("career.chef")), "잠긴 후보는 이름을 숨긴다");
        }

        [Test]
        public void ClickingARow_ShowsThatCandidatesDetail_WithoutChangingTheGame()
        {
            using TestSession t = OpenFromHudButton(("candidate.chef", "hint.chef.introduction"));
            var screen = t.Screen<CareerCandidateScreen>(ScreenCatalog.CareerCandidates);
            int chef = System.Array.FindIndex(screen.Model.Rows, r => r.CandidateId == "candidate.chef");
            Assert.That(chef, Is.GreaterThanOrEqualTo(0));
            Assert.That(screen.Model.Rows[chef].State, Is.EqualTo(CareerCandidateState.Revealed));
            string before = Details(t).Text;
            int hints = t.Session.CareerCandidates.GetHintCount(t.Session.Definitions.Get<CareerCandidate>("candidate.chef"));
            ClickRow(t, chef);
            Assert.That(screen.DetailIndex, Is.EqualTo(chef));
            Assert.That(Details(t).Text, Is.Not.EqualTo(before));
            Assert.That(Details(t).Text, Does.StartWith(t.Text.Get("career.chef")), "발견한 후보는 이름");
            Assert.That(Details(t).Text, Does.Contain(t.Text.Get("ui.career-candidate.state.revealed")));
            Assert.That(Details(t).Text, Does.Contain("단서 " + hints + "개"));
            Assert.That(t.Session.CareerCandidates.GetHintCount(t.Session.Definitions.Get<CareerCandidate>("candidate.chef")), Is.EqualTo(hints));
        }

        [Test]
        public void CloseButton_ClosesTheScreen_AndMovementIsFreeAgain()
        {
            using TestSession t = OpenFromHudButton();
            t.Click(CareerCandidateScreen.CloseId);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.CareerCandidates), Is.False);
            Assert.That(t.Stack.MovementBlocked, Is.False);
        }

        [Test]
        public void ANewHintWhileOpen_UpdatesTheListThroughTheChangeNotice()
        {
            using TestSession t = OpenFromHudButton();
            var screen = t.Screen<CareerCandidateScreen>(ScreenCatalog.CareerCandidates);
            Assert.That(screen.Model.Rows[0].State, Is.EqualTo(CareerCandidateState.Locked));
            CareerTestSupport.RevealCandidate(t, "candidate.chef", "hint.chef.introduction");
            t.Stack.NotifyChanged(ChangeTopics.Career);
            int chef = System.Array.FindIndex(screen.Model.Rows, r => r.CandidateId == "candidate.chef");
            Assert.That(screen.Model.Rows[chef].State, Is.EqualTo(CareerCandidateState.Revealed));
        }

        [Test]
        public void SwitchingLanguage_ChangesTheOpenScreenText()
        {
            using TestSession t = OpenFromHudButton();
            t.Text.Language = "en";
            t.Emit();
            Assert.That(((UiLabel)t.Node(CareerCandidateScreen.TitleId)).Text, Is.EqualTo("Career Candidates"));
            Assert.That(((UiLabel)t.Node(CareerCandidateScreen.CountId)).Text, Is.EqualTo("8 career candidates"));
        }

        [Test]
        public void Text_ExistsInBothLanguages_OrIsKnownMissing()
        {
            using TestSession t = OpenFromHudButton(("candidate.chef", "hint.chef.introduction"), ("candidate.soldier", "hint.soldier.introduction"));
            for (int i = 0; i < 5; i++) ClickRow(t, i); // 보이는 칸만 누를 수 있다
            t.Text.Language = "en";
            t.Emit();
            for (int i = 0; i < 5; i++) ClickRow(t, i); // 보이는 칸만 누를 수 있다
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, t.Stack.Find(ScreenCatalog.CareerCandidates).TextKeys);
        }

        [Test]
        public void EveryCandidateState_HasAnEnglishAndKoreanText()
        {
            using var t = new TestSession();
            foreach (CareerCandidateState state in System.Enum.GetValues(typeof(CareerCandidateState)))
            {
                string key = CareerCandidateModel.StateKey(state);
                Assert.That(t.Text.Has(key, "ko") && t.Text.Has(key, "en"), Is.True, key);
            }
        }
    }
}
