using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rootborn.Core.Building;
using Rootborn.Core.Inventory;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.Core.World;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Net.Room;

namespace Rootborn.Simulation.Rooms
{
    /// <summary>
    /// 같이 쓰는 집의 꾸밈 불변식(집 꾸미기 1단계, 작업 7). 방의 꾸밈 조각(house-build)은 "칸 잡기 → 돈 내기 → 쓰기/풀기"로 움직인다. 여기서는 그 걸음을 바깥에서 센다:
    /// - 장부: 꾸밈 잡기마다 누가 무엇을 몇 칸 잡았나, 돈을 냈나(보낼 목록에 쓰기가 처음 나타났다 = 돈을 빼는 바꾸기와 같은 묶음), 풀었나, 방에서 사라졌나.
    /// - 돈 보존: 나눠 쓰지 않는 지갑이면 그 자리의 지갑이 한 번의 받기 동안 돈 낸 잡기의 비용만큼만 줄어든다. 돈을 낸 잡기는 풀리지 않고, 방이 그 잡기를 지울 때
    ///   (쓰기 적용) 낸 칸 수와 방이 실제로 바꾼 칸 수가 같고 방의 기록에 그 꾸밈이 있다. 방의 꾸밈 기록은 쓰기 없이 바뀌지 않는다(공짜 꾸밈 없음). 조용해지면 자리마다 낸 돈 = 적용된 돈.
    /// - 모두가 같은 것을 본다: 조용하고 가라앉은 뒤 모든 자리의 HouseWallAt·HouseFloorAt(틀의 모든 칸)·HouseBandColors가 방의 기록으로 만든 비추기와 같다.
    /// - 끝난 뒤 남은 잡기 0(영영 막힌 칸 없음), 돈 낸 표시 0, 손님의 자기 house-build.json은 같이 쓰는 집에서 바뀌지 않는다.
    /// 꾸밈 id로 분기하지 않는다. 화면·입력 근거가 아니다(창 없는 몸).
    /// </summary>
    public sealed partial class RoomChecks
    {
        private sealed class BuildHold
        {
            public RoomSeat Seat;
            public string Id, Kind, Surface, FinishId, Tool = string.Empty;
            public int X, Y, Amount;
            public GameSession Created;
            public bool Paid, Released, Gone, Applied, Unknown;
            public string LastUse = string.Empty;
            public int Cost;
            // 돈 낸 것: 각자 쓰는 지갑에서 실제로 줄어든 돈(PaidMeasured면 받기 한 번 동안 잰 값, 아니면 요청의 비용), 같이 쓰는 지갑이었나.
            public int PaidAmount;
            public bool PaidMeasured, SharedWallet, RestartedUse;

            public bool IsBand => Kind == RoomHouseBuild.Band;
            public override string ToString() => Seat?.PlayerId + " " + Kind + " " + FinishId + (IsBand ? string.Empty : " " + Tool + "@" + X + "," + Y + " x" + Amount)
                + (Paid ? " 돈 냄" : string.Empty) + (Released ? " 풀음" : string.Empty) + (Gone ? " 방에서 사라짐" : string.Empty) + " [" + Id + "]";
        }

        private readonly Dictionary<string, BuildHold> _build = new Dictionary<string, BuildHold>(StringComparer.Ordinal);
        private readonly HashSet<string> _serverBuildHolds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<RoomSeat, int> _buildPaid = new Dictionary<RoomSeat, int>();
        private readonly List<BuildHold> _newlyPaid = new List<BuildHold>(2);
        private readonly SharedWalletBook _book = new SharedWalletBook();
        private int _buildRevision = -1;
        private string _buildSignature;
        private HouseFinishes _buildPrev;
        private bool _buildConcurrent;

        private RoomHouseBuild BuildPiece => _play.Server.Pieces.TryGet(RoomHouseBuild.PieceName, out var p) ? (RoomHouseBuild)p : null;

        /// <summary>이 방에 꾸밈 조각이 있다(같이 쓰는 집).</summary>
        public bool TracksBuild => BuildPiece != null;

        private RoomFinishDefinition FinishOf(string id) => !string.IsNullOrEmpty(id) && _play.Atlas.Definitions.TryGet<RoomFinishDefinition>(id, out var d) ? d : null;

        private static int Money(IReadOnlyList<ItemAmount<string>> cost)
        {
            int total = 0;
            if (cost != null) for (int i = 0; i < cost.Count; i++) total += cost[i].Count;
            return total;
        }

        private HouseFinishes BuildView(HouseBuildSaveData data, string surface)
        {
            HouseBuildFrame frame = string.IsNullOrEmpty(surface) ? null : _play.HouseRules.BuildFrameFor(surface);
            if (frame == null) return null;
            var view = new HouseFinishes();
            view.Attach(frame, _play.HouseRules.FindFinish);
            view.Load(data);
            return view;
        }

