using System;
using System.IO;
using NUnit.Framework;

namespace Rootborn.Server.Tests
{
    public sealed class DataFingerprintTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "rootborn-fingerprint", Guid.NewGuid().ToString("N"));
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
        public void SameContent_SameFingerprint_EvenWithWindowsLineEndings()
        {
            string first = DataFingerprint.Compute(_dir);
            File.WriteAllText(Path.Combine(_dir, "registry.json"), "{\r\n  \"a\": 1\r\n}\r\n");
            Assert.That(DataFingerprint.Compute(_dir), Is.EqualTo(first), "줄바꿈만 다른 체크아웃(Windows·Linux)은 같은 데이터다");
            Assert.That(first, Has.Length.EqualTo(16));
        }

        [Test]
        public void ChangedContentOrName_ChangesTheFingerprint()
        {
            string first = DataFingerprint.Compute(_dir);
            File.WriteAllText(Path.Combine(_dir, "items", "food.json"), "[1]\n");
            string changed = DataFingerprint.Compute(_dir);
            Assert.That(changed, Is.Not.EqualTo(first));
            File.Move(Path.Combine(_dir, "items", "food.json"), Path.Combine(_dir, "items", "drink.json"));
            Assert.That(DataFingerprint.Compute(_dir), Is.Not.EqualTo(changed));
        }

        [Test]
        public void NonJsonFiles_AreIgnored()
        {
            string first = DataFingerprint.Compute(_dir);
            File.WriteAllText(Path.Combine(_dir, "README.md"), "notes");
            Assert.That(DataFingerprint.Compute(_dir), Is.EqualTo(first));
        }
    }
}
