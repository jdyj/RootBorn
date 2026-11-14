using System;
using System.Collections.Generic;
using Rootborn.Core.Room;

namespace Rootborn.Simulation.Rooms
{
    /// <summary>
    /// 같이 쓰는 지갑(방 지갑)의 돈 장부. 자리들의 보낼 목록에서 본 지갑 요청(넣기·잡기·풀기)을 모으고, 방이 실제로 반영했는지는 방의 답 장부
    /// (<see cref="RoomRequestLedger"/>)로 가린다(보내기 전에 끊겨 사라진 요청은 세지 않는다). 꾸밈 쓰기와 같은 바꾸기에서 적힌 지갑 쓰기의 범위(잡기 id)는
    /// "꾸밈 잡기"로 잇는다. 방이 조용할 때(잡은 돈 0, 보낼 요청 0):
    ///   방 지갑 잔액 = 기준 + 넣기 + 풀기 − 꾸밈 아닌 잡기 − (방에 적용된 꾸밈마다 방의 기록에서 다시 계산한 비용) − (적용되지 않은 꾸밈 잡기의 실제 순감소).
    /// 그리고 적용된 꾸밈마다: 그 꾸밈으로 방 지갑이 잡은 돈(실제 줄어든 돈) = 다시 계산한 비용, 그 잡기가 방에 반영됐고 풀리지 않았다.
    /// 그래서 이중 청구(잡은 돈 &gt; 비용, 잡기 둘)와 덜 청구(잡은 돈 &lt; 비용, 잡기 없음, 풀어서 돌려받음)가 실패한다.
    /// 꾸밈 아닌 지출(가게·가구 등)은 지갑 쓰기가 꾸밈 쓰기와 같은 바꾸기에 적히지 않으므로 가르지 못하고 요청에 적힌 대로 센다.
    /// 화면·입력 근거가 아니다.
    /// </summary>
    public sealed class SharedWalletBook
    {
        private sealed class Seen
        {
            public string Player;
            public RoomRequestSaveData Request;
        }

        private sealed class Decoration
        {
            public string Player, Scope, Hold;
            public int HeldApplied = -1;   // 방이 반영한 잡기의 돈(모르면 -1)
            public int SeenHold = -1;      // 보낼 목록에서 본 잡기 요청의 돈
            public int Released;           // 방이 반영한 풀기의 돈
            public int Recomputed = -1;    // 방에 적용됐다: 방의 기록에서 다시 계산한 비용(-1이면 아직 또는 모름)
            public bool Landed, Unknown;
        }

        private readonly Dictionary<string, Seen> _pending = new Dictionary<string, Seen>(StringComparer.Ordinal);
        private readonly HashSet<string> _accounted = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _seenHolds = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, Decoration> _decorations = new Dictionary<string, Decoration>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _decorationOfHold = new Dictionary<string, string>(StringComparer.Ordinal);
        private long _deposits, _releasesOther, _holdsOther;
        private long _base = -1;

        /// <summary>센 것(넓이).</summary>
        public int Checks { get; private set; }
        /// <summary>마지막 견주기 때 답이 없던 지갑 요청 수(사라졌거나 아직 닿지 않음).</summary>
        public int Unanswered { get; private set; }
        private readonly List<string> _done = new List<string>();
        public int DecorationsLanded { get; private set; }

        private static string Key(string player, string id) => player + "\n" + id;

        // ---------- 잡기마다 까닭 ----------
        // 방 지갑 잡기(돈을 쓰려는 것)는 처음 보인 때로 까닭을 정한다: 받기 안(방의 답을 처리함) = 꾸밈 허락에 돈을 내는 것뿐이라 그 받기의 꾸밈 허락 수 이내,
        // 판 시작(붙기 전에 온 답) = 꾸밈 허락, 걸음 안 = 그 걸음의 행동 하나에 잡기 하나, 걸음 사이(실시간 화면이 정산) = 실시간 화면이 떠 있을 때만.
        // 꾸밈 허락으로 생긴 잡기는 쓰일 때 꾸밈 쓰기에 이어져야 한다(LinkDecoration) — 아니면 꾸밈 하나에 잡기가 둘(따로 적은 이중 청구)이거나 까닭 없는 지출이다.

        public const string WindowPump = "받기", WindowStart = "판 시작", WindowStep = "걸음", WindowBetween = "걸음 사이";

