using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Core.World;

namespace Rootborn.World.Tests
{
    /// <summary>자동 테스트: 카메라 경계 계산, 때별 밝기와 빛 지점(game/world/lights.json). 화면 근거가 아니다.</summary>
    public sealed class WorldAmbienceTests
    {
        private static readonly string[] Slots = { LocationStateTimeSlots.Morning, LocationStateTimeSlots.Afternoon, LocationStateTimeSlots.Evening, LocationStateTimeSlots.Night };

        private static WorldLightingData Real => WorldLightingData.Load(Path.Combine(Repo.Root, WorldLightingData.DefaultRelativePath));

        // ---------- 카메라 ----------

        [Test]
        public void Camera_StaysInsideTheMapAtEveryEdge()
        {
            var bounds = new AreaRect(0, 0, 2880, 2016);
            foreach ((float fx, float fy) in new[] { (0f, 0f), (2880f, 2016f), (0f, 2016f), (2880f, 0f), (1400f, 1000f), (-500f, -500f), (9000f, 9000f) })
            {
                AreaRect view = CameraFrame.View(fx, fy, bounds, 1280, 720);
                Assert.That(view.X, Is.GreaterThanOrEqualTo(0f), "왼쪽 " + fx + "," + fy);
                Assert.That(view.Y, Is.GreaterThanOrEqualTo(0f), "위 " + fx + "," + fy);
                Assert.That(view.Right, Is.LessThanOrEqualTo(2880f), "오른쪽 " + fx + "," + fy);
                Assert.That(view.Bottom, Is.LessThanOrEqualTo(2016f), "아래 " + fx + "," + fy);
                Assert.That((view.W, view.H), Is.EqualTo((1280f, 720f)));
            }
            Assert.That(CameraFrame.View(1400, 1000, bounds, 1280, 720).CenterX, Is.EqualTo(1400f), "가운데에서는 따라간다");
        }

        [Test]
        public void Camera_CentersAMapSmallerThanTheView()
        {
            var bounds = new AreaRect(0, 0, 320, 352);
            AreaRect view = CameraFrame.View(10, 10, bounds, 640, 360);
            Assert.That(view.CenterX, Is.EqualTo(160f), "가로가 작으면 가운데");
            Assert.That(view.CenterY, Is.EqualTo(176f), "세로가 작으면 가운데");
            // 한 축만 작을 때 다른 축은 그대로 막는다.
            AreaRect wide = CameraFrame.View(0, 0, new AreaRect(0, 0, 320, 2000), 640, 360);
            Assert.That(wide.CenterX, Is.EqualTo(160f));
            Assert.That(wide.Y, Is.EqualTo(0f));
        }

        [Test]
        public void Camera_BoundsComeFromTheMapAndAnAuthoredBoxCanOnlyNarrowThem()
        {
            Assert.That(CameraFrame.Bounds(736, 416, null), Is.EqualTo(new AreaRect(0, 0, 736, 416)), "따로 정한 경계가 없으면 맵 크기");
            Assert.That(CameraFrame.Bounds(736, 416, new AreaRect(0, 0, 640, 416)), Is.EqualTo(new AreaRect(0, 0, 640, 416)), "정한 경계가 맵 안이면 그것");
            Assert.That(CameraFrame.Bounds(2880, 2016, new AreaRect(0, -1376, 2880, 3424)), Is.EqualTo(new AreaRect(0, 0, 2880, 2016)), "맵 밖으로 나간 부분은 잘라 낸다");
            Assert.That(CameraFrame.Bounds(100, 100, new AreaRect(500, 500, 10, 10)), Is.EqualTo(new AreaRect(0, 0, 100, 100)), "맵과 겹치지 않으면 맵 크기");
            // 경계가 맵 안쪽으로 옮겨져 있으면 화면도 그 안에 머문다.
            AreaRect view = CameraFrame.View(0, 0, new AreaRect(100, 50, 1000, 1000), 400, 300);
            Assert.That((view.X, view.Y), Is.EqualTo((100f, 50f)));
        }