        // ---------- 판이 시작될 때 ----------

        private void BuildStarted(RoomSeat seat, GameSession session)
        {
            var client = session.Shares?.Find<RoomHouseBuildClient>();
            if (client == null) return;
            client.Answered += (request, applied) => BuildAnswered(seat, session, request, applied);
            foreach (BuildHold h in _build.Values)
                if (h.Seat == seat && !h.Gone) { Result.Count("다시 들어옴: 풀리지 않은 꾸밈 잡기가 있었다" + (h.Paid ? "(돈 낸 것)" : "(돈 내기 전)")); break; }
        }

        private void BuildAnswered(RoomSeat seat, GameSession session, RoomRequestSaveData request, bool applied)
        {
            if (request.Kind != RoomHouseBuild.Finish && request.Kind != RoomHouseBuild.Band) return;
            Result.Count("꾸밈 잡기 답");
            if (applied)
            {
                _decorationGrants++;
                Result.Count("꾸밈 잡힘");
                RegisterHold(seat, session, request);
                return;
            }
            // 거절: 방에 잡기가 생기지 않았다. 남이 잡은 칸과 겹쳤는지(먼저 온 것만 됨) 센다.
            _build.Remove(request.RequestId);
            Result.Count("먼저 온 것만 됨: 꾸밈");
            foreach (BuildHold h in _build.Values)
                if (h.Seat != seat && !h.Gone && _serverBuildHolds.Contains(h.Id)) { Result.Count("꾸밈 거절: 남이 잡아 둔 것이 있을 때"); break; }
        }

        private BuildHold RegisterHold(RoomSeat seat, GameSession session, RoomRequestSaveData r)
        {
            if (_build.TryGetValue(r.RequestId, out BuildHold known)) return known;
            var h = new BuildHold
            {
                Seat = seat, Id = r.RequestId, Kind = r.Kind, Created = session, Amount = r.Amount,
                Surface = r.Ids != null && r.Ids.Length > 0 ? r.Ids[0] : string.Empty,
                FinishId = r.Ids != null && r.Ids.Length > 1 ? r.Ids[1] : string.Empty,
            };
            if (r.Kind == RoomHouseBuild.Finish && r.Ids != null && r.Ids.Length == 5)
            {
                h.Tool = r.Ids[2];
                int.TryParse(r.Ids[3], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out h.X);
                int.TryParse(r.Ids[4], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out h.Y);
            }
            var def = FinishOf(h.FinishId);
            h.Cost = def == null ? 0 : Money(HouseFinishes.CostFor(def, h.IsBand ? 1 : h.Amount));
            _build[h.Id] = h;
            Result.Count("꾸밈 잡기 요청");
            Result.Count(h.IsBand ? "꾸밈 잡기 요청: 띠" : "꾸밈 잡기 요청: 칸");
            return h;
        }

        // ---------- 받기 한 번 ----------

        /// <summary>보낼 목록의 꾸밈 요청을 장부에 옮긴다(요청이 생기는 때와 쓰기·풀기가 적히는 때를 놓치지 않게 받기 앞뒤로 부른다).</summary>
        private void ScanBuild(RoomSeat seat, GameSession s)
        {
            var requests = s.Shares.Outbox.Requests;
            for (int i = 0; i < requests.Count; i++)
            {
                RoomRequestSaveData r = requests[i];
                if (r.Piece != RoomHouseBuild.PieceName) continue;
                if (r.Kind == RoomHouseBuild.Finish || r.Kind == RoomHouseBuild.Band) RegisterHold(seat, s, r);
                else if (r.Kind == RoomHouseBuild.Use && r.Ids != null && r.Ids.Length == 1) NoteUse(seat, s, r, i + 1 < requests.Count ? requests[i + 1] : null);
                else if (r.Kind == RoomHouseBuild.Release && r.Ids != null && r.Ids.Length == 1) NoteRelease(seat, s, r);
            }
        }

        private BuildHold HoldOf(RoomSeat seat, RoomRequestSaveData r)
        {
            string id = r.Ids[0];
            if (_build.TryGetValue(id, out BuildHold h)) return h;
            // 요청을 보지 못한 잡기(관찰 틈): 비용을 모른다 — 비용 검사에서 뺀다.
            h = new BuildHold { Seat = seat, Id = id, Kind = RoomHouseBuild.Finish, Unknown = true, Created = null };
            _build[id] = h;
            Result.Count("꾸밈: 요청을 보지 못한 잡기(관찰 틈)");
            return h;
        }

