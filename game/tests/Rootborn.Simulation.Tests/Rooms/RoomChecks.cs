using System;
using System.Collections.Generic;
using System.Linq;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Room;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;
using Rootborn.Game;
using Rootborn.Net.Session;

namespace Rootborn.Simulation.Rooms
{
    /// <summary>
    /// 같이 하는 방의 불변식(혼자 하기의 불변식은 자리마다의 PlayRunner가 그대로 본다):
    /// - 시계: 방의 시계가 거꾸로 가지 않는다, 날이 바뀐 때는 07:00, 모두가 보는 (날, 분)이 방의 것과 허용 오차 안, 개인 시계도 거꾸로 가지 않는다.
    ///   "하는 중"인 사람은 끝나는 시각 전에 다른 활동을 시작하지 못한다(지친 사람은 PlayRunner가 본다).
    /// - 돈: 어느 지갑도 음수가 아니다. 같이 쓰면 잡은 것이 없을 때 모두의 잔액이 방의 잔액과 같다. 각자면 남의 걸음 동안 내 지갑이 바뀌지 않는다.
    /// - 물건: 같이 쓰는 상자의 답으로 가방이 바뀐 만큼(그 접속의 받기 안에서 가방이 바뀐 것)이 정확히 그 답의 수다. 잡은 것이 없을 때
    ///   방의 상자 총량 = 답으로 넣은 것 − 꺼낸 것(복사·소실 없음). 모두가 보는 상자 내용물이 방의 것과 같다.
    /// - 집: 잡은 것이 없을 때 모두가 보는 가구 배치가 방의 것과 같고, 같은 칸에 가구 둘이 없다.
    /// - 저장: 서버를 끄고 켜면 방의 시계·조각이 같고, 다시 들어온 사람의 진행이 적어 둔 것과 같다(끊김·쉰 날 뒤에도).
    /// - 걸린 요청·저장이 오래 끝나지 않으면 막힘.
    /// </summary>
    public sealed partial class RoomChecks
    {
        public const string SeverAfterRequest = "요청 직후", SeverBeforeAnswer = "허락 직전", SeverMidSave = "저장 도중", SeverAnyTime = "아무 때";

        /// <summary>모두가 보는 방의 시각과 방의 시계의 차이 허용(분): 받은 시각 사이를 메우다 맞출 때의 폭 + 지연.</summary>
        public const int ClockToleranceMinutes = RemoteRoomTime.ResyncMinutes + 2;

        /// <summary>걸린 요청·저장(잡기, 보내지 못한 저장)이 이만큼 프레임 이어지면 막힘(게임 2시간).</summary>
        public const int StuckFrames = 60 * 60 * 5;

        private readonly RoomPlay _play;
        private readonly Dictionary<string, long> _chestLedger = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<RoomSeat, SeatState> _seats = new Dictionary<RoomSeat, SeatState>();
        private long _lastServerAbs = -1;
        private int _lastServerDay = -1;
        private long _busyFrames;
        private List<string> _serverBefore;
        private (int Day, int Minutes) _clockBefore;

        private sealed class SeatState
        {
            public GameSession StepSession;
            public bool Busy;
            public int EndsAt, Day;
            public int OwnBalance = -1, OwnPaid;
            public GameSession BalanceSession;
            public long LastPersonal = -1;
            public GameSession PersonalSession;
            // 다시 들어온 뒤 비교할 것
            public List<string> Expected;
            public Dictionary<string, int> ExpectedBag;
            public string ExpectedWhy;
            public GameSession ExpectedFrom;
            public StudentDayState ExpectedState;
            public bool Pending, PendingHouse;
            public readonly Dictionary<string, int> ChestBagSince = new Dictionary<string, int>(StringComparer.Ordinal);
            // 받기 한 번 안의 상자 답
            public readonly Dictionary<string, int> Window = new Dictionary<string, int>(StringComparer.Ordinal);
            public bool Watching;
        }

