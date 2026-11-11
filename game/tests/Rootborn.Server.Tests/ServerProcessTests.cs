using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Core.Time;
using Rootborn.Data.Save;
using Rootborn.Net.Room;
using Rootborn.Net.Session;
using Rootborn.Net.Transport;

namespace Rootborn.Server.Tests
{
    /// <summary>
    /// 빌드한 Rootborn.Server를 별도 프로세스로 띄워 실제 UDP 소켓으로 들어간다(실제 data/, 임시 세이브 폴더).
    /// 주소는 같은 기계의 루프백(127.0.0.1)만 쓴다 — 테스트용으로 안전한 값이고 방화벽 창이 뜨지 않는다. 포트는 0으로 넘겨 빈 포트를 받는다.
    /// 자동 테스트다(두 기계·Steam 아님).
    /// </summary>
    [NonParallelizable]
    public sealed class ServerProcessTests
    {
        private string _saveRoot;

        [SetUp]
        public void SetUp()
        {
            _saveRoot = Path.Combine(Path.GetTempPath(), "rootborn-server-process", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_saveRoot)) Directory.Delete(_saveRoot, true);
        }

        [Test]
        public void RealProcess_ListensOnUdp_LetsAPlayerIn_AndSavesOnStop()
        {
            string root = FindRepoRoot();
            using var server = ServerProcess.Start("--root", root, "--save-root", _saveRoot, "--slot", "room-p", "--bind", IPAddress.Loopback.ToString(), "--port", "0", "--max-players", "2");
            try
            {
                int port = server.WaitForPort(TimeSpan.FromSeconds(60));
                var versions = Program.CurrentVersions(Path.Combine(root, "data"));

                using var player = RoomClient.Create(UdpTransport.Connect(IPAddress.Loopback, IPAddress.Loopback.ToString(), port, RoomProtocol.ConnectionKey), versions, "p1", "child|", new SessionClientSettings());
                using var stranger = RoomClient.Create(UdpTransport.Connect(IPAddress.Loopback, IPAddress.Loopback.ToString(), port, RoomProtocol.ConnectionKey),
                    new RoomVersions(versions.Protocol, versions.Game, "other-data"), "p2", "child|", new SessionClientSettings());
                var clock = Stopwatch.StartNew();

                bool Pump(Func<bool> until, double seconds = 10.0)
                {
                    double end = clock.Elapsed.TotalSeconds + seconds;
                    while (clock.Elapsed.TotalSeconds < end)
                    {
                        player.Update(clock.Elapsed.TotalSeconds);
                        stranger.Update(clock.Elapsed.TotalSeconds);
                        if (until()) return true;
                        Thread.Sleep(5);
                    }

                    return false;
                }

                Assert.That(Pump(() => player.Status == ClientStatus.Joined), Is.True, "같은 버전은 들어간다\n" + server.Output);
                Assert.That(Pump(() => stranger.Status == ClientStatus.Refused), Is.True, "데이터가 다르면 거절\n" + server.Output);
                Assert.That(stranger.FarewellReason, Is.EqualTo(RoomRefusal.DataVersionMismatch));

                Assert.That(Pump(() => player.RoomClock.Known), Is.True);
                Assert.That(player.RoomClock.Day, Is.EqualTo(1));
                player.RequestDayEndReady();
                Assert.That(Pump(() => player.RoomClock.Day == 2), Is.True, "혼자인 방: 내가 끝내면 다음 날\n" + server.Output);

                server.Stop();
                Assert.That(server.WaitForExit(TimeSpan.FromSeconds(20)), Is.True, "stop 으로 끝난다\n" + server.Output);
                Assert.That(server.ExitCode, Is.EqualTo(Program.ExitOk), server.Output);
                StringAssert.Contains("저장하고 닫음", server.Output);
                Assert.That(Pump(() => player.Status == ClientStatus.Disconnected), Is.True, "서버가 닫으면 끊긴다");
                Assert.That(player.FarewellReason, Is.EqualTo(RoomRefusal.Shutdown));
            }
            finally
            {
                server.Kill();
            }

            var store = new FileSaveStore(_saveRoot);
            Assert.That(store.TryRead("room-p", RoomClockSaveFiles.FileName, out RoomClockSaveData saved), Is.True);
            Assert.That(saved.Day, Is.EqualTo(2), "끝낼 때 저장한 날");

            using var again = ServerProcess.Start("--root", root, "--save-root", _saveRoot, "--slot", "room-p", "--bind", IPAddress.Loopback.ToString(), "--port", "0");
            try
            {
                again.WaitForPort(TimeSpan.FromSeconds(60));
                StringAssert.Contains("방을 이어서 엶", again.Output);
                again.Stop();
                Assert.That(again.WaitForExit(TimeSpan.FromSeconds(20)), Is.True, again.Output);
            }
            finally
            {
                again.Kill();
            }
        }

