using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;

namespace Rootborn.Host.Tests.TwoGames
{
    /// <summary>
    /// 두 기계 확인의 다른 기계(원격 Linux 장치, 지금은 Strix Halo). ssh로 원격 도우미(<c>Scripts/remote-test/two-machines-remote.sh</c>)를 불러
    /// 데디케이티드 서버나 두 번째 게임을 띄운다. 원격 쪽 준비(배포 폴더·테스트 어셈블리 올리기)와 뒷정리는 <c>Scripts/remote-test/two-machines.sh</c>가 한다.
    /// 접속 값은 환경 변수로만 받는다(저장소에 적지 않는다): <see cref="HostVariable"/>·<see cref="UserVariable"/>·<see cref="KeyVariable"/>,
    /// 원격 실행 폴더(원격 홈 기준 상대 경로)는 <see cref="RunVariable"/>. 하나라도 없으면 <see cref="FromEnvironment"/>가 null과 이유를 준다.
    /// ssh 옵션은 Scripts/remote-test와 같다(호스트 키 검사를 끄지 않는다).
    /// </summary>
    public sealed class RemoteMachine
    {
        public const string HostVariable = "ROOTBORN_REMOTE_HOST";
        public const string UserVariable = "ROOTBORN_REMOTE_USER";
        public const string KeyVariable = "ROOTBORN_REMOTE_KEY";
        public const string RunVariable = "ROOTBORN_TWO_MACHINES_RUN";
        /// <summary>ssh 실행 파일(없으면 PATH의 ssh).</summary>
        public const string SshVariable = "ROOTBORN_REMOTE_SSH";

        private readonly string _ssh, _target, _key;
        private string _home;

        private RemoteMachine(string ssh, string target, string key, string run)
        {
            _ssh = ssh;
            _target = target;
            _key = key;
            RunDir = run;
        }

        /// <summary>원격 실행 폴더(원격 홈 기준 상대 경로).</summary>
        public string RunDir { get; }

        public static RemoteMachine FromEnvironment(out string why)
        {
            var missing = new[] { HostVariable, UserVariable, KeyVariable, RunVariable }.Where(v => string.IsNullOrEmpty(Environment.GetEnvironmentVariable(v))).ToList();
            if (missing.Count > 0)
            {
                why = "두 기계 확인의 환경 변수가 없다: " + string.Join(", ", missing) + " — Scripts/remote-test/two-machines.sh로 돌린다";
                return null;
            }
            string run = Environment.GetEnvironmentVariable(RunVariable);
            if (run.StartsWith("/", StringComparison.Ordinal) || run.Contains("..")) { why = RunVariable + "는 원격 홈 아래 상대 경로여야 한다: " + run; return null; }
            string ssh = Environment.GetEnvironmentVariable(SshVariable);
            why = null;
            return new RemoteMachine(string.IsNullOrEmpty(ssh) ? "ssh" : ssh,
                Environment.GetEnvironmentVariable(UserVariable) + "@" + Environment.GetEnvironmentVariable(HostVariable), Environment.GetEnvironmentVariable(KeyVariable), run);
        }

        /// <summary>환경 변수가 없으면 이 테스트를 건너뛴다(두 기계가 필요하다).</summary>
        public static RemoteMachine RequireOrIgnore()
        {
            RemoteMachine m = FromEnvironment(out string why);
            if (m == null) Assert.Ignore(why);
            return m;
        }

        /// <summary>원격 도우미 명령 하나를 부르는 ssh(원격 명령은 홑따옴표로 묶는다).</summary>
        public ProcessStartInfo Helper(params string[] args)
        {
            var remote = new StringBuilder("bash ").Append(Quote(RunDir + "/bin/two-machines-remote.sh"));
            foreach (string a in args) remote.Append(' ').Append(Quote(a));
            return Ssh(remote.ToString());
        }

        private ProcessStartInfo Ssh(string remoteCommand)
        {
            var info = new ProcessStartInfo(_ssh)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                StandardInputEncoding = new UTF8Encoding(false),
            };
            foreach (string o in new[] { "-i", _key, "-o", "IdentitiesOnly=yes", "-o", "BatchMode=yes", "-o", "StrictHostKeyChecking=yes", "-o", "ServerAliveInterval=15", "-o", "ConnectTimeout=20", _target, remoteCommand })
                info.ArgumentList.Add(o);
            return info;
        }

        /// <summary>짧은 원격 명령: 표준 출력(끝 줄바꿈 뺌). 실패하면 예외.</summary>
        public string Run(params string[] args) => RunWithInput(null, args);

