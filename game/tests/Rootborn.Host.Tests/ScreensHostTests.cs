using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Screens;
using Rootborn.Screens.Settings;
using Rootborn.Ui;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 화면 토대를 실제 창과 게임 루프로: 새 슬롯으로 시작 → Esc로 설정 → 포인터로 켬/끔 → 설정이 떠 있는 채 방향 키로 걷기 → Esc로 닫기.
    /// 장치 이벤트 주입(키는 SDL 이벤트 큐, 포인터는 포인터 장치 계층), OS 입력 계층 미확인. 세이브 폴더는 임시 폴더다.
    /// 스크린샷은 output/host-screens-evidence/.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class ScreensHostTests
    {
        private string _saves;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-screens-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        private PlayerDriver Start(string size, Action<ScreenRegistry> extra = null)
        {
            string root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--save-root", _saves, "--slot", "slot-1", "--size", size,
                "--evidence", Path.Combine(root, "output", "host-screens-evidence"),
            });
            options.TestMode = true;
            var player = new PlayerDriver(options, extra);
            player.Frames(3);
            return player;
        }

        [Test]
        public void SCREENS_001_NewSlot_EscOpensSettings_PointerToggles_WalkWhileOpen_EscCloses()
        {
            using PlayerDriver player = Start("1920x1080");
            Assert.That(player.Game.Session, Is.Not.Null, "슬롯으로 시작하면 GameSession으로 게임 한 판을 시작한다");
            Assert.That(player.Game.Session.Student.CurrentDay, Is.EqualTo(1));
            Assert.That(player.Game.World.SlotStart.NewGame, Is.True);
            player.Screenshot("screens-01-new-slot");

            player.OpenScreenWithKey(TestKey.Escape, ScreenCatalog.Settings);
            Assert.That(player.Screens.MovementBlocked, Is.False);
            Assert.That(player.Node("settings").Rect, Is.EqualTo(new UiRect(630, 285, 660, 510)), "Unity SettingsPanel 660x510 가운데");
            player.ScreenshotScreen(ScreenCatalog.Settings, "screens-02-settings-open");

            var settings = (SettingsScreen)player.Screens.Find(ScreenCatalog.Settings);
            Assert.That(settings.Model.IsOn(1), Is.False);
            player.PressButton(SettingsScreen.ToggleId(1));
            Assert.That(settings.Model.IsOn(1), Is.True, "포인터로 누른 켬/끔이 켜진다");
            player.PressButton(SettingsScreen.ToggleId(0));
            Assert.That(settings.Model.IsOn(0), Is.False);
            player.ScreenshotScreen(ScreenCatalog.Settings, "screens-03-settings-toggled");

            // 설정이 떠 있는 동안에도 방향 키로 걷는다(Unity 판과 같음, 설계 3.1절).
            player.MovePointer(-50, -50);
            float x0 = player.World.Walker.X, y0 = player.World.Walker.Y;
            player.WalkAway(2);
            float moved = Math.Abs(player.World.Walker.X - x0) + Math.Abs(player.World.Walker.Y - y0);
            Assert.That(moved, Is.GreaterThan(player.World.Map.Grid.CellSize), "걸었다");
            Assert.That(player.Screens.IsOpen(ScreenCatalog.Settings), Is.True, "걷는 동안 설정은 그대로 떠 있다");
            Assert.That(player.CollisionViolations, Is.EqualTo(0));
            player.Screenshot("screens-04-walk-with-settings-open");

            player.CloseScreenWithKey(TestKey.Escape, ScreenCatalog.Settings);
            player.Screenshot("screens-05-settings-closed");

            // 다시 열면 바꾼 값이 남아 있다(화면은 한 번만 만든다).
            player.OpenScreenWithKey(TestKey.Escape, ScreenCatalog.Settings);
            Assert.That(((UiToggle)player.Node(SettingsScreen.ToggleId(1))).IsOn, Is.True);
            Rootborn.Screens.Tests.KnownMissingKeys.AssertOnlyKnownMissing(player.Game.Text, System.Array.Empty<string>());
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
            player.CloseScreenWithKey(TestKey.Escape, ScreenCatalog.Settings);
        }

        [Test]
        public void SCREENS_002_ShiftArrowAndEnter_ToggleFromTheKeyboard_PlainArrowsStillWalk()
        {
            using PlayerDriver player = Start("1280x720");
            player.OpenScreenWithKey(TestKey.Escape, ScreenCatalog.Settings);
            var settings = (SettingsScreen)player.Screens.Find(ScreenCatalog.Settings);
            float sx = player.World.Walker.X, sy = player.World.Walker.Y;
            player.KeyDown(TestKey.LeftShift);
            player.PressKey(TestKey.Down);
            player.KeyUp(TestKey.LeftShift);
            Assert.That(player.Game.Ui.Focused?.Id, Is.EqualTo(SettingsScreen.ToggleId(0)));
            Assert.That((player.World.Walker.X, player.World.Walker.Y), Is.EqualTo((sx, sy)), "Shift+방향 키는 포커스만 옮기고 걷지 않는다");
            // Tab은 포커스를 옮기지 않고 목표 수첩을 연다. 큰 패널은 하나만 떠서 설정이 닫힌다(Unity ModernUiPanelInputRouter).
            player.PressKey(TestKey.Tab);
            Assert.That(player.Screens.IsOpen(ScreenCatalog.ObjectiveJournal), Is.True, "Tab은 목표 수첩 키");
            Assert.That(player.Screens.IsOpen(ScreenCatalog.Settings), Is.False);
            player.CloseScreenWithKey(TestKey.Tab, ScreenCatalog.ObjectiveJournal);
            player.OpenScreenWithKey(TestKey.Escape, ScreenCatalog.Settings);
            player.KeyDown(TestKey.LeftShift);
            player.PressKey(TestKey.Down);
            player.KeyUp(TestKey.LeftShift);
            Assert.That(player.Game.Ui.Focused?.Id, Is.EqualTo(SettingsScreen.ToggleId(0)));
            player.PressKey(TestKey.Enter);
            Assert.That(settings.Model.IsOn(0), Is.False);

            float x0 = player.World.Walker.X, y0 = player.World.Walker.Y;
            player.WalkAway(1);
            Assert.That(Math.Abs(player.World.Walker.X - x0) + Math.Abs(player.World.Walker.Y - y0), Is.GreaterThan(0f));
            Assert.That(player.Game.Ui.Focused?.Id, Is.EqualTo(SettingsScreen.ToggleId(0)), "방향 키는 포커스를 옮기지 않는다");
        }

        // ---------- 성능: 1,500칸 카드 격자 ----------

        private sealed class GridProbeScreen : GameScreen
        {
            public const int Items = 1500;
            public UiCardGrid Grid;
            public int Painted;

            public GridProbeScreen(ScreenServices s) : base(s) { }

            protected override UiNode Build()
            {
                var root = new UiPanel { Id = "grid-probe" };
                root.Centered(0, 0, 982, 760);
                Grid = root.Add(new UiCardGrid { Id = "grid-probe.grid", ItemCount = Items, CellWidth = 112, CellHeight = 112, Spacing = 8, Padding = 24 });
                Grid.Stretch(30, 30, 30, 30);
                Grid.DrawCell = (i, rect, state, list, text) =>
                {
                    Painted++;
                    list.Panel(rect, UiSkin.Slot, state.Selected ? UiTheme.ButtonHovered : UiTheme.ButtonNormal);
                    list.Image(new UiRect(rect.X + 32, rect.Y + 20, 48, 48), "icon:interior", default, UiTheme.White);
                    list.Text(rect.X + 12, rect.Y + 80, "#" + (i + 1), 18, UiTheme.Ink);
                };
                return root;
            }
        }

        [Test]
        public void SCREENS_003_Performance_1500CellGrid_OpenAndScroll()
        {
            using PlayerDriver player = Start("1920x1080",
                r => r.Add("grid-probe", ScreenLayer.Panel, true, s => new GridProbeScreen(s)).OpenedBy(ScreenKey.Inventory));

            // 열기: 키를 누른 프레임부터 화면이 그려진 프레임까지
            var open = Stopwatch.StartNew();
            player.PressKey(TestKey.I);
            player.WaitForScreen("grid-probe");
            player.Frames(1);
            open.Stop();
            var screen = (GridProbeScreen)player.Screens.Find("grid-probe");
            Assert.That(screen.Grid.RowCount, Is.EqualTo((1500 + screen.Grid.Columns - 1) / screen.Grid.Columns));

            // 스크롤: 휠 한 칸마다 한 프레임, 60번. 프레임 시간을 잰다.
            player.MovePointerTo("grid-probe.grid");
            var frameMs = new double[60];
            for (int i = 0; i < frameMs.Length; i++)
            {
                screen.Painted = 0;
                var frame = Stopwatch.StartNew();
                player.Wheel(-1f); // 2프레임
                frame.Stop();
                frameMs[i] = frame.Elapsed.TotalMilliseconds / 2.0;
            }
            int paintedPerFrame = screen.Painted / 2;
            Assert.That(screen.Grid.ScrollOffset, Is.GreaterThan(0f));
            Assert.That(paintedPerFrame, Is.LessThan(100), "보이는 칸만 그린다");
            player.ScreenshotScreen("grid-probe", "screens-06-grid-1500-scrolled");

            Array.Sort(frameMs);
            string report = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "1500칸 격자(열 {0}, 줄 {1}): 열기 {2:F1} ms(키 누름부터 첫 그림까지, 4프레임 포함), 스크롤 프레임 중앙값 {3:F2} ms, 최대 {4:F2} ms, 프레임당 그린 칸 {5}",
                screen.Grid.Columns, screen.Grid.RowCount, open.Elapsed.TotalMilliseconds, frameMs[frameMs.Length / 2], frameMs.Last(), paintedPerFrame);
            TestContext.Progress.WriteLine(report);
            File.WriteAllText(Path.Combine(HostOptions.Resolve(new[] { "--sample" }).RepoRoot, "output", "host-screens-evidence", "performance.txt"), report);
            Assert.That(frameMs[frameMs.Length / 2], Is.LessThan(16.7), "스크롤 프레임이 한 프레임(60fps) 안에 끝난다");
        }
    }
}
