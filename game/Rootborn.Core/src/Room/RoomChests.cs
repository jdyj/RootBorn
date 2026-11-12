using System;
using System.Collections.Generic;
using System.Globalization;
using Rootborn.Core.Inventory;
using Rootborn.Core.Save;

namespace Rootborn.Core.Room
{
    /// <summary>
    /// 조각끼리 잇기: 방 조각 묶음이 조각을 모두 만든 뒤 한 번 부른다(같이 쓰는 상자가 같이 쓰는 집의 가구를 보고, 집이 물건이 든 상자를 치우지 않게).
    /// </summary>
    public interface IRoomPieceLinks
    {
        /// <param name="find">조각 이름 → 그 묶음의 조각(없으면 null).</param>
        void Link(Func<string, IRoomPiece> find);
    }

    /// <summary>
    /// 같이 쓰는 상자(멀티플레이 설계 4.1절). 같이 쓰는 집(<see cref="RoomHouse"/>)에 놓인 상자의 내용물은 방의 것이다 — 집을 같이 쓰면 늘 함께 켜진다
    /// (방 설정에서 따로 고르지 않는다, <see cref="RoomPieceCatalog"/>의 따라 켜지는 조각). 가방은 여전히 각자이고, 아이템을 나누는 길은 상자다.
    /// 요청은 지갑처럼 "방이 허락한 뒤에만 가방이 바뀐다"를 위한 잡기 → 쓰기/풀기다(되돌리는 흐름 없음, 설계 3.2절). 요청 종류(Ids의 뜻):
    /// - <see cref="Put"/>(Ids = 상자 인스턴스 id, 아이템 id; Amount = 수): 넣을 자리를 잡는다. 칸이 모자라면 거절(잡힌 자리도 센다 — 먼저 온 요청만 된다).
    /// - <see cref="Take"/>(같은 모양): 꺼낼 물건을 잡는다. 상자에서 빠지고(남은 칸 판정에는 계속 센다) 그 사람만 쓰거나 풀 수 있다. 모자라면 거절.
    /// - <see cref="Use"/>(Ids[0] = 잡은 요청 id): 플레이어 쪽이 가방을 바꿨다. 넣기면 물건이 상자에 들어가고, 꺼내기면 잡은 것이 사라진다(가방으로 갔다).
    /// - <see cref="Release"/>(Ids[0] = 잡은 요청 id): 가방을 바꾸지 못했다. 넣기면 자리를 풀고, 꺼내기면 물건이 상자로 돌아온다.
    /// 잡힌 자리와 물건을 늘 칸 판정에 세므로 쓰기·풀기는 칸이 모자라 실패하지 않는다. 물건이 들었거나 잡힌 것이 있는 상자는 집이 치우지 않는다.
    /// 아이템의 쌓기 수는 방의 집 규칙(<see cref="RoomHouseRules"/> — 서버도 데이터를 읽는다)에서 온다. 모르는 아이템은 받지 않는다.
    /// </summary>
    public sealed class RoomChests : IRoomPiece, IRoomPieceLinks
    {
        public const string PieceName = "chests";
        public const string Put = "put";
        public const string Take = "take";
        public const string Use = "use";
        public const string Release = "release";

        private readonly RoomHouseRules _rules;
        private readonly SortedDictionary<string, List<RoomChestItemSaveData>> _chests = new SortedDictionary<string, List<RoomChestItemSaveData>>(StringComparer.Ordinal);
        private readonly Dictionary<string, RoomChestHoldSaveData> _holds = new Dictionary<string, RoomChestHoldSaveData>(StringComparer.Ordinal);
        private RoomHouse _house;

        public RoomChests(RoomHouseRules rules = null)
        {
            _rules = rules;
        }

        public string Name => PieceName;
        public int Revision { get; private set; }

        public void Link(Func<string, IRoomPiece> find)
        {
            _house = find?.Invoke(RoomHouse.PieceName) as RoomHouse;
            _house?.GuardRemoval(HoldsAnything);
        }