        private string RunWithInput(byte[] input, params string[] args)
        {
            using var p = Process.Start(Helper(args));
            var err = new StringBuilder();
            p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (err) err.AppendLine(e.Data); };
            p.BeginErrorReadLine();
            if (input != null) p.StandardInput.BaseStream.Write(input, 0, input.Length);
            p.StandardInput.Close();
            string output = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(120000)) { p.Kill(true); throw new TimeoutException("원격 명령이 끝나지 않았다: " + string.Join(" ", args)); }
            if (p.ExitCode != 0) throw new InvalidOperationException("원격 명령 실패(" + p.ExitCode + "): " + string.Join(" ", args) + "\n" + err + output);
            return output.TrimEnd('\r', '\n');
        }

        /// <summary>원격 홈의 절대 경로.</summary>
        public string Home => _home ??= Run("home");

        /// <summary>원격 실행 폴더 안의 절대 경로(원격 게임 인자에 넣는다).</summary>
        public string RemotePath(string relative) => Home + "/" + RunDir + "/" + relative;

        /// <summary>원격 장치의 LAN 주소(이 PC가 ssh로 닿은 주소).</summary>
        public string LanAddress() => Run("lan-address");

        /// <summary>이 PC의 LAN 주소(원격이 본 ssh 연결의 출발 주소). 이 PC에서 방을 열 때 이 주소 하나에 묶는다.</summary>
        public string ThisPcAddress() => Run("client-address");

        /// <summary>서버 세이브 폴더의 파일 목록("상대 경로 크기" 줄들).</summary>
        public IReadOnlyList<string> ServerSaveFiles() => Run("guest-files", RunDir).Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();

        /// <summary>이 PC 폴더의 파일을 원격 실행 폴더 안으로 같은 상대 경로로 올린다(이야기가 있는 슬롯 파일처럼 작은 것만).</summary>
        public void Upload(string localDir, string remoteRelative)
        {
            foreach (string file in Directory.GetFiles(localDir, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(localDir, file).Replace('\\', '/');
                string target = RunDir + "/" + remoteRelative + "/" + rel;
                using var p = Process.Start(Ssh("mkdir -p " + Quote(target.Substring(0, target.LastIndexOf('/'))) + " && cat > " + Quote(target)));
                byte[] bytes = File.ReadAllBytes(file);
                p.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
                p.StandardInput.Close();
                p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(60000) || p.ExitCode != 0) throw new InvalidOperationException("올리지 못했다: " + rel + "\n" + p.StandardError.ReadToEnd());
            }
        }

        /// <summary>원격 가상 화면에서 두 번째 게임을 띄운다(동사는 ssh 표준 입출력으로 간다).</summary>
        public PeerGame StartPeer(string name) => PeerGame.Start(Helper("peer", RunDir), name);

        /// <summary>원격 LAN 주소 하나에 묶은 데디케이티드 서버를 띄운다. 서버 폴더는 실행 폴더 안의 이름(다른 커밋의 서버는 "server-other").</summary>
        public RemoteServer StartServer(string share, string slot, string serverFolder = "server")
            => new RemoteServer(Process.Start(Helper("server", RunDir, share ?? string.Empty, slot, serverFolder)));

        private static string Quote(string s) => "'" + s.Replace("'", "'\\''") + "'";
    }

    /// <summary>원격 데디케이티드 서버 하나(ssh 연결 하나). 연결이 닫히면 원격 도우미가 stop을 넣어 저장하고 끝낸다.</summary>
    public sealed class RemoteServer : IDisposable
    {
        public const string ExitPrefix = "@@server-exit ";
        private readonly Process _process;
        private readonly ConcurrentQueue<string> _lines = new ConcurrentQueue<string>();

        internal RemoteServer(Process process)
        {
            _process = process;
            _process.OutputDataReceived += (s, e) => { if (e.Data != null) { _lines.Enqueue(e.Data); TestContext.Progress.WriteLine("[원격 서버] " + e.Data); } };
            _process.ErrorDataReceived += (s, e) => { if (e.Data != null) { _lines.Enqueue("(err) " + e.Data); TestContext.Progress.WriteLine("[원격 서버 err] " + e.Data); } };
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        public string Output => string.Join(Environment.NewLine, _lines);

        public bool Has(string text) => _lines.Any(l => l.Contains(text));

        /// <summary>서버가 듣기 시작한 줄의 "주소:포트"(벽시계 한도는 서버가 뜨지 않았을 때 끝내는 안전장치).</summary>
        public string WaitForAddress(TimeSpan timeout)
        {
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed < timeout)
            {
                foreach (string line in _lines)
                {
                    int at = line.IndexOf(Rootborn.Server.Program.ListeningPrefix, StringComparison.Ordinal);
                    if (at >= 0) return line.Substring(at + Rootborn.Server.Program.ListeningPrefix.Length).Trim();
                }
                if (_process.HasExited) break;
                Thread.Sleep(20);
            }
            throw new AssertionException("원격 서버가 듣기 시작하지 않았다\n" + Output);
        }

        /// <summary>stop을 넣고 끝날 때까지 기다린다. 서버의 종료 코드(원격 도우미가 찍은 줄), 못 읽으면 -1.</summary>
        public int Stop(TimeSpan timeout)
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.StandardInput.WriteLine(Rootborn.Server.Program.StopCommand);
                    _process.StandardInput.Flush();
                }
            }
            catch (IOException) { }
            if (!_process.WaitForExit((int)timeout.TotalMilliseconds)) Kill();
            _process.WaitForExit();
            string exit = _lines.LastOrDefault(l => l.StartsWith(ExitPrefix, StringComparison.Ordinal));
            return exit != null && int.TryParse(exit.Substring(ExitPrefix.Length), out int code) ? code : -1;
        }

        public void Kill()
        {
            try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        }

        public void Dispose()
        {
            if (!_process.HasExited) Stop(TimeSpan.FromSeconds(30));
            _process.Dispose();
        }
    }
}