        private readonly Dictionary<string, List<string>> _fresh = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _holdReason = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _mustBeDecoration = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _usedScopes = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>까닭마다 방 지갑 잡기 수(넓이 — 보고에 쓴다).</summary>
        public readonly SortedDictionary<string, int> Reasons = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 그 자리의 한 구간(받기·판 시작·걸음·걸음 사이)에 처음 보인 방 지갑 잡기에 까닭을 붙인다. 실패면 그 글.
        /// </summary>
        /// <param name="decorationGrants">받기 구간: 그 받기에서 허락받은 꾸밈 잡기 수.</param>
        /// <param name="reason">걸음 구간: 그 걸음의 행동. 걸음 사이: 실시간 화면이 떠 있었으면 그 이름, 아니면 null.</param>
        public string CloseWindow(string player, string window, int decorationGrants, string reason)
        {
            LastClosed = 0;
            if (!_fresh.TryGetValue(player, out List<string> fresh) || fresh.Count == 0) return null;
            LastClosed = fresh.Count;
            string fail = null;
            switch (window)
            {
                case WindowPump:
                    if (fresh.Count > decorationGrants)
                        fail = "방의 답을 받는 동안 꾸밈 허락(" + decorationGrants + ")보다 많은 방 지갑 잡기(" + fresh.Count + ")가 적혔다(까닭 없는 지출·이중 청구): " + player + " " + Ids(fresh);
                    foreach (string k in fresh) { _mustBeDecoration.Add(k); Note(k, "꾸밈 허락"); }
                    break;
                case WindowStart:
                    foreach (string k in fresh) { _mustBeDecoration.Add(k); Note(k, "꾸밈 허락(판 시작)"); }
                    break;
                case WindowStep:
                    int inStep = (_stepHolds.TryGetValue(player, out int already) ? already : 0) + fresh.Count;
                    _stepHolds[player] = inStep;
                    if (inStep > 1) fail = "걸음 하나(" + reason + ")에 방 지갑 잡기가 " + inStep + "개 적혔다(행동 하나에 잡기 하나): " + player + " " + Ids(fresh);
                    foreach (string k in fresh) Note(k, "걸음: " + reason);
                    break;
                default:
                    if (reason == null) fail = "걸음도 받기도 아닌 때 실시간 화면 없이 방 지갑 잡기가 적혔다(까닭 없는 지출): " + player + " " + Ids(fresh);
                    foreach (string k in fresh) Note(k, "걸음 사이: " + (reason ?? "까닭 없음"));
                    break;
            }
            fresh.Clear();
            return fail;
        }

        /// <summary>마지막 Check가 잔액을 실제로 견줬다(기준만 잡은 호출이면 false).</summary>
        public bool LastCompared { get; private set; }

        /// <summary>마지막 CloseWindow가 까닭을 붙인 잡기 수.</summary>
        public int LastClosed { get; private set; }

        private readonly Dictionary<string, int> _stepHolds = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>그 자리의 걸음 하나가 시작됐다(걸음 안의 잡기 수를 다시 센다 — 걸음 동안 받기가 여러 번 돌아도 걸음 하나의 잡기는 하나).</summary>
        public void BeginStep(string player) => _stepHolds[player] = 0;

        private void Note(string key, string reason)
        {
            _holdReason[key] = reason;
            string kind = reason.StartsWith("걸음: ", StringComparison.Ordinal) ? "걸음의 행동" : reason;
            Reasons[kind] = (Reasons.TryGetValue(kind, out int n) ? n : 0) + 1;
        }

        /// <summary>그 방 지갑 잡기(범위)의 까닭(모르면 null).</summary>
        public string ReasonOf(string player, string scope) => _holdReason.TryGetValue(Key(player, scope), out string r) ? r : null;

        private static string Ids(List<string> keys)
        {
            var ids = new List<string>(keys.Count);
            foreach (string k in keys) ids.Add(Strip(k));
            return "[" + string.Join(", ", ids) + "]";
        }