        /// <summary>그 상자에 물건이 들었거나 누가 잡아 둔 것이 있다(치울 수 없다).</summary>
        public bool HoldsAnything(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return false;
            if (_chests.TryGetValue(instanceId, out var items) && items.Count > 0) return true;
            foreach (var h in _holds.Values) if (h.Chest == instanceId) return true;
            return false;
        }

        /// <summary>그 상자에 보이는 그 아이템 수(잡힌 것은 빼고).</summary>
        public int CountOf(string instanceId, string itemId)
        {
            if (!_chests.TryGetValue(instanceId ?? string.Empty, out var items)) return 0;
            int sum = 0;
            foreach (var i in items) if (i.ItemId == itemId) sum += i.Count;
            return sum;
        }

        public int HeldCount => _holds.Count;

        public RoomAnswer Apply(string playerId, RoomRequestSaveData request, int day)
        {
            if (request == null || request.Ids == null) return Answer(RoomOutcomes.Invalid);
            foreach (var id in request.Ids)
                if (string.IsNullOrEmpty(id) || id.IndexOf(RoomHouse.Separator) >= 0) return Answer(RoomOutcomes.Invalid);

            switch (request.Kind)
            {
                case Put:
                case Take:
                {
                    if (request.Ids.Length != 2 || request.Amount <= 0) return Answer(RoomOutcomes.Invalid);
                    string chest = request.Ids[0], item = request.Ids[1];
                    int slots = SlotsOf(chest);
                    if (slots <= 0) return Answer(RoomOutcomes.Refused); // 상자가 없다(누가 치웠다) 또는 상자가 아니다
                    if (_rules == null || _rules.MaxStackOf(item) <= 0) return Answer(RoomOutcomes.Invalid);
                    var items = Items(chest);
                    if (request.Kind == Put)
                    {
                        var rewards = Reserved(chest);
                        rewards.Add(new ItemAmount<string>(item, request.Amount));
                        if (!Plan(items, slots, null, rewards, out _)) return Answer(RoomOutcomes.Refused);
                    }
                    else
                    {
                        if (!Plan(items, slots, new[] { new ItemAmount<string>(item, request.Amount) }, null, out var after)) return Answer(RoomOutcomes.Refused);
                        Store(chest, after);
                    }

                    _holds[request.RequestId] = new RoomChestHoldSaveData
                    {
                        RequestId = request.RequestId, PlayerId = playerId ?? string.Empty, Chest = chest, Kind = request.Kind, ItemId = item, Count = request.Amount,
                    };
                    Revision++;
                    return Answer(RoomOutcomes.Applied);
                }
                case Use:
                case Release:
                {
                    if (request.Ids.Length != 1 || !_holds.TryGetValue(request.Ids[0], out var hold)
                        || !string.Equals(hold.PlayerId, playerId ?? string.Empty, StringComparison.Ordinal)) return Answer(RoomOutcomes.Invalid);
                    bool returnsItems = (request.Kind == Use) == (hold.Kind == Put);
                    if (returnsItems)
                    {
                        // 넣기를 썼다 / 꺼내기를 풀었다: 물건이 상자에 들어간다. 자리는 잡혀 있었으므로 늘 들어간다(혹시 안 되면 잡은 채로 둔다 — 잃지 않는다).
                        int slots = Math.Max(SlotsOf(hold.Chest), StoredSlots(hold.Chest));
                        if (!Plan(Items(hold.Chest), slots, null, new[] { new ItemAmount<string>(hold.ItemId, hold.Count) }, out var after))
                            return Answer(RoomOutcomes.Refused);
                        Store(hold.Chest, after);
                    }

                    _holds.Remove(hold.RequestId);
                    Revision++;
                    return Answer(RoomOutcomes.Applied);
                }
                default:
                    return Answer(RoomOutcomes.Invalid);
            }
        }