        [Test]
        public void RealProcess_MissingArguments_ExitsWithHelp()
        {
            using var server = ServerProcess.Start("--root", FindRepoRoot());
            Assert.That(server.WaitForExit(TimeSpan.FromSeconds(30)), Is.True);
            Assert.That(server.ExitCode, Is.EqualTo(Program.ExitBadArguments));
            StringAssert.Contains("--slot", server.Output);
            StringAssert.Contains("--port", server.Output);
            StringAssert.Contains(ServerOptions.SaveRootVariable, server.Output);
        }

        private static string FindRepoRoot()
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, "data", "registry.json"))) return d.FullName;
            throw new InvalidOperationException("저장소(data/registry.json)를 찾지 못했다");
        }

        /// <summary>테스트 출력 폴더에 복사된 Rootborn.Server.dll을 dotnet으로 띄운다. 출력은 모아 둔다.</summary>
        private sealed class ServerProcess : IDisposable
        {
            private readonly Process _process;
            private readonly ConcurrentQueue<string> _lines = new ConcurrentQueue<string>();

            private ServerProcess(Process process)
            {
                _process = process;
                _process.OutputDataReceived += (s, e) => { if (e.Data != null) _lines.Enqueue(e.Data); };
                _process.ErrorDataReceived += (s, e) => { if (e.Data != null) _lines.Enqueue(e.Data); };
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }

            public string Output => string.Join(Environment.NewLine, _lines);
            public int ExitCode => _process.ExitCode;

            public static ServerProcess Start(params string[] args)
            {
                // dotnet test가 넘겨 주는 dotnet 실행 파일 위치. 없으면 PATH의 dotnet.
                string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
                if (string.IsNullOrEmpty(dotnet) || !File.Exists(dotnet)) dotnet = "dotnet";
                var info = new ProcessStartInfo(dotnet)
                {
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8,
                    StandardInputEncoding = new System.Text.UTF8Encoding(false),
                };
                info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Rootborn.Server.dll"));
                foreach (var arg in args) info.ArgumentList.Add(arg);
                // 실행 환경의 서버 값이 섞이지 않게 비운다.
                foreach (var name in new[] { ServerOptions.SlotVariable, ServerOptions.BindVariable, ServerOptions.PortVariable, ServerOptions.MaxPlayersVariable, ServerOptions.SaveRootVariable, ServerOptions.DataVariable })
                    info.Environment.Remove(name);
                return new ServerProcess(Process.Start(info));
            }

            public int WaitForPort(TimeSpan timeout)
            {
                var watch = Stopwatch.StartNew();
                while (watch.Elapsed < timeout)
                {
                    foreach (var line in _lines)
                    {
                        int at = line.IndexOf(Program.ListeningPrefix, StringComparison.Ordinal);
                        if (at < 0) continue;
                        string endpoint = line.Substring(at + Program.ListeningPrefix.Length).Trim();
                        return int.Parse(endpoint.Substring(endpoint.LastIndexOf(':') + 1));
                    }

                    if (_process.HasExited) break;
                    Thread.Sleep(20);
                }

                throw new AssertionException("서버가 듣기 시작하지 않았다\n" + Output);
            }

            public void Stop()
            {
                _process.StandardInput.WriteLine(Program.StopCommand);
                _process.StandardInput.Flush();
            }

            public bool WaitForExit(TimeSpan timeout)
            {
                if (!_process.WaitForExit((int)timeout.TotalMilliseconds)) return false;
                _process.WaitForExit(); // 출력 읽기가 끝날 때까지
                return true;
            }

            public void Kill()
            {
                try
                {
                    if (!_process.HasExited) _process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }
            }

            public void Dispose()
            {
                Kill();
                _process.Dispose();
            }
        }
    }
}
