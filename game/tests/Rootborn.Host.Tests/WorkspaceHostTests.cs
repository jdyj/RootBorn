using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Inventory;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Workspaces;
using Rootborn.Core.World;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Host.Tests.TwoGames;
using Rootborn.Host.World;
using Rootborn.Screens;
using Rootborn.Screens.AdultFacility;
using Rootborn.Screens.Furniture;
using Rootborn.Screens.Minigames;
using Rootborn.Screens.Workspaces;
using Rootborn.Ui;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 요리사가 식당(일터)을 꾸미고 일해서 더 많이 받는다 — 실제 창과 게임 루프로.
    /// 준비 단계(실제 플레이 아님): 요리사 후보를 준비 상태로 만든 슬롯을 저장 파일 고치기로 두고, GameSession 명령으로 요리사 시설을 맡긴다(성인 요리사 슬롯).
    /// 그 뒤는 PlayerDriver 동사(방향 키·E·Space·Esc·글자·포인터)만: 집 → 문 → 마을 → 문 → 일터 맵(데이터의 일터 면을 가진 맵) → HUD 단추로 배치 화면 →
    /// 검색 칸에 가구 id를 쳐 카드를 누르고 월드 칸을 눌러 놓음(아늑함 이득이 생길 때까지) → 성질 점수 글자가 바뀜 → 템플릿 단추로 템플릿 화면(스크린샷, Esc)
    /// → 배치 끔 → 문 → 마을 → 시설 지점까지 걸어가 E → 시설 화면(스크린샷) → 일을 직접 하기 → 미니게임(순서는 포인터, 박자는 Space) →
    /// 받은 보상이 꾸미기 전 기준(같은 점수, 레벨 비율만 — 일터 이득 0)보다 많다.
    /// 게임 상태는 어디를 누를지 고르고(가구 칸, 지점 좌표, 일 줄) 결과를 확인하는 데에만 읽는다. 장치 이벤트 주입, OS 입력 계층 미확인.
    /// 스크린샷: output/host-screens-evidence/workspace-*.png.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class WorkspaceHostTests
    {
        private const string ChefFacility = "facility.chef";
        private const string Cozy = "quality.cozy";
        private const string Allowance = "item.allowance";
        private const string Materials = "item.facility-materials";
        private string _saves;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-workspace-host-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            _root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        private PlayerDriver Start()
        {
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--save-root", _saves, "--slot", "slot-1", "--size", "1920x1080",
                "--evidence", Path.Combine(_root, "output", "host-screens-evidence"),
            });
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        /// <summary>
        /// 준비 단계(명령·저장 파일 고치기): 하루를 넘겨 슬롯을 저장하고, 스냅샷에서 요리사 후보를 준비 상태로 고친 뒤(FacilityHostTests와 같다)
        /// 새 GameSession에서 요리사 시설을 맡긴다(Game ChefSetup.Adult와 같은 명령).
        /// </summary>
        private void PrepareAdultChefSlot()
        {
            var store = new FileSaveStore(_saves);
            var first = GameSession.StartFromData(Path.Combine(_root, "data"), store, "slot-1", null);
            Assert.That(first.EndDay().Ended && first.StartNextDay(), Is.True, "슬롯을 저장하려고 하루를 넘긴다");
            string name = SaveFileNames.AdultCareerSnapshot(first.PlayerId);
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

            var adult = GameSession.StartFromData(Path.Combine(_root, "data"), store, "slot-1", null);
            Assert.That(adult.ChooseFacility(ChefFacility, "facility:chef").Kind, Is.EqualTo(AdultCareerResultKind.Applied), "준비: 요리사 시설을 맡는다");
        }

        /// <summary>지금 맵에서 그 맵으로 가는 문의 값(문 엔티티의 목적지 값). 맵 데이터에서 읽는다.</summary>
        private static string DoorTo(PlayerDriver p, string map)
        {
            DoorArea door = p.World.Map.Doors.FirstOrDefault(d => d.Destination.HasValue && d.Destination.Value.MapId == map);
            Assert.That(door, Is.Not.Null, p.World.Map.Id + "에 " + map + "로 가는 문이 없다");
            return door.Target;
        }

        /// <summary>
        /// 그 맵으로 가는 문을 지난다. 가장 짧은 길이 다른 문에 닿으면(마을에서 주방 문으로 가는 길이 경찰서 문 밑을 지난다) 그 문에 들어가 버리므로,
        /// 다른 문과 그 둘레 한 칸을 막은 칸 표로 길을 먼저 정하고 꺾이는 칸마다 방향 키로 걸은 뒤 문을 지난다. 길 고르기에만 맵을 읽는다.
        /// </summary>
        private static void Through(PlayerDriver p, string map)
        {
            string target = DoorTo(p, map);
            WorldMap here = p.World.Map;
            MapGrid grid = here.Grid;
            var blocked = new bool[grid.Count];
            for (int y = 0; y < grid.Rows; y++)
                for (int x = 0; x < grid.Columns; x++) blocked[grid.Index(x, y)] = here.Collision.IsBlocked(x, y);
            var targetCells = new HashSet<CellPos>();
            foreach (DoorArea d in here.Doors)
            {
                bool isTarget = d.Destination.HasValue && d.Target == target;
                foreach (CellPos c in CollisionInput.CellsUnder(d.Area, grid.CellSize))
                {
                    if (isTarget) { targetCells.Add(c); continue; }
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            if (grid.Contains(c.X + dx, c.Y + dy)) blocked[grid.Index(c.X + dx, c.Y + dy)] = true;
                }
            }
            foreach (CellPos c in targetCells) if (grid.Contains(c.X, c.Y)) blocked[grid.Index(c.X, c.Y)] = false;
            bool NearTarget(CellPos c) => targetCells.Contains(c) || targetCells.Contains(new CellPos(c.X + 1, c.Y)) || targetCells.Contains(new CellPos(c.X - 1, c.Y))
                || targetCells.Contains(new CellPos(c.X, c.Y + 1)) || targetCells.Contains(new CellPos(c.X, c.Y - 1));
            List<CellPos> path = GridPath.Find(new CollisionGrid(grid, blocked, here.Collision.OpenOutside), p.PlayerCell, NearTarget);
            Assert.That(path, Is.Not.Null, here.Id + "에서 다른 문을 피해 " + target + " 문까지 갈 길이 없다");
            for (int i = 0; i < path.Count; i++)
            {
                bool last = i == path.Count - 1;
                if (!last)
                {
                    CellPos prev = i == 0 ? p.PlayerCell : path[i - 1];
                    if (path[i + 1].X - path[i].X == path[i].X - prev.X && path[i + 1].Y - path[i].Y == path[i].Y - prev.Y) continue; // 곧은 길 가운데
                }
                if (!targetCells.Contains(path[i])) p.WalkTo(path[i]);
            }
            Assert.That(p.WalkThroughDoor(target), Is.EqualTo(map));
            p.DismissPopups();
        }

        private static (int Allowance, int Materials) Counts(GameSession s) => (s.Inventory.CountOf(s.Item(Allowance)), s.Inventory.CountOf(s.Item(Materials)));

        private static (int Allowance, int Materials) Sum(ItemAmount<string>[] rewards)
            => (rewards.Where(r => r.Item == Allowance).Sum(r => r.Count), rewards.Where(r => r.Item == Materials).Sum(r => r.Count));

        /// <summary>
        /// 배치 화면이 열린 채로: 검색 칸에 id를 쳐 카드를 누르고, 화면에 보이고 UI가 덮지 않은 놓을 수 있는 칸(들어오는 칸·문에서 3칸 넘게 떨어진)을 누른다.
        /// 칸 고르기에만 Core 판정(House.CanPlace)을 읽는다.
        /// </summary>
        private static bool PlaceByPointer(PlayerDriver p, FurniturePlacementScreen placement, string stableId)
        {
            var session = p.Game.Session;
            FurnitureDefinition definition = session.Definitions.Get<FurnitureDefinition>(stableId);
            if (placement.Palette.Query.Length > 0)
            {
                p.PressButton(FurniturePlacementScreen.SearchId);
                p.PressKey(TestKey.Backspace, placement.Palette.Query.Length + 2);
                p.PressKey(TestKey.Escape);
            }
            p.PressButton(FurniturePlacementScreen.SearchId);
            p.TypeText(stableId);
            p.PressKey(TestKey.Escape);
            int slot = Enumerable.Range(0, 20).FirstOrDefault(i => placement.Palette.At(i)?.StableId == stableId, -1);
            Assert.That(slot, Is.GreaterThanOrEqualTo(0), "팔레트에서 " + stableId + "를 찾지 못했다");
            p.PressButton(FurniturePlacementScreen.SlotId(slot));
            Assert.That(placement.Active?.StableId, Is.EqualTo(stableId));

            FurnitureRoom room = p.Game.World.Room;
            var keepClear = new List<GridPoint>(room.Grid.Approaches);
            foreach (DoorArea d in p.World.Map.Doors)
                keepClear.Add(room.ToPlacement(CellPos.Containing(d.Area.CenterX, Math.Min(d.Area.CenterY, p.World.Map.Grid.PixelHeight - 1), p.World.Map.Grid.CellSize)));
            var view = new HostPlacementView(p.Game.World, () => p.Game.Fit.Scale);
            float w = p.Game.Fit.LogicalWidth, h = p.Game.Fit.LogicalHeight;
            // 놓인 가구 가까운 칸부터(꾸민 자리가 한곳에 모여 화면에서 보이게).
            float cx = (float)session.House.Placed.Average(f => f.Anchor.X), cy = (float)session.House.Placed.Average(f => f.Anchor.Y);
            foreach (GridPoint c in room.FloorCells.OrderBy(c => Math.Abs(c.X - cx) + Math.Abs(c.Y - cy)))
            {
                UiRect r = view.CellRect(c);
                if (r.X < 0 || r.Y < 0 || r.Right > w || r.Bottom > h || p.Game.Ui.HitTest(r.CenterX, r.CenterY) != null) continue;
                if (keepClear.Any(k => Math.Abs(c.X - k.X) <= 3 && Math.Abs(c.Y - k.Y) <= 3)) continue;
                if (!session.House.CanPlace(definition, c, FurniturePlacementDirection.North)) continue;
                int before = session.House.Placed.Count;
                p.ClickAt(r.CenterX, r.CenterY);
                Assert.That(placement.LastAction, Is.EqualTo("placed"), stableId + " " + c);
                return session.House.Placed.Count == before + 1;
            }
            return false;
        }

        [Test]
        public void ChefDecoratesRestaurant()
        {
            PrepareAdultChefSlot();
            using PlayerDriver player = Start();
            GameSession session = player.Game.Session;
            Assert.That(session.Adult.FacilityId, Is.EqualTo(ChefFacility));
            Workspace workspace = session.Definitions.All<Workspace>().Single(w => w.FacilityId == ChefFacility);

            // 집 → 마을 → 일터 맵(그 면을 가진 맵). 맵 id는 데이터에서 찾는다.
            var interactions = Rootborn.World.InteractionData.Load(Path.Combine(_root, Rootborn.World.InteractionData.DefaultRelativePath));
            string workspaceMap = interactions.MapIds.Single(id => interactions.Map(id).PlacementSurface == workspace.SurfaceId);
            if (player.World.Map.Id != "Town") Through(player, "Town");
            Through(player, workspaceMap);
            Assert.That(session.ActiveWorkspace?.Id, Is.EqualTo(workspace.Id), "일터 맵에 들어오면 Host가 그 면으로 EnterPlacementSurface를 부른다");
            Assert.That(session.OwnsActiveWorkspace, Is.True);
            var seeded = session.Definitions.Get<WorkspaceTemplate>(workspace.DefaultTemplateId);
            Assert.That(session.House.Placed.Select(f => f.Definition.StableId), Is.EquivalentTo(seeded.Placements.Select(t => t.FurnitureId)), "처음 들어오면 기본 템플릿이 깔린다");
            Assert.That(player.Game.World.FurnitureDrawCount, Is.EqualTo(seeded.Placements.Count), "월드가 깔린 가구를 그린다");

            // 꾸미기 전 기준: 지금(기본 템플릿)의 이득. 이 데이터에서는 0이다(아래 기준 보상은 이득 0으로 낸다).
            const string work = "work.chef.level1";
            Assert.That(session.WorkExtrasFor(work).RewardPercentBonus, Is.EqualTo(0), "준비 확인: 기본 템플릿만으로는 보상 이득이 없다");

            // 배치 화면: HUD 단추.
            player.PressButton("hud.button.placement");
            player.WaitForScreen(ScreenCatalog.FurniturePlacement);
            var placement = (FurniturePlacementScreen)player.Screens.Find(ScreenCatalog.FurniturePlacement);
            Assert.That(player.Node(FurniturePlacementScreen.WorkspaceRowId).Visible, Is.True, "일터 줄이 보인다");
            var cozyLabel = (UiLabel)player.Node(FurniturePlacementScreen.QualityId(Cozy));
            string cozyBefore = cozyLabel.Text;

            // 아늑함을 주는 가구를 점수 높은 순으로(데이터의 성질 표), 이득이 생길 때까지 놓는다. 한도 안에서만.
            var quality = session.Definitions.FurnitureQuality;
            var candidates = session.AvailableFurniture()
                .Select(f => (f.StableId, Score: quality.ScoresOf(f.StableId).Where(s => s.QualityId == Cozy).Select(s => s.Score).DefaultIfEmpty(0).Max()))
                .Where(c => c.Score > 0).OrderByDescending(c => c.Score).Select(c => c.StableId).ToList();
            int placedByHand = 0;
            foreach (string id in candidates)
            {
                if (session.WorkExtrasFor(work).RewardPercentBonus > 0) break;
                if (session.CanAddToActiveWorkspace() != WorkspaceResultKind.Applied) break;
                if (session.House.Placed.Any(f => f.Definition.StableId == id)) continue;
                if (PlaceByPointer(player, placement, id)) placedByHand++;
            }
            Assert.That(session.WorkExtrasFor(work).RewardPercentBonus, Is.GreaterThan(0), "한도 안에서 놓아 아늑함 이득에 닿았다(놓은 수 " + placedByHand + ")");
            player.Frames(2);
            Assert.That(cozyLabel.Text, Is.Not.EqualTo(cozyBefore), "성질 점수 글자가 바뀐다");
            player.MovePointer(-50, -50);
            player.Frames(2);
            string placementShot = player.Screenshot("workspace-01-placement");
            TestContext.Out.WriteLine("손으로 놓은 가구 " + placedByHand + "개, 아늑함 " + cozyBefore + " → " + cozyLabel.Text + ", " + placementShot);

            // 템플릿 화면: 템플릿 단추 → 스크린샷 → Esc.
            player.PressButton(FurniturePlacementScreen.TemplatesId);
            player.WaitForScreen(ScreenCatalog.WorkspaceTemplates);
            var templates = (WorkspaceTemplateScreen)player.Screens.Find(ScreenCatalog.WorkspaceTemplates);
            Assert.That(templates.Model.Rows.Length, Is.EqualTo(session.TemplatesForActiveWorkspace().Count));
            player.Frames(2);
            player.MovePointer(-50, -50);
            player.ScreenshotScreen(ScreenCatalog.WorkspaceTemplates, "workspace-02-templates");
            player.PressKey(TestKey.Escape);
            player.WaitForScreenClosed(ScreenCatalog.WorkspaceTemplates);
            Assert.That(player.Screens.IsOpen(ScreenCatalog.FurniturePlacement), Is.True, "Esc는 템플릿 화면만 닫는다");

            // 배치 끔 → 문 → 마을 → 시설 지점.
            player.PressButton("hud.button.placement");
            player.WaitForScreenClosed(ScreenCatalog.FurniturePlacement);
            player.MovePointer(-50, -50);
            Through(player, "Town");
            player.WalkToThing(FacilityScreen.InteractionKind, ChefFacility);
            player.PressKey(TestKey.E);
            player.WaitForScreen(ScreenCatalog.Facility);
            var facility = (FacilityScreen)player.Screens.Find(ScreenCatalog.Facility);
            int index = Array.FindIndex(facility.Model.Works, r => r.Id == work);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            Assert.That(facility.Model.Works[index].RewardPercentBonus, Is.EqualTo(session.WorkExtrasFor(work).RewardPercentBonus), "시설 화면이 일터 이득을 보인다");
            Assert.That(facility.WorkBonusText(work), Is.Not.Null.And.Not.Empty);
            player.Frames(2);
            player.MovePointer(-50, -50);
            player.ScreenshotScreen(ScreenCatalog.Facility, "workspace-03-facility");

            // 일을 직접 하기 → 미니게임(MinigameHostTests와 같은 조작).
            var before = Counts(session);
            UiRect cell = facility.CellRect(FacilityScreen.PlayCell(index));
            Assert.That(player.Game.Ui.HitTest(cell.CenterX, cell.CenterY), Is.SameAs(facility.WorkGrid));
            player.ClickAt(cell.CenterX, cell.CenterY);
            player.WaitForScreen(ScreenCatalog.Minigame);
            Assert.That(session.Adult.HasPendingWork, Is.True);
            var screen = (MinigameScreen)player.Screens.Find(ScreenCatalog.Minigame);
            for (int guard = 0; guard < 8 && !screen.Session.IsComplete; guard++)
            {
                ChallengeSession challenge = screen.Session.Current;
                if (challenge.Mechanic == MinigameMechanic.Sequence)
                {
                    int[] order = challenge.PreviewSequence;
                    for (int i = 0; i < 600 && challenge.PreviewVisible; i++) player.Frames(1);
                    foreach (int answer in order) player.PressButton(SymbolChoiceView.ChoiceId(answer));
                }
                else
                {
                    Assert.That(challenge.Mechanic, Is.EqualTo(MinigameMechanic.Timing), "요리 일 1은 순서 외우기와 박자다");
                    foreach (float beat in challenge.BeatTimes)
                    {
                        player.FramesUntil(() => challenge.Elapsed >= beat - 0.02f, "박자 시각 " + beat);
                        player.PressKey(TestKey.Space);
                    }
                }
                for (int i = 0; i < 180 && screen.Session.Current == challenge && !screen.Session.IsComplete; i++) player.Frames(1);
            }
            Assert.That(screen.Session.IsComplete && screen.Session.Succeeded, Is.True, "점수 " + screen.Session.Score);
            Assert.That(session.Adult.HasPendingWork, Is.False, "정산됐다");
            int score = screen.Session.Score;
            var after = Counts(session);
            var got = (after.Allowance - before.Allowance, after.Materials - before.Materials);

            // 꾸미기 전 기준: 같은 점수, 레벨 비율만(일터 이득 0).
            var definition = session.Definitions.Get<FacilityWork>(work);
            int levelPercent = session.Definitions.Get<Facility>(ChefFacility).GetLevel(session.Adult.FacilityLevel).RewardPercent;
            Assert.That(definition.TryBuildRewards(score, levelPercent, out var plainRewards), Is.True);
            var plain = Sum(plainRewards);
            TestContext.Out.WriteLine("점수 " + score + ": 꾸미기 전 기준 " + plain + ", 받은 것 " + got + ", 이득 +" + session.WorkExtrasFor(work).RewardPercentBonus + "%");
            Assert.That(got.Item1 + got.Item2, Is.GreaterThan(plain.Allowance + plain.Materials), "꾸민 일터의 보상이 꾸미기 전 기준보다 많다");
            Assert.That(got.Item1, Is.GreaterThanOrEqualTo(plain.Allowance));
            Assert.That(got.Item2, Is.GreaterThanOrEqualTo(plain.Materials));

            player.PressKey(TestKey.Escape);
            player.WaitForScreenClosed(ScreenCatalog.Minigame);
            Assert.That(player.CollisionViolations, Is.EqualTo(0));
            Rootborn.Screens.Tests.KnownMissingKeys.AssertOnlyKnownMissing(player.Game.Text, placement.TextKeys.Concat(templates.TextKeys).Concat(facility.TextKeys).Concat(screen.TextKeys));
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
        }
    }
}
