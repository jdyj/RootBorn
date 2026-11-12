using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Dialogue;
using Rootborn.Core.NpcMovement;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;
using Rootborn.Core.World;

namespace Rootborn.World.Tests
{
    /// <summary>
    /// 자동 테스트(창 없음): 실제 일과표·맵·NPC 자리로 NPC가 하루를 지낼 수 있는지 — 시간대 사이에 길이 있고, 만날 수 있는 때가 있고,
    /// 막힌 칸에 서지 않는다. 칸 단위 판정이고 화면 근거가 아니다.
    /// </summary>
    public sealed class NpcRouteContractTests
    {
        private static InteractionData Interactions => InteractionData.Load(Path.Combine(Repo.Root, InteractionData.DefaultRelativePath));

        private static IReadOnlyList<string> NpcOrder => NpcArt.Load(Repo.Root).Order;

        private static MapAnchors Anchors(WorldMapLoader loader) => new MapAnchors(Interactions, loader.Catalog.Maps.Select(m => m.Id));

        private static NpcScheduleLookupCache Schedules => new NpcScheduleLookupCache(Repo.Definitions.All<NpcSchedule>());

        private static TimeRules Rules => Repo.Definitions.TimeRules;

        /// <summary>시간대마다 NPC 모두의 자리(없으면 null). NpcWorld처럼 레지스트리 NPC 순서로 같은 장소의 자리 번호를 센다.</summary>
        private static List<(string SlotId, NpcSpot?[] Spots)> SlotSpots(MapAnchors anchors)
        {
            var slots = new List<(int Minutes, string SlotId)>();
            LocationStateTimeSlots.SlotStarts(Rules.DayStartMinutes, Rules.DayEndMinutes, slots);
            IReadOnlyList<string> order = NpcOrder;
            NpcScheduleLookupCache schedules = Schedules;
            var resolver = new NpcScheduleResolver();
            var result = new List<(string, NpcSpot?[])>();
            foreach (var slot in slots)
            {
                var spots = new NpcSpot?[order.Count];
                var used = new Dictionary<string, int>(StringComparer.Ordinal);
                var context = new NpcScheduleContext(1, 0, slot.SlotId, null);
                for (int i = 0; i < order.Count; i++)
                {
                    if (!schedules.TryGetByNpcId(order[i], out NpcSchedule schedule)) continue;
                    string location = resolver.Resolve(schedule, in context).LocationId;
                    if (string.IsNullOrEmpty(location)) continue;
                    used.TryGetValue(location, out int n);
                    used[location] = n + 1;
                    if (anchors.TryGet(location, n, out NpcSpot spot)) spots[i] = spot;
                }
                result.Add((slot.SlotId, spots));
            }
            return result;
        }

        [Test]
        public void 일과표의_이웃한_시간대_자리_사이에_길이_있다()
        {
            WorldMapLoader loader = Repo.Loader();
            var finder = new NpcRouteFinder(loader);
            List<(string SlotId, NpcSpot?[] Spots)> slots = SlotSpots(Anchors(loader));
            IReadOnlyList<string> order = NpcOrder;
            var missing = new List<string>();
            for (int k = 0; k + 1 < slots.Count; k++)
                for (int i = 0; i < order.Count; i++)
                {
                    NpcSpot? from = slots[k].Spots[i], to = slots[k + 1].Spots[i];
                    if (from == null || to == null) continue;
                    if (finder.Find(from.Value, to.Value) == null)
                        missing.Add(order[i] + ": " + slots[k].SlotId + " " + Describe(from.Value) + " → " + slots[k + 1].SlotId + " " + Describe(to.Value)
                            + (finder.LastProblem != null ? " (" + finder.LastProblem + ")" : ""));
                }
            Assert.That(missing, Is.Empty, string.Join("\n", missing));
        }

        [Test]
        public void 모든_NPC는_하루_중_만날_수_있는_때가_있다()
        {
            WorldMapLoader loader = Repo.Loader();
            MapEntry town = loader.Catalog.Find("Town");
            Assert.That(town, Is.Not.Null, "이 시험의 전제: 마을 맵");
            var reachableMaps = new HashSet<string>(MapChecks.Walk(loader, new WorldPlace(town.Id, MapChecks.StartSpawn(town))).Select(p => p.MapId), StringComparer.Ordinal);
            List<(string SlotId, NpcSpot?[] Spots)> slots = SlotSpots(Anchors(loader));
            IReadOnlyList<string> order = NpcOrder;
            var never = new List<string>();
            for (int i = 0; i < order.Count; i++)
            {
                bool met = false;
                foreach (var slot in slots)
                    if (slot.Spots[i] is NpcSpot spot && reachableMaps.Contains(spot.MapId)) { met = true; break; }
                if (!met) never.Add(order[i]);
            }
            Assert.That(never, Is.Empty, "마을에서 걸어서 닿는 맵에 하루 내내 자리가 없는 NPC: " + string.Join(", ", never));
        }