        // next: 보낼 목록에서 이 쓰기 바로 뒤의 요청. 같이 쓰는 지갑이면 돈을 내는 바꾸기가 꾸밈 쓰기 다음에 방 지갑 쓰기(Ids[0] = 방 지갑 잡기 id)를 적는다
        // (Game PayForHold의 커밋 → RoomWalletClient.TryExchange가 커밋 뒤에 쓰기를 더함).
        private void NoteUse(RoomSeat seat, GameSession s, RoomRequestSaveData r, RoomRequestSaveData next)
        {
            BuildHold h = HoldOf(seat, r);
            if (h.Released) Fail("풀어 버린 꾸밈 잡기에 쓰기가 적혔다: " + h);
            if (!h.Paid)
            {
                h.Paid = true;
                h.LastUse = r.RequestId;
                h.PaidAmount = h.Cost; // 각자 쓰는 지갑이면 받기 한 번 동안 잰 값으로 바꾼다(PumpBuild)
                h.SharedWallet = s.SharesWallet;
                Add(_buildPaid, seat, h.Cost);
                _newlyPaid.Add(h);
                Result.Count("꾸밈 돈 냄(쓰기를 적음)");
                bool restarted = h.Created != null && h.Created != s;
                if (restarted) Result.Count("떠도는 잡기 정리: 다시 켠 판이 쓰기를 적음");
                if (h.SharedWallet)
                {
                    if (next != null && next.Piece == RoomWallet.PieceName && next.Kind == RoomWallet.Use && next.Ids != null && next.Ids.Length == 1)
                    {
                        string fail = _book.LinkDecoration(seat.PlayerId, h.Id, next.Ids[0]);
                        if (fail != null) Fail(fail);
                        Result.Count("꾸밈 돈: 방 지갑 쓰기와 이음");
                    }
                    else if (restarted) { h.RestartedUse = true; Result.Count("꾸밈 돈: 다시 켠 판의 쓰기라 방 지갑 쓰기가 없다(돈은 지난 판이 냈다)"); }
                    else if (h.Cost == 0) Result.Count("꾸밈 돈: 비용 0인 꾸밈(방 지갑 쓰기 없음 — 적용될 때 다시 계산한 비용도 0이어야 한다)");
                    else Fail("같이 쓰는 지갑인데 꾸밈 쓰기와 같은 바꾸기에 방 지갑 쓰기가 없다(돈 없이 꾸밈): " + h + " 다음 요청 " + (next == null ? "(없음)" : next.Piece + "/" + next.Kind));
                }
            }
            else if (h.LastUse != r.RequestId)
            {
                h.LastUse = r.RequestId;
                Result.Count("꾸밈 쓰기 다시 보냄(돈 낸 표시로 또는 거절 뒤)");
            }
        }

        private void NoteRelease(RoomSeat seat, GameSession s, RoomRequestSaveData r)
        {
            BuildHold h = HoldOf(seat, r);
            if (h.Paid) Fail("돈을 낸 꾸밈 잡기를 풀었다 — 낸 돈이 사라진다: " + h);
            if (h.Released) return;
            h.Released = true;
            Result.Count("꾸밈 풀기를 적음(돈 내기 전)");
            if (h.Created != null && h.Created != s) Result.Count("떠도는 잡기 정리: 다시 켠 판이 풀기를 적음");
        }

        private static void Add(Dictionary<RoomSeat, int> into, RoomSeat key, int by) => into[key] = (into.TryGetValue(key, out int n) ? n : 0) + by;

        private int PaidOf(RoomSeat seat) => _buildPaid.TryGetValue(seat, out int n) ? n : 0;

        /// <summary>
        /// 그 자리의 접속 일 한 번을 돈 낸 잡기의 비용과 함께 본다: 지갑을 나눠 쓰지 않으면 이 받기 동안 지갑이 줄어든 만큼이 새로 돈을 낸 잡기들의 비용과 같다
        /// (돈을 빼는 바꾸기와 쓰기를 적는 일이 한 묶음이다). 아니면 앞뒤 장부만 갱신한다.
        /// </summary>
        private void PumpBuild(RoomSeat seat, double now, GameSession s)
        {
            ScanBuild(seat, s);
            _newlyPaid.Clear();
            int balanceBefore = s.Wallet.Balance, day = s.StudentDay;
            PumpChests(seat, now);
            if (seat.Session != s) return;
            ScanBuild(seat, s);
            if (s.SharesWallet || !seat.Connected || s.StudentDay != day) return;
            int spent = balanceBefore - s.Wallet.Balance, requested = 0;
            foreach (BuildHold h in _newlyPaid) requested += h.Cost;
            if (_newlyPaid.Count == 0)
            {
                if (spent != 0) Fail("돈 낸 꾸밈 잡기가 없는데 받기 한 번 동안 각자 쓰는 지갑이 바뀌었다: " + seat + " 지갑 " + balanceBefore + " → " + s.Wallet.Balance);
                return;
            }
            // 받기 한 번 동안 실제로 줄어든 돈이 그 잡기가 낸 돈이다. 방이 쓰기를 적용할 때(Landed) 방의 기록에서 다시 계산한 비용과 견준다.
            if (_newlyPaid.Count == 1)
            {
                BuildHold h = _newlyPaid[0];
                Add(_buildPaid, seat, spent - h.PaidAmount);
                h.PaidAmount = spent;
                h.PaidMeasured = true;
            }
            else if (spent != requested)
                Fail("한 번의 받기에 꾸밈 잡기 여럿이 돈을 냈는데 줄어든 돈이 요청의 비용 합과 다르다: " + seat + " 줄어든 " + spent + ", 비용 합 " + requested);
            Result.Count("꾸밈 돈: 각자 쓰는 지갑에서 줄어든 돈을 잼");
        }