        public RoomChecks(RoomPlay play)
        {
            _play = play;
            // 방의 시계가 알려지는 데 걸리는 프레임(지연 + 흔들림 + 틱 간격 + 받기 순서) 동안의 방의 시각을 기억한다 — 받는 쪽은 그 사이의 어느 값이든 볼 수 있다.
            double lag = play.Settings.LatencySeconds + play.Settings.JitterSeconds + 0.1;
            _recent = new long[(int)Math.Ceiling(lag * 60) + 4];
        }

        private readonly long[] _recent;
        private int _recentAt, _recentCount;

        /// <summary>최근 몇 프레임의 방의 시각 중 하나와 허용 오차 안이다.</summary>
        private bool NearRecentServer(long seen)
        {
            for (int i = 0; i < _recentCount; i++) if (Math.Abs(seen - _recent[i]) <= ClockToleranceMinutes) return true;
            return false;
        }

        private SeatState Of(RoomSeat seat)
        {
            if (!_seats.TryGetValue(seat, out var st)) _seats[seat] = st = new SeatState();
            return st;
        }

        private RoomPlayResult Result => _play.Result;

        private void Fail(string what) => _play.Fail(what);

        // ---------- 판이 시작될 때 ----------

        /// <summary>게임 한 판이 시작됐다: 그 판의 공유 조각 답을 듣는다(그 판이 먼저 듣고 가방을 바꾼 뒤에 이쪽이 듣는다).</summary>
        public void Started(RoomSeat seat, GameSession session)
        {
            Result.Count("들어감");
            Note(seat.PlayerId + " 들어감 " + Describe(session));
            var shares = session.Shares;
            if (shares == null) return;
            // 판이 붙기 전에 온 상자 답(상자 클라이언트가 갖고 있다가 판이 처음 붙을 때 넘긴다)은 시작 안에서 가방을 바꾼다 — 그 몫을 장부에 넣는다.
            Dictionary<string, int> now = Bag(session), before = seat.BagAtLoad ?? new Dictionary<string, int>();
            bool chestUses = shares.Outbox.Requests.Any(r => r.Piece == RoomChests.PieceName && r.Kind == RoomChests.Use);
            foreach (string item in now.Keys.Union(before.Keys).ToList())
            {
                int delta = (now.TryGetValue(item, out int a) ? a : 0) - (before.TryGetValue(item, out int b) ? b : 0);
                if (delta == 0) continue;
                if (!chestUses) Fail("들어오는 동안 가방이 바뀌었는데 상자 답이 아니다: " + seat + " " + item + " " + delta);
                Result.Count(delta < 0 ? "상자에 넣기(들어오며)" : "상자에서 꺼내기(들어오며)");
                Add(_chestLedger, item, -delta);
                Add(Of(seat).ChestBagSince, item, delta);
                Note(seat.PlayerId + " 들어오며 상자 답 반영 " + item + " " + delta);
            }
            var chests = shares.Find<RoomChestsClient>();
            if (chests != null) chests.Answered += (request, applied) => ChestAnswered(seat, session, request, applied);
            BuildStarted(seat, session);
            ScanWallet(seat, session);
            var house = shares.Find<RoomHouseClient>();
            if (house != null) house.Answered += (request, applied) => { Result.Count("집 요청"); if (!applied) Result.Count("먼저 온 것만 됨: 집"); };
            var wallet = shares.Find<RoomWalletClient>();
            if (wallet != null)
            {
                wallet.Granted += _ => Result.Count("지갑 잡기 허락");
                wallet.Refused += _ => Result.Count("먼저 온 것만 됨: 지갑");
            }
        }

        private static readonly bool Trace = Environment.GetEnvironmentVariable("ROOTBORN_SIM_ROOM_TRACE") == "1";

        private void Note(string line)
        {
            if (Trace) Result.Notes.Add("[추적 " + Result.Frames.ToString("D7") + "] " + line);
        }

        private string Describe(GameSession s) => s == null ? "(판 없음)" : "가방[" + string.Join(",", Bag(s).Select(p => p.Key + "x" + p.Value)) + "] 보낼 요청[" + string.Join(",", s.Shares.Outbox.Requests.Select(r => r.Piece + "/" + r.Kind + "/" + r.RequestId + "/" + string.Join("+", r.Ids ?? Array.Empty<string>())))
            + "] 기다림 " + s.WaitingForRoom + " 상자 기다림 " + s.WaitingForStorage;