        [Test]
        public void MapCameraBounds_ReadsTheOptionalFieldFromTheMapList()
        {
            const string json = "{ /* 주석 */ \"maps\": [ { \"id\": \"A\", \"cameraBounds\": [ 0, 0, 640, 416 ] }, { \"id\": \"B\" } ] }";
            MapCameraBounds b = MapCameraBounds.Parse(json);
            Assert.That(b.TryGet("A", out AreaRect a), Is.True);
            Assert.That(a, Is.EqualTo(new AreaRect(0, 0, 640, 416)));
            Assert.That(b.TryGet("B", out _), Is.False, "필드가 없으면 맵 크기를 쓴다");
            Assert.That(() => MapCameraBounds.Parse("{ \"maps\": [ { \"id\": \"A\", \"cameraBounds\": [ 0, 0, -1, 4 ] } ] }"), Throws.TypeOf<InvalidDataException>(), "크기는 양수");
            Assert.That(() => MapCameraBounds.Parse("{ \"maps\": [ { \"id\": \"A\", \"cameraBounds\": [ 0, 0 ] } ] }"), Throws.TypeOf<InvalidDataException>(), "[x, y, 너비, 높이]");
        }

        [Test]
        public void MapCameraBounds_TheRealMapListLoads()
        {
            MapCameraBounds b = MapCameraBounds.Load(Path.Combine(Repo.Root, WorldCatalog.DefaultRelativePath));
            foreach (MapEntry m in Repo.Loader().Catalog.Maps)
                if (b.TryGet(m.Id, out AreaRect r)) Assert.That(r.W > 0 && r.H > 0, Is.True, m.Id);
        }

        // ---------- 때별 밝기 ----------

        [Test]
        public void Ambient_EveryTimeSlotHasABrightnessAndTheyDiffer()
        {
            WorldLightingData data = Real;
            var seen = new HashSet<float>();
            foreach (string slot in Slots)
            {
                Assert.That(data.TryGetAmbient(slot, out LightColor tint), Is.True, "때 " + slot + "의 밝기가 없다");
                seen.Add(tint.R + tint.G + tint.B);
            }
            Assert.That(seen.Count, Is.EqualTo(Slots.Length), "때마다 밝기가 다르다");
            data.TryGetAmbient(LocationStateTimeSlots.Afternoon, out LightColor noon);
            data.TryGetAmbient(LocationStateTimeSlots.Night, out LightColor night);
            Assert.That(night.R + night.G + night.B, Is.LessThan((noon.R + noon.G + noon.B) * 0.4f), "밤은 낮보다 어둡다");
            Assert.That(night.B, Is.GreaterThan(night.R), "밤은 푸르다(Unity 판 프로필)");
        }

        [Test]
        public void Ambient_SnapsOnEnterThenFollowsTheSlotExponentially()
        {
            WorldLightingData data = Real;
            var light = new AmbientLight(data);
            light.Snap(LocationStateTimeSlots.Afternoon);
            data.TryGetAmbient(LocationStateTimeSlots.Afternoon, out LightColor noon);
            data.TryGetAmbient(LocationStateTimeSlots.Night, out LightColor night);
            Assert.That(light.Tint.R, Is.EqualTo(noon.R).Within(1e-5f));
            light.Update(LocationStateTimeSlots.Night, 0.5f);
            float t = 1f - (float)System.Math.Exp(-data.AmbientLerpSpeed * 0.5f);
            Assert.That(light.Tint.R, Is.EqualTo(noon.R + (night.R - noon.R) * t).Within(1e-4f));
            for (int i = 0; i < 600; i++) light.Update(LocationStateTimeSlots.Night, 1f / 60f);
            Assert.That(light.Tint.B, Is.EqualTo(night.B).Within(1e-3f), "결국 목표에 닿는다");
            light.Update("time.unknown", 100f);
            Assert.That(light.Tint.R, Is.EqualTo(1f).Within(1e-3f), "표에 없는 때는 흰색");
        }

