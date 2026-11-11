using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Rootborn.Core.World;
using Rootborn.Host.World;
using Rootborn.Screens;
using Rootborn.Screens.Day;
using Rootborn.Simulation;
using Rootborn.Ui;
using Rootborn.World;

namespace Rootborn.Host.Tests.Simulation
{
    /// <summary>
    /// 오래 플레이했을 때(장치 이벤트 주입, OS 입력 계층 미확인): 돌아다니기 정책(문을 먼저)으로 실제 창에서 여러 날을 플레이하며 일정 걸음마다
    /// 프로세스 전용 메모리, 관리 힙, 그림 캐시, 가만히 선 프레임의 할당을 잰다. 기본은 CI에서 3분 안(6일). 길게 돌리려면
    /// ROOTBORN_SIM_SOAK_DAYS(날 수), ROOTBORN_SIM_SOAK_SAMPLE(몇 걸음마다 잴지). 표는 output/simulation/host-longplay-wander-1/memory.md.
    /// 검사(흔들리지 않게 넉넉한 한계, 여러 번 잰 값의 가장 작은 것):
    /// - 처음 한 바퀴(이 판이 가 볼 맵을 다 가 본 때) 뒤의 메모리보다 끝의 메모리가 정해진 여유 이상 크지 않다.
    /// - 그림 캐시는 한도 안이다(넘으면 맵을 바꿀 때 앞 맵의 그림을 푼다 — WorldFrameCostHostTests).
    /// - 가만히 선 프레임(맨 위 화면 없음)의 할당이 한계 아래다.
    /// 끝에서 일과 판의 넘기기로 그날의 때를 다 쓰고 밤의 마을로 걸어 나가 한 장 찍는다.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class LongPlayHostTests
    {
        /// <summary>처음 한 바퀴 뒤 → 끝까지 전용 메모리가 늘어도 되는 양(MB). 2026-11-12: 6일 판에서 늘지 않았다(±10MB 흔들림).</summary>
        private const long AllowedPrivateGrowthMb = 64;
        /// <summary>같은 구간의 관리 힙(GC 뒤)이 늘어도 되는 양(MB). 2026-11-12: 29MB에서 그대로.</summary>
        private const long AllowedManagedGrowthMb = 16;
        /// <summary>
        /// 맨 위 화면 없이 가만히 선 실제 창 프레임 한 번의 할당 한계(바이트). 2026-11-12 측정 720 — 전부 Rootborn.Ui NineSlice.BuildStretched가
        /// 늘이기 9조각 패널(HUD)마다 int[4] 두 개를 만드는 것이다(그 프로젝트는 이 작업 범위 밖, 보고에 적음). 월드 갱신·그리기는 0(WORLD_FRAME_001).
        /// Ui를 고치면 이 한계를 0 가까이로 내린다.
        /// </summary>
        private const long IdleFrameBytesLimit = 1024;
        /// <summary>메모리를 비교할 때 가장 작은 값을 고르는 창(잰 횟수).</summary>
        private const int Window = 3;
        private const int IdleFrames = 30;

        private sealed class Sample
        {
            public int Step, Day;
            public string Map, Top;
            public long PrivateMb, ManagedMb, IdleBytesPerFrame, TextureBytes;
            public int Textures, MapsSeen;
        }

        private static string Root => HostOptions.Resolve(new[] { "--sample" }).RepoRoot;

        private static long PrivateMb()
        {
            using Process p = Process.GetCurrentProcess();
            return p.PrivateMemorySize64 / 1048576;
        }

        private static Sample Measure(DriverBody body, int step, int mapsSeen)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            body.Player.Frames(IdleFrames); // 아무것도 누르지 않는다
            long idle = (GC.GetAllocatedBytesForCurrentThread() - before) / IdleFrames;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            WorldScreen world = body.Player.Game.World;
            return new Sample
            {
                Step = step, Day = body.Session?.StudentDay ?? 0, MapsSeen = mapsSeen,
                Map = body.InWorld ? body.Walk.Map.Id : "-", Top = body.Screens.FocusScreen?.Id ?? "",
                PrivateMb = PrivateMb(), ManagedMb = GC.GetTotalMemory(false) / 1048576, IdleBytesPerFrame = idle,
                Textures = world?.CachedTextureCount ?? 0, TextureBytes = world?.CachedTextureBytes ?? 0,
            };
        }

