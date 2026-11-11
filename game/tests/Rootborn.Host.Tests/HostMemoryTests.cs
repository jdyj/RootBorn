using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 창을 띄우는 테스트를 여러 건 이어 돌려도 프로세스 메모리가 쌓이지 않는지 본다.
    /// 2026-11-12: Host 테스트 130건을 한 프로세스에서 돌리는 동안 전용 메모리가 10GB를 넘었다. 테스트는 Run() 대신 한 프레임씩 돌리는데,
    /// MonoGame은 닫은 게임의 GL 컨텍스트(그 안의 텍스처 전부)를 Run() 루프 안에서만 지운다. 그래서 테스트마다 수십 MB가 남았다.
    /// 한계는 흔들리지 않게 넉넉히 둔다: 고치기 전에는 한 판에 약 60~70MB씩 늘었다.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class HostMemoryTests
    {
        private const int Games = 6;
        private const long AllowedGrowthPerGameMb = 12;

        private static long PrivateMb()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            using Process p = Process.GetCurrentProcess();
            return p.PrivateMemorySize64 / 1048576;
        }

        private static void PlayOnce()
        {
            string saves = Path.Combine(Path.GetTempPath(), "rootborn-memory-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saves);
            try
            {
                string root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
                HostOptions options = HostOptions.Resolve(new[]
                {
                    "--save-root", saves, "--slot", "slot-1", "--size", "1280x720",
                    "--evidence", Path.Combine(root, "output", "host-memory-evidence"),
                });
                options.TestMode = true;
                using var player = new PlayerDriver(options);
                player.Frames(5);
                player.PressKey(TestKey.I); // 가방 그림과 글자까지 읽게 한다
                player.Frames(5);
            }
            finally
            {
                try { Directory.Delete(saves, true); } catch (IOException) { }
            }
        }

        [Test]
        public void HOST_MEMORY_001_OpeningAndClosingTheGameManyTimes_DoesNotPileUpMemory()
        {
            PlayOnce(); // 처음 한 번은 정의·그림 캐시와 드라이버 초기화가 들어간다
            long start = PrivateMb();
            var trail = new System.Text.StringBuilder();
            for (int i = 0; i < Games; i++)
            {
                PlayOnce();
                trail.Append(PrivateMb()).Append(' ');
            }
            long growth = PrivateMb() - start;
            TestContext.Progress.WriteLine("HOST_MEMORY_001 전용 메모리(MB): 시작 " + start + ", 판마다 " + trail + "→ 늘어난 양 " + growth);
            Assert.That(growth, Is.LessThan(Games * AllowedGrowthPerGameMb),
                Games + "판을 열고 닫는 동안 전용 메모리가 " + growth + "MB 늘었다(" + trail + "). 닫은 게임의 GL 컨텍스트·텍스처가 남는지 본다.");
        }
    }
}