        [Test]
        public void Ambient_ReadsColorTimesIntensity()
        {
            WorldLightingData d = WorldLightingData.Parse("{ \"ambient\": { \"lerpSpeed\": 3, \"byTime\": { \"x\": { \"color\": [ 1, 0.5, 0.25 ], \"intensity\": 0.5 } } } }");
            Assert.That(d.AmbientLerpSpeed, Is.EqualTo(3f));
            Assert.That(d.TryGetAmbient("x", out LightColor c), Is.True);
            Assert.That((c.R, c.G, c.B), Is.EqualTo((0.5f, 0.25f, 0.125f)));
            Assert.That(() => WorldLightingData.Parse("{ \"ambient\": { \"byTime\": { \"x\": { \"color\": [ 1, 1 ], \"intensity\": 1 } } } }"), Throws.TypeOf<InvalidDataException>());
            Assert.That(() => WorldLightingData.Parse("{ \"ambient\": { \"byTime\": { \"x\": { \"color\": [ 1, 1, 1 ], \"intensity\": -1 } } } }"), Throws.TypeOf<InvalidDataException>());
        }

        // ---------- 빛 지점 ----------

        [Test]
        public void Lights_TheTownLightIsTheUnityNightGuideLightMarkedTemporary()
        {
            WorldLightingData data = Real;
            IReadOnlyList<PointLightDef> town = data.LightsOf("Town");
            Assert.That(town.Count, Is.EqualTo(1), "Unity Town.unity의 LocalLightController는 하나");
            PointLightDef l = town[0];
            Assert.That((l.Radius, l.InnerRadius), Is.EqualTo((128f, 24f)));
            Assert.That(l.IntensityAt(LocationStateTimeSlots.Afternoon), Is.EqualTo(0.1f));
            Assert.That(l.IntensityAt(LocationStateTimeSlots.Evening), Is.EqualTo(0.55f));
            Assert.That(l.IntensityAt(LocationStateTimeSlots.Night), Is.EqualTo(0.85f));
            Assert.That(l.Temporary, Is.True);
            Assert.That(data.Temporary.Count, Is.EqualTo(1));
            Assert.That(data.LightsOf("House"), Is.Empty, "빛이 없는 맵은 빈 목록");
        }

