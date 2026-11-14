using System;
using System.Collections.Generic;
using System.Globalization;
using Rootborn.Core.Building;
using Rootborn.Core.Save;
using Rootborn.Core.World;

namespace Rootborn.Core.Room
{
    /// <summary>
    /// 같이 쓰는 집의 꾸밈(벽지·바닥·띠 색, 집 꾸미기 설계 8절). 집(<see cref="RoomHouse"/>)을 같이 쓰면 늘 함께 켜진다(방 설정에서 따로 고르지 않는다).
    /// 방이 꾸밈 기록(혼자 하는 집과 같은 <see cref="HouseBuildSaveData"/>)을 갖고, 같은 Core 규칙(<see cref="HouseFinishes"/>)으로 요청을 다시 판정한다.
    /// 돈은 요청한 쪽이 내므로 상자(<see cref="RoomChests"/>)와 같은 잡기 → 쓰기/풀기다(되돌리는 흐름 없음). 요청 종류(Ids의 뜻):
    /// - <see cref="Finish"/>(Ids = 면, 꾸밈 id, 도구, x, y; Amount = 요청한 쪽이 본 바뀌는 칸 수): 덮는 칸을 잡는다. 꾸밈은 아직 바뀌지 않는다.
    ///   틀이 없거나 붙은 틀과 다르면, 바뀌는 것이 없거나 칸 수가 다르면, 덮는 칸이 다른 잡기와 같은 자리로 겹치면 거절 — 먼저 온 요청만 된다.
    /// - <see cref="Band"/>(Ids = 면, 띠 꾸밈 id; Amount = 1): 집 하나의 띠를 잡는다. 잡힌 띠가 있으면 거절.
    /// - <see cref="Use"/>(Ids[0] = 잡은 요청 id): 요청한 쪽이 돈을 냈다. 같은 꾸밈·도구·칸으로 다시 판정해 적용하고 잡기를 지운다.
    /// - <see cref="Release"/>(Ids[0] = 잡은 요청 id): 돈을 내지 못했다. 꾸밈은 그대로, 잡기만 지운다.
    /// 쓰기·풀기는 잡은 사람만 한다(아니면 Invalid). 해금은 요청한 사람의 진행이라 방은 확인하지 않는다(가구 놓기와 같다).
    /// 쓰기를 적용할 수 없으면(틀을 붙일 수 없음 — 규칙 없이 시작한 방·다른 틀이 먼저 붙음, 또는 다시 시작하는 사이 데이터에서 그 꾸밈이 지워짐)
    /// 잡기를 지우지 않고 Refused다: 돈은 이미 냈으므로 지우면 낸 돈만 사라진다(상자 조각과 같은 원칙 — 잡은 채로 둔다). 다시 쓰려면 새 요청 id로
    /// 쓰기를 보낸다(같은 id는 장부가 같은 Refused를 준다). 지워진 꾸밈은 잡기에 자리(벽지·바닥)가 없어 덮을 칸을 정할 수 없으므로 기록으로 적지 않는다.
    /// 잡힌 칸은 저장하지 않는다 — 틀만으로 정해지므로(<see cref="HouseFinishPreview.Cells"/>) 읽은 뒤 처음 필요할 때 다시 낸다. 꾸밈 id로 분기하지 않는다.
    /// </summary>
    public sealed class RoomHouseBuild : IRoomPiece
    {
        public const string PieceName = "house-build";
        public const string Finish = "finish";
        public const string Band = "band";
        public const string Use = "use";
        public const string Release = "release";

        private readonly RoomHouseRules _rules;
        private HouseFinishes _finishes = new HouseFinishes();
        private readonly SortedDictionary<string, RoomHouseBuildHoldSaveData> _holds = new SortedDictionary<string, RoomHouseBuildHoldSaveData>(StringComparer.Ordinal);
        // 잡힌 (자리, 칸). 잡기가 바뀔 때만 다시 만든다(null이면 다시 만들어야 한다).
        private HashSet<(string Slot, CellPos Cell)> _heldCells;

        /// <param name="rules">틀(면 → 짓는 집)과 꾸밈 정의. 없으면 꾸밈 요청을 판정할 수 없어 거절한다.</param>
        public RoomHouseBuild(RoomHouseRules rules = null)
        {
            _rules = rules;
        }

