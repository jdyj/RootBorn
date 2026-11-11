using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Interaction;
using Rootborn.Core.StudentLife;
using Rootborn.Core.World;

namespace Rootborn.World.Tests
{
    /// <summary>
    /// 자동 테스트(창 없음, 칸 단위 경로): 실제 맵 데이터(maps.json, interactions.json)의 모든 상호작용 지점이 새 게임 시작 자리에서
    /// 문을 지나 걸어서 닿고, 그 자리에 서면 그 지점이 대상으로 골라지며, 가리키는 정의가 data/에 있다.
    /// 탐색·성인 시설·진로 연습은 data/의 정의 전부가 지점을 가진다. 화면 근거가 아니다.
    /// 화면을 여는 상호작용 종류마다 지점이 있는지는 Screens 테스트(WorldEntryPointKindsTests)가 화면 등록 표로 본다.
    /// </summary>
    public sealed class WorldEntryPointsTests
    {
        /// <summary>
        /// 지점 id가 data/의 정의가 아니라 지점 이름일 뿐인 종류. 이 종류의 화면은 id를 읽지 않는다
        /// (일과는 오늘 계획을, 하루 결과는 오늘 하루를 보인다).
        /// </summary>
        private static readonly HashSet<string> KindsWhoseIdIsOnlyAPointName = new HashSet<string>(StringComparer.Ordinal)
        {
            "daily-plan-board", "student-day-end",
        };

        private static InteractionData Interactions => InteractionData.Load(Path.Combine(Repo.Root, InteractionData.DefaultRelativePath));

        private sealed class Reach
        {
            public readonly Dictionary<string, WorldMap> Maps = new Dictionary<string, WorldMap>(StringComparer.Ordinal);
            public readonly Dictionary<string, HashSet<CellPos>> Cells = new Dictionary<string, HashSet<CellPos>>(StringComparer.Ordinal);
        }

        /// <summary>새 게임 자리에서 문을 지나 걸어 닿는 모든 곳과, 맵마다 그 도착 지점들에서 닿는 칸의 합.</summary>
        private static Reach WalkFromTheNewGameSpot()
        {
            WorldMapLoader loader = Repo.Loader();
            WorldEntryPoint start = WorldEntryRules.ResolveNewGame(WorldTravelData.From(Repo.Definitions)).Value;
            var reach = new Reach();
            foreach (WorldPlace place in MapChecks.Walk(loader, new WorldPlace(start.SceneName, start.SpawnId)))
            {
                if (!reach.Maps.TryGetValue(place.MapId, out WorldMap map))
                {
                    map = loader.LoadMap(place.MapId).World;
                    reach.Maps[place.MapId] = map;
                    reach.Cells[place.MapId] = new HashSet<CellPos>();
                }
                reach.Cells[place.MapId].UnionWith(MapChecks.Reachable(map, place.SpawnId));
            }
            return reach;
        }

        private static string Name(string mapId, InteractionThing t) => mapId + ":" + t.Kind + "/" + t.Id + " (" + t.X + ", " + t.Y + ")";

        [Test]
        public void EveryPoint_IsWalkableFromTheNewGameSpot()
        {
            InteractionData data = Interactions;
            Reach reach = WalkFromTheNewGameSpot();
            var missing = new List<string>();
            foreach (string mapId in data.MapIds)
            {
                MapInteractions here = data.Map(mapId);
                if (!reach.Maps.TryGetValue(mapId, out WorldMap map))
                {
                    if (here.Things.Count > 0 || here.NpcAnchors.Count > 0) missing.Add(mapId + ": 맵에 걸어서 들어가지 못한다");
                    continue;
                }
                int size = map.Grid.CellSize;
                foreach (InteractionThing t in here.Things)
                    if (!MapChecks.NextTo(reach.Cells[mapId], t.X, t.Y, size)) missing.Add(Name(mapId, t) + "이 걸어서 닿는 칸 옆에 없다");
                foreach (var pair in here.NpcAnchors)
                    foreach (var a in pair.Value)
                        if (!MapChecks.NextTo(reach.Cells[mapId], a.X, a.Y, size)) missing.Add(mapId + ": NPC 자리 " + pair.Key + " (" + a.X + ", " + a.Y + ")이 걸어서 닿는 칸 옆에 없다");
            }
            Assert.That(missing, Is.Empty, string.Join("\n", missing));
        }

