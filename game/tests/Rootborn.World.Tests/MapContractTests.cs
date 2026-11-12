using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Rootborn.World.Tests
{
    /// <summary>
    /// 자동 테스트: maps.json의 모든 맵에 대해 도는 새 맵 검사(MapChecks). 맵 이름을 적지 않는다.
    /// 지금 맵에서 걸리는 것은 맵을 고치지 않고 KnownMapProblems에 고정한다.
    /// </summary>
    public sealed class MapContractTests
    {
        private static InteractionData Interactions => InteractionData.Load(Path.Combine(Repo.Root, InteractionData.DefaultRelativePath));

        [Test]
        public void EveryMap_KeepsTheMapContract_ExceptTheKnownProblemsOfTodaysMaps()
        {
            WorldMapLoader loader = Repo.Loader();
            List<MapCheckProblem> found = MapChecks.Check(loader, Interactions);

            var unexpected = found.Where(p => !KnownMapProblems.Covers(loader.Catalog, p)).Select(p => p.ToString()).ToList();
            Assert.That(unexpected, Is.Empty, "새로 걸린 것(맵을 고치거나, 지금 맵의 문제라면 KnownMapProblems에 이유와 함께 적는다):\n" + string.Join("\n", unexpected));

            var gone = KnownMapProblems.All.Where(k => k.AppliesTo(loader.Catalog) && !found.Any(p => k.Matches(p))).Select(k => k.ToString()).ToList();
            Assert.That(gone, Is.Empty, "더 걸리지 않는 알려진 문제(목록에서 지운다):\n" + string.Join("\n", gone));
        }

        [Test]
        public void KnownProblems_AreBoundToTheMapFileSoANewMapStartsClean()
        {
            // 같은 id의 맵이라도 LDtk 파일이나 레벨이 바뀌면(새로 그린 맵) 알려진 문제가 적용되지 않는다.
            WorldCatalog catalog = Repo.Loader().Catalog;
            KnownMapProblem any = KnownMapProblems.All.First();
            MapEntry today = catalog.Find(any.MapId);
            Assert.That(any.AppliesTo(catalog), Is.True);

            var redrawn = WorldCatalog.Parse(File.ReadAllText(Path.Combine(Repo.Root, WorldCatalog.DefaultRelativePath)));
            redrawn.Find(any.MapId).Ldtk = "maps/new/" + Path.GetFileName(today.Ldtk);
            Assert.That(any.AppliesTo(redrawn), Is.False);
            Assert.That(KnownMapProblems.Covers(redrawn, new MapCheckProblem(any.MapId, any.Problem)), Is.False);
        }

        [Test]
        public void Checks_CatchEachBrokenPromise()
        {
            // 실제 맵 하나(첫 맵)의 맵 목록 값을 망가뜨려 검사가 하나씩 잡는지 본다.
            WorldCatalog catalog = WorldCatalog.Parse(File.ReadAllText(Path.Combine(Repo.Root, WorldCatalog.DefaultRelativePath)));
            MapEntry entry = catalog.Maps[0];
            Rootborn.Core.World.WorldMap map = new WorldMapLoader(Repo.Root, catalog).LoadMap(entry.Id).World;
            int size = map.Grid.CellSize;
            HashSet<Rootborn.Core.World.CellPos> reached = MapChecks.Reachable(map, MapChecks.StartSpawn(entry));
            // 닿는 칸에서 두 칸 넘게 떨어진 막힌 칸(닿지 않는 자리).
            Rootborn.Core.World.CellPos far = map.Collision.BlockedCells().First(c =>
                !reached.Any(r => Math.Abs(r.X - c.X) <= 2 && Math.Abs(r.Y - c.Y) <= 2));
            entry.Spawns["broken-spawn"] = ((far.X + 0.5f) * size, (far.Y + 0.5f) * size);
            string doorValue = entry.Doors.Keys.First();
            entry.Doors[doorValue] = new Rootborn.Core.World.TravelTarget("NoSuchMap", "nowhere");

            string json = "{ \"radiusCells\": 1.75, \"npcPriority\": 30, \"doorPriority\": 110, \"maps\": { \"" + entry.Id + "\": { \"things\": [ "
                + "{ \"kind\": \"location-visit\", \"id\": \"far-away\", \"at\": [ " + ((far.X + 0.5f) * size) + ", " + ((far.Y + 0.5f) * size) + " ], \"priority\": 35 } ] },"
                + " \"NoSuchMapEither\": { \"things\": [] } } }";
            var loader = new WorldMapLoader(Repo.Root, catalog); // 고친 값으로 다시 읽는다
            List<string> found = MapChecks.Check(loader, InteractionData.Parse(json)).Select(p => p.ToString()).ToList();

            Assert.That(found, Has.Some.EqualTo(entry.Id + ": 도착 지점 broken-spawn이 막힌 칸이다"));
            Assert.That(found, Has.Some.StartsWith(entry.Id + ": 문 '").And.Contains("의 목적지 맵 NoSuchMap이 맵 목록에 없다"));
            Assert.That(found, Has.Some.EqualTo(entry.Id + ": 상호작용 location-visit/far-away이 걸어서 닿는 칸 옆에 없다"));
            Assert.That(found, Has.Some.EqualTo("NoSuchMapEither: 상호작용 파일의 맵이 맵 목록에 없다"));
        }

        [Test]
        public void TemporaryLinksWithoutAReason_AreRefused()
        {
            const string map = @"{ ""npcWalker"": { ""speedCellsPerSecond"": 2.5 }, ""maps"": [ { ""id"": ""A"", ""ldtk"": ""a.ldtk"", ""level"": ""L"", ""viewCells"": 10, ""collision"": { ""rule"": ""interior-auto"" },
                ""doors"": { ""out"": { ""map"": ""B"", ""spawn"": ""b"", ""temporary"": true } }, ""spawns"": { ""a"": [ 16, 16 ] } } ] }";
            Assert.Throws<InvalidDataException>(() => WorldCatalog.Parse(map));
            WorldCatalog ok = WorldCatalog.Parse(map.Replace(@"""temporary"": true", @"""temporary"": true, ""reason"": ""새 맵 전까지"""));
            Assert.That(ok.Find("A").Temporary.Single().ToString(), Is.EqualTo("door out: 새 맵 전까지"));

            const string things = @"{ ""radiusCells"": 1.75, ""npcPriority"": 30, ""doorPriority"": 110, ""maps"": { ""A"": { ""things"": [
                { ""kind"": ""location-visit"", ""id"": ""x"", ""at"": [ 16, 16 ], ""priority"": 35, ""temporary"": true } ] } } }";
            Assert.Throws<InvalidDataException>(() => InteractionData.Parse(things));
        }
    }

    /// <summary>지금 맵의 알려진 문제 하나: 맵 id와 그 맵 파일(LDtk 경로·레벨)에 묶인다.</summary>
    public sealed class KnownMapProblem
    {
        public readonly string MapId, Ldtk, Level, Problem, Reason;

        public KnownMapProblem(string mapId, string ldtk, string level, string problem, string reason)
        {
            MapId = mapId; Ldtk = ldtk; Level = level; Problem = problem; Reason = reason;
        }

        public bool AppliesTo(WorldCatalog catalog)
        {
            MapEntry entry = catalog.Find(MapId);
            return entry != null && entry.Id == MapId && entry.Ldtk == Ldtk && entry.Level == Level;
        }

        public bool Matches(MapCheckProblem p) => p.MapId == MapId && p.Problem.StartsWith(Problem, StringComparison.Ordinal);

        public override string ToString() => MapId + " (" + Ldtk + " " + Level + "): " + Problem + " — " + Reason;
    }

    /// <summary>
    /// 지금 맵(Unity 판에서 가져온 LDtk)의 알려진 문제. 맵을 새로 그리면 그 맵 파일에 묶인 줄은 적용되지 않는다.
    /// 지금 맵은 고치지 않는다(사용자 결정 2026-11-12: 맵은 새로 그린다).
    /// </summary>
    public static class KnownMapProblems
    {
        public static readonly KnownMapProblem[] All =
        {
            new KnownMapProblem("Town", "Assets/LDtk/Town/town.ldtk", "Level_0", "문 'hospital'에 시작 도착 지점 town-entry에서 걸어서 닿지 않는다",
                "병원 문 앞 건물 계단 타일(Hospital 층, 칸 전체 충돌) 세 줄이 막는다. Unity 판에서도 걸어서 닿지 않았다"),
            new KnownMapProblem("Town", "Assets/LDtk/Town/town.ldtk", "Level_0", "문 '' (23,14)의 값이",
                "Door 값이 비어 목적지가 없는 문 엔티티"),
            new KnownMapProblem("Town", "Assets/LDtk/Town/town.ldtk", "Level_0", "문 '' (29,13)의 값이",
                "Door 값이 비어 목적지가 없는 문 엔티티"),
            new KnownMapProblem("HospitalInterior", "Assets/LDtk/Interiors/interiors.ldtk", "Hospital_1", "문 'hospital_2' (18,2)의 값이",
                "병원 2층(Hospital_2 레벨)은 장면 목록(data/locations)에 장면이 없다"),
            new KnownMapProblem("PoliceInterior", "Assets/LDtk/Interiors/interiors.ldtk", "Police_1", "문 '' (1,7)의 값이",
                "Where 값이 비어 목적지가 없는 문 엔티티"),
            new KnownMapProblem("PoliceInterior", "Assets/LDtk/Interiors/interiors.ldtk", "Police_1", "문 '' (9,3)의 값이",
                "Where 값이 비어 목적지가 없는 문 엔티티"),
            new KnownMapProblem("PoliceInterior", "Assets/LDtk/Interiors/interiors.ldtk", "Police_1", "문 '' (9,6)의 값이",
                "Where 값이 비어 목적지가 없는 문 엔티티"),
        };

        public static bool Covers(WorldCatalog catalog, MapCheckProblem p) => All.Any(k => k.AppliesTo(catalog) && k.Matches(p));
    }
}