        public string Name => PieceName;
        public int Revision { get; private set; }
        public int HeldCount => _holds.Count;

        /// <summary>지금 꾸밈 기록(붙었으면 틀 id는 그 틀의 것, 아니면 읽은 값).</summary>
        public HouseBuildSaveData ToSaveData() => _finishes.ToSaveData();

        public RoomAnswer Apply(string playerId, RoomRequestSaveData request, int day)
        {
            if (request == null || request.Ids == null) return Answer(RoomOutcomes.Invalid);
            foreach (var id in request.Ids)
                if (string.IsNullOrEmpty(id) || id.IndexOf(RoomHouse.Separator) >= 0) return Answer(RoomOutcomes.Invalid);
            string player = playerId ?? string.Empty;

            switch (request.Kind)
            {
                case Finish:
                {
                    if (request.Ids.Length != 5 || request.Amount <= 0 || !TryNumber(request.Ids[3], out int x) || !TryNumber(request.Ids[4], out int y))
                        return Answer(RoomOutcomes.Invalid);
                    string surface = request.Ids[0], finishId = request.Ids[1], tool = request.Ids[2];
                    if (!IsTool(tool)) return Answer(RoomOutcomes.Invalid);
                    if (!TryFrame(surface)) return Answer(RoomOutcomes.Refused);
                    var def = _rules.FindFinish(finishId);
                    if (def == null || !IsCellSlot(def.Slot)) return Answer(RoomOutcomes.Invalid);
                    var preview = _finishes.Preview(finishId, tool, new CellPos(x, y), _ => true, _ => true);
                    if (!preview.CanApply || preview.ChangedCells.Count != request.Amount) return Answer(RoomOutcomes.Refused);
                    var held = HeldCells();
                    foreach (var cell in preview.Cells)
                        if (held.Contains((def.Slot, cell))) return Answer(RoomOutcomes.Refused);
                    AddHold(new RoomHouseBuildHoldSaveData
                    {
                        RequestId = request.RequestId ?? string.Empty, PlayerId = player, SurfaceId = surface, Kind = Finish, FinishId = finishId, Tool = tool,
                        X = x, Y = y, Count = request.Amount,
                    });
                    return Answer(RoomOutcomes.Applied);
                }
                case Band:
                {
                    if (request.Ids.Length != 2 || request.Amount != 1) return Answer(RoomOutcomes.Invalid);
                    string surface = request.Ids[0], finishId = request.Ids[1];
                    if (!TryFrame(surface)) return Answer(RoomOutcomes.Refused);
                    var def = _rules.FindFinish(finishId);
                    if (def == null || !string.Equals(def.Slot, RoomFinishSlots.Band, StringComparison.Ordinal)) return Answer(RoomOutcomes.Invalid);
                    if (!_finishes.PreviewBand(finishId, _ => true, _ => true).CanApply || BandHeld()) return Answer(RoomOutcomes.Refused);
                    AddHold(new RoomHouseBuildHoldSaveData
                    {
                        RequestId = request.RequestId ?? string.Empty, PlayerId = player, SurfaceId = surface, Kind = Band, FinishId = finishId, Count = 1,
                    });
                    return Answer(RoomOutcomes.Applied);
                }
                case Use:
                case Release:
                {
                    if (request.Ids.Length != 1 || !_holds.TryGetValue(request.Ids[0], out var hold)
                        || !string.Equals(hold.PlayerId, player, StringComparison.Ordinal)) return Answer(RoomOutcomes.Invalid);
                    if (request.Kind == Use && IsKnownHold(hold))
                    {
                        // 돈은 이미 냈다: 적용할 수 없으면 잡은 채로 거절한다(낸 돈이 꾸밈 없이 사라지지 않게 — 위 요약 참고).
                        if (!TryFrame(hold.SurfaceId)) return Answer(RoomOutcomes.Refused);
                        var def = _rules.FindFinish(hold.FinishId);
                        bool band = string.Equals(hold.Kind, Band, StringComparison.Ordinal);
                        if (def == null || (band ? !string.Equals(def.Slot, RoomFinishSlots.Band, StringComparison.Ordinal) : !IsCellSlot(def.Slot)))
                            return Answer(RoomOutcomes.Refused);
                        // 칸을 잡아 두었으므로 바뀌는 칸 수는 같다. 혹시 다르더라도 적용한다(돈은 이미 냈다).
                        var preview = band
                            ? _finishes.PreviewBand(hold.FinishId, _ => true, _ => true)
                            : _finishes.Preview(hold.FinishId, hold.Tool, new CellPos(hold.X, hold.Y), _ => true, _ => true);
                        _finishes.Apply(preview);
                    }

                    _holds.Remove(hold.RequestId);
                    _heldCells = null;
                    Revision++;
                    return Answer(RoomOutcomes.Applied);
                }
                default:
                    return Answer(RoomOutcomes.Invalid);
            }
        }