        /// <summary>같이 쓰는 지갑이면 보낼 목록의 지갑 요청을 장부에 옮긴다(방이 반영했는지는 조용할 때 방의 답 장부로 가린다).</summary>
        private void ScanWallet(RoomSeat seat, GameSession s)
        {
            if (s == null || !s.SharesWallet) return;
            var requests = s.Shares.Outbox.Requests;
            for (int i = 0; i < requests.Count; i++) if (requests[i].Piece == RoomWallet.PieceName) _book.See(seat.PlayerId, requests[i]);
        }

        // ---------- 방 지갑 잡기의 까닭 ----------

        private RoomSeat _acting;
        private int _decorationGrants;
        private readonly Dictionary<RoomSeat, string> _lastActionScreen = new Dictionary<RoomSeat, string>();

        private bool RealTimeOpen(RoomSeat seat) => seat.Body.Screens.Find(Rootborn.Screens.ScreenCatalog.Minigame) != null || Of(seat).RealTime;

        private void CloseWallet(RoomSeat seat, string window, int grants, string reason)
        {
            string fail = _book.CloseWindow(seat.PlayerId, window, grants, reason);
            if (fail != null) Fail(fail);
            for (int i = 0; i < _book.LastClosed; i++) Result.Count("방 지갑 잡기 까닭: " + window + (reason == null ? string.Empty : "(" + reason + ")"));
        }

        private sealed class PurchaseWindow
        {
            public HashSet<string> Uses, Tokens;
        }

        // 받기 하나 앞의 방 지갑 쓰기 id와 해금 표(같이 쓰는 지갑일 때만).
        private PurchaseWindow OpenPurchaseWindow(GameSession s)
        {
            if (s == null || !s.SharesWallet || s.FurnitureUnlocks == null) return null;
            var w = new PurchaseWindow { Uses = new HashSet<string>(StringComparer.Ordinal), Tokens = new HashSet<string>(s.FurnitureUnlocks.UnlockTokens, StringComparer.Ordinal) };
            foreach (var r in s.Shares.Outbox.Requests) if (r.Piece == RoomWallet.PieceName && r.Kind == RoomWallet.Use) w.Uses.Add(r.RequestId);
            return w;
        }

        private Dictionary<string, int> _priceByToken;

        // 파는 가구·꾸밈의 해금 표 → 값(정의). 같은 표를 둘이 쓰면 값이 다를 때 짝짓지 않는다(-1).
        private Dictionary<string, int> PriceByToken()
        {
            if (_priceByToken != null) return _priceByToken;
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            void Put(string token, int price) => map[token] = map.TryGetValue(token, out int known) && known != price ? -1 : price;
            foreach (var f in _play.Atlas.Definitions.All<Rootborn.Core.Placement.FurnitureDefinition>()) if (f != null && f.IsForSale) Put(f.UnlockToken, f.Price);
            foreach (var f in _play.Atlas.Definitions.All<RoomFinishDefinition>()) if (f != null && f.IsForSale) Put(f.UnlockToken, f.Price);
            return _priceByToken = map;
        }