        /// <summary>자리의 보낼 목록에서 본 지갑 요청(같은 id는 마지막에 본 것).</summary>
        public void See(string player, RoomRequestSaveData r)
        {
            if (r == null || r.Piece != RoomWallet.PieceName || string.IsNullOrEmpty(r.RequestId)) return;
            string key = Key(player, r.RequestId);
            if (_accounted.Contains(key)) return;
            _pending[key] = new Seen { Player = player, Request = r.Copy() };
            if (r.Kind == RoomWallet.Hold)
            {
                if (!_seenHolds.ContainsKey(key))
                {
                    if (!_fresh.TryGetValue(player, out List<string> fresh)) _fresh[player] = fresh = new List<string>(2);
                    fresh.Add(key);
                }
                _seenHolds[key] = r.Amount;
                if (_decorations.TryGetValue(key, out Decoration d)) d.SeenHold = r.Amount;
            }
        }

        /// <summary>
        /// 꾸밈 잡기(hold)의 쓰기와 같은 바꾸기에 지갑 쓰기가 적혔다: 그 지갑 쓰기의 범위(scope = 방 지갑 잡기 id)가 이 꾸밈의 돈이다.
        /// 한 꾸밈에 지갑 범위가 둘이면 실패(이중 청구).
        /// </summary>
        public string LinkDecoration(string player, string hold, string scope)
        {
            string holdKey = Key(player, hold), key = Key(player, scope);
            if (_decorationOfHold.TryGetValue(holdKey, out string known))
                return known == key ? null : "한 꾸밈 잡기에 방 지갑 잡기가 둘 쓰였다(이중 청구): " + player + " 꾸밈 " + hold + " — " + Strip(known) + ", " + scope;
            if (_decorations.ContainsKey(key)) return "방 지갑 잡기 하나가 꾸밈 둘에 쓰였다: " + player + " " + scope;
            _decorationOfHold[holdKey] = key;
            var d = new Decoration { Player = player, Scope = scope, Hold = hold };
            if (_seenHolds.TryGetValue(key, out int amount)) d.SeenHold = amount;
            _decorations[key] = d;
            return null;
        }

        /// <summary>그 꾸밈 잡기에 이어진 방 지갑 잡기가 있다.</summary>
        public bool IsLinked(string player, string hold) => _decorationOfHold.ContainsKey(Key(player, hold));

        /// <summary>
        /// 돈을 낸 꾸밈이 방에 적용됐다(방이 잡기를 지웠다). recomputed = 방의 기록에서 다시 계산한 비용(-1이면 계산하지 못함 — 실제 순감소로 센다).
        /// 지갑 범위가 이어지지 않은 꾸밈이면 실패(돈 없이 꾸밈 — 덜 청구). 잡은 돈을 이미 알면 비용과 견준다.
        /// </summary>
        public string DecorationLanded(string player, string hold, int recomputed)
        {
            if (!_decorationOfHold.TryGetValue(Key(player, hold), out string key))
                return "같이 쓰는 지갑에서 꾸밈이 방에 적용됐는데 방 지갑에서 돈을 잡은 쓰기가 없다(덜 청구): " + player + " 꾸밈 " + hold + ", 비용 " + recomputed;
            Decoration d = _decorations[key];
            d.Landed = true;
            d.Unknown = recomputed < 0;
            d.Recomputed = recomputed;
            DecorationsLanded++;
            int held = d.HeldApplied >= 0 ? d.HeldApplied : d.SeenHold;
            if (!d.Unknown && held >= 0 && held != recomputed)
                return "꾸밈으로 방 지갑이 잡은 돈과 방이 바꾼 것의 비용이 다르다: " + player + " 꾸밈 " + hold + " — 잡은 돈 " + held + ", 방의 기록에서 다시 계산한 비용 " + recomputed;
            return null;
        }

