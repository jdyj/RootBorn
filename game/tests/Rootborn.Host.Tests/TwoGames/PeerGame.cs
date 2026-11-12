using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Rootborn.Host.Tests.TwoGames
{
    /// <summary>
    /// 두 번째 게임: 이 테스트 어셈블리를 자식 프로세스로 띄워(<see cref="PeerProgram"/>) 표준 입출력으로 동사를 보낸다. 자식의 입력도
    /// 장치 이벤트 주입이다(자식 안의 PlayerDriver). 부모의 게임(<see cref="Partner"/>)과 번갈아 한 프레임씩 돈다:
    /// - 부모가 프레임을 돌리면(PlayerDriver.AfterFrame) 자식도 한 프레임("step").
    /// - 자식이 동사를 하며 프레임을 돌리면 프레임마다 묻고(<see cref="PeerProtocol.Tick"/>), 부모가 자기 게임을 한 프레임 돌린 뒤 답한다.
    /// 그래서 어느 쪽이 긴 동작(걷기, 미니게임)을 해도 다른 쪽이 멈춰 있지 않고, 두 게임의 프레임 수가 같이 간다. 벽시계를 기다리지 않는다
    /// (답을 기다리는 시간 한도는 자식이 멈췄을 때 테스트가 끝나게 하는 안전장치일 뿐 조건 판정에 쓰지 않는다).
    /// 끝나면(Dispose) 자식에게 끝내라고 하고, 끝나지 않으면 프로세스 나무째 죽인다.
    /// </summary>
    public sealed class PeerGame : IDisposable
    {
        /// <summary>자식이 한 동사에 답하지 않고 이만큼 지나면 멈춘 것으로 본다(안전장치).</summary>
        private static readonly TimeSpan Stuck = TimeSpan.FromSeconds(180);

        private readonly Process _process;
        private readonly BlockingCollection<string> _lines = new BlockingCollection<string>();
        private readonly List<string> _log = new List<string>();
        private readonly string _name;
        private bool _inCall;
        private PlayerDriver _partner;

        private PeerGame(Process process, string name)
        {
            _process = process;
            _name = name;
            _process.OutputDataReceived += (s, e) => { if (e.Data != null) _lines.Add(e.Data); else _lines.CompleteAdding(); };
            _process.ErrorDataReceived += (s, e) => { if (e.Data != null) Log("(err) " + e.Data); };
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        /// <summary>이 테스트 어셈블리를 두 번째 게임으로 띄운다(아직 게임 창은 없다 — <see cref="Launch"/>).</summary>
        public static PeerGame Start(string name)
        {
            // dotnet test가 넘겨 주는 dotnet 실행 파일 위치. 없으면 PATH의 dotnet(서버 프로세스 테스트와 같다).
            string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            if (string.IsNullOrEmpty(dotnet) || !File.Exists(dotnet)) dotnet = "dotnet";
            var info = new ProcessStartInfo(dotnet)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                StandardInputEncoding = new UTF8Encoding(false),
                WorkingDirectory = AppContext.BaseDirectory,
            };
            info.ArgumentList.Add(typeof(PeerProgram).Assembly.Location);
            info.ArgumentList.Add(PeerProtocol.PeerArgument);
            // 실행 환경의 방·세이브 값이 섞이지 않게 비운다(값은 launch 인자로만 준다).
            foreach (string variable in new[] { HostOptions.JoinVariable, HostOptions.HostVariable, HostOptions.ShareVariable, HostOptions.MaxPlayersVariable, "ROOTBORN_SAVE_ROOT", "ROOTBORN_SAVE_SLOT" })
                info.Environment.Remove(variable);
            return new PeerGame(Process.Start(info) ?? throw new InvalidOperationException("두 번째 게임 프로세스를 띄우지 못했다"), name);
        }

        /// <summary>
        /// 이 테스트 프로세스의 게임. 그 게임이 프레임을 돌리면 자식도 한 프레임 돌고, 자식이 프레임을 돌리는 동안 그 게임이 한 프레임씩 돈다.
        /// null이면 이어 두지 않는다.
        /// </summary>
        public PlayerDriver Partner
        {
            get => _partner;
            set
            {
                if (_partner != null && _partner.AfterFrame == (Action)StepWithPartner) _partner.AfterFrame = null;
                _partner = value;
                if (_partner != null) _partner.AfterFrame = StepWithPartner;
            }
        }

        public bool Exited => _process.HasExited;

        /// <summary>자식이 동사를 하는 동안 짝 게임이 한 프레임 돌 때마다 부른다(그 사이 짝 게임 화면을 보는 데 쓴다 — 읽기만).</summary>
        public Action WhileCalling;

        /// <summary>자식의 로그(게임이 찍은 줄과 오류 줄).</summary>
        public string Output { get { lock (_log) return string.Join(Environment.NewLine, _log); } }

        private void Log(string line)
        {
            lock (_log) _log.Add(line);
            TestContext.Progress.WriteLine("[" + _name + "] " + line);
        }

        private void StepWithPartner()
        {
            if (_inCall || _process.HasExited) return;
            Call("step");
        }

        /// <summary>동사 하나를 보내고 끝날 때까지 기다린다. 그 사이 자식의 프레임마다 짝 게임을 한 프레임 돌린다. 자식이 실패하면 <see cref="PeerException"/>.</summary>
        public string Call(params string[] parts)
        {
            if (_inCall) throw new InvalidOperationException("동사 안에서 다른 동사를 보낼 수 없다");
            if (_process.HasExited) throw new PeerException(_name + "이(가) 이미 끝났다(코드 " + _process.ExitCode + ")\n" + Output);
            _inCall = true;
            try
            {
                _process.StandardInput.WriteLine(PeerProtocol.Encode(parts));
                _process.StandardInput.Flush();
                while (true)
                {
                    if (!_lines.TryTake(out string line, Stuck))
                    {
                        if (_lines.IsCompleted) throw new PeerException(_name + "이(가) 끝났다: " + string.Join(" ", parts) + "\n" + Output);
                        throw new PeerException(_name + "이(가) " + Stuck.TotalSeconds + "초 동안 답하지 않았다: " + string.Join(" ", parts) + "\n" + Output);
                    }
                    if (line == PeerProtocol.Tick)
                    {
                        _partner?.Frames(1); // AfterFrame은 _inCall이라 자식을 다시 부르지 않는다
                        WhileCalling?.Invoke();
                        _process.StandardInput.WriteLine(PeerProtocol.Go);
                        _process.StandardInput.Flush();
                        continue;
                    }
                    if (line.StartsWith(PeerProtocol.Ok + "\t", StringComparison.Ordinal) || line == PeerProtocol.Ok)
                        return line.Length > PeerProtocol.Ok.Length ? PeerProtocol.Decode(line.Substring(PeerProtocol.Ok.Length + 1))[0] : string.Empty;
                    if (line.StartsWith(PeerProtocol.Error, StringComparison.Ordinal))
                        throw new PeerException(_name + ": " + string.Join(" ", parts) + " 실패 — " + PeerProtocol.Decode(line.Substring(Math.Min(line.Length, PeerProtocol.Error.Length + 1)))[0]);
                    Log(line);
                }
            }
            finally
            {
                _inCall = false;
            }
        }

        // ---------- 자주 쓰는 동사 ----------

        /// <summary>자식 안에 게임 창을 띄운다(HostOptions 인자 그대로).</summary>
        public void Launch(params string[] hostArgs) => Call(new[] { "launch" }.Concat(hostArgs).ToArray());

        public string Query(string name, params string[] args) => Call(new[] { "query", name }.Concat(args).ToArray());
        public bool QueryBool(string name, params string[] args) => Query(name, args) == "True";
        public int QueryInt(string name, params string[] args) => int.Parse(Query(name, args), CultureInfo.InvariantCulture);

        public void Frames(int count) => Call("frames", count.ToString(CultureInfo.InvariantCulture));
        public void PressKey(TestKey key, int times = 1) => Call("key", key.ToString(), times.ToString(CultureInfo.InvariantCulture));
        public void PressButton(string id) => Call("press-button", id);
        public void TypeText(string text) => Call("type", text);
        public void WaitForScreen(string id, int maxFrames = 120) => Call("wait-screen", id, maxFrames.ToString(CultureInfo.InvariantCulture));
        public void WaitForScreenClosed(string id, int maxFrames = 120) => Call("wait-screen-closed", id, maxFrames.ToString(CultureInfo.InvariantCulture));
        public string WalkThroughDoor(string target) => Call("walk-door", target);
        public void GoTo(string map) => Call("go-to", map);
        public string Screenshot(string name) => Call("screenshot", name);
        public string ScreenshotScreen(string screenId, string name) => Call("screenshot-screen", screenId, name);

        /// <summary>
        /// 조건이 될 때까지 두 게임을 한 프레임씩 같이 돌린다(이 프로세스의 게임이 한 프레임 → 자식도 한 프레임). 프레임 수로만 센다.
        /// 짝 게임이 없으면 자식만 돌린다.
        /// </summary>
        public bool Until(Func<bool> condition, int maxFrames = 600)
        {
            for (int i = 0; i <= maxFrames; i++)
            {
                if (condition()) return true;
                if (_partner != null) _partner.Frames(1);
                else Call("frames", "1");
            }
            return false;
        }

        /// <summary>자식의 조회 값이 될 때까지 두 게임을 같이 돌린다.</summary>
        public bool UntilQuery(string expected, int maxFrames, string name, params string[] args) => Until(() => Query(name, args) == expected, maxFrames);

        /// <summary>자식에게 끝내라고 한다(게임을 닫고 프로세스가 끝난다). 끝나지 않으면 죽인다.</summary>
        public void Quit()
        {
            if (_process.HasExited) return;
            try
            {
                Call("close");
                Call("quit");
            }
            catch (PeerException) { }
            catch (IOException) { }
            if (!_process.WaitForExit(20000)) Kill();
        }

        /// <summary>게임을 끄지 않고 프로세스를 죽인다(갑자기 꺼짐).</summary>
        public void Kill()
        {
            try
            {
                if (!_process.HasExited) _process.Kill(entireProcessTree: true);
                _process.WaitForExit(10000);
            }
            catch (InvalidOperationException) { }
        }

        public void Dispose()
        {
            Partner = null;
            try { Quit(); }
            finally
            {
                Kill();
                _process.Dispose();
            }
        }
    }
}
