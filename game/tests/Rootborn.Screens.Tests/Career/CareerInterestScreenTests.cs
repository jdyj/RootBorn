using System.Linq;
using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Screens.Career;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Career
{
    public sealed class CareerInterestScreenTests
    {
        private static TestSession OpenFromHudButton(params (string candidate, string hint)[] reveal)
        {
            var t = new TestSession();
            foreach (var (candidate, hint) in reveal) CareerTestSupport.RevealCandidate(t, candidate, hint);
            t.Stack.OpenHud();
            t.Click("hud.button.dream");
            t.Emit();
            return t;
        }

        private static UiCardRow Cards(TestSession t) => (UiCardRow)t.Node(CareerInterestScreen.CardsId);
        private static ScrollText Details(TestSession t) => (ScrollText)t.Node(CareerInterestScreen.DetailsId);
        private static void ClickCard(TestSession t, int index) => CareerTestSupport.ClickRect(t, Cards(t), Cards(t).CardRect(index));

        [Test]
        public void HudDreamButton_OpensTheScreen_ListsTheFourInterests_AndBlocksMovement()
        {
            using TestSession t = OpenFromHudButton();
            var screen = t.Screen<CareerInterestScreen>(ScreenCatalog.CareerInterest);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.CareerInterest), Is.True);
            Assert.That(screen.Model.Cards.Select(c => c.InterestId), Is.EqualTo(new[] { "interest.chef", "interest.emergency-care", "interest.interior", "interest.soldier" }));
            Assert.That(Cards(t).CardCount, Is.EqualTo(4));
            Assert.That(t.Stack.MovementBlocked, Is.True, "Unity 판: 장래희망 패널은 이동을 막는다");
            Assert.That(((UiLabel)t.Node(CareerInterestScreen.TitleId)).Text, Is.EqualTo("내 장래희망"));
        }

        [Test]
        public void Layout_FollowsUnity_PanelCenteredCardsAboveDetails()
        {
            using TestSession t = OpenFromHudButton();
            UiRect panel = t.Node(CareerInterestScreen.PanelId).Rect;
            Assert.That(panel, Is.EqualTo(new UiRect(960 - 720, 540 - 420, 1440, 840)));
            UiRect cards = Cards(t).Rect, details = Details(t).Rect, close = t.Node(CareerInterestScreen.CloseId).Rect;
            Assert.That((cards.W, cards.H), Is.EqualTo((1344f, 314f)));
            Assert.That(cards.Bottom, Is.LessThanOrEqualTo(details.Y), "카드 줄이 상세 위");
            Assert.That(details.W, Is.EqualTo(1344f));
            Assert.That(close.Right, Is.LessThanOrEqualTo(panel.Right));
            Assert.That(close.Y, Is.LessThan(cards.Y), "닫기는 맨 위");
            UiRect c0 = Cards(t).CardRect(0), c1 = Cards(t).CardRect(1);
            Assert.That((c0.W, c1.X - c0.X), Is.EqualTo((300f, 316f)), "카드는 가로로 나란히(폭 300, 간격 16)");
        }

        [Test]
        public void FreshGame_AllInterestsAreLocked_AndTheDetailExplainsWhy()
        {
            using TestSession t = OpenFromHudButton();
            var screen = t.Screen<CareerInterestScreen>(ScreenCatalog.CareerInterest);
            Assert.That(screen.Model.Cards.All(c => c.State == CareerInterestSelectionState.LockedByCondition), Is.True);
            Assert.That(screen.DetailIndex, Is.EqualTo(0));
            Assert.That(Details(t).Text, Does.Contain(t.Text.Get("ui.interest-state.locked")));
            Assert.That(Details(t).Text, Does.Contain(t.Text.Get("dream.ui.locked-reason")), "조건 때문에 잠겼다는 안내");
            ClickCard(t, 1);
            Assert.That(t.Session.CareerInterests.CurrentInterestId, Is.Empty, "잠긴 카드는 고를 수 없다");
            Assert.That(screen.DetailIndex, Is.EqualTo(1), "눌렀으니 상세는 그 카드를 보인다");
            Assert.That(Details(t).Text, Does.StartWith(t.Text.Get("career.emergency-care")));
        }

        [Test]
        public void ClickingARevealedCard_SelectsIt_AndSecondChangeSameDayShowsTheLimit()
        {
            using TestSession t = OpenFromHudButton(("candidate.chef", "hint.chef.introduction"), ("candidate.interior", "hint.interior.introduction"));
            var screen = t.Screen<CareerInterestScreen>(ScreenCatalog.CareerInterest);
            Assert.That(screen.Model.Cards[0].StateKey, Is.EqualTo("ui.interest-state.available"), "단서를 얻으면 선택 가능");
            Assert.That(screen.Model.Cards[0].IsCurrent, Is.False);

            ClickCard(t, 0);
            Assert.That(t.Session.CareerInterests.CurrentInterestId, Is.EqualTo("interest.chef"), "GameSession 상태가 바뀌었다");
            Assert.That(screen.LastResult?.Applied, Is.True);
            Assert.That(screen.Model.Cards[0].IsCurrent, Is.True);
            Assert.That(screen.Model.Cards[0].StateKey, Is.EqualTo("ui.interest-state.selected"));
            Assert.That(screen.Model.Cards[2].StateKey, Is.EqualTo("ui.interest-state.changed"), "같은 날 다른 희망은 '오늘 변경함'");
            Assert.That(Details(t).Text, Does.Contain(t.Text.Get("ui.interest-state.selected")));

            ClickCard(t, 2);
            Assert.That(t.Session.CareerInterests.CurrentInterestId, Is.EqualTo("interest.chef"), "바뀌지 않는다");
            Assert.That(screen.LastResult?.Applied, Is.False);
            Assert.That(screen.LastResult?.State, Is.EqualTo(CareerInterestSelectionState.ChangedToday));
            Assert.That(screen.DetailIndex, Is.EqualTo(2));
            Assert.That(Details(t).Text, Does.Contain(t.Text.Get("ui.interest-state.changed")), "제한 이유가 상세에 보인다");
        }

        [Test]
        public void NextDay_TheLimitedChangeIsAllowedAgain()
        {
            using TestSession t = OpenFromHudButton(("candidate.chef", "hint.chef.introduction"), ("candidate.interior", "hint.interior.introduction"));
            ClickCard(t, 0);
            ClickCard(t, 2);
            Assert.That(t.Session.CareerInterests.CurrentInterestId, Is.EqualTo("interest.chef"));
            // 준비 단계: 하루를 끝내고 다음 날을 시작한다(GameSession 명령). 화면은 알림을 받아 상태를 다시 읽는다.
            Assert.That(t.Session.EndDay().Ended, Is.True);
            Assert.That(t.Session.StartNextDay(), Is.True);
            t.Stack.NotifyChanged(ChangeTopics.Day);
            t.Emit();
            Assert.That(t.Session.StudentDay, Is.EqualTo(2));
            ClickCard(t, 2);
            Assert.That(t.Session.CareerInterests.CurrentInterestId, Is.EqualTo("interest.interior"));
        }

        [Test]
        public void CloseButton_ClosesTheScreen_AndMovementIsFreeAgain()
        {
            using TestSession t = OpenFromHudButton();
            t.Click(CareerInterestScreen.CloseId);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.CareerInterest), Is.False);
            Assert.That(t.Stack.MovementBlocked, Is.False);
        }

        [Test]
        public void ReopeningTheScreen_ShowsTheFirstCardDetailAgain()
        {
            using TestSession t = OpenFromHudButton(("candidate.chef", "hint.chef.introduction"));
            ClickCard(t, 3);
            var screen = t.Screen<CareerInterestScreen>(ScreenCatalog.CareerInterest);
            Assert.That(screen.DetailIndex, Is.EqualTo(3));
            t.Click(CareerInterestScreen.CloseId);
            t.Click("hud.button.dream");
            Assert.That(screen.DetailIndex, Is.EqualTo(0));
        }

        [Test]
        public void SwitchingLanguage_ChangesTheOpenScreenText()
        {
            using TestSession t = OpenFromHudButton();
            t.Text.Language = "en";
            t.Emit();
            Assert.That(((UiLabel)t.Node(CareerInterestScreen.TitleId)).Text, Is.EqualTo("My Dream"));
            Assert.That(Details(t).Text, Does.Contain("Requirements needed"));
        }

        [Test]
        public void Text_ExistsInBothLanguages_OrIsKnownMissing()
        {
            using TestSession t = OpenFromHudButton(("candidate.chef", "hint.chef.introduction"));
            ClickCard(t, 0);
            ClickCard(t, 1);
            t.Text.Language = "en";
            t.Emit();
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, t.Stack.Find(ScreenCatalog.CareerInterest).TextKeys);
        }

        [Test]
        public void LongText_DoesNotLeaveThePanel_AndTheDetailScrollsInstead()
        {
            using TestSession t = OpenFromHudButton();
            UiRect panel = t.Node(CareerInterestScreen.PanelId).Rect;
            foreach (string id in new[] { CareerInterestScreen.TitleId, CareerInterestScreen.CardsId, CareerInterestScreen.DetailsId, CareerInterestScreen.CloseId })
            {
                UiRect r = t.Node(id).Rect;
                Assert.That(r.X >= panel.X && r.Right <= panel.Right && r.Y >= panel.Y && r.Bottom <= panel.Bottom, Is.True, id);
            }
            // 긴 글: 상세 영역 안에서 휠로 넘긴다.
            Details(t).Text = string.Join("\n", Enumerable.Repeat("긴 설명 한 줄 긴 설명 한 줄 긴 설명 한 줄", 40));
            t.Emit();
            Assert.That(Details(t).MaxScroll, Is.GreaterThan(0));
            UiRect d = Details(t).Rect;
            t.Stack.Pointer(d.CenterX, d.CenterY, false, -1);
            Assert.That(Details(t).ScrollOffset, Is.GreaterThan(0), "휠로 넘어간다");
        }
    }
}