        private void ChestAnswered(RoomSeat seat, GameSession session, RoomRequestSaveData request, bool applied)
        {
            Note(seat.PlayerId + " 상자 답 " + request.Kind + " " + request.RequestId + " " + string.Join("+", request.Ids ?? Array.Empty<string>()) + " x" + request.Amount
                + " 허락 " + applied + " 결과 " + session.LastStorageResult + " → " + Describe(session) + (session == seat.Session ? "" : " (지난 판)"));
            if (request.Kind != RoomChests.Put && request.Kind != RoomChests.Take) return;
            Result.Count("상자 답");
            if (!applied) { Result.Count("먼저 온 것만 됨: 상자"); return; }
            if (session.LastStorageResult != Rootborn.Core.Placement.StorageTransferResult.Moved) { Result.Count("상자: 가방이 못 받아 풀기"); return; }
            string item = request.Ids != null && request.Ids.Length > 1 ? request.Ids[1] : null;
            if (item == null) return;
            bool put = request.Kind == RoomChests.Put;
            Result.Count(put ? "상자에 넣기" : "상자에서 꺼내기");
            Add(_chestLedger, item, put ? request.Amount : -request.Amount);
            var st = Of(seat);
            int bag = put ? -request.Amount : request.Amount;
            if (st.Watching) Add(st.Window, item, bag);
            Add(st.ChestBagSince, item, bag);
        }

        // ---------- 프레임 ----------

        /// <summary>그 자리의 접속 일 한 번. 같이 쓰는 상자의 답을 기다리면 받기 앞뒤의 가방을 비교한다.</summary>
        public void Pump(RoomSeat seat, double now)
        {
            GameSession s = seat.Session;
            ScanWallet(seat, s);
            if (s != null && seat.Connected && s.SharesHouseBuild) PumpBuild(seat, now, s);
            else PumpChests(seat, now);
            ScanWallet(seat, seat.Session);
        }

        private void PumpChests(RoomSeat seat, double now)
        {
            GameSession s = seat.Session;
            bool watch = s != null && seat.Connected && s.WaitingForStorage;
            if (!watch) { seat.Pump(now); return; }
            var st = Of(seat);
            bool others = OthersWaiting(s);
            Dictionary<string, int> before = Bag(s);
            st.Window.Clear();
            st.Watching = true;
            try { seat.Pump(now); }
            finally { st.Watching = false; }
            if (others || seat.Session != s) return; // 지갑·집의 답도 같은 받기에 올 수 있다(가게 물건 등) — 그때는 보지 않는다
            Dictionary<string, int> after = Bag(s);
            foreach (string item in before.Keys.Union(after.Keys).Union(st.Window.Keys))
            {
                int changed = (after.TryGetValue(item, out int a) ? a : 0) - (before.TryGetValue(item, out int b) ? b : 0);
                int expected = st.Window.TryGetValue(item, out int w) ? w : 0;
                if (changed != expected)
                    Fail("상자 답에서 가방이 다르게 바뀌었다: " + seat + " " + item + " " + changed + "(답의 수 " + expected + ")");
            }
        }

        private static bool OthersWaiting(GameSession s)
        {
            foreach (var r in s.Shares.Outbox.Requests) if (r.Piece != RoomChests.PieceName) return true;
            return false;
        }

