using System.IO;
using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Core.World;
using Rootborn.World;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 카메라가 맵 밖을 비추지 않는지, 때에 따라 바깥 밝기와 빛 지점이 바뀌고 실내는 그대로인지 실제 창과 게임 루프로 본다.
    /// 걷기는 장치 이벤트 주입(방향 키를 SDL 이벤트 큐에 넣는다, OS 입력 계층 미확인). 시간은 게임 루프가 넘기는 프레임 시간
    /// (테스트 모드 1/60초) × 배속(--time-scale)으로만 흐른다. 픽셀이 아니라 카메라 사각형·화면 색·그린 빛 목록으로 판정한다.
    /// 스크린샷은 output/host-ambience-evidence.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class WorldAmbienceHostTests
    {
        private static PlayerDriver Start(string map, float hour, float scale, string spawn = null)
        {
            var args = new System.Collections.Generic.List<string>
            {
                "--world", map, "--hour", hour.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--time-scale", scale.ToString(System.Globalization.CultureInfo.InvariantCulture), "--size", "1280x720",
            };
            if (spawn != null) { args.Add("--spawn"); args.Add(spawn); }
            HostOptions options = HostOptions.Resolve(args.ToArray());
            options.TestMode = true;
            options.EvidenceDir = Path.Combine(options.RepoRoot, "output", "host-ambience-evidence");
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        private static Rootborn.Host.World.WorldScreen W(PlayerDriver p) => p.Game.World;

        private static void AssertViewInsideMap(PlayerDriver p, string where)
        {
            MapGrid grid = p.World.Map.Grid;
            AreaRect v = W(p).CameraView;
            Assert.That(v.X >= 0f && v.Y >= 0f && v.Right <= grid.PixelWidth && v.Bottom <= grid.PixelHeight, Is.True,
                where + ": 카메라 " + v.X + "," + v.Y + " " + v.W + "x" + v.H + " / 맵 " + grid.PixelWidth + "x" + grid.PixelHeight);
        }

        /// <summary>방향 키를 누르고 있는 동안 프레임마다 카메라가 맵 안인지 본다.</summary>
        private static void HoldAndWatch(PlayerDriver p, TestKey key, int frames, string where)
        {
            p.KeyDown(key);
            try
            {
                for (int i = 0; i < frames; i++)
                {
                    p.Frames(1);
                    AssertViewInsideMap(p, where + " " + i + "프레임");
                }
            }
            finally { p.KeyUp(key); }
        }

        private static bool Same(LightColor a, LightColor b, float within) =>
            System.Math.Abs(a.R - b.R) <= within && System.Math.Abs(a.G - b.G) <= within && System.Math.Abs(a.B - b.B) <= within;

        [Test]
        public void AMBIENCE_PLAY_001_WalkingToEveryEdgeOfTownNeverShowsOutsideTheMap()
        {
            using PlayerDriver p = Start("Town", 12f, 0f);
            MapGrid grid = p.World.Map.Grid;
            Assert.That(grid.PixelWidth > W(p).CameraView.W && grid.PixelHeight > W(p).CameraView.H, Is.True, "마을은 화면보다 크다(따라가는 카메라)");
            AssertViewInsideMap(p, "시작");
            HoldAndWatch(p, TestKey.Left, 240, "왼쪽");
            HoldAndWatch(p, TestKey.Up, 240, "위");
            Assert.That(W(p).CameraView.X, Is.EqualTo(0f), "왼쪽 끝에 닿으면 카메라도 왼쪽 끝에 멈춘다: 걸은 자리 " + p.World.Walker.X);
            HoldAndWatch(p, TestKey.Down, 600, "아래");
            HoldAndWatch(p, TestKey.Right, 900, "오른쪽");
            p.Screenshot("ambience-01-town-edge");
        }

        [Test]
        public void AMBIENCE_PLAY_002_AMapSmallerThanTheScreenIsCentered()
        {
            using PlayerDriver p = Start("PoliceShootingInterior", 12f, 0f);
            MapGrid grid = p.World.Map.Grid;
            AreaRect v = W(p).CameraView;
            Assert.That(v.W > grid.PixelWidth, Is.True, "이 맵은 화면보다 좁다: 화면 " + v.W + " / 맵 " + grid.PixelWidth);
            Assert.That(v.CenterX, Is.EqualTo(grid.PixelWidth / 2f).Within(1f), "가로 가운데");
            Assert.That(v.CenterY, Is.EqualTo(grid.PixelHeight / 2f).Within(1f), "세로 가운데(맵이 화면보다 낮다)");
            float startX = p.World.Walker.X;
            p.KeyDown(TestKey.Right);
            try
            {
                for (int i = 0; i < 120; i++)
                {
                    p.Frames(1);
                    Assert.That(W(p).CameraView.CenterX, Is.EqualTo(grid.PixelWidth / 2f).Within(1f), "걸어도 가운데에 머문다 " + i + "프레임");
                }
            }
            finally { p.KeyUp(TestKey.Right); }
            Assert.That(p.World.Walker.X, Is.Not.EqualTo(startX), "캐릭터는 걸었다");
        }

        [Test]
        public void AMBIENCE_PLAY_003_TownBrightnessAndTheLightFollowTheTimeOfDay()
        {
            // 하루 600초 × 배속 1: 게임 1시간 = 1500프레임. 16.4시(오후)에 시작해 걸어서(약 0.4시간) 빛 지점 가까이 간 뒤 저녁이 되기를 기다린다.
            using PlayerDriver p = Start("Town", 16.4f, 1f, "town-shop-entry"); // 첫 도착 지점에서는 빛으로 가는 길이 집 문을 밟는다
            WorldLightingData data = W(p).LightingData;
            PointLightDef light = data.LightsOf("Town")[0];
            p.WalkNear(light.X, light.Y, p.World.Map.Grid.CellSize * 1.5f); // 빛 가까이(멀리서 멈추면 그 자리가 문 앞일 수 있다)
            Assert.That(W(p).TimeSlot, Is.EqualTo(LocationStateTimeSlots.Afternoon), "아직 오후: " + W(p).CurrentHour);
            data.TryGetAmbient(LocationStateTimeSlots.Afternoon, out LightColor afternoon);
            Assert.That(Same(W(p).Tint, afternoon, 0.01f), Is.True, "오후 밝기 " + W(p).Tint + " / 데이터 " + afternoon);
            Assert.That(W(p).VisibleLights.Count, Is.EqualTo(1), "빛 지점이 화면 안에 들어와 그려진다");
            float afternoonLight = W(p).VisibleLights[0].R;
            Assert.That(afternoonLight, Is.EqualTo(light.Color.R * light.IntensityAt(LocationStateTimeSlots.Afternoon)).Within(0.01f));
            p.Screenshot("ambience-02-town-afternoon");

            for (int i = 0; i < 900 && W(p).TimeSlot != LocationStateTimeSlots.Evening; i++) p.Frames(1);
            Assert.That(W(p).TimeSlot, Is.EqualTo(LocationStateTimeSlots.Evening), "저녁이 됐다: " + W(p).CurrentHour);
            p.Frames(360); // 밝기가 따라오는 시간(속도 2 → 6초면 99.9%). 그사이 게임 시각은 0.24시간 흐른다
            Assert.That(W(p).TimeSlot, Is.EqualTo(LocationStateTimeSlots.Evening));
            data.TryGetAmbient(LocationStateTimeSlots.Evening, out LightColor evening);
            Assert.That(Same(W(p).Tint, evening, 0.01f), Is.True, "저녁 밝기 " + W(p).Tint + " / 데이터 " + evening);
            Assert.That(W(p).Tint.R + W(p).Tint.G + W(p).Tint.B, Is.LessThan(afternoon.R + afternoon.G + afternoon.B), "저녁이 오후보다 어둡다");
            Assert.That(W(p).VisibleLights[0].R, Is.GreaterThan(afternoonLight * 3f), "저녁에는 빛이 밝아진다: " + W(p).VisibleLights[0].R);
            p.Screenshot("ambience-03-town-evening");
        }

        [Test]
        public void AMBIENCE_PLAY_004_TownAtNightIsDarkWithTheLightOn()
        {
            using PlayerDriver p = Start("Town", 22f, 0f, "town-shop-entry");
            WorldLightingData data = W(p).LightingData;
            data.TryGetAmbient(LocationStateTimeSlots.Night, out LightColor night);
            Assert.That(Same(W(p).Tint, night, 0.001f), Is.True, "맵에 들어선 순간 밤 밝기로 바로 맞춘다: " + W(p).Tint);
            Assert.That(W(p).VisibleLights, Is.Empty, "빛 지점이 화면 밖이면 그리지 않는다");
            PointLightDef light = data.LightsOf("Town")[0];
            // 빛 가운데까지 걸어간다(빛은 걸어서 닿는 칸 옆에 있다 — World Lights_EveryLightCircleIsInsideItsMap_NextToWalkableCells).
            p.WalkNear(light.X, light.Y, p.World.Map.Grid.CellSize * 1.5f);
            Assert.That(W(p).VisibleLights.Count, Is.EqualTo(1));
            AreaRect view = W(p).CameraView;
            Assert.That(light.X - light.Radius >= view.X && light.Y - light.Radius >= view.Y && light.X + light.Radius <= view.Right && light.Y + light.Radius <= view.Bottom, Is.True,
                "빛 원 전체가 화면 안: 카메라 " + view.X + "," + view.Y + " " + view.W + "x" + view.H);
            Assert.That(W(p).VisibleLights[0].R, Is.EqualTo(light.Color.R * light.IntensityAt(LocationStateTimeSlots.Night)).Within(0.01f));
            p.Screenshot("ambience-04-town-night");
        }

        [TestCase(12f, "afternoon")]
        [TestCase(19f, "evening")]
        [TestCase(22f, "night")]
        public void AMBIENCE_PLAY_005_InsideTheHouseNothingChangesWithTheTimeOfDay(float hour, string name)
        {
            using PlayerDriver p = Start("House", hour, 0f);
            p.Frames(30);
            LightColor tint = W(p).Tint;
            Assert.That((tint.R, tint.G, tint.B), Is.EqualTo((1f, 1f, 1f)), "실내(맵 목록 dayNight 꺼짐)는 늘 흰색");
            Assert.That(W(p).VisibleLights, Is.Empty, "집에는 빛 지점이 없다");
            AssertViewInsideMap(p, "집");
            p.Screenshot("ambience-05-house-" + name);
        }
    }
}
