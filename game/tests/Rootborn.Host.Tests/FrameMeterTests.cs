using System;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using Rootborn.Host.Measure;

namespace Rootborn.Host.Tests
{
    /// <summary>--measure의 통계(창 없이)와 인자 읽기.</summary>
    [TestFixture]
    public sealed class FrameMeterTests
    {
        private const long PerSecond = 1_000_000; // 틱 1개 = 1마이크로초(시험용 고정 주파수)

        private static long Ticks(double ms) => (long)Math.Round(ms * PerSecond / 1000.0);

        /// <summary>프레임 시간(ms) 목록을 그대로 먹인다(첫 호출은 기준이라 프레임이 아니다).</summary>
        private static FrameMeter Feed(int capacity, long allocPerFrame, params double[] frameMs)
        {
            var meter = new FrameMeter(capacity, PerSecond);
            long t = 0, a = 1000;
            meter.Frame(t, a);
            foreach (double ms in frameMs) { t += Ticks(ms); a += allocPerFrame; meter.Frame(t, a); }
            return meter;
        }

        [Test]
        public void Summary_MedianP99Max_OfKnownFrames()
        {
            var ms = new double[100];
            for (int i = 0; i < 100; i++) ms[i] = i + 1; // 1..100
            FrameSummary s = Feed(1000, 0, ms).Summary();
            Assert.That(s.Frames, Is.EqualTo(100));
            Assert.That(s.MedianMs, Is.EqualTo(50.5).Within(0.001), "짝수 개의 가운데 두 값 평균");
            Assert.That(s.P99Ms, Is.EqualTo(99).Within(0.001), "가까운 순위 99%");
            Assert.That(s.MaxMs, Is.EqualTo(100).Within(0.001));
        }

        [Test]
        public void Summary_OddCount_MedianIsMiddle()
        {
            FrameSummary s = Feed(10, 0, 5, 1, 3).Summary();
            Assert.That(s.MedianMs, Is.EqualTo(3).Within(0.001));
            Assert.That(s.MaxMs, Is.EqualTo(5).Within(0.001));
        }

        [Test]
        public void Summary_NoFrames_IsAllZero()
        {
            FrameSummary s = new FrameMeter(10, PerSecond).Summary();
            Assert.That(s.Frames, Is.Zero);
            Assert.That(s.MedianMs + s.P99Ms + s.MaxMs + s.AllocatedBytesPerFrame, Is.Zero);
            var one = new FrameMeter(10, PerSecond);
            one.Frame(0, 5); // 기준만 있고 프레임은 없다
            Assert.That(one.Summary().Frames, Is.Zero);
        }

        [Test]
        public void Summary_FullRing_OverwritesOldest_ButMaxKeepsTheWholeRun()
        {
            // 용량 4: 앞의 큰 값(1000)은 덮여 지만 최댓값에는 남는다. 가운데 값·99%는 남은 4개(2,2,2,2)로 센다.
            FrameSummary s = Feed(4, 0, 1000, 2, 2, 2, 2).Summary();
            Assert.That(s.Frames, Is.EqualTo(5), "전체 프레임 수");
            Assert.That(s.MaxMs, Is.EqualTo(1000).Within(0.001));
            Assert.That(s.MedianMs, Is.EqualTo(2).Within(0.001));
            Assert.That(s.P99Ms, Is.EqualTo(2).Within(0.001));
        }

        [Test]
        public void Summary_AllocatedBytesPerFrame_IsTheAverageAfterTheBaseline()
        {
            FrameSummary s = Feed(100, 240, 16, 16, 16, 16).Summary();
            Assert.That(s.AllocatedBytesPerFrame, Is.EqualTo(240).Within(0.001));
        }

        [Test]
        public void NoteMemory_KeepsThePeak()
        {
            var meter = Feed(10, 0, 16);
            meter.NoteMemory(300); meter.NoteMemory(900); meter.NoteMemory(500);
            Assert.That(meter.Summary().PeakPrivateBytes, Is.EqualTo(900));
        }

        [Test]
        public void ToJson_IsOneLine_WithEveryKey()
        {
            var meter = Feed(10, 100, 10, 20, 30);
            meter.NoteMemory(123456789);
            string json = meter.Summary().ToJson();
            Assert.That(json, Does.Not.Contain("\n"));
            foreach (string key in new[] { "frames", "frameMsMedian", "frameMsP99", "frameMsMax", "allocatedBytesPerFrame", "peakPrivateBytes" })
                Assert.That(json, Does.Contain("\"" + key + "\":"), key);
            Assert.That(json, Does.Contain("\"frameMsMedian\":20"));
            Assert.That(json, Does.Contain("\"peakPrivateBytes\":123456789"));
            Assert.That(json, Does.Not.Contain(","+" "), "쉼표 뒤 공백 없는 한 줄 JSON");
        }

        [Test]
        public void ToJson_UsesInvariantCulture()
        {
            var old = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                string json = Feed(10, 0, 12.5).Summary().ToJson();
                Assert.That(json, Does.Contain("\"frameMsMedian\":12.5"));
            }
            finally { CultureInfo.CurrentCulture = old; }
        }

        [Test]
        public void Options_MeasureArgument_BeatsEnvironment()
        {
            string root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
            string Env(string name) => name == HostOptions.MeasureVariable ? "from-env.json" : null;
            Assert.That(HostOptions.Resolve(new[] { "--sample", "--root", root }, Env).MeasurePath, Is.EqualTo("from-env.json"));
            Assert.That(HostOptions.Resolve(new[] { "--sample", "--root", root, "--measure", "from-arg.json" }, Env).MeasurePath, Is.EqualTo("from-arg.json"));
            Assert.That(HostOptions.Resolve(new[] { "--sample", "--root", root }, _ => null).MeasurePath, Is.Null, "없으면 재지 않는다");
        }
    }
}