        /// <summary>프레임마다(가볍게): 방의 시계가 거꾸로 가지 않고, 날이 바뀐 때는 07:00. 걸린 것이 오래 풀리지 않으면 막힘.</summary>
        public void EveryFrame()
        {
            var clock = _play.Server.Clock;
            long abs = (long)clock.Day * TimeRules.MinutesPerDay + clock.Minutes;
            if (_lastServerAbs >= 0 && abs < _lastServerAbs) Fail("방의 시계가 거꾸로 갔다: " + _lastServerAbs + " → " + abs + "(날*1440+분)");
            if (_lastServerDay >= 0 && clock.Day > _lastServerDay)
            {
                int start = _play.Atlas.Definitions.TimeRules.DayStartMinutes;
                if (clock.Minutes < start || clock.Minutes > start + 2) Fail("방의 날이 바뀐 시각: " + clock.Minutes + "분(하루 시작 " + start + "분)");
                Result.Count("방의 날 넘김");
            }
            _lastServerAbs = abs;
            _lastServerDay = clock.Day;
            BuildServerStep();
            _recent[_recentAt] = abs;
            _recentAt = (_recentAt + 1) % _recent.Length;
            if (_recentCount < _recent.Length) _recentCount++;
            // 방의 조각이 바뀐 알림은 지연만큼 늦게 닿는다: 조각이 그대로이고 걸린 것이 없는 채로 그만큼 지나야 모두가 같은 것을 본다.
            bool quiet = Quiet;
            var signature = Signature();
            _stableFrames = quiet && signature == _lastSignature ? _stableFrames + 1 : 0;
            _lastSignature = signature;
            if (quiet || _play.Seats.Any(x => x.Away || !x.Joined)) _busyFrames = 0;
            else if (++_busyFrames > StuckFrames) Fail("막힘: 방의 잡기·보낼 저장이 " + StuckFrames + "프레임 동안 끝나지 않았다(" + Busy() + ")");
        }

        // ---------- 걸음 ----------

        public void BeforeStep(RoomSeat seat)
        {
            var st = Of(seat);
            GameSession s = seat.Session;
            st.StepSession = s;
            if (s == null) return;
            st.Busy = s.IsBusy;
            st.EndsAt = s.ActivityEndsAtMinutes;
            st.Day = s.StudentDay;
            // 같이 쓰는 집의 꾸밈은 방이 칸을 잡아 준 뒤(남의 걸음 동안 답이 온다) 내 돈을 낸다 — 그동안 돈 낸 잡기의 비용만큼은 줄 수 있다(장부가 받기마다 그 값을 본다).
            int expected = st.OwnBalance - (PaidOf(seat) - st.OwnPaid);
            if (!s.SharesWallet && st.BalanceSession == s && st.OwnBalance >= 0 && s.Wallet.Balance != expected)
                Fail("각자 쓰는 지갑이 남의 걸음 동안 바뀌었다: " + seat + " " + st.OwnBalance + " → " + s.Wallet.Balance + (expected != st.OwnBalance ? "(돈 낸 꾸밈 잡기의 비용을 빼면 " + expected + "이어야 한다)" : string.Empty));
        }

        public void AfterStep(RoomSeat seat)
        {
            var st = Of(seat);
            GameSession s = seat.Session;
            if (s == null) return;
            Result.Count("걸음");
            ScanWallet(seat, s);
            if (st.Expected != null && s != st.ExpectedFrom && seat.Playing && !s.WaitingForRoom) CompareAfterRejoin(seat, st, s);
            if (st.StepSession == s && st.Busy && s.StudentDay == st.Day && s.ActivityEndsAtMinutes > st.EndsAt && st.EndsAt > s.StudentTimeMinutes)
                Fail("하는 중인 사람이 다른 활동을 시작했다: " + seat + " 끝나는 시각 " + st.EndsAt + " → " + s.ActivityEndsAtMinutes + "(지금 " + s.StudentTimeMinutes + "분)");
            if (st.StepSession == s && st.Busy) Result.Count("하는 중에 걸음");
            // 실시간 화면(미니게임)이 떠 있으면 남이 걷는 동안에도 끝나고 정산된다(사람도 그동안 계속 플레이한다) — 그때는 다음 걸음에 보지 않는다.
            bool realTime = seat.Body.Screens.Find(Rootborn.Screens.ScreenCatalog.Minigame) != null;
            st.OwnBalance = realTime ? -1 : s.Wallet.Balance;
            st.OwnPaid = PaidOf(seat);
            st.BalanceSession = s;
            RoomWide();
        }