        /// <summary>
        /// 받기 하나 안에서 걸음의 행동으로 잡은 방 지갑 돈이 쓰였으면(허락 뒤 다시 한 명령) 그 까닭과 값을 맞춘다: 같은 받기에서 파는 가구·꾸밈의 해금이 생겼으면
        /// 쓴 돈 합 = 정의의 값 합(아니면 실패). 해금이 없으면 값을 데이터로 짝지을 수 없는 지출(시설·일·탐색·일터 템플릿 등)로 그 행동의 화면 이름과 함께 센다.
        /// </summary>
        private void PairPurchases(RoomSeat seat, GameSession s, PurchaseWindow before)
        {
            if (before == null || s.FurnitureUnlocks == null) return;
            int spent = 0, uses = 0;
            foreach (var r in s.Shares.Outbox.Requests)
            {
                if (r.Piece != RoomWallet.PieceName || r.Kind != RoomWallet.Use || before.Uses.Contains(r.RequestId) || r.Ids == null || r.Ids.Length != 1) continue;
                string reason = _book.ReasonOf(seat.PlayerId, r.Ids[0]);
                if (reason == null || !reason.StartsWith("걸음", StringComparison.Ordinal)) continue;
                spent += r.Amount;
                uses++;
            }
            if (uses == 0) return;
            int price = 0;
            bool bought = false;
            foreach (string token in s.FurnitureUnlocks.UnlockTokens)
            {
                if (before.Tokens.Contains(token) || !PriceByToken().TryGetValue(token, out int p) || p < 0) continue;
                price += p;
                bought = true;
            }
            if (!bought)
            {
                // 지금 아는 걸음의 지출은 가구·꾸밈 사기뿐이다(시뮬레이션의 같이 쓰는 지갑 방에서 다른 지출은 지나지 않는다). 시설·일·탐색·일터 템플릿처럼
                // 값을 해금으로 짝지을 수 없는 지출이 나타나면 그 종류의 짝짓기를 더할 때까지 실패한다(짝이 없는 지출을 받아들이지 않는다).
                string screen = _lastActionScreen.TryGetValue(seat, out string sc) ? sc : "-";
                Fail("방 지갑에서 쓴 돈의 까닭을 짝짓지 못했다(가구·꾸밈 사기가 아니다): " + seat + " 쓴 돈 " + spent + "(쓰기 " + uses + "), 마지막 행동의 화면 " + screen);
                return;
            }
            if (price != spent) Fail("가구·꾸밈을 사며 방 지갑에서 쓴 돈이 정의의 값과 다르다: " + seat + " 쓴 돈 " + spent + "(쓰기 " + uses + "), 새로 해금한 것의 값 " + price);
            Result.Count("방 지갑 지출: 가구·꾸밈 사기 — 쓴 돈 = 정의의 값");
        }

        /// <summary>조용할 때: 방 지갑의 잔액 = 기준 + 넣기 + 풀기 − 잡기(꾸밈은 방의 기록에서 다시 계산한 비용). 이 방에 방 지갑이 없으면 하지 않는다.</summary>
        private void CheckWalletBook()
        {
            RoomWallet wallet = Wallet();
            if (wallet == null) return;
            RoomRequestLedger ledger = _play.Server.Pieces.Ledger;
            string fail = _book.Check(wallet.Balance, (string p, string id, out RoomAnswer a) => ledger.TryGet(p, id, out a), wallet.Balance >= PersonalWallet.MaxBalance);
            if (fail != null) Fail(fail);
            Result.Count("방 지갑 잔액 = 넣기 − 잡기 + 풀기(꾸밈은 방의 기록으로 다시 계산)");
        }

        // ---------- 방 쪽 ----------

        /// <summary>프레임마다(가볍게): 꾸밈 조각이 바뀌었을 때만 읽는다. 사라진 잡기는 쓰기·풀기 중 하나여야 하고, 기록은 쓰기 없이 바뀌지 않는다.</summary>
        private void BuildServerStep()
        {
            RoomHouseBuild piece = BuildPiece;
            if (piece == null) return;
            CountConcurrentBuilds();
            if (piece.Revision == _buildRevision) return;
            _buildRevision = piece.Revision;
            RoomPieceState state = piece.State();
            var holds = new List<KeyValuePair<string, string>>();
            if (!RoomHouseBuildLines.TryDecode(state.Ids, out HouseBuildSaveData data, holds)) { Fail("꾸밈 조각의 상태 줄을 읽을 수 없다"); return; }
            var now = new HashSet<string>(StringComparer.Ordinal);
            foreach (var h in holds) now.Add(h.Key);

            string surface = null;
            foreach (BuildHold h in _build.Values) if (!string.IsNullOrEmpty(h.Surface)) { surface = h.Surface; break; }
            HouseFinishes view = BuildView(data, surface);
            HouseFinishes prev = _buildPrev ?? (view == null ? null : BuildView(new HouseBuildSaveData { Version = HouseBuildSaveData.KnownVersion, FrameId = data.FrameId }, surface));

            foreach (string id in now) if (!_serverBuildHolds.Contains(id)) Result.Count("방이 꾸밈 칸을 잡아 줌");
            bool anyUsed = false;
            foreach (string id in _serverBuildHolds)
            {
                if (now.Contains(id)) continue;
                if (!_build.TryGetValue(id, out BuildHold h)) { Fail("방의 꾸밈 잡기를 아무도 요청한 적이 없다: " + id); continue; }
                h.Gone = true;
                if (h.Paid) { anyUsed = true; Landed(h, prev, view); }
                else if (h.Released) Result.Count("꾸밈 풀기가 방에 적용됨");
                else Fail("꾸밈 잡기가 쓰기도 풀기도 없이 방에서 사라졌다: " + h);
            }

            string signature = data.Band + ";" + string.Join(",", data.Finishes.Select(c => c.X + "," + c.Y + "," + c.Wall + "," + c.Floor));
            if (_buildSignature != null && signature != _buildSignature && !anyUsed)
                Fail("방의 꾸밈 기록이 돈 낸 쓰기 없이 바뀌었다(공짜 꾸밈이거나 풀기가 기록을 바꿨다): 전 " + _buildSignature + " 후 " + signature);
            _buildSignature = signature;
            _buildPrev = view;
            _serverBuildHolds.Clear();
            foreach (string id in now) _serverBuildHolds.Add(id);
        }

