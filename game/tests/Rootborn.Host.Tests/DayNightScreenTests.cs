using System;
using Microsoft.Xna.Framework.Graphics;
using NUnit.Framework;
using Rootborn.World;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 시간이 흐르면 마을 화면 색이 바뀌고 집 안은 그대로인지, 실제 창과 게임 루프로 본다. 시간은 게임 루프가 넘기는 프레임 시간
    /// (테스트 모드 1/60초) × 배속으로만 흐른다(--time-scale). 장치 이벤트 주입 없음(입력 없이 시간만 흐름).
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class DayNightScreenTests
    {
        private static PlayerDriver Start(string map, string hour, string scale)
        {
            HostOptions options = HostOptions.Resolve(new[] { "--world", map, "--hour", hour, "--time-scale", scale, "--size", "1280x720" });
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        /// <summary>그려진 화면 버퍼의 평균 밝기(0~255).</summary>
        private static double Brightness(PlayerDriver player)
        {
            RenderTarget2D scene = player.Game.Scene;
            var pixels = new Microsoft.Xna.Framework.Color[scene.Width * scene.Height];
            scene.GetData(pixels);
            double sum = 0;
            for (int i = 0; i < pixels.Length; i += 7) sum += (pixels[i].R + pixels[i].G + pixels[i].B) / 3.0;
            return sum / ((pixels.Length + 6) / 7);
        }

        [Test]
        public void DAYNIGHT_PLAY_001_TownGrowsDarkAsTheClockRunsIntoTheNight()
        {
            // 하루 600초(데이터) × 배속 30: 게임 1시간 = 50프레임. 밝기는 "때"(Core 시간대: 21시부터 밤, 0~12시는 오전)로 정하므로
            // 밤 때(21~24시, 150프레임) 안에서 빛이 따라오기를 기다린다.
            using PlayerDriver player = Start("Town", "12", "30");
            Assert.That(player.Game.World.Clock.CurrentHour, Is.EqualTo(12f).Within(1f), "시작 뒤 몇 프레임(1프레임 = 게임 0.2시간)");
            LightColor noon = player.Game.World.Tint;
            Assert.That(noon.R, Is.EqualTo(1f).Within(0.01f), "정오는 흰 빛, 세기 1");
            double noonBrightness = Brightness(player);
            player.Screenshot("daynight-01-town-noon");

            player.Frames(455); // 21.1시 무렵 — 밤 때
            player.Frames(120); // 빛이 따라오는 시간(속도 2, 2초 → 98%). 자정 전이다
            float hour = player.Game.World.Clock.CurrentHour;
            Assert.That(hour >= 21f && hour < 24f, Is.True, "밤 때다: " + hour);
            LightColor night = player.Game.World.Tint;
            Assert.That(night.R, Is.LessThan(0.2f), "밤 빛 " + night);
            Assert.That(night.B, Is.GreaterThan(night.R), "밤은 푸르다");
            double nightBrightness = Brightness(player);
            Assert.That(nightBrightness, Is.LessThan(noonBrightness * 0.4), "화면이 어두워진다");
            player.Screenshot("daynight-02-town-night");
        }

        [Test]
        public void DAYNIGHT_PLAY_002_InsideTheHouseTheLightStaysWhiteAtNight()
        {
            using PlayerDriver player = Start("House", "23", "300");
            player.Frames(30);
            LightColor tint = player.Game.World.Tint;
            Assert.That((tint.R, tint.G, tint.B), Is.EqualTo((1f, 1f, 1f)), "Unity 판 집 씬에는 2D 조명이 없었다");
            player.Screenshot("daynight-03-house-night");
        }
    }
}