        private void RoomWide()
        {
            var clock = _play.Server.Clock;
            long server = (long)clock.Day * TimeRules.MinutesPerDay + clock.Minutes;
            bool quiet = Quiet && Settled;
            int wallet = Wallet()?.Balance ?? -1;
            foreach (RoomSeat seat in _play.Seats)
            {
                if (!seat.Playing) continue;
                GameSession s = seat.Session;
                var st = Of(seat);
                long seen = (long)s.Room.Day * TimeRules.MinutesPerDay + s.Room.Minutes;
                if (!NearRecentServer(seen))
                    Fail("모두가 보는 시각이 다르다: " + seat + "가 보는 방 " + s.Room.Day + "일 " + s.Room.Minutes + "분, 방의 시계 " + clock.Day + "일 " + clock.Minutes + "분");
                if (s.Student.DayState == StudentDayState.InProgress)
                {
                    long personal = (long)s.StudentDay * TimeRules.MinutesPerDay + s.StudentTimeMinutes;
                    if (st.PersonalSession == s && personal < st.LastPersonal) Fail("개인 시계가 거꾸로 갔다: " + seat + " " + st.LastPersonal + " → " + personal);
                    if (!NearRecentServer(personal))
                        Fail("개인 시계가 방의 시계와 다르다: " + seat + " " + s.StudentDay + "일 " + s.StudentTimeMinutes + "분, 방 " + clock.Day + "일 " + clock.Minutes + "분");
                    st.LastPersonal = personal;
                    st.PersonalSession = s;
                }
                if (s.Wallet.Balance < 0) Fail("지갑이 음수다: " + seat + " " + s.Wallet.Balance);
                if (quiet && s.SharesWallet && s.Wallet.Balance != wallet)
                    Fail("같이 쓰는 지갑의 잔액이 다르다: " + seat + " " + s.Wallet.Balance + ", 방 " + wallet);
            }
            if (!quiet) return;
            CheckHouse();
            CheckChests();
            CheckBuild();
            CheckWalletBook();
        }

        private void CheckHouse()
        {
            if (!_play.Server.Pieces.TryGet(RoomHouse.PieceName, out var piece)) return;
            var house = (RoomHouse)piece;
            var server = new SortedSet<string>(house.Placed.Select(p => p.InstanceId + "|" + p.FurnitureId + "|" + p.SurfaceId + "@" + p.AnchorX + "," + p.AnchorY + " " + p.Direction), StringComparer.Ordinal);
            foreach (RoomSeat seat in _play.Seats)
            {
                if (!seat.Playing) continue;
                var client = seat.Session.Shares?.Find<RoomHouseClient>();
                if (client == null || !client.Known) continue;
                var seen = new SortedSet<string>(client.Placements.Select(p => p.InstanceId + "|" + p.FurnitureId + "|" + p.SurfaceId + "@" + p.Anchor.X + "," + p.Anchor.Y + " " + p.Direction), StringComparer.Ordinal);
                if (!seen.SetEquals(server))
                    Fail("모두가 보는 가구 배치가 다르다: " + seat + "\n  방: " + string.Join("; ", server) + "\n  본 것: " + string.Join("; ", seen));
                HouseFurnitureLayout layout = seat.Session.House;
                if (layout == null) continue;
                var cells = new Dictionary<GridPoint, string>();
                foreach (PlacedFurniture p in layout.Placed)
                    foreach (GridPoint c in p.OccupiedCells)
                    {
                        if (cells.TryGetValue(c, out string other) && other != p.InstanceId)
                            Fail("같은 칸에 가구 둘: " + seat + " 칸 " + c.X + "," + c.Y + " — " + other + ", " + p.InstanceId);
                        cells[c] = p.InstanceId;
                    }
                Result.Count("집 배치 비교");
            }
        }