        // 돈을 낸 잡기가 방에서 지워졌다(쓰기 적용): 낸 칸 수와 방이 실제로 바꾼 칸 수가 같고, 방의 기록에 그 꾸밈이 있고,
        // 낸 돈(각자 지갑에서 잰 감소 / 같이 쓰는 지갑이 잡은 돈) = 방의 기록에서 다시 계산한 비용(정의의 칸당 값 × 방의 앞 기록에서 실제로 바뀌는 칸 수, 띠는 바뀌면 한 번).
        // 같은 판에서 잡기 여럿이 지워지면 before에 차례로 적용해 다음 것의 앞 기록으로 쓴다.
        private void Landed(BuildHold h, HouseFinishes before, HouseFinishes after)
        {
            h.Applied = true;
            Result.Count("꾸밈 쓰기가 방에 적용됨(돈 낸 것)");
            var def = h.Unknown || before == null || after == null ? null : FinishOf(h.FinishId);
            int recomputed = -1;
            if (def != null && h.IsBand)
            {
                if (after.BandColors != def.Art) Fail("돈 낸 띠 꾸밈이 방의 기록에 없다: " + h + " — 보이는 띠 " + after.BandColors + ", 꾸밈의 그림 " + def.Art);
                var band = before.PreviewBand(h.FinishId, _ => true, _ => true);
                recomputed = Money(HouseFinishes.CostFor(def, before.BandColors != def.Art ? 1 : 0));
                before.Apply(band);
            }
            else if (def != null)
            {
                var preview = before.Preview(h.FinishId, h.Tool, new CellPos(h.X, h.Y), _ => true, _ => true);
                if (preview.ChangedCells.Count != h.Amount)
                    Fail("돈 낸 칸 수와 방이 바꾼 칸 수가 다르다: " + h + " — 낸 칸 " + h.Amount + ", 방이 바꾼 칸 " + preview.ChangedCells.Count);
                bool wall = string.Equals(def.Slot, RoomFinishSlots.Wall, StringComparison.Ordinal);
                foreach (CellPos c in preview.ChangedCells)
                {
                    string seen = wall ? after.WallAt(c) : after.FloorAt(c);
                    if (seen != h.FinishId) Fail("돈 낸 꾸밈이 방의 기록에 없다: " + h + " 칸 " + c.X + "," + c.Y + " 보이는 것 " + seen);
                }
                recomputed = Money(HouseFinishes.CostFor(def, preview.ChangedCells.Count));
                before.Apply(preview);
            }
            if (recomputed < 0) Result.Count("꾸밈 돈: 방의 기록으로 비용을 다시 계산하지 못함(관찰 틈)");
            if (h.SharedWallet)
            {
                if (!_book.IsLinked(h.Seat.PlayerId, h.Id))
                {
                    // 방 지갑 쓰기가 없는 적용: 다시 켠 판의 쓰기(돈은 지난 판이 냈다 — 잇지 못한다)이거나, 방의 기록으로도 비용이 0이어야 한다. 아니면 돈 없이 꾸민 것(덜 청구).
                    if (h.RestartedUse || recomputed < 0) { Result.Count("꾸밈 돈: 방 지갑 쓰기와 잇지 못한 적용(다시 켠 판·관찰 틈)"); return; }
                    if (recomputed > 0) Fail(_book.DecorationLanded(h.Seat.PlayerId, h.Id, recomputed));
                    else Result.Count("꾸밈 돈 보존: 비용 0인 꾸밈은 방 지갑을 쓰지 않았다");
                    return;
                }
                string fail = _book.DecorationLanded(h.Seat.PlayerId, h.Id, recomputed);
                if (fail != null) Fail(fail);
                if (recomputed >= 0) Result.Count("꾸밈 돈 보존: 방 지갑이 잡은 돈 = 방의 기록으로 다시 계산한 비용");
            }
            else if (h.PaidMeasured && recomputed >= 0)
            {
                if (h.PaidAmount != recomputed)
                    Fail("꾸밈으로 각자 쓰는 지갑에서 나간 돈과 방이 바꾼 것의 비용이 다르다: " + h + " — 지갑에서 줄어든 돈 " + h.PaidAmount + ", 방의 기록에서 다시 계산한 비용 " + recomputed);
                Result.Count("꾸밈 돈 보존: 지갑 줄어듦 = 방의 기록으로 다시 계산한 비용");
            }
            else if (!h.PaidMeasured) Result.Count("꾸밈 돈: 각자 쓰는 지갑에서 잴 수 없던 지불(끊김·날 바뀜 — 남의 걸음 검사가 본다)");
        }

