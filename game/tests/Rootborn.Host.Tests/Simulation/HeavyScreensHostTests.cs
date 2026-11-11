using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;
using NUnit.Framework;
using Rootborn.Screens;
using Rootborn.Screens.Furniture;
using Rootborn.Screens.Inventory;
using Rootborn.Screens.Objectives;
using Rootborn.Screens.Records;
using Rootborn.Ui;

namespace Rootborn.Host.Tests.Simulation
{
    /// <summary>
    /// 무거운 화면을 되풀이해 열고 넘길 때(장치 이벤트 주입, OS 입력 계층 미확인): 가구 팔레트(실제 data 1,500여 개), 도감, 목표 수첩, 가방을
    /// 여러 번 열고 쪽 넘기기·휠을 되풀이하며 메모리, 프레임당 할당, 프레임당 그리기 명령 수, UI 그림 캐시를 잰다. 벽시계는 표에만 적고 판정하지 않는다
    /// (CI 러너의 소프트웨어 그리기에서 흔들린다). 표는 output/simulation/host-heavy-screens/memory.md.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class HeavyScreensHostTests
    {
        private const int Rounds = 4;
        /// <summary>첫 바퀴 뒤 → 끝 바퀴 전용 메모리가 늘어도 되는 양(MB). 2026-11-12: 늘지 않았다.</summary>
        private const long AllowedPrivateGrowthMb = 48;
        private const long AllowedManagedGrowthMb = 8;
        /// <summary>
        /// 넘기는 동안 프레임 한 번의 할당 한계(바이트). 쪽을 넘기면 칸의 글자·그림을 바꾸고, 늘이기 9조각 패널마다 Rootborn.Ui NineSlice가 int[4] 두 개를 만든다
        /// (범위 밖, 보고에 적음). 항목 수(1,500)에 비례하면 수만 바이트가 된다.
        /// </summary>
        private const long FrameBytesLimit = 8 * 1024;
        /// <summary>한 프레임의 UI 그리기 명령 한계. 보이는 칸만 그리면 수백 개다. 항목마다 그리면 수천 개가 된다.</summary>
        private const int DrawCommandsLimit = 400;

        private string _saves;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-heavy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            _root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        /// <summary>그린 프레임 수와 프레임마다의 UI 그리기 명령 수 중 가장 큰 것.</summary>
        private sealed class DrawMeter : DrawableGameComponent
        {
            private readonly HostGame _game;
            public DrawMeter(HostGame game) : base(game) { _game = game; DrawOrder = int.MaxValue; }
            public long Frames;
            public int MaxCommands;
            public override void Draw(GameTime gameTime)
            {
                Frames++;
                if (_game.DrawCommandCount > MaxCommands) MaxCommands = _game.DrawCommandCount;
            }
        }

        private sealed class Row
        {
            public string Screen;
            public int Round;
            public long PrivateMb, ManagedMb, BytesPerFrame, Frames;
            public int MaxCommands, UiTextures;
            public double WallMs;
        }

        private static long PrivateMb()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            using Process p = Process.GetCurrentProcess();
            return p.PrivateMemorySize64 / 1048576;
        }

        private sealed class Heavy
        {
            public string Name;
            public Action<PlayerDriver> Open, Churn, Close;
        }

        private static void Wheels(PlayerDriver player, string list, int each)
        {
            player.MovePointerTo(list);
            for (int i = 0; i < each; i++) player.Wheel(-1f);
            for (int i = 0; i < each; i++) player.Wheel(1f);
        }

        private IEnumerable<Heavy> Screens()
        {
            yield return new Heavy
            {
                Name = ScreenCatalog.FurniturePlacement,
                Open = p => { p.PressButton("hud.button.placement"); p.WaitForScreen(ScreenCatalog.FurniturePlacement); },
                Churn = p =>
                {
                    var screen = (FurniturePlacementScreen)p.Screens.Find(ScreenCatalog.FurniturePlacement);
                    Assert.That(screen.Palette.TotalCount, Is.GreaterThan(1000), "실제 data의 가구 1,500여 개");
                    int pages = Math.Min(screen.Palette.PageCount - 1, 30);
                    for (int i = 0; i < pages; i++) p.PressButton(FurniturePlacementScreen.NextId);
                    Assert.That(screen.Palette.Page, Is.EqualTo(pages));
                    for (int i = 0; i < pages; i++) p.PressButton(FurniturePlacementScreen.PreviousId);
                },
                Close = p => { p.PressButton(FurniturePlacementScreen.CloseId); p.WaitForScreenClosed(ScreenCatalog.FurniturePlacement); },
            };
            yield return new Heavy
            {
                Name = ScreenCatalog.Encyclopedia,
                Open = p => { p.PressButton("hud.button.encyclopedia"); p.WaitForScreen(ScreenCatalog.Encyclopedia); },
                Churn = p => Wheels(p, EncyclopediaScreen.ListId, 40),
                Close = p => { p.PressButton(EncyclopediaScreen.CloseId); p.WaitForScreenClosed(ScreenCatalog.Encyclopedia); },
            };
            yield return new Heavy
            {
                Name = ScreenCatalog.ObjectiveJournal,
                Open = p => p.OpenScreenWithKey(TestKey.Tab, ScreenCatalog.ObjectiveJournal),
                Churn = p => Wheels(p, ((ObjectiveJournalScreen)p.Screens.Find(ScreenCatalog.ObjectiveJournal)).List.Id, 40),
                Close = p => p.CloseScreenWithKey(TestKey.Tab, ScreenCatalog.ObjectiveJournal),
            };
            yield return new Heavy
            {
                Name = ScreenCatalog.Inventory,
                Open = p => p.OpenScreenWithKey(TestKey.I, ScreenCatalog.Inventory),
                Churn = p => Wheels(p, InventoryScreen.GridId, 40),
                Close = p => p.CloseScreenWithKey(TestKey.I, ScreenCatalog.Inventory),
            };
        }