        private void CheckChests()
        {
            if (!_play.Server.Pieces.TryGet(RoomChests.PieceName, out var piece)) return;
            var chests = (RoomChests)piece;
            var totals = new Dictionary<string, long>(StringComparer.Ordinal);
            var byChest = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string line in chests.State().Ids)
            {
                if (!RoomChestLine.TryDecode(line, out string chest, out string item, out int count)) continue;
                Add(totals, item, count);
                if (!byChest.TryGetValue(chest, out var l)) byChest[chest] = l = new List<string>();
                l.Add(item + "x" + count);
            }
            foreach (string item in totals.Keys.Union(_chestLedger.Keys))
            {
                long have = totals.TryGetValue(item, out long h) ? h : 0, ledger = _chestLedger.TryGetValue(item, out long g) ? g : 0;
                if (have != ledger) Fail("상자의 물건이 복사되거나 사라졌다: " + item + " 방의 상자 " + have + "개, 넣은 것 − 꺼낸 것 " + ledger + "개");
            }
            foreach (RoomSeat seat in _play.Seats)
            {
                if (!seat.Playing) continue;
                var client = seat.Session.Shares?.Find<RoomChestsClient>();
                if (client == null || !client.Known) continue;
                foreach (var pair in byChest)
                {
                    string seen = string.Join(",", client.ContentsOf(pair.Key).Select(x => x.Item + "x" + x.Count));
                    if (seen != string.Join(",", pair.Value)) Fail("모두가 보는 상자 내용물이 다르다: " + seat + " 상자 " + pair.Key + " — 방 " + string.Join(",", pair.Value) + ", 본 것 " + seen);
                }
            }
            Result.Count("상자 총량 비교");
        }

        // ---------- 걸린 것 ----------

        private long _stableFrames;
        private (int, int, int, int, int) _lastSignature;

        private (int, int, int, int, int) Signature()
        {
            var pieces = _play.Server.Pieces;
            int Rev(string name) => pieces.TryGet(name, out var p) ? p.Revision : -1;
            return (Wallet()?.Balance ?? -1, Rev(RoomWallet.PieceName), Rev(RoomHouse.PieceName), Rev(RoomChests.PieceName), Rev(RoomHouseBuild.PieceName));
        }

        /// <summary>걸린 것이 없고 방의 조각이 알림이 닿을 만큼 오래 그대로다(모두가 같은 것을 봐야 한다).</summary>
        public bool Settled => _stableFrames >= _recent.Length;

        private RoomWallet Wallet() => _play.Server.Pieces.TryGet(RoomWallet.PieceName, out var p) ? (RoomWallet)p : null;

        /// <summary>방이 잡아 둔 것이 없고, 모두의 보낼 요청·저장이 비었다.</summary>
        public bool Quiet
        {
            get
            {
                if (Wallet() is RoomWallet w && w.HeldTotal > 0) return false;
                if (_play.Server.Pieces.TryGet(RoomChests.PieceName, out var c) && ((RoomChests)c).HeldCount > 0) return false;
                if (BuildHeld > 0) return false;
                foreach (RoomSeat seat in _play.Seats)
                {
                    if (seat.Uploads.Count > 0) return false;
                    GameSession s = seat.Session;
                    if (s != null && seat.Connected && (s.WaitingForRoom || s.Shares.Outbox.Requests.Count > 0)) return false;
                }
                return true;
            }
        }

        public string Busy()
        {
            var parts = new List<string>();
            if (Wallet() is RoomWallet w && w.HeldTotal > 0) parts.Add("지갑 잡기 " + w.HeldTotal);
            if (_play.Server.Pieces.TryGet(RoomChests.PieceName, out var c) && ((RoomChests)c).HeldCount > 0) parts.Add("상자 잡기 " + ((RoomChests)c).HeldCount);
            if (BuildHeld > 0) parts.Add("꾸밈 잡기 " + BuildHeld + " [" + string.Join("; ", _build.Values.Where(h => !h.Gone)) + "]");
            foreach (RoomSeat seat in _play.Seats)
            {
                if (seat.Uploads.Count > 0) parts.Add(seat.PlayerId + " 보낼 저장 " + seat.Uploads.Count);
                GameSession s = seat.Session;
                if (s != null && seat.Connected && (s.WaitingForRoom || s.Shares.Outbox.Requests.Count > 0))
                    parts.Add(seat.PlayerId + " 요청 " + string.Join(",", s.Shares.Outbox.Requests.Select(r => r.Piece + "/" + r.Kind)) + (s.WaitingForRoom ? " (답을 기다림)" : string.Empty));
            }
            return parts.Count == 0 ? "없음" : string.Join("; ", parts);
        }

