using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;
using Rootborn.Core.Save;

namespace Rootborn.Core.Room
{
    /// <summary>
    /// 방 지갑(돈 공유를 켠 방, 멀티플레이 설계 4.1절). 방이 주인이다. 요청 종류:
    /// - <see cref="Deposit"/>(Amount): 넣는다. 상한(<see cref="PersonalWallet.MaxBalance"/>)을 넘는 만큼은 버린다. 거절하지 않는다.
    /// - <see cref="Hold"/>(Amount): 쓸 돈을 잡아 둔다. 잔액이 모자라면 거절(먼저 온 요청만 된다, 음수가 되지 않는다). 잡은 돈은 잔액에서 빠진다.
    /// - <see cref="Use"/>(Ids[0] = 잡은 요청 id): 잡은 돈을 썼다(플레이어 쪽이 물건을 받았다). 잔액은 그대로다.
    /// - <see cref="Release"/>(Ids[0] = 잡은 요청 id): 잡은 돈을 쓰지 않았다(플레이어 쪽이 적용하지 못했다). 잔액으로 돌린다.
    /// 잡기 → 쓰기/풀기는 "방이 허락한 뒤에만 개인 쪽을 적용한다"를 위한 것이다(설계 3.2절). 개인 쪽을 먼저 바꾸고 되돌리는 흐름은 없다.
    /// 잡은 돈은 그 플레이어만 쓰거나 풀 수 있다. 누가 언제 얼마를 바꿨는지 최근 기록을 남긴다(화면이 보일 수 있게).
    /// </summary>
    public sealed class RoomWallet : IRoomPiece
    {
        public const string PieceName = "wallet";
        public const string Deposit = "deposit";
        public const string Hold = "hold";
        public const string Use = "use";
        public const string Release = "release";

        /// <summary>최근 기록 개수.</summary>
        public const int RecentCapacity = 20;

        private readonly Dictionary<string, RoomWalletHoldSaveData> _holds = new Dictionary<string, RoomWalletHoldSaveData>(StringComparer.Ordinal);
        private readonly List<RoomWalletEntrySaveData> _recent = new List<RoomWalletEntrySaveData>(RecentCapacity + 1);

        public string Name => PieceName;
        public int Revision { get; private set; }
        public int Balance { get; private set; }
        public int HeldTotal
        {
            get
            {
                long sum = 0;
                foreach (var h in _holds.Values) sum += h.Amount;
                return (int)Math.Min(int.MaxValue, sum);
            }
        }

        public IReadOnlyList<RoomWalletEntrySaveData> Recent => _recent;

        public RoomAnswer Apply(string playerId, RoomRequestSaveData request, int day)
        {
            if (request == null) return Answer(RoomOutcomes.Invalid);
            switch (request.Kind)
            {
                case Deposit:
                    if (request.Amount <= 0) return Answer(RoomOutcomes.Invalid);
                    int added = (int)Math.Min(request.Amount, (long)PersonalWallet.MaxBalance - Balance);
                    if (added > 0) Change(playerId, day, added);
                    return Answer(RoomOutcomes.Applied);
                case Hold:
                    if (request.Amount <= 0) return Answer(RoomOutcomes.Invalid);
                    if (request.Amount > Balance) return Answer(RoomOutcomes.Refused);
                    _holds[request.RequestId] = new RoomWalletHoldSaveData { RequestId = request.RequestId, PlayerId = playerId, Amount = request.Amount };
                    Change(playerId, day, -request.Amount);
                    return Answer(RoomOutcomes.Applied);
                case Use:
                case Release:
                    string holdId = request.Ids != null && request.Ids.Length > 0 ? request.Ids[0] : null;
                    if (holdId == null || !_holds.TryGetValue(holdId, out var hold) || !string.Equals(hold.PlayerId, playerId, StringComparison.Ordinal))
                        return Answer(RoomOutcomes.Invalid);
                    _holds.Remove(holdId);
                    if (request.Kind == Release) Change(playerId, day, hold.Amount);
                    else Revision++;
                    return Answer(RoomOutcomes.Applied);
                default:
                    return Answer(RoomOutcomes.Invalid);
            }
        }

        public RoomPieceState State() => new RoomPieceState(PieceName, Balance, Array.Empty<string>());

