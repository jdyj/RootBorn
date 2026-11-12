using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Screens.AdultFacility;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.AdultFacility
{
    /// <summary>
    /// 성인 시설 관리 화면. 실제 data/와 GameSession으로, 요리사 후보가 준비된 새 게임 위에서 눌러 본다. 자동 테스트다(화면 근거가 아니다).
    /// </summary>
    public sealed class FacilityScreenTests
    {
        private static string Label(UiNode node) => ((UiButton)node).Text;

        [Test]
        public void Interaction_OpensTheScreen_ByKind_AndBlocksMovement()
        {
            using var f = new FacilityFixture();
            Assert.That(f.OpenAt(), Is.True);
            f.Emit();
            Assert.That(f.Stack.IsOpen(ScreenCatalog.Facility), Is.True);
            Assert.That(f.Stack.MovementBlocked, Is.True, "Unity 판: 시설 패널은 이동을 막는다(UiGameplayInputLock)");
            Assert.That(f.Screen.Model.FacilityId, Is.EqualTo(FacilityFixture.Chef));
            Assert.That(f.Screen.Model.Works.Select(w => w.Id), Is.EqualTo(new[] { "work.chef.level1", "work.chef.level2", "work.chef.level3" }));
            Assert.That(f.Screen.WorkGrid.ItemCount, Is.EqualTo(9), "일마다 칸 셋(정보, 직접 일하기, 자동 완료)");
        }

        [Test]
        public void UnknownFacility_DoesNotStayOpen()
        {
            using var f = new FacilityFixture();
            f.OpenAt("facility.nothing");
            Assert.That(f.Stack.IsOpen(ScreenCatalog.Facility), Is.False);
            Assert.That(f.Stack.MovementBlocked, Is.False);
        }

        [Test]
        public void BeforeChoosing_ShowsTheRouteHint_AndTheSelectButton()
        {
            using var f = new FacilityFixture();
            f.OpenAt();
            f.Emit();
            Assert.That(((UiTextBlock)f.Node(FacilityScreen.SummaryId)).Text, Does.Contain(f.Text.Get("facility.choose-route")));
            Assert.That(((UiLabel)f.Node(FacilityScreen.TitleId)).Text, Is.EqualTo(f.Text.Get("facility.chef.name")));
            Assert.That(((UiTextBlock)f.Node(FacilityScreen.CostId)).Text, Is.EqualTo(f.Text.Get("facility.transition-requirements")));
            Assert.That(Label(f.Node(FacilityScreen.TransitionId)), Is.EqualTo(f.Text.Get("facility.transition")));
            Assert.That(Label(f.Node(FacilityScreen.UpgradeId)), Is.EqualTo(f.Text.Get("facility.upgrade")));
        }

        [Test]
        public void Layout_FollowsUnity_PanelCentered_AndNothingOverlaps()
        {
            using var f = new FacilityFixture();
            f.OpenAt();
            f.Emit();
            UiRect panel = f.Node(FacilityScreen.RootId).Rect;
            Assert.That(panel, Is.EqualTo(new UiRect(960 - 440, 540 - 340, 880, 680)));
            UiRect title = f.Node(FacilityScreen.TitleId).Rect, close = f.Node(FacilityScreen.CloseId).Rect, summary = f.Node(FacilityScreen.SummaryId).Rect,
                works = f.Node(FacilityScreen.WorksId).Rect, cost = f.Node(FacilityScreen.CostId).Rect, status = f.Node(FacilityScreen.StatusId).Rect;
            UiRect[] buttons = { f.Node(FacilityScreen.TransitionId).Rect, f.Node(FacilityScreen.PracticeId).Rect, f.Node(FacilityScreen.UpgradeId).Rect };
            Assert.That(title.Right, Is.LessThanOrEqualTo(close.X), "닫기는 제목 옆");
            Assert.That(summary.Y, Is.GreaterThanOrEqualTo(title.Bottom));
            Assert.That(works.Y, Is.GreaterThanOrEqualTo(summary.Bottom));
            Assert.That(cost.Y, Is.GreaterThanOrEqualTo(works.Bottom));
            Assert.That(buttons[0].Y, Is.GreaterThanOrEqualTo(cost.Bottom));
            Assert.That(status.Y, Is.GreaterThanOrEqualTo(buttons[0].Bottom));
            Assert.That(buttons[0].Right, Is.LessThanOrEqualTo(buttons[1].X));
            Assert.That(buttons[1].Right, Is.LessThanOrEqualTo(buttons[2].X));
            foreach (UiRect r in new[] { title, close, summary, works, cost, status, buttons[0], buttons[2] })
                Assert.That(r.X >= panel.X && r.Right <= panel.Right && r.Y >= panel.Y && r.Bottom <= panel.Bottom, Is.True, r.ToString());
        }

        /// <summary>
        /// 일 목록(스크롤 막대 포함)은 다른 줄(요약·비용)과 같은 글 칸 안에 들어간다 — 스크롤 막대가 패널 여백·테두리에 겹치지 않게.
        /// 칸 안 글자(정보 칸, 자동 완료 칸의 제목·안내)는 칸 가장자리에서 정보 칸과 같은 만큼(8px) 이상 떨어진다.
        /// </summary>
        [Test]
        public void WorkList_StaysInTheTextColumn_AndCellTextKeepsItsMargin()
        {
            using var f = new FacilityFixture();
            f.OpenAt();
            f.Click(FacilityScreen.TransitionId); // 준비: 진로를 골라 일 목록을 채운다
            f.Emit();
            DrawList list = f.Emit();
            UiRect works = f.Node(FacilityScreen.WorksId).Rect, summary = f.Node(FacilityScreen.SummaryId).Rect;
            Assert.That(works.X, Is.EqualTo(summary.X));
            Assert.That(works.Right, Is.LessThanOrEqualTo(summary.Right), "스크롤 막대까지 글 칸 안: 목록 " + works + " / 요약 " + summary);
            Assert.That(f.Screen.Model.Works.Length, Is.GreaterThan(0));

            var measurer = new TestMeasurer();
            const float margin = 8f;
            var problems = new List<string>();
            for (int cell = 0; cell < f.Screen.Model.Works.Length * 3; cell++)
            {
                UiRect r = f.Screen.CellRect(cell);
                if (r.Bottom > works.Bottom || r.Y < works.Y) continue; // 보이는 칸만
                Assert.That(r.Right, Is.LessThanOrEqualTo(works.Right - UiCardScroller.ScrollbarWidth - UiCardScroller.ScrollbarGap + 0.01f), "칸은 스크롤 막대 자리 왼쪽");
                foreach (DrawCommand c in list.Commands)
                {
                    if (c.Kind != DrawKind.Text || c.Rect.X < r.X || c.Rect.X > r.Right || c.Rect.Y < r.Y || c.Rect.Y > r.Bottom) continue;
                    float width = measurer.Width(c.Text, c.FontSize);
                    if (c.Rect.X < r.X + margin - 0.01f || c.Rect.X + width > r.Right - margin + 0.01f)
                        problems.Add("칸 " + cell + " " + r + ": '" + c.Text + "' x " + c.Rect.X + "~" + (c.Rect.X + width));
                }
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void Closing_ByButton_ClosesTheScreen()
        {
            using var f = new FacilityFixture();
            f.OpenAt();
            f.Emit();
            f.Click(FacilityScreen.CloseId);
            Assert.That(f.Stack.IsOpen(ScreenCatalog.Facility), Is.False);
            Assert.That(f.Stack.MovementBlocked, Is.False);
        }

        [Test]
        public void ChoosingTheRoute_MakesTheStudentAnAdult_AndTheScreenShowsTheLevel()
        {
            using var f = new FacilityFixture();
            f.OpenAt();
            f.Emit();
            f.Click(FacilityScreen.TransitionId);
            Assert.That(f.Screen.LastResult, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(f.Session.Adult.IsAdult, Is.True);
            Assert.That(f.Session.Adult.FacilityId, Is.EqualTo(FacilityFixture.Chef));
            Assert.That(f.Reload().Adult.IsAdult, Is.True, "저장된다");
            f.Emit();
            Assert.That(((UiTextBlock)f.Node(FacilityScreen.SummaryId)).Text, Does.Contain(f.Text.Format("facility.level-reputation", 1, 0)));
            Assert.That(((UiLabel)f.Node(FacilityScreen.TitleId)).Text, Is.EqualTo(f.Text.Get("facility.chef.level1")), "제목은 지금 단계 이름");
            Assert.That(((UiTextBlock)f.Node(FacilityScreen.StatusId)).Text, Is.EqualTo(f.Text.Get("facility.result.Applied")));
        }

        [Test]
        public void ChoosingTheRoute_WhenNotReady_ShowsTheRejection_AndChangesNothing()
        {
            using var f = new FacilityFixture(candidateReady: false);
            f.OpenAt();
            f.Emit();
            f.Click(FacilityScreen.TransitionId);
            Assert.That(f.Screen.LastResult, Is.EqualTo(AdultCareerResultKind.RequirementFailed));
            Assert.That(f.Session.Adult.IsAdult, Is.False);
            f.Emit();
            Assert.That(((UiTextBlock)f.Node(FacilityScreen.StatusId)).Text, Is.EqualTo(f.Text.Get("facility.result.RequirementFailed")));
        }

        [Test]
        public void PlayingAWork_ReservesIt_ClosesThePanel_AndOpensTheMinigame()
        {
            using var f = new FacilityFixture(FacilityFixture.AlreadyAdultAtLevelOne);
            f.OpenAt();
            f.Emit();
            f.ClickCell(FacilityScreen.PlayCell(0));
            Assert.That(f.Session.Adult.HasPendingWork, Is.True, "일이 예약됐다");
            Assert.That(f.Session.Adult.PendingWork.WorkId, Is.EqualTo("work.chef.level1"));
            Assert.That(f.Stack.IsOpen(ScreenCatalog.Facility), Is.False, "Unity: 예약하면 패널은 닫힌다");
            Assert.That(f.Stack.IsOpen(ScreenCatalog.Minigame), Is.True, "미니게임 화면이 저절로 열린다");
        }

        [Test]
        public void PlayingAWork_WhenNotAdult_ShowsTheRejection_AndStaysOpen()
        {
            using var f = new FacilityFixture();
            f.OpenAt();
            f.Emit();
            f.ClickCell(FacilityScreen.PlayCell(0));
            Assert.That(f.Session.Adult.HasPendingWork, Is.False);
            Assert.That(f.Stack.IsOpen(ScreenCatalog.Facility), Is.True);
            Assert.That(f.Screen.LastResult, Is.Not.Null.And.Not.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(f.Screen.StatusText, Is.EqualTo(f.Text.Get(FacilityModel.ResultKey(f.Screen.LastResult.Value))));
        }

        /// <summary>일 줄이 걸리는 시간과 함께 끝나는 시각을 보인다(장소 화면과 같은 문구, 흐르는 시간 틈 2026-11-12).</summary>
        [Test]
        public void WorkRows_ShowWhenTheyEnd()
        {
            using var f = new FacilityFixture(FacilityFixture.AlreadyAdultAtLevelOne);
            f.OpenAt();
            FacilityWorkRow row = f.Screen.Model.Works[0];
            Assert.That(row.Minutes, Is.GreaterThan(0));
            int end = Rootborn.Screens.Day.StudentTimeText.ClockMinutes(f.Session.Student.TimeMinutes + row.Minutes);
            string key = row.Minutes > f.Session.MinutesUntilBedtime ? "life.time.cost-ends-late" : "life.time.cost-ends";
            string expected = string.Format(f.Text.Get(key), string.Format(f.Text.Get("life.time.cost"), row.Minutes), end / 60, end % 60);
            string drawn = string.Join(" ", f.Emit().Commands.Where(c => c.Kind == DrawKind.Text).Select(c => c.Text));
            Assert.That(drawn, Does.Contain(expected));
        }

        [Test]
        public void TheInfoCell_DoesNothing()
        {
            using var f = new FacilityFixture(FacilityFixture.AlreadyAdultAtLevelOne);
            f.OpenAt();
            f.Emit();
            f.ClickCell(0);
            Assert.That(f.Screen.LastResult, Is.Null);
            Assert.That(f.Session.Adult.HasPendingWork, Is.False);
        }

        [Test]
        public void AutoSkip_WithoutTheRequirements_ShowsTheRejection_AndChangesNothing()
        {
            using var f = new FacilityFixture(FacilityFixture.AlreadyAdultAtLevelOne);
            f.OpenAt();
            f.Emit();
            int time = f.Session.Student.TimePointsRemaining;
            f.ClickCell(FacilityScreen.SkipCell(0));
            Assert.That(f.Screen.LastResult, Is.EqualTo(AdultCareerResultKind.RequirementFailed));
            Assert.That(f.Session.Student.TimePointsRemaining, Is.EqualTo(time));
            Assert.That(f.Session.Adult.GetSuccessfulCompletions(f.Session.Definitions.Get<FacilityWork>("work.chef.level1")), Is.EqualTo(0));
        }

        [Test]
        public void AutoSkip_WithMasteryAndSuccesses_CompletesTheWorkAtOnce_AndShowsTheNewProgress()
        {
            using var f = new FacilityFixture(s =>
            {
                FacilityFixture.AlreadyAdult(s, 2); // 자동 완료는 시설 2단계부터(데이터의 장비 조건)
                s.Adult.Works = new[] { new FacilityWorkProgressSaveData { WorkId = "work.chef.level1", Mastery = 100, SuccessfulCompletions = 5 } };
            });
            f.OpenAt();
            f.Emit();
            var work = f.Session.Definitions.Get<FacilityWork>("work.chef.level1");
            f.ClickCell(FacilityScreen.SkipCell(0));
            Assert.That(f.Screen.LastResult, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(f.Session.Adult.GetSuccessfulCompletions(work), Is.EqualTo(6));
            Assert.That(f.Reload().Adult.GetSuccessfulCompletions(work), Is.EqualTo(6), "저장된다");
            Assert.That(f.Screen.Model.Works[0].Successes, Is.EqualTo(6), "화면 값이 새로 읽힌다");
            Assert.That(f.Stack.IsOpen(ScreenCatalog.Facility), Is.True, "자동 완료는 패널을 닫지 않는다");
        }

        [Test]
        public void Upgrade_WithoutEnoughItems_ShowsTheRejection_AndChangesNothing()
        {
            using var f = new FacilityFixture(s => FacilityFixture.AlreadyAdult(s, 2));
            f.OpenAt();
            f.Emit();
            Assert.That(f.Screen.Model.NextLevel, Is.EqualTo(3));
            Assert.That(((UiTextBlock)f.Node(FacilityScreen.CostId)).Text, Does.Contain(f.Text.Get(f.Session.Item("item.facility-materials").DisplayKey)), "다음 단계 비용이 보인다");
            f.Click(FacilityScreen.UpgradeId);
            Assert.That(f.Screen.LastResult, Is.EqualTo(AdultCareerResultKind.InsufficientResources));
            Assert.That(f.Session.Adult.FacilityLevel, Is.EqualTo(2));
            Assert.That(((UiTextBlock)f.Node(FacilityScreen.StatusId)).Text, Is.EqualTo(f.Text.Get("facility.result.InsufficientResources")));
        }

        [Test]
        public void Upgrade_WithTheItems_PaysTheCost_AndReachesTheMaximum()
        {
            using var f = new FacilityFixture(s =>
            {
                FacilityFixture.AlreadyAdult(s, 2);
                FacilityFixture.Fill(s, "item.allowance", 300);
                FacilityFixture.Fill(s, "item.facility-materials", 10);
            });
            f.OpenAt();
            f.Emit();
            f.Click(FacilityScreen.UpgradeId);
            Assert.That(f.Screen.LastResult, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(f.Session.Adult.FacilityLevel, Is.EqualTo(3));
            Assert.That(f.Session.Inventory.CountOf(f.Session.Item("item.allowance")), Is.EqualTo(0));
            f.Emit();
            Assert.That(((UiTextBlock)f.Node(FacilityScreen.CostId)).Text, Is.EqualTo(f.Text.Get("facility.maximum-level")));
            Assert.That(((UiTextBlock)f.Node(FacilityScreen.SummaryId)).Text, Does.Contain(f.Text.Get("facility.route-complete")));
        }

        [Test]
        public void Practice_ReservesTheMinigame_OrShowsARejection()
        {
            using var f = new FacilityFixture();
            f.OpenAt();
            f.Emit();
            f.Click(FacilityScreen.PracticeId);
            Assert.That(f.Screen.LastResult, Is.Not.Null);
            Assert.That(f.Screen.StatusText, Is.EqualTo(f.Text.Get(FacilityModel.ResultKey(f.Screen.LastResult.Value))));
        }

        [Test]
        public void SwitchingLanguage_ChangesTheOpenScreenText()
        {
            using var f = new FacilityFixture();
            f.OpenAt();
            f.Emit();
            string korean = Label(f.Node(FacilityScreen.CloseId));
            f.Text.Language = "en";
            f.Emit();
            Assert.That(Label(f.Node(FacilityScreen.CloseId)), Is.EqualTo(f.Text.Get("facility.close")));
            Assert.That(Label(f.Node(FacilityScreen.CloseId)), Is.Not.EqualTo(korean));
        }

        [Test]
        public void Text_ExistsInBothLanguages_OrIsKnownMissing()
        {
            using var f = new FacilityFixture(s => FacilityFixture.AlreadyAdult(s, 2));
            f.OpenAt();
            f.Emit();
            f.Click(FacilityScreen.UpgradeId);
            f.Click(FacilityScreen.TransitionId);
            f.Click(FacilityScreen.PracticeId);
            f.Text.Language = "en";
            f.Emit();
            KnownMissingKeys.AssertOnlyKnownMissing(f.Text, f.Stack.Find(ScreenCatalog.Facility).TextKeys);
        }

        [Test]
        public void LongText_KeepsEveryTextInsideThePanel()
        {
            using var f = new FacilityFixture(s => FacilityFixture.AlreadyAdult(s, 2), longText: true);
            Assert.That(f.OpenAt(), Is.True);
            var measurer = new WideMeasurer();
            f.Stack.Emit(new DrawList(), measurer);
            f.Stack.Layout(1920, 1080);
            UiNode root = f.Screen.Root;
            foreach (UiNode node in Descendants(root))
            {
                if (node is UiCardGrid || !node.IsVisibleInTree()) continue; // 목록 칸 글자는 부품이 칸 안으로 자른다
                var list = new DrawList();
                node.Emit(list, measurer);
                foreach (DrawCommand text in list.Commands.Where(c => c.Kind == DrawKind.Text))
                {
                    float right = text.Rect.X + measurer.Width(text.Text, text.FontSize), bottom = text.Rect.Y + measurer.LineHeight(text.FontSize);
                    UiRect box = node.Rect;
                    Assert.That(text.Rect.X >= box.X - 0.5f && right <= box.Right + 0.5f && text.Rect.Y >= box.Y - 0.5f && bottom <= box.Bottom + 0.5f, Is.True,
                        (node.Id ?? node.GetType().Name) + ": " + text.Text);
                }
            }
        }

        private static System.Collections.Generic.IEnumerable<UiNode> Descendants(UiNode node)
        {
            yield return node;
            foreach (UiNode child in node.Children)
                foreach (UiNode inner in Descendants(child)) yield return inner;
        }
    }
}
