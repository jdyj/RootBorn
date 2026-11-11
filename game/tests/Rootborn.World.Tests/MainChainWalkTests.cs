using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.World;

namespace Rootborn.World.Tests
{
    /// <summary>
    /// 자동 테스트(창 없음, Core 경로 찾기): 새 게임 시작 자리에서 메인 체인이 거치는 모든 지점까지 문을 지나 걸어서 닿고,
    /// 닿은 모든 곳에서 시작 자리로 걸어서 돌아온다. 임시 연결(game/world/README.md "임시 연결 목록")을 포함한다.
    /// 화면 근거가 아니다 — 걸음은 칸 단위 상하좌우 경로로만 본다.
    /// </summary>
    public sealed class MainChainWalkTests
    {
        /// <summary>
        /// 메인 체인(Unity MainChainOpeningE2EScenarioTests 001~011)이 거치는 지점. 맵 이름이 아니라 상호작용 종류와 id로 적는다.
        /// "npc-at"은 그 장소의 NPC 자리(interactions.json npcAnchors), "location"은 location-visit 또는 location-activity 지점.
        /// </summary>
        public static readonly (string Kind, string Id, string Why)[] Stops =
        {
            ("npc-at", "location.home", "001 엄마와 이야기"),
            ("daily-plan-board", null, "001 하루 계획 고르기"),
            ("npc-at", "location.school", "001 학교 선생님과 이야기"),
            ("student-day-end", null, "002·005 집에서 하루 마감"),
            ("location", "location.restaurant", "003 일터 둘러보기"),
            ("location", "location.medical-clinic", "003 일터 둘러보기"),
            ("location", "location.design-studio", "003 일터 둘러보기"),
            ("location", "location.training-ground", "003 일터 둘러보기"),
            ("part-time-work", "work.shift.corner-store", "004 가게 알바"),
            ("location", "location.library", "006 도서관에서 직업 책 읽기"),
            ("location", "location.corner-store", "008~009 가게에서 가구 사기"),
        };

        private static bool Matches((string Kind, string Id, string Why) stop, InteractionThing t)
        {
            if (stop.Kind == "location") return (t.Kind == "location-visit" || t.Kind == "location-activity") && t.Id == stop.Id;
            return t.Kind == stop.Kind && (stop.Id == null || t.Id == stop.Id);
        }

        [Test]
        public void FromTheNewGameSpot_EveryMainChainStopIsWalkableAndEveryPlaceLeadsBackHome()
        {
            WorldMapLoader loader = Repo.Loader();
            InteractionData interactions = InteractionData.Load(Path.Combine(Repo.Root, InteractionData.DefaultRelativePath));
            WorldEntryPoint start = WorldEntryRules.ResolveNewGame(WorldTravelData.From(Repo.Definitions)).Value;
            var home = new WorldPlace(start.SceneName, start.SpawnId);

            List<WorldPlace> places = MapChecks.Walk(loader, home);
            var found = new Dictionary<string, string>();
            foreach (WorldPlace place in places)
            {
                WorldMap map = loader.LoadMap(place.MapId).World;
                int size = map.Grid.CellSize;
                HashSet<CellPos> reached = MapChecks.Reachable(map, place.SpawnId);
                MapInteractions here = interactions.Map(map.Id);
                foreach (var stop in Stops)
                {
                    string key = stop.Kind + "/" + stop.Id;
                    if (found.ContainsKey(key)) continue;
                    if (stop.Kind == "npc-at")
                    {
                        if (here.NpcAnchors.TryGetValue(stop.Id, out var anchors) && anchors.Any(a => MapChecks.NextTo(reached, a.X, a.Y, size)))
                            found[key] = place.ToString();
                    }
                    else if (here.Things.Any(t => Matches(stop, t) && MapChecks.NextTo(reached, t.X, t.Y, size)))
                        found[key] = place.ToString();
                }
            }

            var missing = Stops.Where(s => !found.ContainsKey(s.Kind + "/" + s.Id)).Select(s => s.Kind + "/" + s.Id + " (" + s.Why + ")").ToList();
            Assert.That(missing, Is.Empty, "걸어서 닿지 못한 메인 체인 지점. 다닌 곳: " + string.Join(", ", places) + "\n" + string.Join("\n", missing));

            var stuck = places.Where(p => !MapChecks.Walk(loader, p).Contains(home)).Select(p => p.ToString()).ToList();
            Assert.That(stuck, Is.Empty, "시작 자리(" + home + ")로 걸어서 돌아오지 못하는 곳");
            foreach (var pair in found) TestContext.WriteLine(pair.Key + " ← " + pair.Value);
        }
    }
}
