using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using NUnit.Framework;
using Rootborn.Core.World;
using Rootborn.Host.Display;
using Rootborn.Host.Input;
using Rootborn.Host.World;
using Rootborn.Screens;
using Rootborn.Screens.Day;
using Rootborn.Screens.Inventory;
using Rootborn.Ui;
using Rootborn.World;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 창과 해상도를 실제 창과 게임 루프로: 해상도마다 창 모드로 새 슬롯을 시작해 HUD → I로 가방(포인터로 닫기) → 걷기 → 하루 마감 지점까지 걸어가 E로
    /// 하루 결과(포인터로 다음 날)를 띄우고, 보이는 UI가 화면 밖으로 나가지 않는지, 월드가 정수 배율이고 맵 밖을 비추지 않는지 본다.
    /// 실행 중에 창 크기를 바꾼 뒤에도 같고, 최소 크기 아래로는 줄지 않고, F11은 Host의 표시 모드 상태만 바꾼다(테스트 모드는 실제로 전체 화면이 되지 않는다).
    /// 장치 이벤트 주입(키는 SDL 이벤트 큐, 포인터는 포인터 장치 계층), OS 입력 계층 미확인. 세이브 폴더는 임시 폴더.
    /// 모니터보다 큰 해상도는 창을 띄울 수 없어 같은 화면들을 그 해상도의 논리 크기로 배치만 해 본다(그림은 그리지 않는다).
    /// 스크린샷: output/host-screens-evidence/window-*.png.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class WindowResolutionHostTests
    {
        private string _saves;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-window-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            _root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        private PlayerDriver Start(int width, int height, string slot = "slot-1")
        {
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--save-root", _saves, "--slot", slot, "--size", width + "x" + height,
                "--evidence", Path.Combine(_root, "output", "host-screens-evidence"),
            });
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        private static (int Width, int Height)? _display;

        /// <summary>
        /// 모니터 크기. MonoGame은 창(SDL)을 만든 뒤에야 모니터 크기를 알려 주므로 작은 견본 창을 한 번 띄워 읽고 닫는다.
        /// </summary>
        private static (int Width, int Height) Display()
        {
            if (_display.HasValue) return _display.Value;
            HostOptions options = HostOptions.Resolve(new[] { "--sample", "--size", "640x360" });
            options.TestMode = true;
            using (var probe = new PlayerDriver(options))
            {
                DisplayMode mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
                _display = (mode.Width, mode.Height);
            }
            return _display.Value;
        }

        /// <summary>창이 모니터 안에 들어가는가(창 크기가 모니터보다 크면 창을 띄워 보지 않는다).</summary>
        private static bool FitsOnDisplay(int width, int height) => width <= Display().Width && height <= Display().Height;

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3840, 2160)]
        [TestCase(1920, 1200)]
        [TestCase(2560, 1080)]
        [TestCase(1280, 800)]
        public void WINDOW_001_EachResolution_HudInventoryDayResult_StayOnScreen_PointerClicks_Walks(int width, int height)
        {
            if (!FitsOnDisplay(width, height))
            {
                // 모니터보다 큰 창은 운영체제가 줄여 버려 그 해상도의 화면 버퍼가 되지 않는다. 그래서 1920x1080 창에서 같은 흐름을 밟고
                // 화면마다 그 해상도의 논리 크기로 배치만 다시 해 화면 안에 들어가는지 본다(포인터·그림은 실제 창 크기 기준).
                ScreenFit target = ScreenFit.For(width, height);
                using PlayerDriver small = Start(1920, 1080);
                RunJourney(small, "window-" + width + "x" + height + "-layout-only", target);
                Assert.Pass("모니터(" + Display().Width + "x" + Display().Height + ")보다 커서 " + width + "x" + height + "은 논리 크기 배치만 검증했다");
            }

            using PlayerDriver player = Start(width, height);
            Assert.That(player.Game.Fullscreen, Is.False, "테스트와 --size는 창 모드");
            Assert.That(player.Game.Scene.Width, Is.EqualTo(width));
            Assert.That(player.Game.Scene.Height, Is.EqualTo(height));
            Assert.That(player.Game.Fit, Is.EqualTo(ScreenFit.For(width, height)));
            RunJourney(player, "window-" + width + "x" + height, null);
        }

        [Test]
        public void WINDOW_002_ResizeWhileRunning_RefitsUiAndWorld_AndStopsAtMinimum()
        {
            using PlayerDriver player = Start(1280, 720);
            Assert.That(player.Game.MinimumWindowSize, Is.EqualTo((DisplayChoice.MinimumWidth, DisplayChoice.MinimumHeight)));
            Assert.That(Sdl2Native.GetWindowMinimumSize(player.Game.Window.Handle), Is.EqualTo(player.Game.MinimumWindowSize), "창 최소 크기가 SDL 창에 걸렸다");
            AssertHudOnScreen(player, null);

            foreach ((int w, int h) in new[] { (1920, 1200), (2560, 1080), (1920, 1080) })
            {
                if (!FitsOnDisplay(w, h)) continue;
                player.Game.Resize(w, h);
                player.Frames(3);
                Assert.That(player.Game.Scene.Width, Is.EqualTo(w));
                Assert.That(player.Game.Scene.Height, Is.EqualTo(h));
                Assert.That(player.Game.Fit, Is.EqualTo(ScreenFit.For(w, h)), "창 크기가 바뀌면 화면 맞춤도 다시 한다");
                AssertHudOnScreen(player, null);
                OpenInventoryAndCloseWithPointer(player, "window-resized-" + w + "x" + h, null);
                AssertWalksAndWorldFits(player);
            }

            // 최소 크기 아래로 줄이면 최소 크기에서 멈춘다.
            player.Game.Resize(800, 450);
            player.Frames(3);
            Assert.That((player.Game.Scene.Width, player.Game.Scene.Height), Is.EqualTo((1280, 720)));
            Assert.That(player.Game.Fit, Is.EqualTo(ScreenFit.For(1280, 720)));
            AssertHudOnScreen(player, null);
            OpenInventoryAndCloseWithPointer(player, "window-resized-below-minimum", null);
            Assert.That(player.CollisionViolations, Is.EqualTo(0));
        }

        [Test]
        public void WINDOW_003_F11_TogglesDisplayModeState_SavesIt_ButTestStaysWindowed()
        {
            using (PlayerDriver player = Start(1280, 720))
            {
                Assert.That(player.Game.Fullscreen, Is.False);
                Assert.That(player.Game.SavedFullscreen, Is.Null, "새 세이브 폴더에는 표시 모드 설정이 없다");
                player.PressKey(TestKey.F11);
                Assert.That(player.Game.Fullscreen, Is.True, "F11이 전체 화면 요청을 Host 상태에 반영했다");
                Assert.That(player.Game.Settings.Settings.GetToggle(DisplayChoice.FullscreenSetting), Is.True, "고른 모드를 설정에 남긴다");
                Assert.That((player.Game.Scene.Width, player.Game.Scene.Height), Is.EqualTo((1280, 720)), "테스트 모드는 실제 창을 바꾸지 않는다");
                Assert.That(player.Game.Window.ClientBounds.Width, Is.EqualTo(1280));
                AssertHudOnScreen(player, null);
                player.WalkAway(1);

                player.PressKey(TestKey.F11);
                Assert.That(player.Game.Fullscreen, Is.False, "다시 F11이면 창 모드");
                player.PressKey(TestKey.F11);
                Assert.That(player.Game.Fullscreen, Is.True);
            }

            // 다음 실행은 설정 파일의 값을 읽는다(테스트 모드라 실제로는 창 모드로 뜬다).
            using (PlayerDriver again = Start(1280, 720))
            {
                Assert.That(again.Game.SavedFullscreen, Is.True, "설정 파일에서 표시 모드를 읽었다");
                Assert.That(again.Game.Fullscreen, Is.False, "테스트 모드는 늘 창 모드");
            }
            Assert.That(DisplayChoice.StartFullscreen(null, false, false, true), Is.True, "테스트가 아니고 --size도 없으면 저장된 전체 화면을 따른다");
        }

        [Test]
        public void WINDOW_005_SettingsDisplayRow_PointerClick_FlipsHostMode_SavesIt_AndFollowsF11()
        {
            using (PlayerDriver player = Start(1280, 720))
            {
                player.OpenScreenWithKey(TestKey.Escape, ScreenCatalog.Settings);
                var value = (UiButton)player.Node(Rootborn.Screens.Settings.SettingsScreen.DisplayButtonId);
                string windowed = player.Game.Text.Get("ui.settings.display-windowed");
                string fullscreen = player.Game.Text.Get("ui.settings.display-fullscreen");
                Assert.That(value.Text, Is.EqualTo(windowed));

                player.Click(Rootborn.Screens.Settings.SettingsScreen.DisplayButtonId);
                Assert.That(player.Game.Fullscreen, Is.True, "설정 화면 단추가 Host 표시 모드를 바꿨다");
                Assert.That(player.Game.Settings.Settings.GetToggle(DisplayChoice.FullscreenSetting), Is.True, "고른 모드를 설정에 남긴다");
                Assert.That(value.Text, Is.EqualTo(fullscreen));
                Assert.That(player.Game.Window.ClientBounds.Width, Is.EqualTo(1280), "테스트 모드는 실제 창을 바꾸지 않는다");
                player.ScreenshotScreen(ScreenCatalog.Settings, "settings-display-fullscreen");

                player.PressKey(TestKey.F11);
                Assert.That(player.Game.Fullscreen, Is.False);
                Assert.That(value.Text, Is.EqualTo(windowed), "F11로 바꾼 것이 열린 설정 화면에 바로 보인다");
                player.Click(Rootborn.Screens.Settings.SettingsScreen.DisplayButtonId);
                Assert.That(player.Game.Fullscreen, Is.True);
            }
            using (PlayerDriver again = Start(1280, 720))
                Assert.That(again.Game.SavedFullscreen, Is.True, "다음 실행이 설정 화면에서 고른 모드를 읽는다");
        }

        [Test]
        public void WINDOW_004_FullscreenStart_PreparesBorderlessBackbufferAtDisplaySize_WithoutRunning()
        {
            (int dw, int dh) = Display();
            HostOptions options = HostOptions.Resolve(new[] { "--sample", "--fullscreen" });
            Assert.That(options.TestMode, Is.False);
            // 창을 보이지 않는다: 만들기만 하고 Run·RunOneFrame을 부르지 않는다(사용자의 화면을 덮지 않는다).
            using var game = new HostGame(options);
            var graphics = (Microsoft.Xna.Framework.GraphicsDeviceManager)game.Services.GetService(typeof(Microsoft.Xna.Framework.IGraphicsDeviceManager));
            Assert.That(game.Fullscreen, Is.True);
            Assert.That(graphics.IsFullScreen, Is.True);
            Assert.That(graphics.HardwareModeSwitch, Is.False, "테두리 없는 전체 화면(모니터 해상도를 바꾸지 않는다)");
            Assert.That((graphics.PreferredBackBufferWidth, graphics.PreferredBackBufferHeight), Is.EqualTo((dw, dh)));
            Assert.That(game.WindowedSize, Is.EqualTo((1280, 720)), "F11로 창 모드가 되면 이 크기");
        }

        // ---------- 흐름 ----------

        /// <param name="layoutFit">null이면 지금 창 크기로 본다. 주면 화면마다 그 논리 크기로 배치를 다시 해 보고 원래대로 돌린다.</param>
        private void RunJourney(PlayerDriver player, string shotPrefix, ScreenFit? layoutFit)
        {
            Assert.That(player.World.Map.Id, Is.EqualTo("House"));
            AssertHudOnScreen(player, layoutFit);
            player.Screenshot(shotPrefix + "-hud");

            OpenInventoryAndCloseWithPointer(player, shotPrefix, layoutFit);
            AssertWalksAndWorldFits(player);

            InteractionThing end = InteractionData.Load(Path.Combine(_root, InteractionData.DefaultRelativePath)).Map("House").Things.First(t => t.Kind == DayResultScreen.InteractionKind);
            player.WalkNear(end.X, end.Y, 24f);
            Assert.That(player.Game.World.Interactions.Target?.Kind, Is.EqualTo(DayResultScreen.InteractionKind));
            player.PressKey(TestKey.E);
            player.WaitForScreen(ScreenCatalog.DayResult);
            player.MovePointer(-50, -50);
            player.Frames(2);
            AssertOnScreen(player, layoutFit, "하루 결과");
            player.ScreenshotScreen(ScreenCatalog.DayResult, shotPrefix + "-day-result");
            int day = player.Game.Session.StudentDay;
            player.PressButton(DayResultScreen.NextDayId);
            player.WaitForScreenClosed(ScreenCatalog.DayResult);
            Assert.That(player.Game.Session.StudentDay, Is.EqualTo(day + 1), "포인터로 누른 다음 날 단추가 눌렸다");

            Assert.That(player.CollisionViolations, Is.EqualTo(0));
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
        }

        private static void OpenInventoryAndCloseWithPointer(PlayerDriver player, string shotPrefix, ScreenFit? layoutFit)
        {
            player.OpenScreenWithKey(TestKey.I, ScreenCatalog.Inventory);
            player.MovePointer(-50, -50);
            player.Frames(2);
            AssertOnScreen(player, layoutFit, "가방");
            player.ScreenshotScreen(ScreenCatalog.Inventory, shotPrefix + "-inventory");
            player.PressButton(InventoryScreen.CloseId);
            player.WaitForScreenClosed(ScreenCatalog.Inventory);
        }

        private static void AssertHudOnScreen(PlayerDriver player, ScreenFit? layoutFit)
        {
            Assert.That(player.Screens.IsOpen(ScreenCatalog.HudClock) && player.Screens.IsOpen(ScreenCatalog.HudVitals), Is.True, "HUD가 떠 있다");
            AssertOnScreen(player, layoutFit, "HUD");
        }

        /// <summary>보이는 UI 요소가 모두 논리 화면 안에 있다. layoutFit을 주면 그 논리 크기로 배치를 다시 해 보고 지금 창 크기로 되돌린다.</summary>
        private static void AssertOnScreen(PlayerDriver player, ScreenFit? layoutFit, string what)
        {
            ScreenFit fit = layoutFit ?? player.Game.Fit;
            if (layoutFit.HasValue) player.Screens.Layout(fit.LogicalWidth, fit.LogicalHeight);
            try
            {
                var outside = new List<string>();
                Collect(player.Game.Ui.Root, fit.LogicalWidth, fit.LogicalHeight, outside);
                Assert.That(outside, Is.Empty, what + "의 요소가 " + fit.LogicalWidth + "x" + fit.LogicalHeight + " 논리 화면 밖으로 나갔다");
            }
            finally
            {
                if (layoutFit.HasValue) player.Screens.Layout(player.Game.Fit.LogicalWidth, player.Game.Fit.LogicalHeight);
            }
        }

        private static void Collect(UiNode node, float width, float height, List<string> outside)
        {
            if (!node.Visible) return;
            UiRect r = node.Rect;
            const float slack = 0.5f;
            if (r.W > 0 && r.H > 0 && (r.X < -slack || r.Y < -slack || r.Right > width + slack || r.Bottom > height + slack))
                outside.Add((node.Id ?? "(id 없음)") + " " + r);
            foreach (UiNode child in node.Children) Collect(child, width, height, outside);
        }

        /// <summary>걷는다. 월드는 정수 배율이고, 맵이 화면보다 크면 카메라가 맵 안에 머물고 작으면 가운데에 둔다(맵 밖 타일을 비추지 않는다).</summary>
        private static void AssertWalksAndWorldFits(PlayerDriver player)
        {
            float x0 = player.World.Walker.X, y0 = player.World.Walker.Y;
            player.WalkAway(2);
            float moved = Math.Abs(player.World.Walker.X - x0) + Math.Abs(player.World.Walker.Y - y0);
            Assert.That(moved, Is.GreaterThan(player.World.Map.Grid.CellSize), "걸었다");
            player.Frames(1);

            WorldScreen world = player.Game.World;
            Assert.That(world.Scale, Is.EqualTo(MathF.Round(world.Scale)), "월드는 정수 배율");
            Assert.That(world.Scale, Is.GreaterThanOrEqualTo(1f));
            MapGrid grid = world.Current.World.Grid;
            float viewW = player.Game.Scene.Width / world.Scale, viewH = player.Game.Scene.Height / world.Scale;
            AssertAxis(world.CameraOffset.X, grid.PixelWidth, viewW, "가로");
            AssertAxis(world.CameraOffset.Y, grid.PixelHeight, viewH, "세로");
        }

        private static void AssertAxis(float offset, float mapSize, float viewSize, string axis)
        {
            if (mapSize <= viewSize)
                Assert.That(offset, Is.EqualTo(MathF.Round(-(viewSize - mapSize) / 2f)).Within(0.5f), axis + ": 맵이 화면보다 작으면 가운데");
            else
            {
                Assert.That(offset, Is.GreaterThanOrEqualTo(0f), axis + ": 맵 왼쪽(위) 밖을 비추지 않는다");
                Assert.That(offset + viewSize, Is.LessThanOrEqualTo(mapSize + 0.5f), axis + ": 맵 오른쪽(아래) 밖을 비추지 않는다");
            }
        }
    }
}
