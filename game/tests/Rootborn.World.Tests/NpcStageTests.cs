using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Dialogue;
using Rootborn.Core.Interaction;
using Rootborn.Core.NpcMovement;
using Rootborn.Core.World;

namespace Rootborn.World.Tests
{
    /// <summary>
    /// 자동 테스트(창 없음): 실제 일과표·맵·NPC 자리로 NpcStage가 지금 맵의 NPC를 고르고, NPC 상호작용 후보를 옮기고 켜고 끄는지.
    /// 칸·픽셀 판정이고 화면 근거가 아니다.
    /// </summary>
    public sealed class NpcStageTests
    {
        private static InteractionData Interactions => InteractionData.Load(Path.Combine(Repo.Root, InteractionData.DefaultRelativePath));

        private static IReadOnlyList<string> Order => NpcArt.Load(Repo.Root).Order;

        /// <summary>실제 데이터로 만든 NpcStage(Host·창 없는 판과 같은 공장). 맵 원본을 주지 않으면 맵 읽개 그대로.</summary>
        internal static NpcStage RealStage(WorldMapLoader loader, IWorldMapSource maps = null) =>
            NpcStage.Create(new NpcScheduleLookupCache(Repo.Definitions.All<NpcSchedule>()), NpcArt.Load(Repo.Root).Order, Interactions,
                loader.Catalog, maps ?? loader, Repo.Definitions.TimeRules);

        private static (NpcStage Stage, List<InteractionCandidate> Candidates) Enter(WorldMapLoader loader, string mapId, IWorldMapSource maps = null)
        {
            NpcStage stage = RealStage(loader, maps);
            List<InteractionCandidate> candidates = stage.EnterMap(mapId, Interactions.Map(mapId), loader.LoadMap(mapId).World.Doors);
            return (stage, candidates);
        }

        [Test]
        public void 지금_맵에_있는_NPC만_보인다()
        {
            var (stage, _) = Enter(Repo.Loader(), "Town");
            stage.Tick(1, 900);
            Assert.That(stage.Count, Is.GreaterThan(0), "오후 한가운데 마을에 NPC가 있다");
            for (int i = 0; i < stage.Count; i++)
            {
                Assert.That(stage[i].Pose.Present, Is.True);
                Assert.That(stage[i].Pose.MapId, Is.EqualTo("Town"), stage[i].NpcId);
            }
            int elsewhere = Order.Count(id => stage.TryPose(id, out NpcPose p) && p.MapId != "Town");
            Assert.That(stage.Count + elsewhere, Is.LessThanOrEqualTo(Order.Count));
            Assert.Throws<ArgumentOutOfRangeException>(() => { var _ = stage[stage.Count]; });
        }

        [Test]
        public void 떠난_NPC의_후보는_꺼진다()
        {
            var (stage, candidates) = Enter(Repo.Loader(), "School");
            InteractionCandidate teacher = candidates.Single(c => c.Kind == "npc" && c.Id == "npc.teacher");
            stage.Tick(1, 500);
            Assert.That(teacher.Active, Is.True, "오전에는 학교에 있다");
            Assert.That(stage.Count, Is.EqualTo(1));
            stage.Tick(1, 900);
            Assert.That(teacher.Active, Is.False, "오후에는 광장으로 떠났다");
            Assert.That(stage.Count, Is.EqualTo(0));
            Assert.That(stage.TryPose("npc.teacher", out NpcPose pose), Is.True);
            Assert.That(pose.MapId, Is.EqualTo("Town"));
        }

        [Test]
        public void 후보_좌표는_NPC_자리를_따라간다()
        {
            var (stage, candidates) = Enter(Repo.Loader(), "Town");
            double t = 900;
            bool moving = false;
            for (; t < 1000 && !moving; t += 0.05)
            {
                stage.Tick(1, t);
                for (int i = 0; i < stage.Count; i++) moving |= stage[i].Pose.Moving;
            }
            Assert.That(moving, Is.True, "이 시험의 전제: 오후 마을에 걷는 NPC가 있다");
            for (int k = 0; k < 2; k++, t += 0.05)
            {
                stage.Tick(1, t);
                for (int i = 0; i < stage.Count; i++)
                {
                    NpcOnMap npc = stage[i];
                    InteractionCandidate c = candidates.Single(x => x.Kind == "npc" && x.Id == npc.NpcId);
                    Assert.That(c.Active, Is.True);
                    Assert.That((c.X, c.Y), Is.EqualTo((npc.Pose.X, npc.Pose.Y)), npc.NpcId + " " + t + "분");
                }
            }
        }