        [Test]
        public void 하루를_10분씩_훑어도_NPC는_막힌_칸에_서지_않는다()
        {
            WorldMapLoader loader = Repo.Loader();
            NpcStage stage = NpcStageTests.RealStage(loader);
            var blocked = new List<string>();
            int present = 0;
            for (int minutes = 420; minutes <= 1859; minutes += 10)
            {
                stage.Tick(1, minutes);
                foreach (string npcId in NpcOrder)
                {
                    if (!stage.TryPose(npcId, out NpcPose pose)) continue;
                    present++;
                    WorldMap map = loader.LoadMap(pose.MapId).World;
                    CellPos cell = CellPos.Containing(pose.X, pose.Y, map.Grid.CellSize);
                    if (map.Collision.IsBlocked(cell))
                        blocked.Add(npcId + " " + minutes + "분: " + pose.MapId + " 칸 (" + cell.X + ", " + cell.Y + ")");
                }
            }
            Assert.That(present, Is.GreaterThan(0), "이 시험의 전제: 하루 중 어느 맵엔가 선 NPC가 있다");
            Assert.That(blocked, Is.Empty, string.Join("\n", blocked));
        }

        [Test]
        public void npcWalker를_읽는다()
        {
            Assert.That(Repo.Loader().Catalog.NpcWalker.SpeedCellsPerSecond, Is.EqualTo(2.5f));
        }

        [Test]
        public void npcWalker가_없는_맵_목록은_읽기를_거절한다()
        {
            const string map = @"{ ""maps"": [ { ""id"": ""A"", ""ldtk"": ""a.ldtk"", ""level"": ""L"", ""viewCells"": 10, ""collision"": { ""rule"": ""interior-auto"" },
                ""doors"": {}, ""spawns"": { ""a"": [ 16, 16 ] } } ] }";
            Assert.Throws<InvalidDataException>(() => WorldCatalog.Parse(map));
            Assert.That(WorldCatalog.Parse(map.Replace(@"{ ""maps""", @"{ ""npcWalker"": { ""speedCellsPerSecond"": 2.5 }, ""maps""")).NpcWalker.SpeedCellsPerSecond,
                Is.EqualTo(2.5f));
        }

        [Test]
        public void MapAnchors는_맵_목록_순서로_처음_나오는_맵의_자리를_쓴다()
        {
            InteractionData data = InteractionData.Parse(@"{ ""radiusCells"": 1.75, ""npcPriority"": 30, ""doorPriority"": 110, ""maps"": {
                ""B"": { ""npcAnchors"": { ""location.x"": [ [ 10, 20 ], [ 30, 40 ] ] } },
                ""A"": { ""npcAnchors"": { ""location.x"": [ [ 50, 60 ] ], ""location.y"": [ [ 70, 80 ] ] } } } }");
            var anchors = new MapAnchors(data, new[] { "A", "B" });

            Assert.That(anchors.TryGet("location.x", 0, out NpcSpot first), Is.True);
            Assert.That((first.MapId, first.X, first.Y), Is.EqualTo(("A", 50f, 60f)));
            Assert.That(anchors.TryGet("location.x", 1, out _), Is.False, "처음 나오는 맵(A)의 자리가 모자라면 다음 맵(B)에서 찾지 않는다");
            Assert.That(anchors.TryGet("location.y", 0, out NpcSpot y), Is.True);
            Assert.That((y.MapId, y.X, y.Y), Is.EqualTo(("A", 70f, 80f)));
            Assert.That(anchors.TryGet("location.none", 0, out _), Is.False);
            Assert.That(anchors.TryGet("location.x", -1, out _), Is.False);
            Assert.That(new MapAnchors(data, new[] { "B" }).TryGet("location.x", 1, out NpcSpot second), Is.True);
            Assert.That((second.MapId, second.X, second.Y), Is.EqualTo(("B", 30f, 40f)));
        }

        private static string Describe(NpcSpot spot) => spot.MapId + " (" + spot.X + ", " + spot.Y + ")";
    }
}