        /// <summary>
        /// 방이 조용할 때(잡은 돈 0, 모두의 보낼 요청 0): 본 요청을 방의 답 장부로 가르고 잔액을 견준다. 처음 부르면 기준만 잡는다. 실패면 그 글, 아니면 null.
        /// </summary>
        /// <param name="balance">방 지갑의 지금 잔액.</param>
        /// <param name="answered">(플레이어, 요청 id) → 방의 답. 답이 없으면 보내지 못하고 사라진 요청이다.</param>
        /// <param name="atCap">잔액이 상한이다(넣기가 잘렸을 수 있다 — 기준을 다시 잡는다).</param>
        public string Check(int balance, TryAnswer answered, bool atCap)
        {
            Checks++;
            _done.Clear();
            foreach (var pair in _pending)
            {
                Seen seen = pair.Value;
                RoomRequestSaveData r = seen.Request;
                // 답이 없으면 아직 방에 닿지 않았거나(나가 있는 자리의 보낼 목록) 보내기 전에 끊겨 사라졌다 — 세지 않고 남겨 둔다(id는 자리마다 겹치지 않는다).
                if (!answered(seen.Player, r.RequestId, out RoomAnswer answer)) continue;
                _done.Add(pair.Key);
                _accounted.Add(pair.Key);
                if (answer.Outcome != RoomOutcomes.Applied) continue;
                string scope = r.Kind == RoomWallet.Hold ? r.RequestId : r.Ids != null && r.Ids.Length > 0 ? r.Ids[0] : null;
                _decorations.TryGetValue(Key(seen.Player, scope ?? string.Empty), out Decoration d);
                switch (r.Kind)
                {
                    case RoomWallet.Deposit: _deposits += r.Amount; break;
                    case RoomWallet.Hold:
                        if (d != null) d.HeldApplied = r.Amount; else _holdsOther += r.Amount;
                        break;
                    case RoomWallet.Release:
                        if (d != null) d.Released += r.Amount; else _releasesOther += r.Amount;
                        break;
                    case RoomWallet.Use:
                        if (scope != null) _usedScopes.Add(Key(seen.Player, scope));
                        break;
                }
            }
            foreach (string key in _done) _pending.Remove(key);
            Unanswered = _pending.Count;

            string fail = null;
            // 꾸밈 허락으로 생긴 잡기가 쓰였는데 꾸밈 쓰기에 이어지지 않았다: 꾸밈 하나에 잡기가 둘(따로 적힌 이중 청구)이거나 까닭 없는 지출이다.
            foreach (string key in _mustBeDecoration)
                if (_usedScopes.Contains(key) && !_decorations.ContainsKey(key))
                    fail ??= "꾸밈 허락으로 생긴 방 지갑 잡기가 꾸밈 쓰기에 이어지지 않은 채 쓰였다(따로 적힌 이중 청구 또는 까닭 없는 지출): " + key.Replace('\n', ' ');
            long decorations = 0;
            foreach (Decoration d in _decorations.Values)
            {
                int held = Math.Max(0, d.HeldApplied);
                if (d.Landed && !d.Unknown)
                {
                    if (d.HeldApplied < 0 || d.Released > 0)
                        fail ??= "꾸밈이 방에 적용됐는데 그 돈을 방 지갑이 잡지 않았거나 돌려주었다(덜 청구): " + d.Player + " 꾸밈 " + d.Hold + " — 잡은 돈 " + d.HeldApplied + ", 돌려준 돈 " + d.Released + ", 비용 " + d.Recomputed;
                    else if (held != d.Recomputed)
                        fail ??= "꾸밈으로 방 지갑이 잡은 돈과 방이 바꾼 것의 비용이 다르다: " + d.Player + " 꾸밈 " + d.Hold + " — 잡은 돈 " + held + ", 다시 계산한 비용 " + d.Recomputed;
                    decorations += d.Recomputed;
                }
                else decorations += held - d.Released;
            }

            long expected = _base + _deposits + _releasesOther - _holdsOther - decorations;
            LastCompared = !(_base < 0 || atCap);
            if (!LastCompared)
            {
                // 기준: 지금 잔액이 지금까지 센 것을 모두 반영한 값이다.
                _base = balance - (_deposits + _releasesOther - _holdsOther - decorations);
                return fail;
            }
            if (balance != expected)
                fail ??= "방 지갑의 잔액이 알려진 지출과 맞지 않는다: 잔액 " + balance + ", 셈 " + expected + " (기준 " + _base + " + 넣기 " + _deposits + " + 꾸밈 아닌 풀기 " + _releasesOther
                    + " − 꾸밈 아닌 잡기 " + _holdsOther + " − 꾸밈 " + decorations + ")";
            return fail;
        }

        // ---------- 걸음의 지출을 값과 짝짓기(순수 함수 — 시뮬레이션과 단위 테스트가 같은 코드를 부른다) ----------

        /// <summary>방 지갑 쓰기 하나(받기 하나에서 새로 적힌 것): 범위(잡기 id)의 까닭과 쓴 돈.</summary>
        public readonly struct WalletUse
        {
            public WalletUse(string reason, int amount) { Reason = reason; Amount = amount; }
            public string Reason { get; }
            public int Amount { get; }
        }