        private void CountConcurrentBuilds()
        {
            int mask = 0;
            foreach (BuildHold h in _build.Values) if (!h.Gone) mask |= 1 << h.Seat.Index;
            bool two = (mask & (mask - 1)) != 0;
            if (two && !_buildConcurrent) Result.Count("꾸밈 동시 요청: 두 사람 이상이 한꺼번에 잡기를 가짐");
            _buildConcurrent = two;
        }

        private void BuildSevered(RoomSeat seat, string kind)
        {
            bool paid = false, held = false, requested = false;
            foreach (BuildHold h in _build.Values)
            {
                if (h.Seat != seat || h.Gone) continue;
                if (h.Paid) paid = true;
                else if (_serverBuildHolds.Contains(h.Id)) held = true;
                else requested = true;
            }
            if (paid) Result.Count("끊김: 꾸밈 돈 낸 뒤 쓰기가 방에 닿기 전");
            else if (held) Result.Count("끊김: 꾸밈 잡기 허락 뒤(돈 내기 전)");
            else if (requested) Result.Count("끊김: 꾸밈 잡기 요청 직후");
        }

        private bool OwnBuildHold(RoomSeat seat)
        {
            foreach (BuildHold h in _build.Values) if (h.Seat == seat && !h.Gone && _serverBuildHolds.Contains(h.Id)) return true;
            return false;
        }

        private int BuildHeld => BuildPiece?.HeldCount ?? 0;

        /// <summary>방에 꾸밈 잡기가 없고 어느 자리의 저장에도 돈 낸 꾸밈 표시가 없다(끝난 뒤 가만히 기다리기를 일찍 끝낼 수 있다).</summary>
        public bool BuildAtRest
        {
            get
            {
                if (BuildHeld > 0) return false;
                foreach (RoomSeat seat in _play.Seats) if (PaidMarks(seat) > 0) return false;
                return true;
            }
        }

        private void BuildAfterRestart() => _buildRevision = -1;

        // ---------- 조용할 때 ----------

        /// <summary>
        /// 조용하고 가라앉았을 때: 걸린 것이 없는데 방에 없는 요청은 끊김으로 사라진 것이라 장부에서 지운다. 모든 자리가 보는 꾸밈이 방의 기록과 같다.
        /// 낸 돈 = 적용된 돈(자리마다).
        /// </summary>
        private void CheckBuild()
        {
            RoomHouseBuild piece = BuildPiece;
            if (piece == null) return;
            foreach (string id in _build.Where(p => !p.Value.Gone && !p.Value.Paid && !_serverBuildHolds.Contains(p.Key)).Select(p => p.Key).ToList())
            {
                _build.Remove(id);
                Result.Count("꾸밈 요청이 방에 닿기 전에 사라짐(끊김 — 돈은 내지 않았다)");
            }
            // 방에 잡기가 없으면 돈 낸 잡기는 모두 방에 적용됐어야 한다(낸 돈이 꾸밈 없이 남지 않는다). (전의 "낸 돈 = 적용된 돈"은 같은 비용을 양쪽에 쌓아 늘 같았다 —
            // 돈의 양은 Landed가 방의 기록으로 다시 계산해 지갑의 실제 감소와 견준다.)
            if (piece.HeldCount == 0)
                foreach (BuildHold h in _build.Values)
                    if (h.Paid && !h.Gone) Fail("방에 꾸밈 잡기가 없는데 돈 낸 잡기가 방에 적용되지 않았다: " + h);
            HouseBuildSaveData data = piece.ToSaveData();
            foreach (RoomSeat seat in _play.Seats)
            {
                if (!seat.Playing) continue;
                GameSession s = seat.Session;
                if (!s.SharesHouseBuild || s.House == null || seat.Body.HouseFrame == null) continue;
                HouseFinishes reference = BuildView(data, s.House.SurfaceId);
                if (reference == null) continue;
                if (s.HouseBandColors() != reference.BandColors)
                    Fail("모두가 보는 띠 색이 다르다: " + seat + " " + s.HouseBandColors() + ", 방의 기록 " + reference.BandColors);
                foreach (var cell in reference.Frame.Tiles.Cells)
                {
                    CellPos c = cell.Cell;
                    if (s.HouseWallAt(c) != reference.WallAt(c)) Fail("모두가 보는 벽지가 다르다: " + seat + " 칸 " + c.X + "," + c.Y + " — 본 것 '" + s.HouseWallAt(c) + "', 방의 기록 '" + reference.WallAt(c) + "'");
                    if (s.HouseFloorAt(c) != reference.FloorAt(c)) Fail("모두가 보는 바닥이 다르다: " + seat + " 칸 " + c.X + "," + c.Y + " — 본 것 '" + s.HouseFloorAt(c) + "', 방의 기록 '" + reference.FloorAt(c) + "'");
                }
                Result.Count("꾸밈 비추기 비교");
            }
        }

