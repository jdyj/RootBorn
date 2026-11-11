using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Host.World;
using Rootborn.Screens;
using Rootborn.Screens.Career;
using Rootborn.Ui;
using Rootborn.World;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 묶음 8(진로)을 실제 창과 게임 루프로. 장래희망: 단서를 얻어 둔 슬롯 → HUD의 장래희망 버튼을 포인터로 누름 → 카드를 포인터로 고름 →
    /// 닫기 → 같은 날 다시 열어 다른 카드를 누르면 바뀌지 않고 제한 이유(오늘 변경함)가 보임. 진로 후보도 HUD 버튼으로 연다.
    /// 탐색: 시험용 상호작용 파일로 지점 하나를 두고 걸어가 E → 선택지를 포인터로 → 결과 → Esc.
    /// 준비 단계 fixture(실제 플레이가 아님): 단서 두 개를 GameSession으로 미리 적용해 슬롯에 저장한다(게임 안에서 단서를 얻는 길은 이 묶음 밖이다).
    /// 조작은 포인터·방향 키·E·Esc뿐(장치 이벤트 주입, OS 입력 계층 미확인). 스크린샷: output/host-screens-evidence/career-*.png, exploration-*.png.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class CareerHostTests
    {
        private string _saves;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-career-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            _root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        private PlayerDriver Start(params string[] extra)
        {
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--save-root", _saves, "--slot", "slot-1", "--size", "1920x1080",
                "--evidence", Path.Combine(_root, "output", "host-screens-evidence"),
            }.Concat(extra).ToArray());
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        /// <summary>준비 단계: 슬롯에 요리사·인테리어 후보의 소개 단서를 저장해 두 장래희망을 고를 수 있게 한다.</summary>
        private void PrepareSlotWithTwoRevealedCandidates()
        {
            var session = GameSession.StartFromData(Path.Combine(_root, "data"), new FileSaveStore(_saves), "slot-1", null);
            foreach ((string hint, string log) in new[] { ("hint.chef.introduction", "opening.introduce.restaurant"), ("hint.interior.introduction", "opening.introduce.design-studio") })
            {
                var result = session.CareerCandidates.ApplyHint(session.Definitions.Get<CareerHint>(hint), session.Student, "fixture:" + hint, new[] { log });
                Assert.That(result.Applied, Is.True, hint);
            }
            Assert.That(session.VisitLocation("location.library").Kind, Is.Not.EqualTo(Rootborn.Core.StudentLife.LocationVisitResultKind.InvalidRequest), "슬롯을 저장하려고 장소를 한 번 방문한다");
        }

        private static T Screen<T>(PlayerDriver player, string id) where T : GameScreen => (T)player.Screens.Find(id);

        private static void ClickCard(PlayerDriver player, UiCardRow row, int index)
        {
            UiRect r = row.CardRect(index);
            Assert.That(player.Game.Ui.HitTest(r.CenterX, r.CenterY), Is.SameAs(row), "카드 자리 맨 위 요소는 카드 줄이어야 한다");
            player.ClickAt(r.CenterX, r.CenterY);
        }

        [Test]
        public void CAREER_001_HudDreamButton_PickACard_Close_ReopenSameDay_ShowsTheLimit()
        {
            PrepareSlotWithTwoRevealedCandidates();
            using PlayerDriver player = Start();
            Assert.That(player.Game.Session.CareerCandidates.GetHintCount(player.Game.Session.Definitions.Get<CareerCandidate>("candidate.chef")), Is.EqualTo(1), "저장한 슬롯을 이어 받았다");

            player.PressButton("hud.button.dream");
            player.WaitForScreen(ScreenCatalog.CareerInterest);
            var screen = Screen<CareerInterestScreen>(player, ScreenCatalog.CareerInterest);
            Assert.That(player.Screens.MovementBlocked, Is.True);
            Assert.That(screen.Model.Cards.Length, Is.EqualTo(4));
            player.Frames(2);
            player.MovePointer(-50, -50);
            player.ScreenshotScreen(ScreenCatalog.CareerInterest, "career-01-interest-open");

            // 요리사 카드를 포인터로 고른다.
            var cards = (UiCardRow)player.Node(CareerInterestScreen.CardsId);
            ClickCard(player, cards, 0);
            Assert.That(player.Game.Session.CareerInterests.CurrentInterestId, Is.EqualTo("interest.chef"));
            Assert.That(screen.Model.Cards[0].IsCurrent, Is.True);
            player.Frames(2);
            player.MovePointer(-50, -50);
            player.ScreenshotScreen(ScreenCatalog.CareerInterest, "career-02-interest-selected");

            // 닫고 같은 날 다시 열어 인테리어 카드를 누르면 바뀌지 않는다.
            player.PressButton(CareerInterestScreen.CloseId);
            player.WaitForScreenClosed(ScreenCatalog.CareerInterest);
            Assert.That(player.Screens.MovementBlocked, Is.False);
            player.PressButton("hud.button.dream");
            player.WaitForScreen(ScreenCatalog.CareerInterest);
            cards = (UiCardRow)player.Node(CareerInterestScreen.CardsId);
            ClickCard(player, cards, 2);
            Assert.That(player.Game.Session.CareerInterests.CurrentInterestId, Is.EqualTo("interest.chef"), "같은 날에는 바뀌지 않는다");
            Assert.That(screen.LastResult?.State, Is.EqualTo(CareerInterestSelectionState.ChangedToday));
            var details = (UiScrollText)player.Node(CareerInterestScreen.DetailsId);
            Assert.That(details.Text, Does.Contain(player.Game.Text.Get("ui.interest-state.changed")), "제한 이유가 보인다");
            player.Frames(2);
            player.MovePointer(-50, -50);
            player.ScreenshotScreen(ScreenCatalog.CareerInterest, "career-03-interest-limit");

            // 선택이 세이브에 남는다(GameSession 명령이 저장한다).
            Assert.That(new FileSaveStore(_saves).TryRead<CareerInterestProgressSaveData>("slot-1", SaveFileNames.CareerInterest(player.Game.Session.PlayerId), out var saved), Is.True);
            Assert.That(saved.CurrentInterestId, Is.EqualTo("interest.chef"));

            player.PressButton(CareerInterestScreen.CloseId);
            player.WaitForScreenClosed(ScreenCatalog.CareerInterest);
            Assert.That(player.CollisionViolations, Is.EqualTo(0));
            Rootborn.Screens.Tests.KnownMissingKeys.AssertOnlyKnownMissing(player.Game.Text, Array.Empty<string>());
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
        }

        [Test]
        public void CAREER_002_HudCareersButton_OpensTheCandidateList_ClickARow_CloseWithTheButton()
        {
            PrepareSlotWithTwoRevealedCandidates();
            using PlayerDriver player = Start();
            player.PressButton("hud.button.careers");
            player.WaitForScreen(ScreenCatalog.CareerCandidates);
            var screen = Screen<CareerCandidateScreen>(player, ScreenCatalog.CareerCandidates);
            Assert.That(screen.Model.Rows.Length, Is.EqualTo(8));
            Assert.That(player.Screens.MovementBlocked, Is.True);
            player.Frames(2);
            player.MovePointer(-50, -50);
            player.ScreenshotScreen(ScreenCatalog.CareerCandidates, "career-04-candidates-open");

            int chef = Array.FindIndex(screen.Model.Rows, r => r.CandidateId == "candidate.chef");
            var list = (UiCardGrid)player.Node(CareerCandidateScreen.ListId);
            UiRect cell = list.CellRect(chef);
            Assert.That(player.Game.Ui.HitTest(cell.CenterX, cell.CenterY), Is.SameAs(list));
            player.ClickAt(cell.CenterX, cell.CenterY);
            Assert.That(screen.DetailIndex, Is.EqualTo(chef));
            player.Frames(2);
            player.MovePointer(-50, -50);
            player.ScreenshotScreen(ScreenCatalog.CareerCandidates, "career-05-candidates-chef");

            // 긴 목록은 휠로 넘긴다.
            UiRect view = list.Rect;
            player.MovePointer(view.CenterX, view.CenterY);
            player.Wheel(-3);
            Assert.That(list.ScrollOffset, Is.GreaterThan(0), "휠로 목록이 넘어간다");

            player.PressButton(CareerCandidateScreen.CloseId);
            player.WaitForScreenClosed(ScreenCatalog.CareerCandidates);
            Rootborn.Screens.Tests.KnownMissingKeys.AssertOnlyKnownMissing(player.Game.Text, Array.Empty<string>());
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
        }

        /// <summary>시험용 상호작용 파일: 실제 파일에 집 안 탐색 지점(kind "exploration") 하나를 더한다. 마을의 탐색 지점은 아직 월드 데이터에 없다.</summary>
        private string InteractionsWithExplorationPoint(float x, float y)
        {
            string text = File.ReadAllText(Path.Combine(_root, InteractionData.DefaultRelativePath));
            const string houseThings = "\"things\": [";
            int house = text.IndexOf("\"House\"", StringComparison.Ordinal);
            int things = text.IndexOf(houseThings, house, StringComparison.Ordinal);
            string point = "\n        { \"kind\": \"exploration\", \"id\": \"exploration.test.alley-crate\", \"at\": [ "
                + x.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", " + y.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ], \"priority\": 50 },";
            text = text.Insert(things + houseThings.Length, point);
            string path = Path.Combine(_saves, "interactions-with-exploration.json");
            File.WriteAllText(path, text);
            return path;
        }

        [Test]
        public void EXPLORATION_001_WalkToThePoint_E_Choose_ResultShown_Escape()
        {
            const float px = 624f, py = 560f;
            using PlayerDriver player = Start("--interactions", InteractionsWithExplorationPoint(px, py));
            player.WalkNear(px, py, 40f);
            Assert.That(player.Game.World.Interactions.Target?.Kind, Is.EqualTo("exploration"));
            player.PressKey(TestKey.E);
            player.WaitForScreen(ScreenCatalog.Exploration);
            var screen = Screen<ExplorationScreen>(player, ScreenCatalog.Exploration);
            Assert.That(screen.Model.InteractionId, Is.EqualTo("exploration.test.alley-crate"));
            Assert.That(player.Screens.MovementBlocked, Is.True);
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.Exploration, "exploration-01-open");

            // 막는 동안은 걷지 않는다.
            float x0 = player.World.Walker.X, y0 = player.World.Walker.Y;
            player.HoldKey(TestKey.Up, 30);
            Assert.That((player.World.Walker.X, player.World.Walker.Y), Is.EqualTo((x0, y0)));

            int index = Array.FindIndex(screen.Model.Choices, c => c.Available);
            player.PressButton(ExplorationScreen.ChoiceId(index));
            Assert.That(screen.LastResult, Is.Not.Null);
            Assert.That(screen.ResultText, Is.Not.Empty);
            player.Frames(2);
            player.MovePointer(-50, -50);
            player.ScreenshotScreen(ScreenCatalog.Exploration, "exploration-02-result");

            player.PressKey(TestKey.Escape);
            player.WaitForScreenClosed(ScreenCatalog.Exploration);
            Assert.That(player.Screens.MovementBlocked, Is.False);
            player.WalkAway(1);
            Rootborn.Screens.Tests.KnownMissingKeys.AssertOnlyKnownMissing(player.Game.Text, Array.Empty<string>());
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
        }
    }
}