        /// <summary>상태 줄(<see cref="RoomHouseBuildLines"/>): 틀, 띠, 기록 칸마다, 잡기마다. Amount는 기록 칸 수.</summary>
        public RoomPieceState State()
        {
            var data = _finishes.ToSaveData();
            var lines = new List<string>(2 + data.Finishes.Length + _holds.Count);
            lines.Add(RoomHouseBuildLines.Frame(data.FrameId));
            lines.Add(RoomHouseBuildLines.BandLine(data.Band));
            foreach (var cell in data.Finishes) lines.Add(RoomHouseBuildLines.Cell(cell));
            foreach (var hold in _holds.Values) lines.Add(RoomHouseBuildLines.Hold(hold.RequestId, hold.PlayerId));
            return new RoomPieceState(PieceName, data.Finishes.Length, lines.ToArray());
        }

        public void Load(ISaveStore store, string saveSlot)
        {
            _finishes = new HouseFinishes();
            _holds.Clear();
            _heldCells = null;
            if (store == null) return;
            // 기록은 HouseFinishes.Load와 같은 규칙으로 모두 든다(데이터에 없는 id, 바닥이 아닌 칸, 다른 틀의 기록 — 같은 칸이면 얹는다).
            if (store.TryRead(saveSlot, SaveFileNames.RoomPiece(PieceName), out HouseBuildSaveData data)) _finishes.Load(data);
            if (store.TryRead(saveSlot, SaveFileNames.RoomHouseBuildHolds, out RoomHouseBuildHoldsSaveData holds) && holds.Holds != null)
                foreach (var h in holds.Holds)
                    if (h != null && !string.IsNullOrEmpty(h.RequestId) && !_holds.ContainsKey(h.RequestId)) _holds[h.RequestId] = h; // 모르는 종류도 버리지 않는다
        }

        /// <summary>꾸밈 기록과 잡기 두 파일을 쓴다(붙었든 아니든). <see cref="RoomPieceSet.Save"/>의 묶음 안에서 불린다.</summary>
        public void Save(ISaveStore store, string saveSlot)
        {
            if (store == null) return;
            store.Write(saveSlot, SaveFileNames.RoomPiece(PieceName), _finishes.ToSaveData());
            store.Write(saveSlot, SaveFileNames.RoomHouseBuildHolds, new RoomHouseBuildHoldsSaveData
            {
                Version = RoomHouseBuildHoldsSaveData.KnownVersion,
                Holds = new List<RoomHouseBuildHoldSaveData>(_holds.Values).ToArray(),
            });
        }

        // 그 면의 틀이 붙어 있게 한다: 처음이면 붙이고, 이미 다른 틀이 붙었으면 false. 붙을 때 보이는 틀 id가 바뀌면 상태가 바뀐 것이다.
        private bool TryFrame(string surfaceId)
        {
            var frame = _rules?.BuildFrameFor(surfaceId);
            if (frame == null) return false;
            if (_finishes.Attached) return string.Equals(_finishes.Frame.FrameId, frame.FrameId, StringComparison.Ordinal);
            string before = _finishes.ToSaveData().FrameId;
            _finishes.Attach(frame, _rules.FindFinish);
            _heldCells = null;
            if (!string.Equals(before, frame.FrameId, StringComparison.Ordinal)) Revision++;
            return true;
        }