        /// <summary>정책의 꾸미기 누름 하나 뒤(진단): 화면이 적용·기다림·거절 중 무엇이라 보였고 거절이면 미리 보기의 이유. 불변식이 아니라 넓이를 보는 숫자다.</summary>
        public void AfterAction(RoomSeat seat, PlayAction action)
        {
            if (action != null) _lastActionScreen[seat] = string.IsNullOrEmpty(action.Screen) || action.Screen == "-" ? action.Category ?? action.Key : action.Screen;
            if (action == null || (action.Category != "finish-cell" && action.Category != "finish-cell-blocked" && !(action.Category == "row" && action.Element == Rootborn.Screens.Furniture.FurniturePlacementScreen.FinishBandListId))) return;
            if (!(seat.Body.Screens.Find(Rootborn.Screens.ScreenCatalog.FurniturePlacement) is Rootborn.Screens.Furniture.FurniturePlacementScreen p) || !p.IsOpen) return;
            string what = p.FinishLastAction;
            string reason = what == "refused" && action.Category.StartsWith("finish-cell", StringComparison.Ordinal) ? p.FinishPreview?.ReasonKey : null;
            Result.Count("꾸밈 누름(" + (action.Category.StartsWith("finish-cell", StringComparison.Ordinal) ? "칸" : "띠") + "): " + what + (reason == null ? string.Empty : " — " + reason));
        }

        // ---------- 파일 ----------

        private SaveFileSet SavedFilesOf(RoomSeat seat)
        {
            SaveFileSet files = _play.Guests.Read(seat.PlayerId);
            files.Overlay(seat.Uploads.Merged());
            return files;
        }

        /// <summary>손님의 자기 house-build.json은 같이 쓰는 집에서 바뀌지 않는다(쓰이지 않는다).</summary>
        private void CheckOwnBuildFile(RoomSeat seat)
        {
            foreach (var pair in SavedFilesOf(seat).Writes)
                if (pair.Key == SaveFileNames.HouseBuild || pair.Key.EndsWith("/" + SaveFileNames.HouseBuild, StringComparison.Ordinal)
                    || pair.Key.EndsWith("\\" + SaveFileNames.HouseBuild, StringComparison.Ordinal))
                    Fail("손님의 자기 house-build.json이 같이 쓰는 집에서 쓰였다: " + seat + " (" + pair.Key + ")");
        }

        private int PaidMarks(RoomSeat seat)
        {
            var slot = new MemorySaveSlot(RoomGuestLink.Slot, SavedFilesOf(seat).Writes);
            return slot.TryRead(RoomGuestLink.Slot, SaveFileNames.RoomHouseBuildPaid(seat.PlayerId), out RoomHouseBuildPaidSaveData data) && data.Holds != null ? data.Holds.Length : 0;
        }

        /// <summary>서버를 끄기 전: 자기 파일 검사.</summary>
        private void BuildBeforeRestart()
        {
            if (BuildPiece == null) return;
            foreach (RoomSeat seat in _play.Seats) CheckOwnBuildFile(seat);
        }

        /// <summary>
        /// 끝난 뒤(모두 다시 들어와 가만히 있었다): 방에 꾸밈 잡기가 남지 않았고, 돈 낸 표시가 0이고, 모두가 같은 것을 보고, 낸 돈 = 적용된 돈이다.
        /// </summary>
        public void BuildFinal()
        {
            RoomHouseBuild piece = BuildPiece;
            if (piece == null) return;
            if (piece.HeldCount > 0) Fail("끝난 뒤 모두 다시 들어와 가만히 있었는데 방에 꾸밈 잡기가 남았다(영영 막힌 칸): " + string.Join("; ", _build.Values.Where(h => !h.Gone)));
            foreach (BuildHold h in _build.Values.Where(h => !h.Gone && (h.Paid || _serverBuildHolds.Contains(h.Id)))) Fail("끝난 뒤에도 장부의 꾸밈 잡기가 끝나지 않았다: " + h);
            CheckBuild();
            if (Quiet) CheckWalletBook();
            foreach (RoomSeat seat in _play.Seats)
            {
                CheckOwnBuildFile(seat);
                int marks = PaidMarks(seat);
                if (marks > 0) Fail("끝난 뒤에도 돈 낸 꾸밈 표시가 남았다: " + seat + " " + marks + "개 — 방에는 잡기가 없는데 저장에 표시가 남는다");
            }
            Result.Count("끝난 뒤 가만히: 꾸밈 잡기 0, 돈 낸 표시 0");
        }
    }
}
