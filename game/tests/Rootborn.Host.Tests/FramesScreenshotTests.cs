using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// --frames N --screenshot &lt;파일&gt;: 빌드된 Host를 실제 프로세스로 띄워 N프레임 뒤 화면을 저장하고 스스로 끝나는지 본다(배포 폴더에서 화면을 확인하는 길).
    /// 입력 없음. 저장된 PNG는 그려진 화면 버퍼다.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class FramesScreenshotTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "rootborn-frames-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_dir, true); } catch (IOException) { }
        }

        private static (int Code, string Error) RunHost(params string[] args)
        {
            string host = Path.Combine(AppContext.BaseDirectory, "Rootborn.Host.dll");
            Assert.That(File.Exists(host), Is.True, "빌드된 Host: " + host);
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
            start.Environment.Remove(HostOptions.SaveRootVariable); // 도움말 시험이 사용자의 세이브 폴더 설정에 기대지 않는다
            start.ArgumentList.Add(host);
            foreach (string a in args) start.ArgumentList.Add(a);
            using Process process = Process.Start(start);
            var error = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(60_000))
            {
                process.Kill(entireProcessTree: true);
                Assert.Fail("Host가 --frames 뒤에 끝나지 않았다");
            }
            // 죽인 뒤에도 자식이 쥔 파이프 때문에 읽기가 안 끝날 수 있다: 벽시계 한도를 준다.
            if (!process.WaitForExit(30_000)) Assert.Fail("Host를 죽인 뒤에도 30초 안에 끝나지 않았다");
            if (!Task.WhenAll(output, error).Wait(30_000)) Assert.Fail("Host 출력을 30초 안에 다 읽지 못했다(--frames 뒤)");
            return (process.ExitCode, error.Result);
        }

        private static (int W, int H) PngSize(string path)
        {
            byte[] head = new byte[24];
            using (FileStream f = File.OpenRead(path)) Assert.That(f.Read(head, 0, head.Length), Is.EqualTo(head.Length));
            Assert.That(head[1] == 'P' && head[2] == 'N' && head[3] == 'G', Is.True, "PNG");
            int Big(int at) => (head[at] << 24) | (head[at + 1] << 16) | (head[at + 2] << 8) | head[at + 3];
            return (Big(16), Big(20));
        }

        [Test]
        public void Frames_ThenScreenshot_SavesTheScreen_AndExits()
        {
            string shot = Path.Combine(_dir, "sub", "sample.png");
            (int code, string error) = RunHost("--sample", "--size", "960x540", "--frames", "5", "--screenshot", shot);
            Assert.That(code, Is.Zero, error);
            Assert.That(File.Exists(shot), Is.True, "화면을 저장한다(없는 폴더도 만든다)");
            Assert.That(PngSize(shot), Is.EqualTo((960, 540)), "창 크기 그대로");
        }

        [Test]
        public void Measure_WithFrames_WritesOneLineJsonWithEveryKey()
        {
            string file = Path.Combine(_dir, "deeper", "measure.json");
            (int code, string error) = RunHost("--sample", "--size", "640x360", "--frames", "20", "--measure", file, "--evidence", _dir);
            Assert.That(code, Is.Zero, error);
            Assert.That(File.Exists(file), Is.True, "끝날 때 파일을 쓴다(없는 폴더도 만든다)");
            string[] lines = File.ReadAllLines(file);
            Assert.That(lines, Has.Length.EqualTo(1), "JSON 한 줄");
            foreach (string key in new[] { "frames", "frameMsMedian", "frameMsP99", "frameMsMax", "allocatedBytesPerFrame", "peakPrivateBytes" })
                Assert.That(lines[0], Does.Contain("\"" + key + "\":"), key);
            Assert.That(lines[0], Does.Contain("\"frames\":19"), "첫 프레임은 기준이라 20프레임이면 19");
        }

        [Test]
        public void Frames_WithoutMeasure_WritesNoMeasureFile()
        {
            (int code, string error) = RunHost("--sample", "--size", "640x360", "--frames", "2", "--evidence", _dir);
            Assert.That(code, Is.Zero, error);
            Assert.That(Directory.GetFiles(_dir, "*.json", SearchOption.AllDirectories), Is.Empty);
        }

        [Test]
        public void Frames_Alone_Exits_WithoutAScreenshot()
        {
            (int code, string error) = RunHost("--sample", "--size", "640x360", "--frames", "2", "--evidence", _dir);
            Assert.That(code, Is.Zero, error);
            Assert.That(Directory.GetFiles(_dir, "*.png", SearchOption.AllDirectories), Is.Empty);
        }

        [TestCase(new[] { "--sample", "--screenshot", "x.png" }, "--frames")]
        [TestCase(new[] { "--sample", "--frames", "0" }, "--frames")]
        [TestCase(new[] { "--sample", "--frames", "many" }, "--frames")]
        public void BadArguments_AreRefused(string[] args, string mentions)
        {
            var e = Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(args));
            Assert.That(e.Message, Does.Contain(mentions));
        }

        [Test]
        public void Help_ListsTheThemeLanguageAndFrameArguments()
        {
            (int code, string error) = RunHost("--size", "640x360");
            Assert.That(code, Is.EqualTo(2));
            foreach (string arg in new[] { "--ui-theme", "--lang", "--frames", "--screenshot", "--measure" }) Assert.That(error, Does.Contain(arg));
        }
    }
}