        [Test]
        public void EveryPoint_BecomesTheTargetFromSomeWalkableCell_UnlessAStrongerPointSitsOnTheSameSpot()
        {
            // 같은 자리에 더 높은 우선순위의 지점이 있으면 가려진다(장소 방문은 같은 자리의 장소 활동 밑에 있다 — README 5절).
            // 그 밖의 지점은 걸어 닿는 칸 하나 이상(칸 가운데에 섰을 때)에서 Core 대상 고르기(InteractionTargets: 가까운 것 먼저,
            // 거리가 같으면 우선순위)가 그 지점을 고른다. 문·NPC 자리도 후보에 넣는다.
            InteractionData data = Interactions;
            Reach reach = WalkFromTheNewGameSpot();
            var hidden = new List<string>();
            foreach (var pair in reach.Maps)
            {
                WorldMap map = pair.Value;
                int size = map.Grid.CellSize;
                MapInteractions here = data.Map(pair.Key);
                var npcs = here.NpcAnchors.SelectMany(a => a.Value.Select((p, i) => new PlacedNpc("anchor:" + a.Key + "#" + i, a.Key, p.X, p.Y)));
                var targets = new InteractionTargets(data.RadiusCells * size);
                targets.SetCandidates(NpcPlacement.Candidates(data, here, npcs, map.Doors));
                foreach (InteractionThing t in here.Things)
                {
                    bool covered = here.Things.Any(o => !ReferenceEquals(o, t) && o.X == t.X && o.Y == t.Y && o.Priority > t.Priority);
                    if (covered) continue;
                    bool chosen = reach.Cells[pair.Key].Any(c =>
                    {
                        InteractionCandidate best = targets.Update((c.X + 0.5f) * size, (c.Y + 0.5f) * size);
                        return best != null && best.Kind == t.Kind && best.Id == t.Id && best.X == t.X && best.Y == t.Y;
                    });
                    if (!chosen) hidden.Add(Name(pair.Key, t) + ": 걸어 닿는 어느 칸에서도 대상이 되지 않는다(같은 자리의 다른 지점·문이 먼저 골라진다)");
                }
            }
            Assert.That(hidden, Is.Empty, string.Join("\n", hidden));
        }

        [Test]
        public void EveryPointId_IsADefinitionInData()
        {
            InteractionData data = Interactions;
            var unknown = new List<string>();
            foreach (string mapId in data.MapIds)
                foreach (InteractionThing t in data.Map(mapId).Things)
                    if (!KindsWhoseIdIsOnlyAPointName.Contains(t.Kind) && !Defined(t.Id)) unknown.Add(Name(mapId, t));
            Assert.That(unknown, Is.Empty, "data/에 정의가 없는 지점 id:\n" + string.Join("\n", unknown));
        }

        private static bool Defined(string id)
        {
            try { return Repo.Definitions.TryGet<object>(id, out _); }
            catch (InvalidOperationException) { return true; } // 같은 id의 정의가 여러 형으로 있다(장소와 장소 활동처럼)
        }

        /// <summary>data/에 정의된 탐색 지점·성인 시설·진로 연습은 하나도 빠짐없이 그 종류의 지점을 가진다(골라 빼지 않는다).</summary>
        [TestCase("exploration", typeof(ExplorationInteraction))]
        [TestCase("career-facility", typeof(Facility))]
        [TestCase("career-practice", typeof(CareerPractice))]
        [TestCase("discovery", typeof(Discovery))]
        public void EveryDefinition_OfThisKind_HasAWalkablePoint(string kind, Type definitionType)
        {
            var ids = ((System.Collections.IEnumerable)typeof(Rootborn.Data.GameDefinitions).GetMethod(nameof(Rootborn.Data.GameDefinitions.All))
                    .MakeGenericMethod(definitionType).Invoke(Repo.Definitions, null))
                .Cast<object>().Select(d => (string)definitionType.GetProperty("Id").GetValue(d)).ToList();
            Assert.That(ids, Is.Not.Empty, "data/에 " + definitionType.Name + " 정의가 있어야 이 검사가 뜻이 있다");

            InteractionData data = Interactions;
            Reach reach = WalkFromTheNewGameSpot();
            var placed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in reach.Maps)
                foreach (InteractionThing t in data.Map(pair.Key).Things)
                    if (t.Kind == kind && MapChecks.NextTo(reach.Cells[pair.Key], t.X, t.Y, pair.Value.Grid.CellSize)) placed.Add(t.Id);
            var missing = ids.Where(id => !placed.Contains(id)).ToList();
            Assert.That(missing, Is.Empty, kind + " 지점이 걸어서 닿는 곳에 없는 정의: " + string.Join(", ", missing));
        }
    }
}