        [Test]
        public void HEAVY_SCREENS_001_OpeningAndPagingRepeatedly_MemoryFlat_DrawsOnlyWhatIsVisible()
        {
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--save-root", _saves, "--slot", "slot-1", "--size", "1920x1080",
                "--evidence", Path.Combine(_root, "output", "host-heavy-screens-evidence"),
            });
            options.TestMode = true;
            using var player = new PlayerDriver(options);
            player.Frames(3);
            var meter = new DrawMeter(player.Game);
            player.Game.Components.Add(meter);
            var rows = new List<Row>();
            var failures = new StringBuilder();

            foreach (Heavy heavy in Screens())
            {
                var mine = new List<Row>();
                for (int round = 0; round < Rounds; round++)
                {
                    heavy.Open(player);
                    long frames = meter.Frames, bytes = GC.GetAllocatedBytesForCurrentThread();
                    meter.MaxCommands = 0;
                    var clock = Stopwatch.StartNew();
                    heavy.Churn(player);
                    double wall = clock.Elapsed.TotalMilliseconds;
                    long churnFrames = meter.Frames - frames, churnBytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
                    if (round == 0)
                    {
                        player.MovePointer(-50, -50);
                        player.Frames(2);
                        player.ScreenshotScreen(heavy.Name, "heavy-" + heavy.Name);
                    }
                    heavy.Close(player);
                    var row = new Row
                    {
                        Screen = heavy.Name, Round = round + 1, Frames = churnFrames, BytesPerFrame = churnFrames > 0 ? churnBytes / churnFrames : 0,
                        MaxCommands = meter.MaxCommands, UiTextures = player.Game.Renderer.CachedTextureCount, WallMs = wall,
                        PrivateMb = PrivateMb(), ManagedMb = GC.GetTotalMemory(false) / 1048576,
                    };
                    mine.Add(row);
                    rows.Add(row);
                }
                // 첫 바퀴는 그림·글자 캐시를 채운다. 둘째 바퀴 대비 끝 바퀴.
                Row second = mine[1], last = mine[mine.Count - 1];
                if (last.PrivateMb - second.PrivateMb >= AllowedPrivateGrowthMb) failures.AppendLine(heavy.Name + ": 전용 메모리가 바퀴마다 는다 " + second.PrivateMb + " → " + last.PrivateMb + "MB");
                if (last.ManagedMb - second.ManagedMb >= AllowedManagedGrowthMb) failures.AppendLine(heavy.Name + ": 관리 힙이 바퀴마다 는다 " + second.ManagedMb + " → " + last.ManagedMb + "MB");
                if (last.UiTextures != second.UiTextures) failures.AppendLine(heavy.Name + ": UI 그림 캐시가 바퀴마다 는다 " + second.UiTextures + " → " + last.UiTextures);
                for (int i = 1; i < mine.Count; i++)
                {
                    if (mine[i].BytesPerFrame > FrameBytesLimit) failures.AppendLine(heavy.Name + " " + mine[i].Round + "바퀴: 프레임당 할당 " + mine[i].BytesPerFrame + "바이트");
                    if (mine[i].MaxCommands > DrawCommandsLimit) failures.AppendLine(heavy.Name + " " + mine[i].Round + "바퀴: 그리기 명령 " + mine[i].MaxCommands + "개");
                }
            }

            string table = Table(rows);
            string dir = Path.Combine(_root, "output", "simulation", "host-heavy-screens");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "memory.md"), table);
            TestContext.Progress.WriteLine(table);
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty, "못 찾은 그림");
            Assert.That(failures.ToString(), Is.Empty, table);
        }

        private static string Table(List<Row> rows)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("# 무거운 화면 표 (실제 창, 장치 이벤트 주입 — OS 입력 계층 미확인)");
            sb.AppendLine();
            sb.AppendLine("- 바퀴마다: 열기 → 쪽 넘기기(가구 30쪽 앞뒤) 또는 휠 40칸 앞뒤 → 닫기. 메모리는 닫은 뒤 GC 두 번 뒤. 벽시계는 참고(판정 안 함).");
            sb.AppendLine();
            sb.AppendLine("| 화면 | 바퀴 | 프레임 | 프레임당 할당 B | 최대 그리기 명령 | UI 그림 수 | 전용 MB | 관리 힙 MB | 벽시계 ms/프레임 |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|");
            foreach (Row r in rows)
                sb.AppendLine(string.Format(c, "| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} | {8:0.00} |",
                    r.Screen, r.Round, r.Frames, r.BytesPerFrame, r.MaxCommands, r.UiTextures, r.PrivateMb, r.ManagedMb, r.Frames > 0 ? r.WallMs / r.Frames : 0));
            return sb.ToString();
        }
    }
}