        [Test]
        public void 맵에_들어온_때가_달라도_같은_시각에는_같은_자리다()
        {
            WorldMapLoader loader = Repo.Loader();
            var (early, _) = Enter(loader, "School");
            for (int m = 500; m <= 900; m++) early.Tick(1, m);
            var (late, _) = Enter(loader, "Town");
            late.Tick(1, 900);
            foreach (string id in Order)
            {
                bool a = early.TryPose(id, out NpcPose p), b = late.TryPose(id, out NpcPose q);
                Assert.That(b, Is.EqualTo(a), id);
                Assert.That((q.MapId, q.X, q.Y, q.Facing, q.Moving), Is.EqualTo((p.MapId, p.X, p.Y, p.Facing, p.Moving)), id);
            }
        }

        [Test]
        public void Tick은_새_객체를_만들지_않는다()
        {
            var (stage, _) = Enter(Repo.Loader(), "Town");
            stage.Tick(1, 899.99); // 계획 만들기와 JIT를 뺀다
            stage.Tick(1, 899.995);
            IReadOnlyList<string> order = Order;
            var moving = new bool[order.Count];
            for (int i = 0; i < order.Count; i++) moving[i] = stage.TryPose(order[i], out NpcPose p) && p.Moving;

            long bytes = 0;
            int measured = 0;
            for (int k = 0; k < 200; k++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                stage.Tick(1, 900 + k * 0.01);
                long used = GC.GetAllocatedBytesForCurrentThread() - before;
                // 어슬렁 걸음이 끝나 다음 걸음을 뽑는 프레임(걷기 ↔ 쉼이 바뀐다)은 머무는 행동이 새 길을 만든다 — 재는 범위에서 뺀다.
                bool stepChanged = false;
                for (int i = 0; i < order.Count; i++)
                {
                    bool now = stage.TryPose(order[i], out NpcPose p) && p.Moving;
                    if (now != moving[i]) stepChanged = true;
                    moving[i] = now;
                }
                if (stepChanged) continue;
                bytes += used;
                measured++;
            }
            Assert.That(measured, Is.GreaterThan(150), "이 시험의 전제: 걸음이 바뀌지 않은 Tick이 대부분이다");
            Assert.That(bytes, Is.EqualTo(0), measured + "번 Tick에 새로 잡은 바이트");
        }

        [Test]
        public void 가구가_바뀐_맵은_ForgetMap_뒤에_새_격자로_걷는다()
        {
            // 가구 방(FurnitureRoom)처럼 충돌 격자의 배열을 제자리에서 고친다. 맵 원본은 같은 WorldMap을 돌려준다.
            WorldMapLoader loader = Repo.Loader();
            WorldMap house = loader.LoadMap("House").World;
            MapGrid grid = house.Grid;
            var blocked = new bool[grid.Count];
            for (int y = 0; y < grid.Rows; y++)
                for (int x = 0; x < grid.Columns; x++)
                    blocked[grid.Index(x, y)] = house.Collision.IsBlocked(x, y);
            var spawns = new Dictionary<string, (float X, float Y)>();
            foreach (string id in house.SpawnIds) { house.TryGetSpawn(id, out var p); spawns[id] = p; }
            var room = new WorldMap(house.Id, new CollisionGrid(grid, blocked, house.Collision.OpenOutside), house.Doors, spawns);
            var maps = new OneMapSource(loader, room);
            var (stage, _) = Enter(loader, "House", maps);

            CellPos anchor = CellPos.Containing(624f, 432f, grid.CellSize);
            var visited = new HashSet<CellPos>();
            for (double t = 420; t < 600; t += 0.25)
            {
                stage.Tick(1, t);
                Assert.That(stage.TryPose("npc.first-guide", out NpcPose p), Is.True);
                visited.Add(CellPos.Containing(p.X, p.Y, grid.CellSize));
            }
            visited.Remove(anchor);
            Assert.That(visited, Is.Not.Empty, "이 시험의 전제: 엄마가 자리 둘레를 걸어 다녔다");

            foreach (CellPos c in visited) blocked[grid.Index(c.X, c.Y)] = true; // 가구가 그 칸들을 막았다
            stage.ForgetMap("House");
            var stepped = new List<string>();
            for (double t = 600; t < 720; t += 0.25)
            {
                stage.Tick(1, t);
                Assert.That(stage.TryPose("npc.first-guide", out NpcPose p), Is.True);
                CellPos c = CellPos.Containing(p.X, p.Y, grid.CellSize);
                if (room.Collision.IsBlocked(c)) stepped.Add(t + "분 (" + c.X + ", " + c.Y + ")");
            }
            Assert.That(stepped, Is.Empty, "가구가 막은 칸에 들어갔다(" + stepped.Count + "번): " + string.Join(", ", stepped.Take(10)));
        }

