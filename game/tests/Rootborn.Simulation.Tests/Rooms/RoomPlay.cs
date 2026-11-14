using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Rootborn.Core.Room;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Net.Room;
using Rootborn.Net.Transport;
using Rootborn.Server;
using Rootborn.Simulation.Headless;

namespace Rootborn.Simulation.Rooms
{
    /// <summary>같이 하는 방 한 판의 설정. 기본값은 CI에서 빨리 끝나게, 길게 도는 것은 환경 변수로(RoomPlayTests).</summary>
    public sealed class RoomPlaySettings
    {
        public string Name = "room";
        public int Seed = 1;
        public int Players = 3;
        /// <summary>같이 쓰는 조각(Core RoomPieceCatalog 이름 — wallet, town, house). 비면 시간만.</summary>
        public string[] SharedPieces = Array.Empty<string>();
        /// <summary>자리마다의 정책(모자라면 되풀이). 비면 기본 등록 표의 순서.</summary>
        public string[] Policies;
        public int Days = 2;
        public int StepsPerDay = 40;
        /// <summary>한 판의 차례(모든 자리가 한 걸음씩) 상한.</summary>
        public int MaxRounds = 3000;
        /// <summary>한 걸음 앞에 끊김을 넣을 확률(요청·저장이 걸려 있으면 이 값, 아니면 4분의 1). 0이면 끊지 않는다.</summary>
        public double DisconnectChance;
        /// <summary>날이 바뀔 때마다 서버를 끄고 같은 세이브로 다시 켠다(모두 다시 들어온다).</summary>
        public bool RestartEachDay = true;
        /// <summary>1일째 낮에 한 사람이 나갔다가 다음 날 다시 들어온다(쉰 날).</summary>
        public bool OneSitsOutADay;
        public double LatencySeconds, JitterSeconds, LossRate;

        public string Describe() => "자리 " + Players + ", 같이 쓰는 것 [" + string.Join(",", SharedPieces) + "], 날 " + Days + ", 하루 걸음 " + StepsPerDay
            + ", 끊김 " + DisconnectChance.ToString("0.###", CultureInfo.InvariantCulture) + ", 다시 켜기 " + RestartEachDay + ", 쉰 날 " + OneSitsOutADay
            + ", 지연 " + LatencySeconds.ToString("0.###", CultureInfo.InvariantCulture) + "초, 흔들림 " + JitterSeconds.ToString("0.###", CultureInfo.InvariantCulture)
            + "초, 손실 " + LossRate.ToString("0.###", CultureInfo.InvariantCulture);
    }

