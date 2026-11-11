using System;
using System.IO;
using NUnit.Framework;

namespace Rootborn.Data.Tests
{
    /// <summary>
    /// 게임 데이터 지문(방에 들어갈 때 서버와 클라이언트가 맞춰 보는 값). 서버와 Host가 같은 셈법을 쓰도록 Rootborn.Data에 있다.
    /// 고정값은 서버에 있던 셈법(상대 경로 + 0 + 줄바꿈을 LF로 맞춘 내용 + 0, 이름 순, SHA-256 앞 16자리)으로 따로 셈한 것이다 —
    /// 셈법이 바뀌면 이미 나눠 준 서버와 맞지 않으므로 이 값이 바뀌면 안 된다.
    /// </summary>
    public sealed class DataFingerprintTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "rootborn-data-fingerprint", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_dir, "items"));
            File.WriteAllText(Path.Combine(_dir, "registry.json"), "{\n  \"a\": 1\n}\n");
            File.WriteAllText(Path.Combine(_dir, "items", "food.json"), "[\n]\n");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void KnownFolder_HasTheFixedFingerprint()
        {
            Assert.That(DataFingerprint.Compute(_dir), Is.EqualTo("104b64b2b643d6ad"));
        }

        [Test]
        public void WindowsLineEndings_AndNonJsonFiles_DoNotChangeIt()
        {
            File.WriteAllText(Path.Combine(_dir, "registry.json"), "{\r\n  \"a\": 1\r\n}\r\n");
            File.WriteAllText(Path.Combine(_dir, "README.md"), "notes");
            Assert.That(DataFingerprint.Compute(_dir), Is.EqualTo("104b64b2b643d6ad"));
        }

        [Test]
        public void ChangedContent_ChangesIt()
        {
            File.WriteAllText(Path.Combine(_dir, "items", "food.json"), "[1]\n");
            Assert.That(DataFingerprint.Compute(_dir), Is.Not.EqualTo("104b64b2b643d6ad"));
        }

        [Test]
        public void MissingFolder_Throws()
        {
            Assert.Throws<DirectoryNotFoundException>(() => DataFingerprint.Compute(Path.Combine(_dir, "nope")));
        }

        [Test]
        public void RealData_IsStable()
        {
            string first = DataFingerprint.Compute(RepoPaths.DataDirectory);
            Assert.That(first, Has.Length.EqualTo(16));
            Assert.That(DataFingerprint.Compute(RepoPaths.DataDirectory), Is.EqualTo(first));
        }
    }
}