        /// <summary>파는 가구·꾸밈의 해금 표 → 값(정의의 Price). 같은 표를 둘이 쓰고 값이 다르면 -1(짝짓지 않는다).</summary>
        public static Dictionary<string, int> PricesByToken(IEnumerable<Rootborn.Core.Placement.FurnitureDefinition> furniture, IEnumerable<Rootborn.Core.Building.RoomFinishDefinition> finishes)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            void Put(string token, int price) => map[token] = map.TryGetValue(token, out int known) && known != price ? -1 : price;
            if (furniture != null) foreach (var f in furniture) if (f != null && f.IsForSale) Put(f.UnlockToken, f.Price);
            if (finishes != null) foreach (var f in finishes) if (f != null && f.IsForSale) Put(f.UnlockToken, f.Price);
            return map;
        }

        /// <summary>
        /// 받기 하나 동안 걸음의 행동으로 잡은 방 지갑 돈이 쓰였을 때 그 까닭과 값을 맞춘다. 문제가 없으면 빈 목록.
        /// - 걸음 까닭이 아닌 쓰기(꾸밈 허락 등)는 보지 않는다(꾸밈은 방의 기록으로 따로 견준다).
        /// - 같은 받기에 새로 생긴 파는 가구·꾸밈의 해금이 없으면 짝이 없는 지출이다(시설 고르기·일·시설 올리기·탐색 선택·일터 템플릿처럼
        ///   값을 해금으로 짝지을 수 없는 종류도 여기서 실패한다 — 그 종류의 짝짓기를 더할 때까지 받아들이지 않는다). 글에 행동 종류를 적는다.
        /// - 해금 수와 쓰기 수가 다르면 실패(사기 하나에 쓰기 하나). 쓴 돈 합 ≠ 해금한 것의 값 합이면 실패(더 냄·덜 냄). 값을 모르는 해금 표(-1)는 짝짓지 못한 것으로 실패.
        /// 지금 시뮬레이션의 같이 쓰는 지갑 방에서는 걸음의 방 지갑 지출이 지나지 않는다(가게에서 사는 걸음이 없다) — 이 판정은 단위 테스트(RoomMoneyAndRejoinChecksTests)가 본다.
        /// </summary>
        public static List<string> PairStepSpends(IReadOnlyList<WalletUse> uses, IEnumerable<string> newTokens, IReadOnlyDictionary<string, int> priceByToken, string actionKind)
        {
            var problems = new List<string>();
            int spent = 0, count = 0;
            foreach (WalletUse u in uses)
            {
                if (u.Reason == null || !u.Reason.StartsWith(WindowStep, StringComparison.Ordinal)) continue;
                spent += u.Amount;
                count++;
            }
            if (count == 0) return problems;
            int price = 0, bought = 0;
            var unknown = new List<string>();
            foreach (string token in newTokens)
            {
                if (priceByToken == null || !priceByToken.TryGetValue(token, out int p)) continue; // 파는 것이 아닌 해금(보상 등)은 사기가 아니다
                if (p < 0) { unknown.Add(token); continue; }
                price += p;
                bought++;
            }
            if (unknown.Count > 0) problems.Add("값을 정할 수 없는 해금(같은 표에 값이 둘): " + string.Join(", ", unknown));
            if (bought == 0)
            {
                problems.Add("방 지갑에서 쓴 돈의 까닭을 짝짓지 못했다(가구·꾸밈 사기가 아니다 — 값을 데이터로 짝지을 수 없는 종류): 행동 종류 " + (actionKind ?? "-") + ", 쓴 돈 " + spent + "(쓰기 " + count + ")");
                return problems;
            }
            if (bought != count) problems.Add("사기 하나에 방 지갑 쓰기 하나여야 한다: 해금 " + bought + ", 쓰기 " + count + "(행동 종류 " + (actionKind ?? "-") + ")");
            if (price != spent) problems.Add("가구·꾸밈을 사며 방 지갑에서 쓴 돈이 정의의 값과 다르다: 쓴 돈 " + spent + ", 해금한 것의 값 " + price + "(행동 종류 " + (actionKind ?? "-") + ")");
            return problems;
        }

        public delegate bool TryAnswer(string player, string requestId, out RoomAnswer answer);

        private static string Strip(string key) => key.Substring(key.IndexOf('\n') + 1);
    }
}