        [Test]
        public void 어슬렁거리는_NPC가_곁에_와도_지점_칸_가운데에_서면_대상은_그_지점이다()
        {
            // 플레이어가 지점 칸 가운데(지점의 가운데)에 서면 그 지점까지 거리는 0이다 — NPC가 같은 칸에 있어도 더 가깝지 않고, 같은 거리면 우선순위(지점 > NPC).
            WorldMapLoader loader = Repo.Loader();
            InteractionData data = Interactions;
            var (stage, candidates) = Enter(loader, "Town");
            int cell = loader.LoadMap("Town").World.Grid.CellSize;
            var targets = new InteractionTargets(data.RadiusCells * cell);
            targets.SetCandidates(candidates);
            List<InteractionThing> points = data.Map("Town").Things.Where(t => t.Priority > data.NpcPriority).ToList();
            int near = 0, sameCell = 0;
            var wrong = new List<string>();
            for (double t = 420; t < 1860; t += 0.25)
            {
                stage.Tick(1, t);
                for (int i = 0; i < stage.Count; i++)
                {
                    NpcPose pose = stage[i].Pose;
                    foreach (InteractionThing point in points)
                    {
                        float dx = pose.X - point.X, dy = pose.Y - point.Y;
                        if (dx * dx + dy * dy > targets.RadiusPixels * targets.RadiusPixels) continue;
                        near++;
                        if (CellPos.Containing(pose.X, pose.Y, cell).Equals(CellPos.Containing(point.X, point.Y, cell))) sameCell++;
                        InteractionCandidate target = targets.Update(point.X, point.Y);
                        if (target == null || target.Kind == "npc" || target.X != point.X || target.Y != point.Y)
                            wrong.Add(t + "분 " + stage[i].NpcId + " 곁의 " + point.Kind + "/" + point.Id + " → " + (target == null ? "(없음)" : target.Kind + "/" + target.Id));
                    }
                }
            }
            TestContext.WriteLine("NPC가 지점 곁(상호작용 반경 안)에 온 순간 " + near + "번, 같은 칸 " + sameCell + "번");
            Assert.That(near, Is.GreaterThan(0), "이 시험의 전제: 어슬렁거리는 NPC가 활동·알바 지점 곁에 온다");
            Assert.That(sameCell, Is.GreaterThan(0), "이 시험의 전제: 지점과 같은 칸에 NPC가 서는 순간이 있다");
            Assert.That(wrong, Is.Empty, string.Join("\n", wrong.Take(20)));
        }

        /// <summary>한 맵만 바꿔 주는 맵 원본(나머지는 맵 읽개).</summary>
        private sealed class OneMapSource : IWorldMapSource
        {
            private readonly IWorldMapSource _inner;
            private readonly WorldMap _map;

            public OneMapSource(IWorldMapSource inner, WorldMap map) { _inner = inner; _map = map; }

            public WorldMap Load(string mapId) => string.Equals(mapId, _map.Id, StringComparison.Ordinal) ? _map : _inner.Load(mapId);
        }
    }
}