        public void Load(ISaveStore store, string saveSlot)
        {
            _holds.Clear();
            _recent.Clear();
            Balance = 0;
            if (store == null || !store.TryRead(saveSlot, SaveFileNames.RoomPiece(PieceName), out RoomWalletSaveData data)) return;
            Balance = Math.Max(0, data.Balance);
            if (data.Holds != null)
                foreach (var h in data.Holds)
                    if (h != null && !string.IsNullOrEmpty(h.RequestId) && h.Amount > 0) _holds[h.RequestId] = h;
            if (data.Recent != null)
                foreach (var e in data.Recent)
                    if (e != null && _recent.Count < RecentCapacity) _recent.Add(e);
        }

        public void Save(ISaveStore store, string saveSlot)
        {
            if (store == null) return;
            store.Write(saveSlot, SaveFileNames.RoomPiece(PieceName), new RoomWalletSaveData
            {
                Version = RoomWalletSaveData.KnownVersion,
                Balance = Balance,
                Holds = new List<RoomWalletHoldSaveData>(_holds.Values).ToArray(),
                Recent = _recent.ToArray(),
            });
        }

        private void Change(string playerId, int day, int change)
        {
            Balance += change;
            Revision++;
            _recent.Insert(0, new RoomWalletEntrySaveData { PlayerId = playerId, Day = day, Change = change });
            if (_recent.Count > RecentCapacity) _recent.RemoveAt(_recent.Count - 1);
        }

        private RoomAnswer Answer(string outcome) => new RoomAnswer(PieceName, string.Empty, outcome, Balance);
    }

    [Serializable]
    public sealed class RoomWalletSaveData : IVersionedSave
    {
        public const int KnownVersion = 1;

        public int Version;
        public int Balance;
        public RoomWalletHoldSaveData[] Holds = Array.Empty<RoomWalletHoldSaveData>();
        public RoomWalletEntrySaveData[] Recent = Array.Empty<RoomWalletEntrySaveData>();

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }

    [Serializable]
    public sealed class RoomWalletHoldSaveData
    {
        public string RequestId;
        public string PlayerId;
        public int Amount;
    }

    /// <summary>최근 기록 한 줄: 누가, 방의 몇째 날에, 잔액을 얼마나 바꿨는지(넣으면 양수, 잡으면 음수).</summary>
    [Serializable]
    public sealed class RoomWalletEntrySaveData
    {
        public string PlayerId;
        public int Day;
        public int Change;
    }

    /// <summary>
    /// 방 지갑을 플레이어 쪽에서 주는 쪽(<see cref="IWallet"/>). 가방(ItemInventory)과 규칙 코드는 개인 지갑과 구별하지 않는다.
    /// - 잔액은 방이 마지막으로 알린 잔액 + 내가 보냈고 아직 답을 받지 않은 넣기·풀기다.
    /// - 넣기(cost 0): 바로 적용하고 요청을 적는다(방은 넣기를 거절하지 않으므로 되돌릴 일이 없다).
    /// - 쓰기(cost &gt; 0): 방이 허락한 뒤에만 된다. 명령 하나를 범위(<see cref="BeginScope"/>)로 감싸고, 처음에는 다른 확인을 모두 지나도록
    ///   "될 수 있다"고 답한 뒤 실제 바꾸기에서 거절하며 잡기 요청을 적는다(<see cref="WasAsked"/>). 방이 잡아 주면(<see cref="Granted"/>)
    ///   같은 범위로 명령을 다시 하면 그때 그 잡은 돈으로 바뀐다(쓰기 요청을 적는다). 다시 해도 안 되면 <see cref="ReleaseGrant"/>.
    /// 범위 밖의 쓰기는 거절한다(방에 묻지 않고 돈을 쓰는 길이 없게).
    /// </summary>
    public sealed class RoomWalletClient : IWallet, IRoomPieceClient
    {
        private readonly RoomOutbox _outbox;
        private readonly Dictionary<string, int> _grants = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> _asked = new HashSet<string>(StringComparer.Ordinal);
        private string _scope;
        private bool _inProgress;
        private int _roomBalance;

        public RoomWalletClient(RoomOutbox outbox)
        {
            _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        }

        public string Name => RoomWallet.PieceName;

        /// <summary>방이 잔액을 알렸다(들어온 뒤 첫 상태를 받았다).</summary>
        public bool Known { get; private set; }

        /// <summary>방이 마지막으로 알린 잔액.</summary>
        public int RoomBalance => _roomBalance;

        public int Balance
        {
            get
            {
                long sum = _roomBalance;
                foreach (var r in _outbox.Requests)
                {
                    if (r.Piece != RoomWallet.PieceName) continue;
                    if (r.Kind == RoomWallet.Deposit || r.Kind == RoomWallet.Release) sum += r.Amount;
                }

                return (int)Math.Max(0, Math.Min(PersonalWallet.MaxBalance, sum));
            }
        }