    /// <summary>방 한 판에서 일어난 일 하나(기록과 재생의 단위). 걸음은 그 자리의 달리는 쪽이 한 걸음, 나머지는 방을 흔드는 일.</summary>
    public readonly struct RoomEvent
    {
        public const string Step = "step", Sever = "sever", Leave = "leave", Join = "join", Restart = "restart", Idle = "idle";

        public RoomEvent(string kind, int seat, string detail = null) { Kind = kind; Seat = seat; Detail = detail ?? string.Empty; }

        public string Kind { get; }
        public int Seat { get; }
        public string Detail { get; }

        public string Line => Kind + "\t" + Seat.ToString(CultureInfo.InvariantCulture) + "\t" + Detail;

        public static bool TryParse(string line, out RoomEvent e)
        {
            e = default;
            if (string.IsNullOrEmpty(line) || line[0] == '#') return false;
            string[] parts = line.Split('\t');
            if (parts.Length < 2 || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int seat)) return false;
            e = new RoomEvent(parts[0], seat, parts.Length > 2 ? parts[2] : null);
            return true;
        }
    }

    /// <summary>방 한 판의 결과: 자리마다의 결과, 방의 일 기록, 넓이, 실패.</summary>
    public sealed class RoomPlayResult
    {
        public RoomPlaySettings Settings;
        public readonly List<PlayResult> Players = new List<PlayResult>();
        public readonly List<RoomEvent> Events = new List<RoomEvent>();
        public readonly SortedDictionary<string, int> Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public readonly List<string> Notes = new List<string>();
        public int ServerDays, Restarts, Rounds;
        public long Frames;
        public PlayFailure Failure;
        public string OutputDir;

        public void Count(string what, int by = 1) => Counts[what] = (Counts.TryGetValue(what, out int n) ? n : 0) + by;
        public int CountOf(string what) => Counts.TryGetValue(what, out int n) ? n : 0;

        /// <summary>방의 실패 또는 첫 자리의 실패.</summary>
        public PlayFailure FirstFailure => Failure ?? Players.Select(p => p.Failure).FirstOrDefault(f => f != null);
    }

    /// <summary>
    /// 같이 하는 방 한 판(창 없는 층, 자동 테스트): 서버(방) 하나(Rootborn.Server와 같은 RoomServer + 손님 세이브 폴더 + 집·상자 규칙) + 자동 플레이어 N명이
    /// 같은 프로세스 안 전송으로 한 방에서 플레이한다. 서버 시계는 손으로 돌린다(실시간 없음 — 몸이 프레임 하나를 돌 때마다 1/60초).
    /// - 자리마다 창 없는 몸(화면 쌓기 + 월드 걷기)과 달리는 쪽(정책·불변식·날마다 다시 읽기)이 있다. 차례마다 씨앗으로 섞은 순서로 한 걸음씩.
    ///   한 사람이 걷는 동안 다른 사람의 몸은 가만히 선 프레임을 돈다(시계·방의 알림은 그대로 받는다).
    /// - 방의 불변식(<see cref="RoomChecks"/>)은 걸음마다, 서버 끄고 켜기는 날마다. 끊김은 걸음 사이에 넣는다(요청 직후, 허락 직전, 저장 도중).
    /// - 같은 씨앗은 같은 판(결정적). 일 기록(<see cref="RoomEvent"/>)과 자리마다의 행동 기록으로 그대로 다시 돌릴 수 있다(<see cref="RoomRecord"/>).
    /// 화면·입력 근거가 아니다(창 없는 몸, 장치 입력 없음).
    /// </summary>
    public sealed class RoomPlay : IDisposable
    {
        public static readonly RoomVersions Versions = new RoomVersions(RoomProtocol.Version, "sim-game", "sim-data");
        public const string RoomSlot = "room-a";

        private readonly RoomPlaySettings _settings;
        private readonly Random _random;
        private readonly IReadOnlyList<RoomEvent> _replay;
        private readonly string _root;
        private LoopbackNetwork _network;
        private RoomServer _server;
        private RoomLoop _loop;
        private readonly SimClock _clock = new SimClock();
        private int _lastServerDay = -1;
        private bool _sitOutDone;
        private int _sitter = -1;
        private string _refused;

        /// <param name="replay">다시 돌릴 일 기록(없으면 씨앗으로 정한다). 그때 정책은 자리마다의 ReplayPolicy다.</param>
        public RoomPlay(PlayAtlas atlas, RoomPlaySettings settings, IReadOnlyList<IPlayPolicy> policies = null, IReadOnlyList<RoomEvent> replay = null)
        {
            Atlas = atlas ?? throw new ArgumentNullException(nameof(atlas));
            _settings = settings ?? new RoomPlaySettings();
            _random = new Random(_settings.Seed);
            _replay = replay;
            _root = Path.Combine(Path.GetTempPath(), "rootborn-sim-room-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            Result = new RoomPlayResult { Settings = _settings };
            StartServer();
            Checks = new RoomChecks(this);

            string[] names = _settings.Policies != null && _settings.Policies.Length > 0 ? _settings.Policies : RoomPolicies.Mix(_settings.Seed, _settings.Players);
            var playSettings = new PlaySettings { Days = _settings.Days, StepsPerDay = _settings.StepsPerDay, MaxSteps = _settings.MaxRounds,
                // 같이 하는 방: 남이 걷는 동안 시계가 흘러 하루 마감 순서가 길어진다(07:00이면 날이 넘어간다).
                WindDownSteps = 200 };
            for (int i = 0; i < _settings.Players; i++)
            {
                IPlayPolicy policy = policies != null ? policies[i] : RoomPolicies.Create(names[i % names.Length], atlas);
                var seat = new RoomSeat(this, i, "p" + (i + 1).ToString(CultureInfo.InvariantCulture), policy);
                seat.Body = new HeadlessBody(atlas, Path.Combine(_root, "player-" + seat.PlayerId), seat);
                seat.Runner = new PlayRunner(seat.Body, policy, _settings.Seed * 1000 + i, playSettings, atlas, RoomRecord.PlayerName(_settings.Name, seat.PlayerId), saved: seat);
                seat.SessionStarted += Checks.Started;
                seat.Runner.AfterStep += (step, action) => Checks.AfterAction(seat, action);
                seat.UploadRefused += (s, reason) => { Result.Count("저장 거절: " + reason); _refused ??= s + " 저장 거절(" + reason + ")"; };
                Seats.Add(seat);
            }
            foreach (RoomSeat seat in Seats) seat.Connect(_network);
            for (int i = 0; i < 600 && Seats.Any(s => !s.Ready); i++) IdleFrame();
            if (Seats.Any(s => !s.Ready)) throw new InvalidOperationException("자리가 방에 들어가지 못했다: " + string.Join(", ", Seats.Where(s => !s.Ready)));
        }

        public PlayAtlas Atlas { get; }
        public RoomPlaySettings Settings => _settings;
        public RoomPlayResult Result { get; }
        public readonly List<RoomSeat> Seats = new List<RoomSeat>();
        public RoomServer Server => _server;
        /// <summary>방이 쓰는 같이 쓰는 집의 규칙(틀과 꾸밈 정의) — 불변식이 방의 기록을 비추기로 만들 때 쓴다.</summary>
        public RoomHouseRules HouseRules { get; private set; }
        public GuestSaveFolder Guests { get; private set; }
        public RoomChecks Checks { get; }
        public double Now => _clock.Now;
        public LoopbackNetwork Network => _network;

        private void StartServer()
        {
            var definitions = Atlas.Definitions;
            _network = new LoopbackNetwork(_settings.Seed) { LatencySeconds = _settings.LatencySeconds, JitterSeconds = _settings.JitterSeconds, LossRate = _settings.LossRate };
            string saves = Path.Combine(_root, "server");
            Guests = new GuestSaveFolder(GuestSaveFolder.FolderFor(saves, RoomSlot), GameSession.SaveCheck(definitions, GameStartSettings.FromDefinitions(definitions)));
            HouseRules = HouseGrids.Rules(definitions, Atlas.RepoRoot, null);
            var pieces = RoomPieceCatalog.CreateDefault(HouseRules);
            var roomSettings = new RoomServerSettings { MaxPlayers = Math.Max(_settings.Players, 1), SharedPieces = _settings.SharedPieces };
            _server = new RoomServer(_network.Listen(), roomSettings, Versions, new FileSaveStore(saves), RoomSlot, line => Result.Notes.Add("[방] " + line),
                Program.RoomTimeRules(definitions), Guests, pieces);
            _loop = new RoomLoop(_server, _clock, roomSettings.TickSeconds);
        }

        // ---------- 프레임 ----------

        private bool _inFrame;

        /// <summary>한 몸이 프레임 하나를 돌았다(그 몸이 부른다): 시계 1/60초, 다른 몸의 가만히 선 프레임, 모든 접속, 방의 틱, 프레임 불변식.</summary>
        public void AfterFrame(HeadlessBody acting)
        {
            if (_inFrame) return;
            _inFrame = true;
            try
            {
                _clock.Now += HeadlessBody.FrameSeconds;
                Result.Frames++;
                foreach (RoomSeat seat in Seats) if (seat.Body != acting) seat.Body.IdleFrame();
                foreach (RoomSeat seat in Seats) Checks.Pump(seat, _clock.Now);
                _loop.RunDue();
                Checks.EveryFrame();
            }
            finally { _inFrame = false; }
        }

        /// <summary>아무도 걷지 않는 프레임 하나(모두 가만히 선다).</summary>
        public void IdleFrame()
        {
            _inFrame = true;
            try
            {
                _clock.Now += HeadlessBody.FrameSeconds;
                Result.Frames++;
                foreach (RoomSeat seat in Seats) seat.Body?.IdleFrame();
                foreach (RoomSeat seat in Seats) Checks.Pump(seat, _clock.Now);
                _loop.RunDue();
                Checks.EveryFrame();
            }
            finally { _inFrame = false; }
        }

        // ---------- 한 판 ----------

        public RoomPlayResult Run()
        {
            try
            {
                if (_replay != null) foreach (RoomEvent e in _replay) { Do(e); if (Failed) break; }
                else Live();
            }
            catch (PlayFailure f) { Result.Failure = f; }
            foreach (RoomSeat seat in Seats) Result.Players.Add(seat.Runner.Finish());
            Result.ServerDays = _server.Clock.Day;
            // 같이 쓰는 집이면 끝낸 뒤 모두 다시 들어와 가만히 있게 하고 방에 남은 꾸밈 잡기·돈 낸 표시가 없는지 본다(기록·재생에 넣지 않는다 — 끝 상태는 위에서 이미 잡았다).
            if (_replay == null && !Failed && Checks.TracksBuild)
                try { SettleAtEnd(); }
                catch (PlayFailure f) { Result.Failure = f; }
            return Result;
        }

        /// <summary>끊겼던 자리를 다시 들여보내고(걸음 한 번 — 시작 화면의 이어 하기), 아무도 걷지 않는 채로 방이 가라앉기를 기다린 뒤 끝난 방의 꾸밈 검사를 한다.</summary>
        private void SettleAtEnd()
        {
            for (int round = 0; round < 600 && Seats.Any(s => !s.Playing); round++)
            {
                foreach (RoomSeat seat in Seats)
                {
                    if (seat.Away) { seat.Away = false; seat.Connect(_network); }
                    if (seat.Playing) continue;
                    seat.Body.ReturnToStartIfLost();
                    if (!seat.Ready || seat.Body.Session != null) continue;
                    seat.Runner.Resume();
                    seat.Runner.Step();
                }
                IdleFrame();
            }
            if (Seats.Any(s => !s.Playing)) Fail("끝난 뒤 다시 들어오지 못한 자리: " + string.Join(", ", Seats.Where(s => !s.Playing)));
            // 가만히 서서 걸린 것이 끝나기를 기다린다(1시간 저장이 한 번 지나가 돈 낸 표시를 정리하게 게임 시계로 1시간 이상).
            for (int i = 0; i < SettleFrames; i++) IdleFrame();
            for (int i = 0; i < 1200 && !(Checks.Quiet && Checks.Settled); i++) IdleFrame();
            Checks.BuildFinal();
        }

        /// <summary>끝난 뒤 가만히 있는 프레임 수(게임 시계로 약 1시간 반: 10초 = 게임 24분).</summary>
        public const int SettleFrames = 2400;

        private bool Failed => Result.Failure != null || Seats.Any(s => s.Runner.Result.Failure != null);

        private bool AllDone => Seats.All(s => s.Runner.Done && !s.Away);

        private void Live()
        {
            var order = new List<RoomSeat>(Seats);
            for (int round = 0; round < _settings.MaxRounds && !AllDone && !Failed; round++)
            {
                Result.Rounds++;
                Shuffle(order);
                bool stepped = false;
                foreach (RoomSeat seat in order)
                {
                    if (Failed) return;
                    if (seat.Away || seat.Runner.Done) continue;
                    MaybeSever(seat);
                    if (seat.Session == null && !seat.Ready) continue; // 다시 들어가는 중(방 설정·세이브를 기다린다)
                    Do(new RoomEvent(RoomEvent.Step, seat.Index));
                    stepped = true;
                    if (!Failed) MaybeSever(seat); // 방금 보낸 요청·저장이 아직 걸려 있을 때(요청 직후, 허락 직전, 저장 도중)
                    DayEvents();
                }
                if (!stepped) Do(new RoomEvent(RoomEvent.Idle, -1, "30"));
            }
            if (!AllDone && !Failed) Fail("방이 끝나지 않았다: " + _settings.MaxRounds + "차례 안에 모두 " + _settings.Days + "일을 넘기지 못했다");
        }

        private void Shuffle(List<RoomSeat> order)
        {
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
        }

        /// <summary>끊김 넣기: 요청을 보냈고 답을 기다리거나(허락 직전 — 방이 이미 잡아 둔 것이 있다), 저장이 아직 방에 닿지 않았으면 높은 확률로.</summary>
        private void MaybeSever(RoomSeat seat)
        {
            if (_settings.DisconnectChance <= 0 || !seat.Playing) return;
            string kind = Checks.SeverWindow(seat);
            // 요청이 걸린 때는 드물어 높게, 저장은 거의 걸음마다 걸려 있어 그대로, 아무 때는 낮게.
            double chance = kind == RoomChecks.SeverAnyTime ? _settings.DisconnectChance / 4
                : kind == RoomChecks.SeverMidSave ? _settings.DisconnectChance / 2 : Math.Min(1.0, _settings.DisconnectChance * 6);
            if (_random.NextDouble() < chance) Do(new RoomEvent(RoomEvent.Sever, seat.Index, kind));
        }

        /// <summary>날이 바뀌었으면(방의 시계): 서버 끄고 켜기, 쉰 사람 다시 들이기. 1일째 낮이면 한 사람 내보내기.</summary>
        private void DayEvents()
        {
            int day = _server.Clock.Day;
            if (_lastServerDay < 0) _lastServerDay = day;
            if (day != _lastServerDay)
            {
                _lastServerDay = day;
                if (_sitter >= 0 && Seats[_sitter].Away) Do(new RoomEvent(RoomEvent.Join, _sitter));
                if (_settings.RestartEachDay) Do(new RoomEvent(RoomEvent.Restart, -1));
            }
            if (_settings.OneSitsOutADay && !_sitOutDone && day == 1 && _server.Clock.Minutes >= 12 * 60 && Seats.Count > 1)
            {
                _sitOutDone = true;
                RoomSeat who = Seats.Where(s => s.Playing && !s.Runner.Done).OrderBy(s => s.Index).LastOrDefault();
                if (who != null) { _sitter = who.Index; Do(new RoomEvent(RoomEvent.Leave, who.Index)); }
            }
        }

        /// <summary>일 하나를 한다(살아 있는 판과 재생이 같은 길).</summary>
        public void Do(RoomEvent e)
        {
            Result.Events.Add(e);
            RoomSeat seat = e.Seat >= 0 && e.Seat < Seats.Count ? Seats[e.Seat] : null;
            switch (e.Kind)
            {
                case RoomEvent.Step:
                    if (seat == null) throw new InvalidOperationException("걸음의 자리가 없다: " + e.Line);
                    seat.Body.ReturnToStartIfLost();
                    Checks.BeforeStep(seat);
                    seat.Runner.Step();
                    if (seat.Runner.Result.Failure == null) Checks.AfterStep(seat);
                    if (_refused != null) Fail("방이 손님의 저장을 거절했다 — 그 진행은 방에 남지 않는다: " + _refused);
                    break;
                case RoomEvent.Sever:
                    Result.Count("끊김: " + e.Detail);
                    Checks.Severed(seat, e.Detail);
                    seat.Sever(_network);
                    seat.Body.ReturnToStartIfLost();
                    seat.Connect(_network);
                    break;
                case RoomEvent.Leave:
                    Result.Count("나감(쉰 날)");
                    Checks.Leaving(seat);
                    seat.Leave();
                    seat.Away = true;
                    seat.Body.ReturnToStartIfLost();
                    break;
                case RoomEvent.Join:
                    seat.Away = false;
                    seat.Connect(_network);
                    break;
                case RoomEvent.Restart:
                    Restart();
                    break;
                case RoomEvent.Idle:
                    int frames = int.TryParse(e.Detail, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 30;
                    for (int i = 0; i < frames; i++) IdleFrame();
                    break;
                default:
                    throw new InvalidOperationException("모르는 일: " + e.Line);
            }
        }

        /// <summary>
        /// 서버를 끄고 같은 세이브로 다시 켠다: 걸려 있는 요청·저장이 끝나기를 기다리고(상한 있음), 모두의 진행·방의 조각을 적고, 끄고, 새 전송으로 다시 켜고,
        /// 방에 있던 사람을 모두 다시 잇는다(몸은 시작 화면으로 돌아가 슬롯을 골라 다시 들어간다 — 다시 들어오면 적은 것과 비교한다).
        /// </summary>
        private void Restart()
        {
            Result.Restarts++;
            for (int i = 0; i < 600 && !Checks.Quiet; i++) IdleFrame();
            if (!Checks.Quiet) Result.Notes.Add("서버 다시 켜기: 걸린 요청·저장이 끝나지 않은 채 껐다(" + Checks.Busy() + ")");
            Checks.BeforeRestart();
            _server.Shutdown();
            foreach (RoomSeat seat in Seats) { seat.Leave(); seat.Body.ReturnToStartIfLost(); }
            StartServer();
            Checks.AfterRestart();
            foreach (RoomSeat seat in Seats) if (!seat.Away) seat.Connect(_network);
            for (int i = 0; i < 600 && Seats.Any(s => !s.Away && !s.Ready); i++) IdleFrame();
        }

        public void Fail(string what) => throw new PlayFailure(what, Situation());

        public string Situation()
        {
            var lines = new List<string>
            {
                "방: " + _settings.Name + ", 씨앗 " + _settings.Seed + ", " + _settings.Describe(),
                "방의 시계 " + _server.Clock.Day + "일 " + _server.Clock.Minutes + "분, 프레임 " + Result.Frames + ", 일 " + Result.Events.Count,
            };
            foreach (RoomSeat s in Seats)
            {
                var g = s.Session;
                lines.Add("  " + s + ": " + (s.Away ? "나가 있음" : s.Joined ? "접속" : "끊김") + (g == null ? ", 시작 화면"
                    : ", " + g.StudentDay + "일 " + g.StudentTimeMinutes + "분, 방 " + (g.Room.Known ? g.Room.Day + "일 " + g.Room.Minutes + "분" : "(모름)")
                    + (g.IsBusy ? ", 하는 중" : string.Empty) + (g.IsSleepingInRoom ? ", 자는 중" : string.Empty) + (g.WaitingForRoom ? ", 방의 답을 기다림" : string.Empty)
                    + ", 지갑 " + g.Wallet.Balance));
            }
            lines.Add("최근 일:");
            foreach (RoomEvent e in Result.Events.Skip(Math.Max(0, Result.Events.Count - 15))) lines.Add("  " + e.Line);
            return string.Join("\n", lines);
        }

        public void Dispose()
        {
            foreach (RoomSeat seat in Seats) { seat.Leave(); seat.Body?.Dispose(); }
            try { _server?.Shutdown(); } catch (ObjectDisposedException) { }
            if (Environment.GetEnvironmentVariable("ROOTBORN_SIM_ROOM_KEEP") == "1") { Result.Notes.Add("세이브 폴더를 남김: " + _root); return; } // 손으로 들여다볼 때
            try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        private sealed class SimClock : IRoomClock
        {
            public double Now { get; set; }
        }
    }
}