        /// <summary>상태: 상자마다 보이는 칸 한 줄씩 "인스턴스|아이템|수"(칸 순서). Amount는 줄 수.</summary>
        public RoomPieceState State()
        {
            var lines = new List<string>(16);
            foreach (var pair in _chests)
                foreach (var item in pair.Value)
                    lines.Add(RoomChestLine.Encode(pair.Key, item.ItemId, item.Count));
            return new RoomPieceState(PieceName, lines.Count, lines.ToArray());
        }

        public void Load(ISaveStore store, string saveSlot)
        {
            _chests.Clear();
            _holds.Clear();
            if (store == null || !store.TryRead(saveSlot, SaveFileNames.RoomPiece(PieceName), out RoomChestsSaveData data)) return;
            if (data.Chests != null)
                foreach (var c in data.Chests)
                {
                    if (c == null || string.IsNullOrEmpty(c.InstanceId) || c.Items == null) continue;
                    var items = new List<RoomChestItemSaveData>(c.Items.Length);
                    foreach (var i in c.Items) if (i != null && !string.IsNullOrEmpty(i.ItemId) && i.Count > 0) items.Add(i);
                    if (items.Count > 0) _chests[c.InstanceId] = items;
                }

            if (data.Holds != null)
                foreach (var h in data.Holds)
                    if (h != null && !string.IsNullOrEmpty(h.RequestId) && h.Count > 0 && !string.IsNullOrEmpty(h.Chest)) _holds[h.RequestId] = h;
        }

        public void Save(ISaveStore store, string saveSlot)
        {
            if (store == null) return;
            var chests = new List<RoomChestSaveData>(_chests.Count);
            foreach (var pair in _chests)
                if (pair.Value.Count > 0) chests.Add(new RoomChestSaveData { InstanceId = pair.Key, Items = pair.Value.ToArray() });
            store.Write(saveSlot, SaveFileNames.RoomPiece(PieceName), new RoomChestsSaveData
            {
                Version = RoomChestsSaveData.KnownVersion,
                Chests = chests.ToArray(),
                Holds = new List<RoomChestHoldSaveData>(_holds.Values).ToArray(),
            });
        }

        // 상자의 칸 수: 같이 쓰는 집에 놓인 가구이고 그 가구가 상자일 때만.
        private int SlotsOf(string instanceId)
        {
            if (_house == null || _rules == null || !_house.TryFind(instanceId, out var placement)) return 0;
            return _rules.StorageSlotsOf(placement.FurnitureId);
        }

        private int StoredSlots(string chest) => _chests.TryGetValue(chest, out var items) ? items.Count + 1 : 1;

        private List<ItemStack<string>> Items(string chest)
        {
            var stacks = new List<ItemStack<string>>(8);
            if (_chests.TryGetValue(chest, out var items))
                foreach (var i in items) stacks.Add(new ItemStack<string>(i.ItemId, i.Count));
            return stacks;
        }

        // 그 상자에 잡혀 있는 것(넣기 자리와 꺼낸 물건) — 남은 칸 판정에 늘 센다.
        private List<ItemAmount<string>> Reserved(string chest)
        {
            var list = new List<ItemAmount<string>>(4);
            foreach (var h in _holds.Values)
                if (h.Chest == chest) list.Add(new ItemAmount<string>(h.ItemId, h.Count));
            return list;
        }

        private bool Plan(List<ItemStack<string>> items, int slots, IReadOnlyList<ItemAmount<string>> costs, IReadOnlyList<ItemAmount<string>> rewards,
            out List<ItemStack<string>> next)
            => InventoryPlanner.TryPlanExchange(items, slots, costs, rewards, id => Math.Max(1, _rules?.MaxStackOf(id) ?? 1), out next);

        private void Store(string chest, List<ItemStack<string>> stacks)
        {
            var items = new List<RoomChestItemSaveData>(stacks.Count);
            foreach (var s in stacks)
                if (!string.IsNullOrEmpty(s.Item) && s.Count > 0) items.Add(new RoomChestItemSaveData { ItemId = s.Item, Count = s.Count });
            if (items.Count == 0) _chests.Remove(chest);
            else _chests[chest] = items;
        }

