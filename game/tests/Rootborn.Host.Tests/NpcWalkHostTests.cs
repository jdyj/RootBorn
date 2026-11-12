using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.NpcMovement;
using Rootborn.Core.Placement;
using Rootborn.Core.World;
using Rootborn.Host.World;
using Rootborn.Screens;
using Rootborn.Screens.Dialogue;
using Rootborn.Screens.Furniture;
using Rootborn.Ui;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 걷는 NPC를 실제 창과 게임 루프로(장치 이벤트 주입, OS 입력 계층 미확인): 새 슬롯에서 방향 키로 걸어가 학교 문 앞에 서서 시계가 흐르기를 기다린다
    /// (시각을 강제로 바꾸지 않는다, 배속 1) → 정오에 학교를 나와 마을을 걸어가는 선생님 → 따라가 E로 말을 건다 → 선생님이 떠난 학교 자리에는 대상이 없다.
    /// 집에서는 HUD 버튼·검색 칸·포인터로 가구를 놓은 뒤 2시간을 흘려 보내며 안내인이 가구를 지나지 않는지 본다. NPC 자리를 읽는 것은 어디로 걸을지 정하고
    /// 확인하는 데만 쓴다. 스크린샷: output/host-screens-evidence/npc-walk-*.png.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class NpcWalkHostTests
    {
        private const string Teacher = "npc.teacher";
        private const string Guide = "npc.first-guide";
        private const string Desk = "growth31.assembly.desk";
        /// <summary>
        /// 선생님이 학교를 나와 마을을 걷는 시각(그날의 분). 정오(720분)에 학교 자리를 떠나 학교 안을 약 15분 걸어 735분쯤 마을 북쪽 문에 나오고,
        /// 753분쯤 광장 자리에 닿는다(NPC 초당 2.5칸 = 게임 1분에 약 1칸, 2026-11-12 data). 그 사이의 740분.
        /// </summary>
        private const double TeacherWalkingMinutes = 740;
        /// <summary>새 게임 07:00부터 740분까지 배속 1로 약 8,000프레임(게임 1분 ≈ 25프레임). 걸어가는 프레임을 빼고 넉넉히.</summary>
        private const int WaitFrames = 12000;

        private string _saves;
        private string _root;
        private PlayerDriver _player;

        [OneTimeSetUp]
        public void SetUp()
        {
            _root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-npc-walk-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            _player?.Dispose();
            _player = null;
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        private PlayerDriver Start(string slot)
        {
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--save-root", _saves, "--slot", slot, "--size", "1280x720",
                "--evidence", Path.Combine(_root, "output", "host-screens-evidence"),
            });
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        /// <summary>앞의 순서 시험들이 같이 쓰는 판(새 슬롯 slot-1). 순서 1이 띄운다.</summary>
        private PlayerDriver Player => _player ?? throw new InvalidOperationException("순서 1(선생님이 학교에서 나와 마을을 걸어간다)이 먼저 돌아야 한다");

        private static NpcPose PoseOf(PlayerDriver p, string npcId)
        {
            Assert.That(p.Game.World.Npcs.TryPose(npcId, out NpcPose pose), Is.True, npcId + "가 어느 맵에도 없다");
            return pose;
        }

        /// <summary>저절로 뜬 하루 이벤트가 있으면 닫기로 닫는다(시계를 멈추는 모달).</summary>
        private static void DismissPopups(PlayerDriver p)
        {
            p.Frames(2);
            if (!p.Screens.IsOpen(ScreenCatalog.DailyEvent)) return;
            p.PressButton(DailyEventScreen.CloseId);
            p.WaitForScreenClosed(ScreenCatalog.DailyEvent);
        }

        /// <summary>마을의 학교 문(목적지 north) 둘레 2~3칸의 걸을 수 있는 칸까지 걸어가 선다(문 칸에는 들어가지 않는다).</summary>
        private static void StandInFrontOfSchoolDoor(PlayerDriver p)
        {
            WorldMap map = p.World.Map;
            DoorArea door = map.Doors.First(d => d.Destination.HasValue && DoorRoutes.Normalize(d.Target) == DoorRoutes.Normalize("north"));
            var doorCells = new HashSet<CellPos>(CollisionInput.CellsUnder(door.Area, map.Grid.CellSize));
            int Distance(CellPos c) => doorCells.Min(d => Math.Abs(d.X - c.X) + Math.Abs(d.Y - c.Y));
            List<CellPos> path = GridPath.Find(map.Collision, p.PlayerCell, c => !doorCells.Contains(c) && Distance(c) >= 2 && Distance(c) <= 3);
            Assert.That(path, Is.Not.Null, "학교 문 앞으로 갈 길");
            p.WalkTo(path.Count > 0 ? path[path.Count - 1] : p.PlayerCell);
            Assert.That(p.World.Map.Id, Is.EqualTo("Town"), "문을 지나지 않고 문 앞에 섰다");
        }

        [Test, Order(1)]
        public void 선생님이_학교에서_나와_마을을_걸어간다()
        {
            _player = Start("slot-1");
            PlayerDriver p = _player;
            Assert.That(p.World.Map.Id, Is.EqualTo("House"), "새 슬롯은 집에서 시작한다");
            Assert.That(p.WalkThroughDoor("town"), Is.EqualTo("Town"));
            DismissPopups(p);
            StandInFrontOfSchoolDoor(p);
            Assert.That(p.Game.Session.DayMinutesExact, Is.LessThan(720), "이 시험의 전제: 정오 전에 학교 문 앞에 섰다");
            Assert.That(PoseOf(p, Teacher).MapId, Is.EqualTo("School"), "정오 전에는 학교에 있다");

            p.WaitUntilDayMinutes(TeacherWalkingMinutes, WaitFrames);
            NpcPose walking = PoseOf(p, Teacher);
            TestContext.WriteLine(TeacherWalkingMinutes + "분: 선생님 " + walking.MapId + " " + walking.X + "," + walking.Y + (walking.Moving ? " 걷는 중" : " 서 있음"));
            Assert.That(walking.MapId, Is.EqualTo("Town"), "정오가 지나 학교를 나와 마을에 있다");
            Assert.That(walking.Moving, Is.True, "걷고 있다");
            Assert.That(Enumerable.Range(0, p.Game.World.Npcs.Count).Select(i => p.Game.World.Npcs[i].NpcId), Does.Contain(Teacher), "지금 맵의 NPC 목록에 있다(그린다)");

            p.Frames(30);
            NpcPose later = PoseOf(p, Teacher);
            Assert.That((later.X, later.Y), Is.Not.EqualTo((walking.X, walking.Y)), "30프레임 뒤 자리가 달라졌다");
            Assert.That(p.CollisionViolations, Is.EqualTo(0));
            p.Screenshot("npc-walk-01-teacher-walks-in-town");
        }

        [Test, Order(2)]
        public void 걸어가는_NPC에게_다가가_말을_건다()
        {
            PlayerDriver p = Player;
            p.WalkToNpc(Teacher);
            Assert.That(PoseOf(p, Teacher).MapId, Is.EqualTo("Town"));
            Assert.That(p.Game.World.Interactions.Target?.Id, Is.EqualTo(Teacher), "걷는 선생님 곁에서 E의 대상");
            p.PressKey(TestKey.E);
            p.WaitForScreen(ScreenCatalog.Dialogue);
            Assert.That(p.Game.Session.CurrentDialogueNpcId, Is.EqualTo(Teacher));

            // 혼자 하기: 대화가 떠 있으면 시계가 멈추고 NPC도 그 자리에 선다.
            NpcPose talking = PoseOf(p, Teacher);
            double minutes = p.Game.Session.DayMinutesExact;
            p.Frames(60);
            NpcPose after = PoseOf(p, Teacher);
            Assert.That(p.Game.Session.DayMinutesExact, Is.EqualTo(minutes), "대화 중 시계가 멈춘다");
            Assert.That((after.MapId, after.X, after.Y), Is.EqualTo((talking.MapId, talking.X, talking.Y)), "대화 중 NPC 자리가 그대로다");
            p.ScreenshotScreen(ScreenCatalog.Dialogue, "npc-walk-02-talking-to-walking-teacher");
            p.PressButton(DialogueScreen.CloseId);
            p.WaitForScreenClosed(ScreenCatalog.Dialogue);
            Assert.That(p.CollisionViolations, Is.EqualTo(0));
        }

        [Test, Order(3)]
        public void NPC가_떠난_자리에서는_상호작용이_없다()
        {
            PlayerDriver p = Player;
            Assert.That(p.WalkThroughDoor("north"), Is.EqualTo("School"));
            DismissPopups(p);
            Assert.That(p.Game.Session.DayMinutesExact, Is.GreaterThanOrEqualTo(720), "오후");
            Assert.That(PoseOf(p, Teacher).MapId, Is.Not.EqualTo("School"), "선생님은 학교를 떠났다");

            (float X, float Y) seat = p.Game.World.HereData.NpcAnchors["location.school"][0];
            p.WalkNear(seat.X, seat.Y, p.World.Map.Grid.CellSize * 1.25f);
            var here = p.Game.World.Interactions.Target;
            Assert.That(here?.Kind, Is.Not.EqualTo("npc"), "선생님 자리에서 E의 대상이 NPC가 아니다 (" + here?.Kind + "/" + here?.Id + ")");
            int logged = p.Game.World.InteractionLog.Count;
            p.PressKey(TestKey.E);
            p.Frames(10);
            Assert.That(p.Screens.IsOpen(ScreenCatalog.Dialogue), Is.False, "대화 화면이 열리지 않는다");
            Assert.That(p.Game.World.InteractionLog.Skip(logged).Any(e => e.Kind == "npc"), Is.False, "NPC 상호작용이 나오지 않았다");
            Assert.That(p.World.Map.Id, Is.EqualTo("School"));
            p.Screenshot("npc-walk-03-teachers-seat-empty");
        }

        // ---------- 집: 가구를 놓고 2시간 ----------

        private static FurniturePlacementScreen Placement(PlayerDriver p) => (FurniturePlacementScreen)p.Screens.Find(ScreenCatalog.FurniturePlacement);

        private static HostPlacementView View(PlayerDriver p) => new HostPlacementView(p.Game.World, () => p.Game.Fit.Scale);

        [Test, Order(4)]
        public void 집의_NPC는_가구를_지나지_않는다()
        {
            using PlayerDriver p = Start("slot-2");
            Assert.That(p.World.Map.Id, Is.EqualTo("House"));
            FurnitureRoom room = p.Game.World.Room;
            var house = p.Game.Session.House;
            FurnitureDefinition desk = p.Game.Session.Definitions.Get<FurnitureDefinition>(Desk);
            (float X, float Y) home = p.Game.World.HereData.NpcAnchors["location.home"][0];
            int size = p.World.Map.Grid.CellSize;
            GridPoint anchor = room.ToPlacement(CellPos.Containing(home.X, home.Y, size));

            // HUD 버튼으로 배치를 켜고 검색 칸에 책상 id를 쳐 고른 뒤, 안내인이 걸어 다니는 둘레(반지름 3칸) 안의 칸을 포인터로 누른다.
            p.PressButton("hud.button.placement");
            p.WaitForScreen(ScreenCatalog.FurniturePlacement);
            FurniturePlacementScreen screen = Placement(p);
            p.PressButton(FurniturePlacementScreen.SearchId);
            p.TypeText(Desk);
            int slot = Enumerable.Range(0, 20).First(i => screen.Palette.At(i)?.StableId == Desk);
            p.PressButton(FurniturePlacementScreen.SlotId(slot));
            HostPlacementView view = View(p);
            GridPoint spawn = room.Grid.Spawn;
            GridPoint target = room.FloorCells
                .Where(c => Math.Max(Math.Abs(c.X - anchor.X), Math.Abs(c.Y - anchor.Y)) is >= 1 and <= 3)
                .Where(c => Math.Abs(c.X - spawn.X) > 2 || Math.Abs(c.Y - spawn.Y) > 2)
                .Where(c => house.CanPlace(desk, c, FurniturePlacementDirection.North))
                .Where(c => { UiRect r = view.CellRect(c); return r.X >= 0 && r.Y >= 0 && r.Right <= 1920 && r.Bottom <= 1080 && p.Game.Ui.HitTest(r.CenterX, r.CenterY) == null; })
                .OrderBy(c => Math.Abs(c.X - anchor.X) + Math.Abs(c.Y - anchor.Y))
                .First();
            UiRect cellRect = view.CellRect(target);
            p.ClickAt(cellRect.CenterX, cellRect.CenterY);
            Assert.That(screen.LastAction, Is.EqualTo("placed"), "안내인 둘레에 책상을 놓았다");
            Assert.That(house.BlockingCells, Is.Not.Empty, "책상이 칸을 막는다");
            p.PressButton("hud.button.placement");
            p.WaitForScreenClosed(ScreenCatalog.FurniturePlacement);

            // 2시간을 흘려 보내며 프레임마다 안내인의 칸이 막힌 칸(벽·가구)이 아닌지 본다.
            var blocked = new HashSet<CellPos>(house.BlockingCells.Select(room.ToMap));
            double end = p.Game.Session.DayMinutesExact + 120;
            var problems = new List<string>();
            int framesInHouse = 0, movingFrames = 0;
            for (int i = 0; i < 4000 && p.Game.Session.DayMinutesExact < end; i++)
            {
                p.Frames(1);
                if (!p.Game.World.Npcs.TryPose(Guide, out NpcPose pose) || pose.MapId != "House") continue;
                framesInHouse++;
                if (pose.Moving) movingFrames++;
                CellPos cell = CellPos.Containing(pose.X, pose.Y, size);
                if (p.World.Map.Collision.IsBlocked(cell.X, cell.Y) || blocked.Contains(cell))
                    problems.Add(p.Game.Session.DayMinutesExact.ToString("0.00") + "분: 안내인이 막힌 칸 " + cell + "(" + pose.X + "," + pose.Y + ")");
            }
            TestContext.WriteLine("책상 칸 (" + target.X + "," + target.Y + "), 안내인 자리 칸 (" + anchor.X + "," + anchor.Y + "), 집에 있던 프레임 " + framesInHouse + ", 걷던 프레임 " + movingFrames);
            Assert.That(p.Game.Session.DayMinutesExact, Is.GreaterThanOrEqualTo(end), "2시간이 흘렀다");
            Assert.That(framesInHouse, Is.GreaterThan(0), "이 시험의 전제: 안내인이 집에 있었다");
            Assert.That(movingFrames, Is.GreaterThan(0), "이 시험의 전제: 안내인이 걸어 다녔다");
            Assert.That(problems, Is.Empty, string.Join("\n", problems.Take(10)));
            p.Screenshot("npc-walk-04-house-guide-around-desk");
        }
    }
}