        /// <summary>
        /// 빛 지점은 화면에 보이는 자리에 둔다: 빛이 닿는 원 전체가 맵 안이고, 가운데가 걸어서 닿는 칸 옆이다(그 맵의 도착 지점들에서 걸어서).
        /// 맵 모서리에 두면 원 대부분이 맵 밖이라 카메라가 맵 안만 비추는 화면에 거의 보이지 않는다(2026-11-12 밝기 세션 보고).
        /// </summary>
        [Test]
        public void Lights_EveryLightCircleIsInsideItsMap_NextToWalkableCells()
        {
            WorldLightingData data = Real;
            WorldMapLoader loader = Repo.Loader();
            var problems = new List<string>();
            foreach (string mapId in data.MapIds)
            {
                WorldMap map = loader.LoadMap(mapId).World;
                var reached = new HashSet<CellPos>();
                foreach (string spawn in map.SpawnIds) reached.UnionWith(MapChecks.Reachable(map, spawn));
                foreach (PointLightDef l in data.LightsOf(mapId))
                {
                    string name = mapId + ": 빛 " + l.Name + " (" + l.X + ", " + l.Y + ") 반경 " + l.Radius;
                    if (l.X - l.Radius < 0f || l.Y - l.Radius < 0f || l.X + l.Radius > map.Grid.PixelWidth || l.Y + l.Radius > map.Grid.PixelHeight)
                        problems.Add(name + "의 원이 맵(" + map.Grid.PixelWidth + "x" + map.Grid.PixelHeight + ") 밖으로 나간다");
                    if (!MapChecks.NextTo(reached, l.X, l.Y, map.Grid.CellSize)) problems.Add(name + "이 걸어서 닿는 칸 옆에 없다");
                }
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void Lights_EveryMapInTheLightFileIsInTheMapList()
        {
            WorldCatalog catalog = Repo.Loader().Catalog;
            foreach (string map in Real.MapIds) Assert.That(catalog.Find(map), Is.Not.Null, "lights.json의 맵 " + map + "이 maps.json에 없다");
        }

        [Test]
        public void Lights_MissingSlotIsOffAndDataIsChecked()
        {
            WorldLightingData d = WorldLightingData.Parse("{ \"maps\": { \"M\": [ { \"name\": \"a\", \"at\": [ 10, 20 ], \"radius\": 50, \"color\": [ 1, 1, 1 ], \"intensityByTime\": { \"n\": 1 } } ] } }");
            PointLightDef l = d.LightsOf("M")[0];
            Assert.That(l.IntensityAt("other"), Is.EqualTo(0f), "표에 없는 때는 꺼짐");
            Assert.That(l.InnerRadius, Is.EqualTo(0f));
            Assert.That(l.LerpSpeed, Is.EqualTo(PointLightDef.DefaultLerpSpeed));
            Assert.That(d.TryGetAmbient("n", out _), Is.False, "ambient가 없어도 읽는다");
            Assert.That(() => WorldLightingData.Parse("{ \"maps\": { \"M\": [ { \"name\": \"a\", \"at\": [ 1, 1 ], \"radius\": 0, \"color\": [ 1, 1, 1 ] } ] } }"), Throws.TypeOf<InvalidDataException>(), "반경은 양수");
            Assert.That(() => WorldLightingData.Parse("{ \"maps\": { \"M\": [ { \"name\": \"a\", \"at\": [ 1, 1 ], \"radius\": 10, \"innerRadius\": 20, \"color\": [ 1, 1, 1 ] } ] } }"), Throws.TypeOf<InvalidDataException>(), "안 반경은 반경 이하");
            Assert.That(() => WorldLightingData.Parse("{ \"maps\": { \"M\": [ { \"name\": \"a\", \"at\": [ 1, 1 ], \"radius\": 10, \"color\": [ 1, 1, 1 ], \"temporary\": true } ] } }"), Throws.TypeOf<InvalidDataException>(), "임시에는 reason");
        }

        [Test]
        public void MapLights_FollowTheSlotAndOnlyLightsInViewAreCollected()
        {
            WorldLightingData d = WorldLightingData.Parse("{ \"maps\": { \"M\": [" +
                "{ \"name\": \"near\", \"at\": [ 100, 100 ], \"radius\": 50, \"color\": [ 1, 0.5, 0 ], \"lerpSpeed\": 6, \"intensityByTime\": { \"night\": 0.8, \"day\": 0.1 } }," +
                "{ \"name\": \"far\", \"at\": [ 5000, 5000 ], \"radius\": 50, \"color\": [ 1, 1, 1 ], \"intensityByTime\": { \"night\": 1 } }," +
                "{ \"name\": \"edge\", \"at\": [ 440, 100 ], \"radius\": 50, \"color\": [ 1, 1, 1 ], \"intensityByTime\": { \"night\": 1, \"day\": 1 } } ] } }");
            var state = new MapLights();
            state.Enter(d.LightsOf("M"), "day");
            var visible = new List<VisibleLight>();
            var view = new AreaRect(0, 0, 400, 300);
            state.CollectVisible(view, visible);
            Assert.That(visible.Count, Is.EqualTo(2), "화면 안(가장자리에 걸친 것 포함)의 빛만");
            Assert.That(visible[0].R, Is.EqualTo(0.1f).Within(1e-5f), "들어선 순간 바로 맞춘다: 색 × 세기");
            Assert.That(visible[0].G, Is.EqualTo(0.05f).Within(1e-5f));

            state.Update("night", 0.25f);
            float t = 1f - (float)System.Math.Exp(-6f * 0.25f);
            state.CollectVisible(view, visible);
            Assert.That(visible[0].R, Is.EqualTo(0.1f + 0.7f * t).Within(1e-4f), "세기는 따라간다");

            state.Enter(d.LightsOf("M"), "noon");
            state.CollectVisible(view, visible);
            Assert.That(visible, Is.Empty, "꺼진 빛은 그리지 않는다(목록은 매번 비우고 다시 채운다)");
            state.Enter(new PointLightDef[0], "night");
            state.CollectVisible(view, visible);
            Assert.That(visible, Is.Empty);
        }
    }
}