        private static RoomAnswer Answer(string outcome) => new RoomAnswer(PieceName, string.Empty, outcome, 0);
    }

    [Serializable]
    public sealed class RoomChestsSaveData : IVersionedSave
    {
        public const int KnownVersion = 1;

        public int Version;
        public RoomChestSaveData[] Chests = Array.Empty<RoomChestSaveData>();
        public RoomChestHoldSaveData[] Holds = Array.Empty<RoomChestHoldSaveData>();

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }

    /// <summary>같이 쓰는 집의 상자 하나(방이 준 인스턴스 id)와 보이는 칸들.</summary>
    [Serializable]
    public sealed class RoomChestSaveData
    {
        public string InstanceId = string.Empty;
        public RoomChestItemSaveData[] Items = Array.Empty<RoomChestItemSaveData>();
    }

    [Serializable]
    public sealed class RoomChestItemSaveData
    {
        public string ItemId = string.Empty;
        public int Count;
    }

    /// <summary>잡아 둔 넣기 자리나 꺼낸 물건: 잡은 요청 id, 누가, 어느 상자, 넣기·꺼내기, 아이템, 수.</summary>
    [Serializable]
    public sealed class RoomChestHoldSaveData
    {
        public string RequestId = string.Empty;
        public string PlayerId = string.Empty;
        public string Chest = string.Empty;
        public string Kind = string.Empty;
        public string ItemId = string.Empty;
        public int Count;
    }

    /// <summary>상자 상태 한 줄 "인스턴스|아이템|수".</summary>
    public static class RoomChestLine
    {
        public static string Encode(string instanceId, string itemId, int count)
            => instanceId + RoomHouse.Separator + itemId + RoomHouse.Separator + count.ToString(CultureInfo.InvariantCulture);