        /// <summary>쓸 수 있다고 볼 돈: 잔액에서 답을 기다리는 잡기를 뺀 것.</summary>
        private long Available
        {
            get
            {
                long sum = Balance;
                foreach (var r in _outbox.Requests)
                    if (r.Piece == RoomWallet.PieceName && r.Kind == RoomWallet.Hold) sum -= r.Amount;
                return sum;
            }
        }

        public event Action Changed;

        /// <summary>방이 잡기를 허락했다(범위 id). 같은 범위로 명령을 다시 할 차례다.</summary>
        public event Action<string> Granted;

        /// <summary>방이 잡기를 거절했다(범위 id, 돈이 모자람). 개인 쪽은 바뀐 것이 없다.</summary>
        public event Action<string> Refused;

        /// <summary>허락받고 아직 쓰지도 풀지도 않은 잡기.</summary>
        public IReadOnlyCollection<string> Grants => _grants.Keys;

        public bool HasGrant(string scope) => scope != null && _grants.ContainsKey(scope);

        public bool WasAsked(string scope) => scope != null && _asked.Contains(scope);

        public void BeginScope(string scope)
        {
            if (string.IsNullOrEmpty(scope)) throw new ArgumentException("범위 id가 비어 있다", nameof(scope));
            if (_scope != null) throw new InvalidOperationException("범위 안에서 다른 범위를 열 수 없다");
            _scope = scope;
        }

        public void EndScope() => _scope = null;

        public bool CanExchange(int cost, int reward)
        {
            if (_inProgress || cost < 0 || reward < 0) return false;
            if ((long)Balance - cost + reward > PersonalWallet.MaxBalance) return false;
            if (cost == 0) return true;
            if (_scope == null) return false;
            if (_grants.TryGetValue(_scope, out int granted)) return granted == cost;
            return !_asked.Contains(_scope) && cost <= Available;
        }

        public bool TryExchange(int cost, int reward, Action commit)
        {
            if (!CanExchange(cost, reward)) return false;
            if (cost > 0 && !_grants.ContainsKey(_scope))
            {
                // 아직 허락받지 않았다: 잡기를 적고 거절한다. 다른 확인은 이미 지났으므로 허락받으면 같은 명령이 된다.
                _asked.Add(_scope);
                _outbox.Add(new RoomRequestSaveData { Piece = RoomWallet.PieceName, RequestId = _scope, Kind = RoomWallet.Hold, Amount = cost });
                return false;
            }

            _inProgress = true;
            try
            {
                commit?.Invoke();
                if (cost > 0)
                {
                    _grants.Remove(_scope);
                    _outbox.Add(new RoomRequestSaveData
                    {
                        Piece = RoomWallet.PieceName, RequestId = _outbox.NewId(), Kind = RoomWallet.Use, Amount = cost, Ids = new[] { _scope },
                    });
                }

                if (reward > 0) _outbox.Add(new RoomRequestSaveData { Piece = RoomWallet.PieceName, RequestId = _outbox.NewId(), Kind = RoomWallet.Deposit, Amount = reward });
                Changed?.Invoke();
                return true;
            }
            finally { _inProgress = false; }
        }

        /// <summary>허락받은 잡기를 쓰지 않는다(다시 한 명령이 되지 않았다, 꺼졌다 켜져 다시 할 명령이 없다). 방이 잔액으로 돌린다.</summary>
        public bool ReleaseGrant(string scope)
        {
            if (scope == null || !_grants.TryGetValue(scope, out int amount)) return false;
            _grants.Remove(scope);
            _asked.Remove(scope);
            _outbox.Add(new RoomRequestSaveData { Piece = RoomWallet.PieceName, RequestId = _outbox.NewId(), Kind = RoomWallet.Release, Amount = amount, Ids = new[] { scope } });
            Changed?.Invoke();
            return true;
        }

        public void Receive(RoomPieceState state)
        {
            Known = true;
            if (_roomBalance == state.Amount) return;
            _roomBalance = state.Amount;
            Changed?.Invoke();
        }

        public void Receive(RoomRequestSaveData request, RoomAnswer answer)
        {
            if (request.Kind != RoomWallet.Hold)
            {
                Changed?.Invoke();
                return;
            }

            if (answer.Outcome == RoomOutcomes.Applied)
            {
                _grants[request.RequestId] = request.Amount;
                Granted?.Invoke(request.RequestId);
            }
            else
            {
                _asked.Remove(request.RequestId);
                Refused?.Invoke(request.RequestId);
            }

            Changed?.Invoke();
        }
    }
}