        /// <summary>끊김을 넣을 때 그 자리의 상태: 요청을 보냈고 답을 기다린다(방이 이미 잡아 둔 것이 있으면 허락 직전), 저장이 아직 방에 닿지 않았다.</summary>
        public string SeverWindow(RoomSeat seat)
        {
            GameSession s = seat.Session;
            if (s == null) return SeverAnyTime;
            if (s.WaitingForRoom || s.Shares.Outbox.Requests.Count > 0)
            {
                bool held = Wallet() is RoomWallet w && w.HeldTotal > 0
                    || _play.Server.Pieces.TryGet(RoomChests.PieceName, out var c) && ((RoomChests)c).HeldCount > 0
                    || OwnBuildHold(seat);
                return held ? SeverBeforeAnswer : SeverAfterRequest;
            }
            return seat.Uploads.Count > 0 ? SeverMidSave : SeverAnyTime;
        }

        // ---------- 다시 들어오기 ----------

        /// <summary>시각에 따라 달라지는 줄(다시 들어오는 동안 시간이 흐른다 — 날은 따로 본다).</summary>
        private static bool Volatile(string line) => line.StartsWith("day=", StringComparison.Ordinal) || line.StartsWith("energy=", StringComparison.Ordinal)
            || line.StartsWith("time-points=", StringComparison.Ordinal) || line.StartsWith("today=", StringComparison.Ordinal)
            || line.StartsWith("item ", StringComparison.Ordinal) || StateSnapshot.IsFurniture(line);

        private static bool Progress(string line) => line.StartsWith("quest ", StringComparison.Ordinal) || line.StartsWith("chain ", StringComparison.Ordinal);

        private void Remember(RoomSeat seat, string why)
        {
            GameSession s = seat.Session;
            if (s == null) return;
            var st = Of(seat);
            // 방에 보낸 요청이 걸려 있으면(가구 놓기, 지갑 잡기 등) 다시 들어온 뒤 그 답으로 진행이 나아갈 수 있다(가구를 놓아 목표를 채움) —
            // 그때는 퀘스트·체인 줄을 같음으로 보지 않는다(받은 보상이 사라지지 않는지는 자리의 PlayRunner가 계속 본다).
            st.Pending = s.Shares != null && s.Shares.Outbox.Requests.Count > 0;
            // 가구 놓기·꾸밈 요청이 걸려 있으면 다시 들어온 뒤 그 답으로 인테리어 기술이 자란다(적용되면 하루 한도 안에서 자란다) — 그때는 기술 줄을 같음으로 보지 않는다.
            st.PendingHouse = s.Shares != null && s.Shares.Outbox.Requests.Any(r => r.Piece == RoomHouse.PieceName || r.Piece == RoomHouseBuild.PieceName);
            st.Expected = StateSnapshot.Of(s).Where(l => !Volatile(l)).ToList(); // 퀘스트·체인 줄을 뺄지는 다시 들어온 뒤에 정한다
            st.ExpectedBag = Bag(s);
            st.ExpectedWhy = why;
            st.ExpectedFrom = s;
            st.ChestBagSince.Clear();
            st.Day = s.StudentDay;
            st.ExpectedState = s.Student.DayState;
        }

        public void Severed(RoomSeat seat, string kind)
        {
            Note(seat.PlayerId + " 끊김(" + kind + ") " + Describe(seat.Session) + " 보낼 저장 " + seat.Uploads.Count + " 방 " + Busy());
            Remember(seat, "끊김(" + kind + ")");
            BuildSevered(seat, kind);
        }

        public void Leaving(RoomSeat seat) => Remember(seat, "쉰 날");

