using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Core.World;

namespace Rootborn.World.Tests
{
    /// <summary>
    /// 자동 테스트(창 없음, 칸 단위 경로): data/의 알바 정의(PartTimeWorkDefinition) 하나하나가 실제 맵 데이터(interactions.json)에
    /// 일터 지점(part-time-work)을 가지고, 그 지점이 새 게임 자리에서 문을 지나 걸어 닿는 칸 옆에 있다.
    /// Unity 판 Town.unity에는 알바 아홉 개 모두의 PartTimeWorkInteractor가 있었다(LDtk 마을 그림 밖 북쪽 구역). 새 맵 전까지 북쪽 광장의 임시 지점이다.
    /// 지점이 대상으로 골라지는지·정의가 있는지는 WorldEntryPointsTests가 모든 지점에 대해 본다. 화면 근거가 아니다.
    /// </summary>
    public sealed class PartTimeWorkPointsTests
    {
        private const string Kind = "part-time-work";

        [Test]
        public void EveryPartTimeWork_HasAWalkableWorkPoint()
        {
            List<string> ids = Repo.Definitions.All<PartTimeWork>().Select(w => w.Id).ToList();
            Assert.That(ids, Has.Count.GreaterThan(1), "data/에 알바 정의가 여럿 있어야 이 검사가 뜻이 있다");

            InteractionData data = InteractionData.Load(Path.Combine(Repo.Root, InteractionData.DefaultRelativePath));
            WorldMapLoader loader = Repo.Loader();
            WorldEntryPoint start = WorldEntryRules.ResolveNewGame(WorldTravelData.From(Repo.Definitions)).Value;
            var maps = new Dictionary<string, WorldMap>(StringComparer.Ordinal);
            var cells = new Dictionary<string, HashSet<CellPos>>(StringComparer.Ordinal);
            foreach (WorldPlace place in MapChecks.Walk(loader, new WorldPlace(start.SceneName, start.SpawnId)))
            {
                if (!maps.TryGetValue(place.MapId, out WorldMap map))
                {
                    map = loader.LoadMap(place.MapId).World;
                    maps[place.MapId] = map;
                    cells[place.MapId] = new HashSet<CellPos>();
                }
                cells[place.MapId].UnionWith(MapChecks.Reachable(map, place.SpawnId));
            }

            var placed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in maps)
                foreach (InteractionThing t in data.Map(pair.Key).Things)
                    if (t.Kind == Kind && MapChecks.NextTo(cells[pair.Key], t.X, t.Y, pair.Value.Grid.CellSize)) placed.Add(t.Id);

            List<string> missing = ids.Where(id => !placed.Contains(id)).ToList();
            Assert.That(missing, Is.Empty, "일터 지점이 걸어서 닿는 곳에 없는 알바: " + string.Join(", ", missing));
        }
    }
}
