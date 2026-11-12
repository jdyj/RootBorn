using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NUnit.Framework;

namespace Rootborn.Server.Tests
{
    /// <summary>
    /// 테스트 출력 폴더에 복사된 Rootborn.Server.dll을 dotnet으로 띄운다(실제 프로세스, 실제 UDP). 표준 출력·오류는 모아 두고, 콘솔에 줄을 넣을 수 있다.
    /// 실행 환경의 서버 값(환경 변수)은 비운다 — 테스트가 준 인자만 쓴다.
    /// </summary>
    internal sealed class ServerProcess : IDisposable
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
        public bool HasExited => _process.HasExited;

        public static string RepoRoot()
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, "data", "registry.json"))) return d.FullName;
            throw new InvalidOperationException("저장소(data/registry.json)를 찾지 못했다");
        }

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
            foreach (var name in new[]
                     {
                         ServerOptions.SlotVariable, ServerOptions.BindVariable, ServerOptions.PortVariable, ServerOptions.MaxPlayersVariable, ServerOptions.SaveRootVariable,
                         ServerOptions.DataVariable, ServerOptions.ShareVariable, ServerOptions.DayCopiesVariable, ServerOptions.LogVariable, ServerOptions.LogMaxMbVariable,
                         ServerOptions.LogFilesVariable, ServerOptions.AutosaveVariable,
                     })
                info.Environment.Remove(name);
            return new ServerProcess(Process.Start(info));
        }

        public int WaitForPort(TimeSpan timeout)
        {
            string line = WaitForLine(Program.ListeningPrefix, timeout);
            int at = line.IndexOf(Program.ListeningPrefix, StringComparison.Ordinal);
            string endpoint = line.Substring(at + Program.ListeningPrefix.Length).Trim();
            return int.Parse(endpoint.Substring(endpoint.LastIndexOf(':') + 1));
        }

        /// <summary>그 글자가 든 줄이 나올 때까지 기다린다(이미 나온 줄도 본다). 나오지 않으면 실패.</summary>
        public string WaitForLine(string contains, TimeSpan timeout, int skip = 0)
        {
            var watch = Stopwatch.StartNew();
            while (true)
            {
                bool exited = _process.HasExited;
                if (exited) _process.WaitForExit(); // 남은 출력까지 읽는다
                int seen = 0;
                foreach (var line in _lines)
                    if (line.IndexOf(contains, StringComparison.Ordinal) >= 0 && seen++ >= skip) return line;
                if (exited || watch.Elapsed >= timeout) break;
                Thread.Sleep(20);
            }

            throw new AssertionException("서버 출력에 \"" + contains + "\" 이 나오지 않았다\n" + Output);
        }

        /// <summary>그 글자가 든 줄 수.</summary>
        public int Count(string contains)
        {
            int n = 0;
            foreach (var line in _lines)
                if (line.IndexOf(contains, StringComparison.Ordinal) >= 0) n++;
            return n;
        }

        public void Send(string line)
        {
            _process.StandardInput.WriteLine(line);
            _process.StandardInput.Flush();
        }

        public void Stop() => Send(Program.StopCommand);

        public bool WaitForExit(TimeSpan timeout)
        {
            if (!_process.WaitForExit((int)timeout.TotalMilliseconds)) return false;
            _process.WaitForExit(); // 출력 읽기가 끝날 때까지
            return true;
        }

        /// <summary>프로세스를 죽인다(저장 없이 — 정전·강제 종료와 같다).</summary>
        public void Kill()
        {
            try
            {
                if (!_process.HasExited) _process.Kill(entireProcessTree: true);
                _process.WaitForExit(10000);
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
