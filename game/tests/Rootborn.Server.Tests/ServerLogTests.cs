using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Rootborn.Server.Tests
{
    /// <summary>운영 로그: 시각 붙이기, 파일에도 쓰기, 한도에서 돌려 쓰기(지난 파일 N개), 여러 스레드. 임시 폴더만 쓴다. 자동 테스트.</summary>
    public sealed class ServerLogTests
    {
        private string _dir;
        private static readonly DateTime At = new DateTime(2026, 11, 12, 21, 5, 9);

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "rootborn-server-log", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void ConsoleOnly_StampsEachLine_AndWritesNoFile()
        {
            var console = new StringWriter();
            using (var log = new ServerLog(console, null, now: () => At)) log.Write("들어옴: p1");
            Assert.That(console.ToString().Trim(), Is.EqualTo("2026-11-12 21:05:09 들어옴: p1"));
            Assert.That(Directory.GetFiles(_dir), Is.Empty);
        }

        [Test]
        public void File_GetsTheSameLines_InAFolderItCreates()
        {
            string path = Path.Combine(_dir, "logs", "server.log");
            var console = new StringWriter();
            using (var log = new ServerLog(console, path, now: () => At))
            {
                log.Write("한 줄");
                log.Write("두 줄");
            }

            Assert.That(File.ReadAllLines(path), Is.EqualTo(new[] { "2026-11-12 21:05:09 한 줄", "2026-11-12 21:05:09 두 줄" }));
            Assert.That(console.ToString(), Does.Contain("두 줄"));
        }

        [Test]
        public void Rotates_BeforeTheLimit_KeepingOnlyNOldFiles()
        {
            string path = Path.Combine(_dir, "server.log");
            using (var log = new ServerLog(TextWriter.Null, path, maxBytes: 1024, keepFiles: 2, now: () => At))
                for (int i = 0; i < 200; i++) log.Write("줄 " + i.ToString("000") + " " + new string('x', 40));

            Assert.That(new FileInfo(path).Length, Is.LessThanOrEqualTo(1024));
            Assert.That(File.Exists(path + ".1"), Is.True);
            Assert.That(File.Exists(path + ".2"), Is.True);
            Assert.That(File.Exists(path + ".3"), Is.False, "지난 파일은 2개만");
            foreach (var f in new[] { path, path + ".1", path + ".2" }) Assert.That(new FileInfo(f).Length, Is.LessThanOrEqualTo(1024));
            Assert.That(File.ReadAllLines(path).Last(), Does.Contain("줄 199"), "지금 파일에 마지막 줄");
            Assert.That(File.ReadAllLines(path + ".1").Last(), Is.Not.EqualTo(File.ReadAllLines(path).Last()));
            // 줄은 잘리지 않는다: 모든 줄이 시각으로 시작한다.
            foreach (var f in new[] { path, path + ".1", path + ".2" })
                Assert.That(File.ReadAllLines(f).All(l => l.StartsWith("2026-11-12 21:05:09 줄 ")), Is.True, f);
        }

        [Test]
        public void KeepZero_DropsTheOldFile()
        {
            string path = Path.Combine(_dir, "server.log");
            using (var log = new ServerLog(TextWriter.Null, path, maxBytes: 1024, keepFiles: 0, now: () => At))
                for (int i = 0; i < 100; i++) log.Write(new string('y', 50));
            Assert.That(Directory.GetFiles(_dir).Select(Path.GetFileName), Is.EqualTo(new[] { "server.log" }));
        }

        [Test]
        public void ManyThreads_LinesDoNotInterleave()
        {
            string path = Path.Combine(_dir, "server.log");
            using (var log = new ServerLog(TextWriter.Null, path, now: () => At))
                Parallel.For(0, 8, t =>
                {
                    for (int i = 0; i < 200; i++) log.Write("스레드 " + t + " 줄 " + i);
                });
            var lines = File.ReadAllLines(path);
            Assert.That(lines.Length, Is.EqualTo(1600));
            Assert.That(lines.All(l => l.StartsWith("2026-11-12 21:05:09 스레드 ")), Is.True);
        }

        [Test]
        public void BadLimits_AreRefused()
        {
            Assert.Throws<ArgumentException>(() => new ServerLog(TextWriter.Null, null, maxBytes: 10));
            Assert.Throws<ArgumentException>(() => new ServerLog(TextWriter.Null, null, keepFiles: -1));
        }
    }
}