        public static bool TryDecode(string line, out string instanceId, out string itemId, out int count)
        {
            instanceId = itemId = null;
            count = 0;
            if (string.IsNullOrEmpty(line)) return false;
            var parts = line.Split(RoomHouse.Separator);
            if (parts.Length != 3 || parts[0].Length == 0 || parts[1].Length == 0) return false;
            if (!int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out count) || count <= 0) return false;
            instanceId = parts[0];
            itemId = parts[1];
            return true;
        }
    }

    /// <summary>
    /// 같이 쓰는 상자를 플레이어 쪽에서 비춘다. 방이 알린 상자마다의 칸(<see cref="ContentsOf"/>)을 갖고, 넣기·꺼내기를 잡기 요청으로 적는다.
    /// 게임 한 판은 허락(<see cref="Answered"/>)이 오면 가방을 바꾸고 같은 바꾸기 안에서 쓰기를, 바꾸지 못하면 풀기를 적는다(<see cref="AddUse"/>, <see cref="AddRelease"/>).
    /// </summary>
    public sealed class RoomChestsClient : IRoomPieceClient
    {
        private readonly RoomOutbox _outbox;
        private readonly Dictionary<string, List<ItemStack<string>>> _contents = new Dictionary<string, List<ItemStack<string>>>(StringComparer.Ordinal);
        private string[] _lines = Array.Empty<string>();
        private static readonly IReadOnlyList<ItemStack<string>> None = Array.Empty<ItemStack<string>>();

        public RoomChestsClient(RoomOutbox outbox)
        {
            _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        }

        public string Name => RoomChests.PieceName;

        /// <summary>방이 상자 상태를 알렸다.</summary>
        public bool Known { get; private set; }

        public event Action Changed;

        private Action<RoomRequestSaveData, bool> _answered;
        private readonly List<KeyValuePair<RoomRequestSaveData, bool>> _unheard = new List<KeyValuePair<RoomRequestSaveData, bool>>(2);

        /// <summary>
        /// 내 잡기 요청의 답이 왔다(그 요청, 허락됐는지). 듣는 쪽이 붙기 전에 온 답(게임 한 판이 세이브를 읽는 동안 맞춘 답)은 갖고 있다가
        /// 처음 붙을 때 넘긴다 — 허락받은 잡기를 쓰지도 풀지도 않은 채 잊지 않게.
        /// </summary>
        public event Action<RoomRequestSaveData, bool> Answered
        {
            add
            {
                _answered += value;
                if (_unheard.Count == 0 || value == null) return;
                var queued = new List<KeyValuePair<RoomRequestSaveData, bool>>(_unheard);
                _unheard.Clear();
                foreach (var pair in queued) _answered?.Invoke(pair.Key, pair.Value);
            }
            remove => _answered -= value;
        }

        /// <summary>그 상자에 방이 알린 칸들(아이템 id, 수).</summary>
        public IReadOnlyList<ItemStack<string>> ContentsOf(string instanceId)
            => instanceId != null && _contents.TryGetValue(instanceId, out var list) ? list : None;

        /// <summary>답을 기다리는 넣기·꺼내기가 있다.</summary>
        public bool HasPending
        {
            get
            {
                foreach (var r in _outbox.Requests)
                    if (r.Piece == RoomChests.PieceName && (r.Kind == RoomChests.Put || r.Kind == RoomChests.Take)) return true;
                return false;
            }
        }

        public string RequestPut(string instanceId, string itemId, int count) => AddHold(RoomChests.Put, instanceId, itemId, count);

        public string RequestTake(string instanceId, string itemId, int count) => AddHold(RoomChests.Take, instanceId, itemId, count);

        /// <summary>잡은 것을 썼다(가방을 바꿨다). 가방 바꾸기 안에서 부른다(던지지 않는다).</summary>
        public void AddUse(string holdId) => _outbox.Add(new RoomRequestSaveData { Piece = RoomChests.PieceName, RequestId = _outbox.NewId(), Kind = RoomChests.Use, Ids = new[] { holdId } });

        /// <summary>잡은 것을 쓰지 못했다(가방을 바꾸지 못했다).</summary>
        public void AddRelease(string holdId) => _outbox.Add(new RoomRequestSaveData { Piece = RoomChests.PieceName, RequestId = _outbox.NewId(), Kind = RoomChests.Release, Ids = new[] { holdId } });

        public void Receive(RoomPieceState state)
        {
            bool first = !Known;
            Known = true;
            if (!first && SameLines(state.Ids)) return;
            _lines = (string[])state.Ids.Clone();
            _contents.Clear();
            foreach (var line in state.Ids)
            {
                if (!RoomChestLine.TryDecode(line, out string chest, out string item, out int count)) continue;
                if (!_contents.TryGetValue(chest, out var list)) _contents[chest] = list = new List<ItemStack<string>>(8);
                list.Add(new ItemStack<string>(item, count));
            }

            Changed?.Invoke();
        }

        public void Receive(RoomRequestSaveData request, RoomAnswer answer)
        {
            if (request.Kind != RoomChests.Put && request.Kind != RoomChests.Take)
            {
                Changed?.Invoke();
                return;
            }

            bool applied = answer.Outcome == RoomOutcomes.Applied;
            if (_answered == null) _unheard.Add(new KeyValuePair<RoomRequestSaveData, bool>(request, applied));
            else _answered(request, applied);
        }

        private bool SameLines(string[] ids)
        {
            if (ids.Length != _lines.Length) return false;
            for (int i = 0; i < ids.Length; i++) if (!string.Equals(ids[i], _lines[i], StringComparison.Ordinal)) return false;
            return true;
        }

        private string AddHold(string kind, string instanceId, string itemId, int count)
        {
            if (string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(itemId) || instanceId.IndexOf(RoomHouse.Separator) >= 0
                || itemId.IndexOf(RoomHouse.Separator) >= 0 || count <= 0) throw new ArgumentException("상자 요청의 id나 수가 잘못됐다");
            string requestId = _outbox.NewId();
            _outbox.Add(new RoomRequestSaveData { Piece = RoomChests.PieceName, RequestId = requestId, Kind = kind, Amount = count, Ids = new[] { instanceId, itemId } });
            return requestId;
        }
    }
}