        [Test]
        public void LONGPLAY_001_WanderingForDays_MemoryStaysFlat_TexturesStayInBudget_IdleFramesAreCheap()
        {
            string root = Root;
            int days = PlaySettings.EnvInt("ROOTBORN_SIM_SOAK_DAYS", 6), every = PlaySettings.EnvInt("ROOTBORN_SIM_SOAK_SAMPLE", 10);
            var settings = new PlaySettings { Days = days, StepsPerDay = 40, MaxSteps = days * 120 };
            PlayAtlas atlas = PlayAtlas.Load(root);
            const string name = "host-longplay-wander-1";
            using var body = new DriverBody(root, Path.Combine(root, "output", "host-sim-evidence"));
            var runner = new PlayRunner(body, PlayPolicyCatalog.Default().Create(PlayPolicyCatalog.Wander, atlas), 1, settings, atlas, name);
            var samples = new List<Sample>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int transitions = 0;
            string lastMap = null;
            var clock = Stopwatch.StartNew();
            runner.AfterStep += (step, action) =>
            {
                if (body.InWorld)
                {
                    string map = body.Walk.Map.Id;
                    if (lastMap != null && map != lastMap) transitions++;
                    lastMap = map;
                    seen.Add(map);
                }
                if (step % every == 0) samples.Add(Measure(body, step, seen.Count));
            };
            PlayResult result = runner.Run();
            samples.Add(Measure(body, result.Steps.Count, seen.Count));
            double seconds = clock.Elapsed.TotalSeconds;
            PlayRecord.Write(result, root, settings, ScreenCatalog.Create().Screens.Select(s => s.Id), atlas);
            Rootborn.Simulation.Tests.KnownDefects.AssertNoNewFailure(result);

            // 처음 한 바퀴: 이 판이 가 본 맵을 다 가 본 첫 측정. 그 뒤 Window번의 가장 작은 값과 끝 Window번의 가장 작은 값을 비교한다.
            int round = samples.FindIndex(s => s.MapsSeen >= seen.Count);
            string table = Table(samples, round, result, seconds, transitions, seen);
            string dir = PlayRecord.Directory(root, name);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "memory.md"), table);
            TestContext.Progress.WriteLine(table);

            Assert.That(result.DaysCompleted, Is.GreaterThanOrEqualTo(days), "날을 넘기며 플레이했다");
            Assert.That(seen.Count, Is.GreaterThanOrEqualTo(4), "여러 맵을 오갔다: " + string.Join(", ", seen));
            Assert.That(samples.Count - round, Is.GreaterThanOrEqualTo(2 * Window),
                "처음 한 바퀴 뒤에 잰 것이 모자라다(ROOTBORN_SIM_SOAK_DAYS를 늘린다). 한 바퀴: " + round + "/" + samples.Count);
            long privateAfterRound = samples.Skip(round).Take(Window).Min(s => s.PrivateMb), privateEnd = samples.Skip(samples.Count - Window).Min(s => s.PrivateMb);
            long managedAfterRound = samples.Skip(round).Take(Window).Min(s => s.ManagedMb), managedEnd = samples.Skip(samples.Count - Window).Min(s => s.ManagedMb);
            Assert.That(privateEnd - privateAfterRound, Is.LessThan(AllowedPrivateGrowthMb), "전용 메모리가 계속 는다(MB)\n" + table);
            Assert.That(managedEnd - managedAfterRound, Is.LessThan(AllowedManagedGrowthMb), "관리 힙이 계속 는다(MB)\n" + table);
            long budget = body.Player.Game.World.TextureBudgetBytes;
            Assert.That(samples.Max(s => s.TextureBytes), Is.LessThanOrEqualTo(budget), "그림 캐시가 한도를 넘었다\n" + table);
            List<Sample> idle = samples.Where(s => s.Map != "-" && s.Top.Length == 0).ToList();
            Assert.That(idle, Is.Not.Empty, "맨 위 화면 없이 잰 것이 있다");
            Assert.That(idle.Max(s => s.IdleBytesPerFrame), Is.LessThanOrEqualTo(IdleFrameBytesLimit), "가만히 선 프레임 할당(바이트)\n" + table);

            NightInTown(body, atlas);
            Assert.That(body.Player.Game.Renderer.MissingImages, Is.Empty, "못 찾은 그림");
        }

        /// <summary>
        /// 끝: 일과 판이 있는 맵으로 걸어가 넘기기로 오늘의 때를 다 쓰면 방의 때가 밤이 된다. 낮밤이 있는 맵으로 걸어 나가 찍는다(직접 볼 스크린샷).
        /// </summary>
        private static void NightInTown(DriverBody body, PlayAtlas atlas)
        {
            CloseScreens(body);
            WalkTo(body, atlas, map => atlas.HasThing(map, DailyPlanScreen.InteractionKind, null));
            InteractionThing board = atlas.Interactions.Map(body.Walk.Map.Id).Things.First(t => t.Kind == DailyPlanScreen.InteractionKind);
            Assert.That(body.WalkNear(board.X, board.Y, 24f), Is.True, "일과 판까지 걸었다");
            body.Interact();
            body.Player.WaitForScreen(ScreenCatalog.DailyPlan);
            for (int i = 0; i < 8 && ((UiButton)body.Player.Node(DailyPlanScreen.SkipId)).Enabled; i++) body.Player.PressButton(DailyPlanScreen.SkipId);
            body.Player.PressButton(DailyPlanScreen.CloseBottomId);
            body.Player.WaitForScreenClosed(ScreenCatalog.DailyPlan);
            Assert.That(body.Player.Game.World.LightTimeSlot, Is.EqualTo(Rootborn.Core.StudentLife.LocationStateTimeSlots.Night), "오늘의 때를 다 쓰면 밤");

            WalkTo(body, atlas, map => atlas.Maps.Catalog.Find(map)?.DayNight == true);
            body.Player.Frames(2);
            Assert.That(atlas.Maps.Catalog.Find(body.Walk.Map.Id).DayNight, Is.True);
            Assert.That(body.Player.Game.World.Tint.R, Is.LessThan(0.3f), "밤의 바깥은 어둡다: " + body.Player.Game.World.Tint);
            body.Player.MovePointer(-50, -50);
            body.Player.Frames(2);
            TestContext.Progress.WriteLine("스크린샷: " + body.Screenshot("longplay-end-" + body.Walk.Map.Id + "-night"));
        }

        private static void CloseScreens(DriverBody body)
        {
            for (int i = 0; i < 10 && body.Screens.FocusScreen != null; i++)
            {
                GameScreen top = body.Screens.FocusScreen;
                body.Player.PressKey(TestKey.Escape);
                if (body.Screens.FocusScreen == top) body.Player.Frames(10);
            }
            Assert.That(body.Screens.FocusScreen, Is.Null, "화면을 다 닫았다");
        }

        private static void WalkTo(DriverBody body, PlayAtlas atlas, Func<string, bool> goal)
        {
            for (int hop = 0; hop < 8; hop++)
            {
                string first = atlas.FirstDoorToward(body.Walk.Map.Id, goal);
                Assert.That(first, Is.Not.Null, body.Walk.Map.Id + "에서 갈 길이 없다");
                if (first.Length == 0) return;
                DoorArea door = body.Walk.Map.Doors.First(d => d.Target == first);
                Assert.That(body.WalkThroughDoor(door), Is.True, body.Walk.Map.Id + "의 문 " + first);
            }
            Assert.Fail("문 8개 안에 닿지 못했다");
        }

        private static string Table(List<Sample> samples, int round, PlayResult result, double seconds, int transitions, HashSet<string> seen)
        {
            var sb = new StringBuilder();
            CultureInfo c = CultureInfo.InvariantCulture;
            sb.AppendLine("# 긴 플레이 메모리 표 (실제 창, 장치 이벤트 주입 — OS 입력 계층 미확인)");
            sb.AppendLine();
            sb.AppendLine(string.Format(c, "- 정책 {0}, 씨앗 {1}, {2}일 완료, 걸음 {3}, 프레임 {4}, 맵 바꿈 {5}번, 벽시계 {6:0}초",
                result.Policy, result.Seed, result.DaysCompleted, result.Steps.Count, result.Frames, transitions, seconds));
            sb.AppendLine("- 가 본 맵: " + string.Join(", ", seen.OrderBy(m => m, StringComparer.Ordinal)));
            sb.AppendLine("- 처음 한 바퀴(가 본 맵을 다 가 본 첫 측정): " + (round >= 0 ? "걸음 " + samples[round].Step : "없음"));
            sb.AppendLine("- 전용 메모리 = Process.PrivateMemorySize64(GC 두 번 뒤), 관리 힙 = GC.GetTotalMemory, 가만히 선 프레임 = 아무것도 누르지 않은 " + IdleFrames
                + "프레임의 스레드 할당 / 프레임, 그림 MB = 월드 그림 캐시 가로×세로×4 추정");
            sb.AppendLine();
            sb.AppendLine("| 걸음 | 날 | 맵 | 맨 위 화면 | 전용 MB | 관리 힙 MB | 가만히 선 프레임 B | 그림 수 | 그림 MB |");
            sb.AppendLine("|---:|---:|---|---|---:|---:|---:|---:|---:|");
            foreach (Sample s in samples)
                sb.AppendLine(string.Format(c, "| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} | {8:0.0} |",
                    s.Step, s.Day, s.Map, s.Top.Length == 0 ? "-" : s.Top, s.PrivateMb, s.ManagedMb, s.IdleBytesPerFrame, s.Textures, s.TextureBytes / 1048576.0));
            return sb.ToString();
        }
    }
}
