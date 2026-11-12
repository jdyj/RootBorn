using System;
using System.IO;
using NUnit.Framework;

namespace Rootborn.Host.Tests
{
    /// <summary>한도 없는 기다림을 없앤 동사(<see cref="PlayerDriver.FramesUntil"/>)가 한도에서 무엇을 기다렸는지 담아 실패하는지 본다.</summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class PlayerDriverWaitTests
    {
        private string _saves;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-wait-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        private PlayerDriver Start()
        {
            string root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--save-root", _saves, "--slot", "slot-1", "--size", "1280x720",
                "--evidence", Path.Combine(root, "output", "host-wait-evidence"),
            });
            options.TestMode = true;
            return new PlayerDriver(options);
        }

        [Test]
        public void FramesUntil_NeverTrue_FailsAfterLimitWithWhatAndTopScreen()
        {
            using PlayerDriver player = Start();
            player.Frames(3);
            int frames = 0;
            var ex = Assert.Throws<InvalidOperationException>(() =>
                player.FramesUntil(() => false, "절대 참이 되지 않는 조건", 7, () => { frames++; player.Frames(1); }));
            Assert.That(ex.Message, Does.Contain("절대 참이 되지 않는 조건"));
            Assert.That(ex.Message, Does.Contain("7"));
            Assert.That(ex.Message, Does.Contain("맨 위 화면"));
            Assert.That(frames, Is.EqualTo(7), "한도 프레임만큼만 돌았다");
        }

        [Test]
        public void FramesUntil_TrueLater_ReturnsFrameCount()
        {
            using PlayerDriver player = Start();
            int n = 0;
            int passed = player.FramesUntil(() => ++n > 3, "세 번 뒤 참", 50);
            Assert.That(passed, Is.EqualTo(3));
        }
    }
}
