using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rootborn.Core.World;
using Rootborn.World.Ldtk;

namespace Rootborn.World.Tests
{
    /// <summary>자동 테스트: 마을 Level_0 밖의 길(해변, 학교)과 Kitchen2의 Animation 엔티티. 화면 근거가 아니다.</summary>
    public sealed class MissingPathsTests
    {
        [Test]
        public void Beach_IsTownLevel1AndItsTopEdgeLeadsBackToTheTownBottom()
        {
            LoadedMap beach = Repo.Loader().LoadMap("Beach");
            Assert.That((beach.World.Grid.Columns, beach.World.Grid.Rows), Is.EqualTo((44, 30)), "Level_1 1408x960");
            Assert.That(beach.Problems, Is.Empty);
            DoorArea back = beach.World.Doors.Single();
            Assert.That(back.Destination, Is.EqualTo(new TravelTarget("Town", "town-bottom-entry")));
            Assert.That(back.OpenSeconds, Is.EqualTo(0.25f));
            beach.World.TryGetSpawn("beach-top-entry", out var start);
            Assert.That(ArrivalRules.Check(beach.World.Collision, beach.World.Doors, start), Is.Empty, "도착하자마자 경계에 닿지 않는다");
            CellPos from = CellPos.Containing(start.X, start.Y, 32);
            List<CellPos> path = GridPath.Find(beach.World.Collision, from, c => back.Area.Overlaps(new AreaRect(c.X * 32, c.Y * 32, 32, 32)));
            Assert.That(path, Is.Not.Null, "도착 지점에서 위 경계까지 걸어갈 수 있다");
        }

        [Test]
        public void Town_BottomEdgeIsReachableAndBeachSpawnIsInTheTravelCatalog()
        {
            LoadedMap town = Repo.Loader().LoadMap("Town");
            DoorArea toBeach = town.World.Doors.Single(d => d.Target == "beach");
            town.World.TryGetSpawn("town-bottom-entry", out var bottom);
            Assert.That(GridPath.Find(town.World.Collision, CellPos.Containing(bottom.X, bottom.Y, 32),
                c => toBeach.Area.Overlaps(new AreaRect(c.X * 32, c.Y * 32, 32, 32))), Is.Not.Null);
            Assert.That(WorldEntryRules.ResolvePair(WorldTravelData.Load(Repo.Root), "Beach", "beach-top-entry"), Is.Not.Null);
        }

        [Test]
        public void School_IsInteriorsSchool1AndCollisionMatchesTheStructureCollidersBakedInTheScene()
        {
            LoadedMap school = Repo.Loader().LoadMap("School");
            Assert.That((school.World.Grid.Columns, school.World.Grid.Rows), Is.EqualTo((23, 18)));
            Assert.That(school.Problems, Is.Empty);
            // School.unity의 Structure_x_y 상자(칸마다 하나). 줄 밀림이 없어 씬 칸 (x, y) = LDtk 칸 (x, -y-1).
            string scene = File.ReadAllText(Path.Combine(Repo.Root, "Assets/Scenes/School.unity"));
            var baked = new HashSet<CellPos>(Regex.Matches(scene, @"m_Name: Structure_(-?\d+)_(-?\d+)")
                .Select(m => new CellPos(int.Parse(m.Groups[1].Value), -int.Parse(m.Groups[2].Value) - 1)));
            var blocked = new HashSet<CellPos>();
            CollisionGrid grid = school.World.Collision;
            for (int y = 0; y < grid.Grid.Rows; y++)
                for (int x = 0; x < grid.Grid.Columns; x++)
                    if (grid.IsBlocked(x, y)) blocked.Add(new CellPos(x, y));
            Assert.That(blocked.Except(baked), Is.Empty, "자동 판정만 막은 칸");
            Assert.That(baked.Where(c => grid.Grid.Contains(c.X, c.Y)).Except(blocked), Is.Empty, "씬만 막은 칸");
            school.World.TryGetSpawn("school-entry", out var spawn);
            Assert.That(grid.IsBlocked(CellPos.Containing(spawn.X, spawn.Y, 32).X, CellPos.Containing(spawn.X, spawn.Y, 32).Y), Is.False);
        }

        [Test]
        public void Kitchen2_AnimationEntitiesHaveNoArtAndTheStaticKitchenTilesStayUnderThem()
        {
            // Unity 판도 이 엔티티를 재생하지 않았다: 필드는 name 하나, 타일셋 없음, 애니메이션 그림이 저장소에 없다.
            LoadedMap kitchen = Repo.Loader().LoadMap("Kitchen2Interior");
            List<LdtkEntity> animations = kitchen.Level.Entities("Animation").ToList();
            Assert.That(animations.Select(a => a.Field("name")), Is.EquivalentTo(new[] { "냉장고", "냉장고", "fire", "fire", "fire2" }));
            Assert.That(animations.All(a => !a.HasTile), Is.True);
            LdtkLayer tiles = kitchen.Level.Layer("Kitchen");
            foreach (LdtkEntity a in animations)
                Assert.That(tiles.Tiles.Any(t => t.Px + tiles.OffsetX == (int)a.Left && t.Py + tiles.OffsetY == (int)a.Top), Is.True, a.Field("name") + " 자리에는 정지 타일이 그려진다");
        }
    }
}
