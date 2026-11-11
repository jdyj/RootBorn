using System;
using NUnit.Framework;
using Rootborn.Host.Display;
using Rootborn.Ui;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 창 없이 도는 테스트: 표시 모드 고르기 순서, 최소 창 크기, 표시 모드 실행 인자, 해상도별 화면 맞춤 계산. 자동 테스트.
    /// </summary>
    public sealed class DisplayChoiceTests
    {
        [Test]
        public void StartFullscreen_DefaultIsBorderlessFullscreen_LikeUnity()
        {
            Assert.That(DisplayChoice.StartFullscreen(null, false, false, null), Is.True);
        }

        [Test]
        public void StartFullscreen_SavedSettingBeatsDefault()
        {
            Assert.That(DisplayChoice.StartFullscreen(null, false, false, false), Is.False);
            Assert.That(DisplayChoice.StartFullscreen(null, false, false, true), Is.True);
        }

        [Test]
        public void StartFullscreen_ArgumentBeatsSavedSetting()
        {
            Assert.That(DisplayChoice.StartFullscreen(true, false, false, false), Is.True);
            Assert.That(DisplayChoice.StartFullscreen(false, false, false, true), Is.False);
        }

        [Test]
        public void StartFullscreen_SizeGiven_IsWindowed_EvenIfSavedFullscreen()
        {
            Assert.That(DisplayChoice.StartFullscreen(null, true, false, true), Is.False);
            Assert.That(DisplayChoice.StartFullscreen(true, true, false, null), Is.True, "--fullscreen을 함께 주면 그것이 먼저다");
        }

        [Test]
        public void StartFullscreen_TestMode_IsAlwaysWindowed()
        {
            Assert.That(DisplayChoice.StartFullscreen(true, false, true, true), Is.False, "테스트는 사용자의 화면을 덮지 않는다");
            Assert.That(DisplayChoice.StartFullscreen(null, false, true, null), Is.False);
        }

        [Test]
        public void MinimumSize_Is1280x720_UnlessStartedSmaller()
        {
            Assert.That(DisplayChoice.MinimumSize(1920, 1080), Is.EqualTo((1280, 720)));
            Assert.That(DisplayChoice.MinimumSize(640, 360), Is.EqualTo((640, 360)));
        }

        [Test]
        public void ClampToMinimum_StopsAtMinimum()
        {
            var min = DisplayChoice.MinimumSize(1920, 1080);
            Assert.That(DisplayChoice.ClampToMinimum(800, 450, min), Is.EqualTo((1280, 720)));
            Assert.That(DisplayChoice.ClampToMinimum(2000, 500, min), Is.EqualTo((2000, 720)));
            Assert.That(DisplayChoice.ClampToMinimum(2560, 1440, min), Is.EqualTo((2560, 1440)));
        }

        [TestCase("--fullscreen", true)]
        [TestCase("--windowed", false)]
        public void Options_DisplayModeFlags(string flag, bool fullscreen)
        {
            HostOptions o = HostOptions.Resolve(new[] { "--sample", flag });
            Assert.That(o.FullscreenRequested, Is.EqualTo(fullscreen));
            Assert.That(o.SizeGiven, Is.False);
        }

        [Test]
        public void Options_NoDisplayFlag_LeavesItToSettings_AndSizeIsRemembered()
        {
            HostOptions o = HostOptions.Resolve(new[] { "--sample", "--size", "1920x1080" });
            Assert.That(o.FullscreenRequested, Is.Null);
            Assert.That(o.SizeGiven, Is.True);
            Assert.That(HostOptions.Resolve(new[] { "--sample" }).SizeGiven, Is.False);
        }

        [Test]
        public void Options_FullscreenAndWindowedTogether_IsAnError()
        {
            Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--sample", "--fullscreen", "--windowed" }));
        }

        /// <summary>
        /// 해상도별 화면 맞춤(기준 1920x1080이 늘 들어가는 Expand): UI 배율은 가로·세로로 늘이지 않고 한 값이며,
        /// 논리 크기 × 배율 = 창 크기(남는 곳 없이 창 전체가 UI 영역), 가장 큰 패널(1440x900)이 논리 크기 안에 들어가고,
        /// 포인터 픽셀 → 논리 좌표 → 픽셀이 1픽셀 안에서 돌아온다.
        /// </summary>
        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3840, 2160)]
        [TestCase(1920, 1200)]
        [TestCase(2560, 1080)]
        public void ScreenFit_EachResolution_FillsWindow_KeepsAspect_FitsLargestPanel(int width, int height)
        {
            ScreenFit fit = ScreenFit.For(width, height);
            Assert.That(fit.LogicalWidth * fit.Scale, Is.EqualTo(width).Within(0.01));
            Assert.That(fit.LogicalHeight * fit.Scale, Is.EqualTo(height).Within(0.01));
            Assert.That(fit.LogicalWidth / fit.LogicalHeight, Is.EqualTo(width / (float)height).Within(1e-3), "가로세로를 따로 늘이지 않는다");
            Assert.That(fit.LogicalWidth, Is.GreaterThanOrEqualTo(ScreenFit.ReferenceWidth - 0.01f), "기준 1920x1080이 통째로 들어간다");
            Assert.That(fit.LogicalHeight, Is.GreaterThanOrEqualTo(ScreenFit.ReferenceHeight - 0.01f), "기준 1920x1080이 통째로 들어간다");
            Assert.That(fit.LogicalWidth, Is.GreaterThanOrEqualTo(LargestPanelWidth), "가장 넓은 패널이 들어간다");
            Assert.That(fit.LogicalHeight, Is.GreaterThanOrEqualTo(LargestPanelHeight), "가장 높은 패널이 들어간다");
            Assert.That(fit.TileScale, Is.GreaterThanOrEqualTo(1));
            foreach ((int px, int py) in new[] { (0, 0), (width - 1, height - 1), (width / 3, height / 2) })
            {
                float lx = px / fit.Scale, ly = py / fit.Scale;
                Assert.That(Math.Abs(fit.ToPixel(lx) - px), Is.LessThanOrEqualTo(1));
                Assert.That(Math.Abs(fit.ToPixel(ly) - py), Is.LessThanOrEqualTo(1));
            }
        }

        /// <summary>지금 등록된 화면 중 가장 넓은 패널(수첩·슬롯 선택 1440)과 가장 높은 패널(하루 결과 900).</summary>
        private const float LargestPanelWidth = 1440f, LargestPanelHeight = 900f;
    }
}