        private void AddHold(RoomHouseBuildHoldSaveData hold)
        {
            _holds[hold.RequestId] = hold;
            _heldCells = null;
            Revision++;
        }

        private bool BandHeld()
        {
            foreach (var h in _holds.Values)
                if (string.Equals(h.Kind, Band, StringComparison.Ordinal)) return true;
            return false;
        }

        // 잡힌 (자리, 칸): 칸 잡기마다 같은 꾸밈·도구·칸의 덮는 칸. 틀이 붙은 뒤에만 부른다. 데이터에 없는 꾸밈의 잡기는 칸을 막지 않는다.
        private HashSet<(string Slot, CellPos Cell)> HeldCells()
        {
            if (_heldCells != null) return _heldCells;
            var set = new HashSet<(string Slot, CellPos Cell)>();
            foreach (var h in _holds.Values)
            {
                if (!string.Equals(h.Kind, Finish, StringComparison.Ordinal)) continue;
                // 다른 틀의 면에서 잡은 것은 그 틀의 좌표다 — 붙은 틀의 칸을 막지 않는다.
                if (!string.Equals(_rules?.BuildFrameFor(h.SurfaceId)?.FrameId, _finishes.Frame.FrameId, StringComparison.Ordinal)) continue;
                var def = _rules?.FindFinish(h.FinishId);
                if (def == null || !IsCellSlot(def.Slot)) continue;
                var preview = _finishes.Preview(h.FinishId, h.Tool, new CellPos(h.X, h.Y), _ => true, _ => true);
                foreach (var cell in preview.Cells) set.Add((def.Slot, cell));
            }

            _heldCells = set;
            return set;
        }

        private static bool IsKnownHold(RoomHouseBuildHoldSaveData hold) =>
            string.Equals(hold.Kind, Finish, StringComparison.Ordinal) || string.Equals(hold.Kind, Band, StringComparison.Ordinal);

        private static bool IsTool(string tool) =>
            string.Equals(tool, HouseFinishTools.Room, StringComparison.Ordinal) || string.Equals(tool, HouseFinishTools.Brush, StringComparison.Ordinal);

        private static bool IsCellSlot(string slot) =>
            string.Equals(slot, RoomFinishSlots.Wall, StringComparison.Ordinal) || string.Equals(slot, RoomFinishSlots.Floor, StringComparison.Ordinal);

        private static bool TryNumber(string text, out int value) =>
            int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);

