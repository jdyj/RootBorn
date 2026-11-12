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

        /// <summary>
        /// 성능 측정(설계 8절): NPC 60명(실제 일과표 다섯을 id만 달리해 열두 벌씩 메모리에서 만든 것)으로 계획 만들기와 Tick 10,000번을 잰다.
        /// 기준: Tick 한 번 평균 0.05ms 아래, 걸음이 넘어가지 않는 Tick에서 할당 0. 잰 값은 테스트 출력에 남긴다(벽시계는 기계마다 흔들린다 — 기준은 넉넉하다).
        /// </summary>
        [Test]
        public void 성능_NPC_60명의_계획과_Tick()
        {
            const int Copies = 12, Ticks = 10000;
            WorldMapLoader loader = Repo.Loader();
            IReadOnlyList<NpcSchedule> real = Repo.Definitions.All<NpcSchedule>();
            var schedules = new List<NpcSchedule>(real.Count * Copies);
            var ids = new List<string>(real.Count * Copies);
            for (int k = 0; k < Copies; k++)
                foreach (NpcSchedule s in real)
                {
                    NpcDefinition n = s.Npc;
                    string id = n.Id + "#" + k;
                    var npc = new NpcDefinition(id, n.DisplayNameKey, n.IntroductionKey, n.HomeLocationId, n.Roles.ToArray(), n.DefaultDialogue,
                        n.DefaultDialogueConditions.ToArray(), n.StageDialogues.ToArray(), n.Quests.ToArray());
                    schedules.Add(new NpcSchedule(s.Id + "#" + k, npc, s.FallbackLocationId, s.Entries.ToArray()));
                    ids.Add(id);
                }
            Assert.That(ids.Count, Is.EqualTo(60));
            InteractionData data = Interactions;
            var anchors = new CrowdedAnchors(new MapAnchors(data, loader.Catalog.Maps.Select(m => m.Id)));
            var world = new NpcWorld(ids, new NpcScheduleLookupCache(schedules), anchors, loader, loader.Catalog.NpcWalker, Repo.Definitions.TimeRules);
            var stage = new NpcStage(world, data);
            stage.EnterMap("Town", data.Map("Town"), loader.LoadMap("Town").World.Doors);

            var watch = System.Diagnostics.Stopwatch.StartNew();
            stage.Tick(1, 900);
            double coldMs = watch.Elapsed.TotalMilliseconds; // 첫 계획: 맵 격자 읽기와 JIT 포함
            int present = 0;
            foreach (string id in ids) if (stage.TryPose(id, out NpcPose _)) present++;
            Assert.That(present, Is.EqualTo(60), "이 시험의 전제: 60명 모두 어느 맵엔가 있다");

            watch.Restart();
            stage.Tick(2, 900);
            double nextDayMs = watch.Elapsed.TotalMilliseconds; // 날이 바뀐 Prepare(격자는 기억)

            // 가구를 놓은 순간: ForgetMap 직후 한 프레임에 60명 계획을 다시 만든다.
            var rebuilds = new List<double>(5);
            for (int r = 0; r < 5; r++)
            {
                stage.ForgetMap("House");
                watch.Restart();
                stage.Tick(2, 900);
                rebuilds.Add(watch.Elapsed.TotalMilliseconds);
            }
            rebuilds.Sort();

            var moving = new bool[ids.Count];
            for (int i = 0; i < ids.Count; i++) moving[i] = stage.TryPose(ids[i], out NpcPose p) && p.Moving;
            long quietBytes = 0, allBytes = 0;
            int quiet = 0, walking = 0;
            double tickMs = 0;
            const double FrameMinutes = 1.0 / 60.0 * 1440.0 / 600.0; // 60프레임/초, 하루 실제 600초일 때 한 프레임의 게임 분(측정용 간격)
            for (int k = 1; k <= Ticks; k++)
            {
                double t = 900 + k * FrameMinutes;
                long before = GC.GetAllocatedBytesForCurrentThread();
                long started = System.Diagnostics.Stopwatch.GetTimestamp();
                stage.Tick(2, t);
                tickMs += (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                long used = GC.GetAllocatedBytesForCurrentThread() - before;
                allBytes += used;
                bool stepChanged = false;
                for (int i = 0; i < ids.Count; i++)
                {
                    bool now = stage.TryPose(ids[i], out NpcPose p) && p.Moving;
                    if (now) walking++;
                    if (now != moving[i]) stepChanged = true;
                    moving[i] = now;
                }
                if (stepChanged) continue;
                quiet++;
                quietBytes += used;
            }
            double meanMs = tickMs / Ticks;
            TestContext.WriteLine("[NPC 60명] 첫 계획(격자 읽기·JIT 포함) " + coldMs.ToString("0.0") + "ms, 다음 날 계획 " + nextDayMs.ToString("0.0")
                + "ms, 가구를 놓은 뒤 다시 만들기(ForgetMap 직후 한 Tick) 가장 짧은 " + rebuilds[0].ToString("0.00") + "ms / 가운데 " + rebuilds[2].ToString("0.00")
                + "ms / 가장 긴 " + rebuilds[4].ToString("0.00") + "ms");
            TestContext.WriteLine("[NPC 60명] Tick " + Ticks + "번(게임 " + (Ticks * FrameMinutes).ToString("0") + "분) 합 " + tickMs.ToString("0.0") + "ms, 평균 "
                + (meanMs * 1000).ToString("0.0") + "µs, 걷는 NPC 평균 " + (walking / (double)Ticks).ToString("0.0") + "명, 걸음이 넘어가지 않은 Tick " + quiet
                + "번의 할당 " + quietBytes + "바이트, 전체 할당 " + allBytes + "바이트(새 머무름이 시작될 때 — 시간대가 바뀌어 자리에 닿은 NPC의 어슬렁 칸 목록)");
            Assert.That(walking, Is.GreaterThan(Ticks), "이 시험의 전제: 재는 동안 걷는 NPC가 있다");
            Assert.That(quiet, Is.GreaterThan(Ticks / 2), "이 시험의 전제: 걸음이 넘어가지 않는 Tick이 대부분이다");
            Assert.That(quietBytes, Is.EqualTo(0), quiet + "번 Tick에 새로 잡은 바이트");
            Assert.That(meanMs, Is.LessThan(0.05), "Tick 한 번 평균(ms)");
        }

        /// <summary>장소의 자리보다 NPC가 많으면 남는 NPC는 그 장소의 마지막 자리를 같이 쓴다(측정용 — 몸끼리 막지 않는다).</summary>
        private sealed class CrowdedAnchors : INpcAnchors
        {
            private readonly INpcAnchors _inner;

            public CrowdedAnchors(INpcAnchors inner) { _inner = inner; }

            public bool TryGet(string locationId, int slot, out NpcSpot spot)
            {
                for (int s = slot; s >= 0; s--)
                    if (_inner.TryGet(locationId, s, out spot)) return true;
                spot = default;
                return false;
            }
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