        private void CompareAfterRejoin(RoomSeat seat, SeatState st, GameSession s)
        {
            // 나가 있는 동안 날이 바뀌면 "그 날 이상" 같은 시간 조건이 풀려 퀘스트·체인이 나아간다(받은 보상이 사라지지 않는지는 PlayRunner가 계속 본다). 보낼 요청이 걸려 있었을 때도 같다.
            bool progressMoves = st.Pending || s.StudentDay > st.Day, skillMoves = st.PendingHouse;
            bool Skip(string l) => (progressMoves && Progress(l)) || (skillMoves && l.StartsWith("skill ", StringComparison.Ordinal));
            List<string> now = StateSnapshot.Of(s).Where(l => !Volatile(l) && !Skip(l)).ToList();
            var diff = StateSnapshot.Diff(st.Expected.Where(l => !Skip(l)).ToList(), now);
            Dictionary<string, int> bag = Bag(s);
            foreach (string item in bag.Keys.Union(st.ExpectedBag.Keys).Union(st.ChestBagSince.Keys))
            {
                int expected = (st.ExpectedBag.TryGetValue(item, out int e) ? e : 0) + (st.ChestBagSince.TryGetValue(item, out int c) ? c : 0);
                int have = bag.TryGetValue(item, out int h) ? h : 0;
                if (expected != have) diff.Add("가방 " + item + ": 나가기 전(+상자 답) " + expected + ", 다시 들어온 뒤 " + have);
            }
            if (s.StudentDay < st.Day) diff.Add("날이 거꾸로: " + st.Day + " → " + s.StudentDay);
            // 잤는데(하루 결과) 다시 들어오니 그날 낮이면 잠이 사라진 것이다(같은 날이면). 쉰 날 뒤는 다음 날이라 보지 않는다.
            if (s.StudentDay == st.Day && st.ExpectedState != StudentDayState.InProgress && s.Student.DayState == StudentDayState.InProgress)
                diff.Add("하루 상태: 나가기 전 " + st.ExpectedState + ", 다시 들어온 뒤 " + s.Student.DayState + "(" + s.StudentDay + "일)");
            if (s.StudentDay != _play.Server.Clock.Day && s.Student.DayState == StudentDayState.InProgress)
                diff.Add("다시 들어온 날 " + s.StudentDay + "이 방의 날 " + _play.Server.Clock.Day + "과 다르다");
            string why = st.ExpectedWhy;
            st.Expected = null;
            Result.Count("다시 들어와 비교: " + why);
            if (diff.Count > 0) Fail("다시 들어온 진행이 다르다(" + why + "): " + seat + "\n" + string.Join("\n", diff));
        }

        // ---------- 서버 끄고 켜기 ----------

        public void BeforeRestart()
        {
            foreach (RoomSeat seat in _play.Seats) if (!seat.Away) Remember(seat, "서버 다시 켜기");
            BuildBeforeRestart();
            _serverBefore = ServerLines();
            _clockBefore = (_play.Server.Clock.Day, _play.Server.Clock.Minutes);
        }

        public void AfterRestart()
        {
            var clock = _play.Server.Clock;
            if ((clock.Day, clock.Minutes) != _clockBefore)
                Fail("서버를 다시 켠 뒤 방의 시계가 다르다: " + _clockBefore.Day + "일 " + _clockBefore.Minutes + "분 → " + clock.Day + "일 " + clock.Minutes + "분");
            var diff = StateSnapshot.Diff(_serverBefore, ServerLines());
            if (diff.Count > 0) Fail("서버를 다시 켠 뒤 방의 조각이 다르다:\n" + string.Join("\n", diff));
            BuildAfterRestart();
            _lastServerAbs = -1;
            _lastServerDay = -1;
            _recentCount = 0;
            _stableFrames = 0;
        }

        private List<string> ServerLines()
        {
            var lines = new List<string>();
            foreach (IRoomPiece piece in _play.Server.Pieces.Pieces)
            {
                RoomPieceState state = piece.State();
                lines.Add(state.Piece + " " + state.Amount);
                foreach (string id in state.Ids) lines.Add(state.Piece + " | " + id);
            }
            return lines;
        }

        // ---------- 돕는 것 ----------

        public static Dictionary<string, int> Bag(GameSession s)
        {
            var bag = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var slot in s.Inventory.Slots)
                if (slot.Item != null) bag[slot.Item.Id] = (bag.TryGetValue(slot.Item.Id, out int n) ? n : 0) + slot.Count;
            return bag;
        }

        private static void Add(Dictionary<string, int> into, string key, int by) => into[key] = (into.TryGetValue(key, out int n) ? n : 0) + by;
        private static void Add(Dictionary<string, long> into, string key, long by) => into[key] = (into.TryGetValue(key, out long n) ? n : 0) + by;
    }
}