        private static RoomAnswer Answer(string outcome) => new RoomAnswer(PieceName, string.Empty, outcome, 0);
    }

    /// <summary>
    /// 같이 쓰는 집의 꾸밈 조각이 잡아 둔 꾸밈 요청들(<see cref="SaveFileNames.RoomHouseBuildHolds"/>). 필드 이름과 형은 바꾸지 않는다.
    /// 꾸밈 기록은 이 파일이 아니라 <see cref="HouseBuildSaveData"/>(<c>room-piece-house-build.json</c>)에 있다.
    /// </summary>
    [Serializable]
    public sealed class RoomHouseBuildHoldsSaveData : IVersionedSave
    {
        public const int KnownVersion = 1;

        public int Version;
        public RoomHouseBuildHoldSaveData[] Holds = Array.Empty<RoomHouseBuildHoldSaveData>();

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }

    /// <summary>
    /// 잡아 둔 꾸밈 요청 하나: 잡은 요청 id, 누가, 면, 종류(finish·band), 꾸밈 id, 도구, 가리킨 칸, 요청한 바뀌는 칸 수.
    /// 덮는 칸은 저장하지 않는다(틀로 다시 낸다). 목록 기록 안에 필드를 더하지 않는다 — 잡기마다 새 값은 맨 위 새 배열로 둔다(옛 빌드가 다시 쓰면 목록 안 필드는 사라진다).
    /// </summary>
    [Serializable]
    public sealed class RoomHouseBuildHoldSaveData
    {
        public string RequestId = "";
        public string PlayerId = "";
        public string SurfaceId = "";
        public string Kind = "";
        public string FinishId = "";
        public string Tool = "";
        public int X;
        public int Y;
        public int Count;
    }

    /// <summary>
    /// 꾸밈 조각의 상태 줄: <c>f|틀 id</c>(한 줄), <c>b|띠 꾸밈 id</c>(한 줄, 빈 글 허용), <c>c|x|y|벽지|바닥</c>(기록 칸마다, y 다음 x 순, 빈 자리는 빈 글),
    /// <c>h|요청 id|플레이어 id</c>(잡기마다, 요청 id 순). 모르는 머리 글자의 줄은 넘긴다(뒤 판이 더할 수 있다).
    /// </summary>
    public static class RoomHouseBuildLines
    {
        private const string FramePrefix = "f";
        private const string BandPrefix = "b";
        private const string CellPrefix = "c";
        private const string HoldPrefix = "h";

        public static string Frame(string frameId) => FramePrefix + RoomHouse.Separator + (frameId ?? string.Empty);

        public static string BandLine(string band) => BandPrefix + RoomHouse.Separator + (band ?? string.Empty);

        public static string Cell(HouseFinishCellSaveData cell) =>
            CellPrefix + RoomHouse.Separator + cell.X.ToString(CultureInfo.InvariantCulture) + RoomHouse.Separator + cell.Y.ToString(CultureInfo.InvariantCulture)
            + RoomHouse.Separator + (cell.Wall ?? string.Empty) + RoomHouse.Separator + (cell.Floor ?? string.Empty);

        public static string Hold(string requestId, string playerId) => HoldPrefix + RoomHouse.Separator + requestId + RoomHouse.Separator + (playerId ?? string.Empty);

        /// <summary>
        /// 상태 줄을 꾸밈 기록으로 읽는다(<paramref name="holds"/>가 있으면 (요청 id, 플레이어 id)를 채운다). 아는 머리 글자의 줄이 깨졌으면 false.
        /// </summary>
        public static bool TryDecode(IReadOnlyList<string> lines, out HouseBuildSaveData data, List<KeyValuePair<string, string>> holds)
        {
            holds?.Clear();
            data = new HouseBuildSaveData { Version = HouseBuildSaveData.KnownVersion };
            if (lines == null) return false;
            var cells = new List<HouseFinishCellSaveData>(lines.Count);
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line)) return false;
                var parts = line.Split(RoomHouse.Separator);
                string head = parts[0];
                if (head == FramePrefix)
                {
                    if (parts.Length != 2) return false;
                    data.FrameId = parts[1];
                }
                else if (head == BandPrefix)
                {
                    if (parts.Length != 2) return false;
                    data.Band = parts[1];
                }
                else if (head == CellPrefix)
                {
                    if (parts.Length < 5
                        || !int.TryParse(parts[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int x)
                        || !int.TryParse(parts[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int y)) return false;
                    cells.Add(new HouseFinishCellSaveData { X = x, Y = y, Wall = parts[3], Floor = parts[4] });
                }
                else if (head == HoldPrefix)
                {
                    if (parts.Length < 3 || parts[1].Length == 0) return false;
                    holds?.Add(new KeyValuePair<string, string>(parts[1], parts[2]));
                }
            }

            data.Finishes = cells.ToArray();
            return true;
        }
    }

    /// <summary>
    /// 같이 쓰는 집의 꾸밈을 플레이어 쪽에서 비춘다. 방이 알린 꾸밈 기록(<see cref="Data"/>)과 잡기 목록을 갖고, 꾸밈·띠를 잡기 요청으로 적는다.
    /// 게임 한 판은 허락(<see cref="Answered"/>)이 오면 돈을 내는 바꾸기 안에서 쓰기를, 못 내면 풀기를 적는다(<see cref="AddUse"/>, <see cref="AddRelease"/>).
    /// </summary>
    public sealed class RoomHouseBuildClient : IRoomPieceClient
    {
        private readonly RoomOutbox _outbox;
        private readonly List<KeyValuePair<string, string>> _holds = new List<KeyValuePair<string, string>>(4);
        private string[] _lines = Array.Empty<string>();
        // 이 판에서 쓰기가 거절된 잡기 → 그때의 방 상태 판(_linesVersion). 같은 판이면 같은 id로, 상태가 바뀌었으면 새 id로 다시 적는다. 메모리만.
        private readonly Dictionary<string, int> _refusedUses = new Dictionary<string, int>(StringComparer.Ordinal);
        private int _linesVersion;
        private Action<RoomRequestSaveData, bool> _answered;
        private readonly List<KeyValuePair<RoomRequestSaveData, bool>> _unheard = new List<KeyValuePair<RoomRequestSaveData, bool>>(2);

        public RoomHouseBuildClient(RoomOutbox outbox)
        {
            _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        }

        public string Name => RoomHouseBuild.PieceName;

        /// <summary>방이 꾸밈 상태를 알렸다.</summary>
        public bool Known { get; private set; }

        /// <summary>방이 마지막으로 알린 꾸밈 기록(처음에는 빈 기록 — null이 아니다).</summary>
        public HouseBuildSaveData Data { get; private set; } = new HouseBuildSaveData { Version = HouseBuildSaveData.KnownVersion };

        public event Action Changed;

        /// <summary>
        /// 내 꾸밈·띠 잡기 요청의 답(그 요청, 허락됐는지). 듣는 쪽이 붙기 전에 온 답은 갖고 있다가 처음 붙을 때 넘긴다(<see cref="RoomChestsClient.Answered"/>와 같다).
        /// 쓰기·풀기의 답은 여기로 오지 않는다(<see cref="Changed"/>만). 쓰기가 거절되면 이 조각이 새 id로 쓰기를 다시 적는다(<see cref="Receive(RoomRequestSaveData, RoomAnswer)"/>).
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

        /// <summary>답을 기다리는 꾸밈·띠 잡기 요청이 있다.</summary>
        public bool HasPending
        {
            get
            {
                foreach (var r in _outbox.Requests)
                    if (r.Piece == RoomHouseBuild.PieceName && (r.Kind == RoomHouseBuild.Finish || r.Kind == RoomHouseBuild.Band)) return true;
                return false;
            }
        }

        /// <summary>그 플레이어의 잡기(요청 id 순).</summary>
        public void CopyHoldsOf(string playerId, List<string> holdIds)
        {
            holdIds.Clear();
            foreach (var pair in _holds)
                if (string.Equals(pair.Value, playerId, StringComparison.Ordinal)) holdIds.Add(pair.Key);
        }

        public string RequestFinish(string surfaceId, string finishId, string tool, CellPos pointer, int expectedCells)
        {
            if (expectedCells <= 0) throw new ArgumentException("바뀌는 칸 수는 1 이상이다", nameof(expectedCells));
            return Add(RoomHouseBuild.Finish, expectedCells, surfaceId, finishId, tool,
                pointer.X.ToString(CultureInfo.InvariantCulture), pointer.Y.ToString(CultureInfo.InvariantCulture));
        }

        public string RequestBand(string surfaceId, string finishId) => Add(RoomHouseBuild.Band, 1, surfaceId, finishId);

        /// <summary>잡은 것을 썼다(돈을 냈다). 돈을 내는 바꾸기 안에서 부른다(던지지 않는다).</summary>
        public void AddUse(string holdId) => _outbox.Add(new RoomRequestSaveData { Piece = RoomHouseBuild.PieceName, RequestId = _outbox.NewId(), Kind = RoomHouseBuild.Use, Ids = new[] { holdId } });

        /// <summary>잡은 것을 쓰지 못했다(돈을 내지 못했다).</summary>
        public void AddRelease(string holdId) => _outbox.Add(new RoomRequestSaveData { Piece = RoomHouseBuild.PieceName, RequestId = _outbox.NewId(), Kind = RoomHouseBuild.Release, Ids = new[] { holdId } });

        public void Receive(RoomPieceState state)
        {
            bool first = !Known;
            Known = true;
            if (!first && SameLines(state.Ids)) return;
            _lines = (string[])state.Ids.Clone();
            _linesVersion++;
            var holds = new List<KeyValuePair<string, string>>(4);
            if (RoomHouseBuildLines.TryDecode(state.Ids, out var data, holds))
            {
                Data = data;
                _holds.Clear();
                _holds.AddRange(holds);
            }

            if (_refusedUses.Count > 0) ResendRefusedUses();
            Changed?.Invoke();
        }

        public void Receive(RoomRequestSaveData request, RoomAnswer answer)
        {
            if (request.Kind != RoomHouseBuild.Finish && request.Kind != RoomHouseBuild.Band)
            {
                // 쓰기 = 돈을 이미 냈다. 방이 적용하지 못해 잡은 채로 거절했으면(RoomHouseBuild 요약) 쓰기를 보낼 목록에 다시 적어 둔다 — 그래서 목록에는
                // 늘 그 잡기의 쓰기가 남아, 게임 한 판이 이 잡기를 "떠도는 잡기"로 보고 풀지 않는다(풀면 낸 돈만 사라진다). 저장하지 않는다(다음 저장과 함께).
                // 방 장부에 줄이 쌓이지 않게: 이 판의 첫 거절이거나 그 뒤로 방의 상태가 바뀌었으면 새 id(방이 다시 판정한다), 아니면 같은 id(장부가 같은
                // 거절을 다시 준다 — 새 줄 없음). 방의 상태가 바뀌면 그때 새 id로 바꿔 둔다(ResendRefusedUses). Invalid(잡기가 없거나 남의 것)는 다시 적지 않는다.
                if (request.Kind == RoomHouseBuild.Use && request.Ids != null && request.Ids.Length == 1)
                {
                    string hold = request.Ids[0];
                    if (answer.Outcome == RoomOutcomes.Refused)
                    {
                        if (_refusedUses.TryGetValue(hold, out int seen) && seen == _linesVersion)
                        {
                            // 같은 id: 목록·세이브에는 남기고 방 접속이 따로 다시 보내지는 않는다(되풀이해 오가지 않게).
                            _outbox.Add(request.Copy());
                            _outbox.Defer(request.RequestId);
                        }
                        else AddUse(hold);
                        _refusedUses[hold] = _linesVersion;
                    }
                    else _refusedUses.Remove(hold);
                }

                Changed?.Invoke();
                return;
            }

            bool applied = answer.Outcome == RoomOutcomes.Applied;
            if (_answered == null) _unheard.Add(new KeyValuePair<RoomRequestSaveData, bool>(request, applied));
            else _answered(request, applied);
        }

        // 방의 상태가 바뀌었다: 거절된 쓰기를 새 id로 바꿔 둔다(다음 저장과 함께 가서 방이 다시 판정한다). 잡기가 상태에서 사라졌으면 잊는다.
        private void ResendRefusedUses()
        {
            foreach (var hold in new List<string>(_refusedUses.Keys))
            {
                if (!HoldKnown(hold))
                {
                    _refusedUses.Remove(hold);
                    continue;
                }

                string pending = null;
                foreach (var r in _outbox.Requests)
                    if (r.Piece == RoomHouseBuild.PieceName && r.Kind == RoomHouseBuild.Use && r.Ids != null && r.Ids.Length == 1 && r.Ids[0] == hold)
                    {
                        pending = r.RequestId;
                        break;
                    }

                if (pending == null) continue;
                _outbox.Answered(pending);
                AddUse(hold);
                _refusedUses[hold] = _linesVersion;
            }
        }

        private bool HoldKnown(string hold)
        {
            foreach (var pair in _holds) if (pair.Key == hold) return true;
            return false;
        }

        private bool SameLines(string[] ids)
        {
            if (ids.Length != _lines.Length) return false;
            for (int i = 0; i < ids.Length; i++) if (!string.Equals(ids[i], _lines[i], StringComparison.Ordinal)) return false;
            return true;
        }

        private string Add(string kind, int amount, params string[] ids)
        {
            foreach (var id in ids)
                if (string.IsNullOrEmpty(id) || id.IndexOf(RoomHouse.Separator) >= 0) throw new ArgumentException("꾸밈 요청의 id가 비었거나 구분 글자가 들었다: " + id);
            string requestId = _outbox.NewId();
            _outbox.Add(new RoomRequestSaveData { Piece = RoomHouseBuild.PieceName, RequestId = requestId, Kind = kind, Amount = amount, Ids = ids });
            return requestId;
        }
    }
}
