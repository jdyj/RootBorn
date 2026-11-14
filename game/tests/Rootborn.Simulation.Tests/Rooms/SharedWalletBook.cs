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

        /// <summary>자리의 보낼 목록에서 본 지갑 요청(같은 id는 마지막에 본 것).</summary>
        public void See(string player, RoomRequestSaveData r)
        {
            if (r == null || r.Piece != RoomWallet.PieceName || string.IsNullOrEmpty(r.RequestId)) return;
            string key = Key(player, r.RequestId);
            if (_accounted.Contains(key)) return;
            _pending[key] = new Seen { Player = player, Request = r.Copy() };
            if (r.Kind == RoomWallet.Hold)
            {
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
                }
            }
            foreach (string key in _done) _pending.Remove(key);
            Unanswered = _pending.Count;

            string fail = null;
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
            if (_base < 0 || atCap)
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

        public delegate bool TryAnswer(string player, string requestId, out RoomAnswer answer);

        private static string Strip(string key) => key.Substring(key.IndexOf('\n') + 1);
    }
}
