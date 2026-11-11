using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Host.World;
using Rootborn.Screens;
using Rootborn.Screens.AdultFacility;
using Rootborn.Ui;
using Rootborn.World;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 성인 시설 관리 화면을 실제 창과 게임 루프로. 시험용 상호작용 파일로 집 안에 시설 지점(kind "career-facility", 요리사)을 하나 두고,
    /// 걸어가 E → 이 성인 진로 선택(포인터) → 시설 확장 시도(재료가 없어 거절 이유가 보임) → 일 하나를 직접 하기(포인터) → 미니게임 화면이 열림 → Esc.
    /// 준비 단계 fixture(실제 플레이가 아님): 요리사 후보를 준비 상태로 만든 슬롯을 GameSession과 저장 파일 고치기로 미리 저장해 둔다
    /// (게임 안에서 후보를 키우는 길은 이 묶음 밖이다). 마을에는 시설 지점이 아직 월드 데이터에 없다.
    /// 조작은 포인터·방향 키·E·Esc뿐(장치 이벤트 주입, OS 입력 계층 미확인). 스크린샷: output/host-screens-evidence/facility-*.png.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class FacilityHostTests
    {
        private string _saves;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-facility-host-" + Guid.NewGuid().ToString("N"));
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

        /// <summary>준비 단계: 하루를 끝내고 다음 날로 넘겨 슬롯을 저장한 뒤, 저장된 스냅샷에서 요리사 후보를 준비 상태로 고친다.</summary>
        private void PrepareSlotWithChefCandidateReady()
        {
            var store = new FileSaveStore(_saves);
            var session = GameSession.StartFromData(Path.Combine(_root, "data"), store, "slot-1", null);
            Assert.That(session.EndDay().Ended && session.StartNextDay(), Is.True, "슬롯을 저장하려고 하루를 넘긴다");
            string name = SaveFileNames.AdultCareerSnapshot(session.PlayerId);
            Assert.That(store.TryRead<AdultCareerSnapshotSaveData>("slot-1", name, out var snapshot), Is.True);
            var student = snapshot.Student;
            student.CurrentDay = Math.Max(4, student.CurrentDay);
            student.DayState = StudentDayState.InProgress.ToString();
            student.TimePointsSpentToday = 0;
            student.CareerHintIds = student.CareerHintIds.Concat(new[] { "career.chef" }).Distinct().ToArray();
            student.CareerCandidates ??= new CareerCandidateProgressSaveData { SaveSlot = student.SaveSlot, PlayerId = student.PlayerId };
            student.CareerCandidates.Candidates = new[]
            {
                new CareerCandidateProgressSaveData.CandidateEntry
                {
                    CandidateId = "candidate.chef", UnderstandingScore = 3, HintIds = new[] { "hint.chef.practice" }, LastHintSourceId = "hint.chef.practice",
                },
            };
            store.Write("slot-1", name, snapshot);
        }

        /// <summary>시험용 상호작용 파일: 실제 파일에 집 안 시설 지점(kind "career-facility", 요리사) 하나를 더한다.</summary>
        private string InteractionsWithFacilityPoint(float x, float y)
        {
            string text = File.ReadAllText(Path.Combine(_root, InteractionData.DefaultRelativePath));
            const string houseThings = "\"things\": [";
            int house = text.IndexOf("\"House\"", StringComparison.Ordinal);
            int things = text.IndexOf(houseThings, house, StringComparison.Ordinal);
            string point = "\n        { \"kind\": \"career-facility\", \"id\": \"facility.chef\", \"at\": [ "
                + x.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", " + y.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ], \"priority\": 55 },";
            text = text.Insert(things + houseThings.Length, point);
            string path = Path.Combine(_saves, "interactions-with-facility.json");
            File.WriteAllText(path, text);
            return path;
        }

        private static void ClickCell(PlayerDriver player, FacilityScreen screen, int cell)
        {
            UiRect r = screen.CellRect(cell);
            Assert.That(player.Game.Ui.HitTest(r.CenterX, r.CenterY), Is.SameAs(screen.WorkGrid), "칸 자리 맨 위 요소는 일 목록이어야 한다");
            player.ClickAt(r.CenterX, r.CenterY);
        }

        [Test]
        public void FACILITY_001_WalkToTheFacility_E_ChooseRoute_UpgradeRefused_PlayAWork_MinigameOpens()
        {
            PrepareSlotWithChefCandidateReady();
            const float px = 624f, py = 560f;
            using PlayerDriver player = Start("--interactions", InteractionsWithFacilityPoint(px, py));
            player.WalkNear(px, py, 40f);
            Assert.That(player.Game.World.Interactions.Target?.Kind, Is.EqualTo("career-facility"));
            player.PressKey(TestKey.E);
            player.WaitForScreen(ScreenCatalog.Facility);
            var screen = (FacilityScreen)player.Screens.Find(ScreenCatalog.Facility);
            Assert.That(player.Screens.MovementBlocked, Is.True);
            Assert.That(screen.Model.Works.Length, Is.EqualTo(3));
            player.Frames(2);
            player.MovePointer(-50, -50);
            player.ScreenshotScreen(ScreenCatalog.Facility, "facility-01-open");

            // 막는 동안은 걷지 않는다.
            float x0 = player.World.Walker.X, y0 = player.World.Walker.Y;
            player.HoldKey(TestKey.Up, 30);
            Assert.That((player.World.Walker.X, player.World.Walker.Y), Is.EqualTo((x0, y0)));

            // 이 성인 진로 선택(포인터).
            player.PressButton(FacilityScreen.TransitionId);
            Assert.That(screen.LastResult, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(player.Game.Session.Adult.IsAdult, Is.True);
            Assert.That(new FileSaveStore(_saves).TryRead<AdultCareerSnapshotSaveData>("slot-1", SaveFileNames.AdultCareerSnapshot(player.Game.Session.PlayerId), out var saved), Is.True);
            Assert.That(saved.Adult.FacilityId, Is.EqualTo("facility.chef"), "선택이 세이브에 남는다");
            player.Frames(2);
            player.MovePointer(-50, -50);
            player.ScreenshotScreen(ScreenCatalog.Facility, "facility-02-route-chosen");

            // 시설 확장 시도: 재료가 없으니 거절 이유가 보인다.
            player.PressButton(FacilityScreen.UpgradeId);
            Assert.That(screen.LastResult, Is.EqualTo(AdultCareerResultKind.InsufficientResources));
            Assert.That(player.Game.Session.Adult.FacilityLevel, Is.EqualTo(1));
            Assert.That(((UiTextBlock)player.Node(FacilityScreen.StatusId)).Text, Is.EqualTo(player.Game.Text.Get("facility.result.InsufficientResources")));
            player.Frames(2);
            player.MovePointer(-50, -50);
            player.ScreenshotScreen(ScreenCatalog.Facility, "facility-03-upgrade-refused");

            // 자동 완료 시도: 조건이 안 돼 거절된다.
            ClickCell(player, screen, FacilityScreen.SkipCell(0));
            Assert.That(screen.LastResult, Is.EqualTo(AdultCareerResultKind.RequirementFailed));

            // 일 하나를 직접 하기: 예약되고 패널이 닫히고 미니게임 화면이 열린다.
            ClickCell(player, screen, FacilityScreen.PlayCell(0));
            Assert.That(player.Game.Session.Adult.HasPendingWork, Is.True);
            player.WaitForScreen(ScreenCatalog.Minigame);
            Assert.That(player.Screens.IsOpen(ScreenCatalog.Facility), Is.False);
            player.Frames(2);
            player.MovePointer(-50, -50);
            player.ScreenshotScreen(ScreenCatalog.Minigame, "facility-04-minigame-opened");

            // 닫으면 끝나지 않은 판은 포기로 정산된다.
            player.PressKey(TestKey.Escape);
            player.WaitForScreenClosed(ScreenCatalog.Minigame);
            Assert.That(player.Game.Session.Adult.HasPendingWork, Is.False);
            Assert.That(player.Screens.MovementBlocked, Is.False);
            Assert.That(player.CollisionViolations, Is.EqualTo(0));
            Rootborn.Screens.Tests.KnownMissingKeys.AssertOnlyKnownMissing(player.Game.Text, Array.Empty<string>());
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
        }

        [Test]
        public void FACILITY_002_Escape_ClosesThePanel()
        {
            PrepareSlotWithChefCandidateReady();
            const float px = 624f, py = 560f;
            using PlayerDriver player = Start("--interactions", InteractionsWithFacilityPoint(px, py));
            player.WalkNear(px, py, 40f);
            player.PressKey(TestKey.E);
            player.WaitForScreen(ScreenCatalog.Facility);
            player.PressKey(TestKey.Escape);
            player.WaitForScreenClosed(ScreenCatalog.Facility);
            Assert.That(player.Screens.MovementBlocked, Is.False);
        }
    }
}
