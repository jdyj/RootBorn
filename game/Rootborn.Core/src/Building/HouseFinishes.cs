using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;
using Rootborn.Core.Placement;
using Rootborn.Core.World;

namespace Rootborn.Core.Building
{
    /// <summary>짓는 집의 틀: 틀 id(맵 build.frameId), 칸 출처표, 자리마다 프레임 기본 꾸밈 id, 기본 띠 색 줄 이름(맵 build.bandColors).</summary>
    public sealed class HouseBuildFrame
    {
        public HouseBuildFrame(string frameId, RoomTilePlan tiles, string defaultWall, string defaultFloor, string defaultBand)
        {
            FrameId = frameId ?? string.Empty;
            Tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
            DefaultWall = defaultWall ?? string.Empty;
            DefaultFloor = defaultFloor ?? string.Empty;
            DefaultBand = defaultBand ?? string.Empty;
        }

        public string FrameId { get; }
        public RoomTilePlan Tiles { get; }
        public string DefaultWall { get; }
        public string DefaultFloor { get; }
        public string DefaultBand { get; }
    }

    /// <summary>꾸밈 도구 이름(글 상수 — 닫힌 종류가 아니다).</summary>
    public static class HouseFinishTools
    {
        /// <summary>방 채우기: 가리킨 칸의 방(<see cref="RoomFill.RoomOf"/>).</summary>
        public const string Room = "room";
        /// <summary>칸 붓: 가리킨 칸 하나.</summary>
        public const string Brush = "brush";
    }

    /// <summary>미리 보기 거절 이유(문구 키).</summary>
    public static class HouseFinishReasons
    {
        public const string NoTarget = "ui.house-build.reason.no-target";
        public const string UnknownFinish = "ui.house-build.reason.unknown-finish";
        public const string Locked = "ui.house-build.reason.locked";
        public const string NothingToChange = "ui.house-build.reason.nothing-to-change";
        public const string NotEnoughMoney = "ui.house-build.reason.not-enough-money";
        public const string NotInHouse = "ui.house-build.reason.not-in-house";
        /// <summary>같이 쓰는 집에서 방이 꾸밈 잡기를 거절했다(그 사이 다른 사람이 먼저 바꿨거나 잡았다). 미리 보기 이유가 아니라 마지막 요청의 결과다.</summary>
        public const string RoomRefused = "ui.house-build.reason.room-refused";
    }

    /// <summary>
    /// 꾸밈 한 번의 미리 보기. 이유(<see cref="ReasonKey"/>)가 있어도 덮는 칸·바뀌는 칸·비용은 채운다(화면이 보인다).
    /// <see cref="StateVersion"/>은 만든 때의 <see cref="HouseFinishes.Version"/>이고, 다르면 적용하지 않는다.
    /// </summary>
    public sealed class HouseFinishPreview
    {
        private static readonly IReadOnlyList<CellPos> NoCells = Array.Empty<CellPos>();
        private static readonly IReadOnlyList<ItemAmount<string>> NoCost = Array.Empty<ItemAmount<string>>();

        internal HouseFinishPreview(string finishId, string slot, string tool, bool hasTarget, CellPos target,
            IReadOnlyList<CellPos> cells, IReadOnlyList<CellPos> changedCells, IReadOnlyList<ItemAmount<string>> cost,
            string reasonKey, int stateVersion, IReadOnlyList<CellPos> highlightCells = null)
        {
            FinishId = finishId ?? string.Empty;
            Slot = slot ?? string.Empty;
            Tool = tool ?? string.Empty;
            HasTarget = hasTarget;
            Target = hasTarget ? target : new CellPos(0, 0);
            Cells = cells ?? NoCells;
            ChangedCells = changedCells ?? NoCells;
            HighlightCells = highlightCells ?? ChangedCells;
            Cost = cost ?? NoCost;
            ReasonKey = reasonKey;
            StateVersion = stateVersion;
        }

        public string FinishId { get; }
        public string Slot { get; }
        public string Tool { get; }
        public bool HasTarget { get; }
        public CellPos Target { get; }
        /// <summary>도구가 덮는 칸. 방 채우기는 <see cref="RoomFill.RoomOf"/>의 같은 목록 인스턴스.</summary>
        public IReadOnlyList<CellPos> Cells { get; }
        /// <summary>보이는 값이 바뀌는 칸(벽지는 정면 주인 칸만).</summary>
        public IReadOnlyList<CellPos> ChangedCells { get; }
        /// <summary>
        /// 화면이 밝힐 칸: 바닥은 바뀌는 칸 그대로, 벽지는 바뀌는 주인 칸이 바라보는 정면 벽 칸(출처표의 face·narrow 부분이 있는 칸, 겹치지 않게).
        /// 미리 보기를 만들 때 한 번 정한다. 비용·바뀌는 칸 수와 무관하다.
        /// </summary>
        public IReadOnlyList<CellPos> HighlightCells { get; }
        /// <summary>칸당 비용 × 바뀌는 칸 수, 아이템별로 합친 것(정의 순서).</summary>
        public IReadOnlyList<ItemAmount<string>> Cost { get; }
        /// <summary>거절 이유 문구 키. null이면 적용할 수 있다.</summary>
        public string ReasonKey { get; }
        public int StateVersion { get; }
        public bool CanApply => ReasonKey == null;

        /// <summary>같은 꾸밈·자리·이유·판이고 덮는 칸이 같은가(같은 인스턴스이거나 칸이 같다). 포인터가 같은 방 안에서 움직일 때 화면이 다시 그리지 않게.</summary>
        public bool SameAs(HouseFinishPreview other)
        {
            if (other == null) return false;
            if (!string.Equals(FinishId, other.FinishId, StringComparison.Ordinal)
                || !string.Equals(Slot, other.Slot, StringComparison.Ordinal)
                || !string.Equals(ReasonKey, other.ReasonKey, StringComparison.Ordinal)
                || StateVersion != other.StateVersion)
                return false;
            if (ReferenceEquals(Cells, other.Cells)) return true;
            if (Cells.Count != other.Cells.Count) return false;
            for (int i = 0; i < Cells.Count; i++)
                if (!Cells[i].Equals(other.Cells[i])) return false;
            return true;
        }

        /// <summary>칸을 보지 않고 거절한 미리 보기(집 밖, 같이 쓰는 집 등).</summary>
        public static HouseFinishPreview Refused(string finishId, string slot, string tool, string reasonKey) =>
            new HouseFinishPreview(finishId, slot, tool, false, default, null, null, null,
                reasonKey ?? throw new ArgumentNullException(nameof(reasonKey)), 0);
    }

    /// <summary>
    /// 짓는 집의 꾸밈 상태: 바닥 칸마다 벽지·바닥 기록과 집 하나의 띠 색 기록, 미리 보기, 적용(메모리만). 꾸밈 id로 분기하지 않는다 — 정의는 <c>find</c>로 찾는다.
    /// 기록은 읽은 것을 모두 들고 있다가(데이터에 없는 id, 지금 바닥이 아닌 칸·격자 밖 칸 포함) 그대로 다시 쓴다.
    /// </summary>
    public sealed class HouseFinishes
    {
        private sealed class Record
        {
            public string Wall = "";
            public string Floor = "";
            public bool IsEmpty => Wall.Length == 0 && Floor.Length == 0;
        }

        private readonly Dictionary<CellPos, Record> _records = new Dictionary<CellPos, Record>();
        private readonly HashSet<CellPos> _faceOwners = new HashSet<CellPos>();
        // 주인 바닥 칸 → 그 칸의 정면(face·narrow 부분)이 그려지는 벽 칸들(출처표 순서). 붙일 때 한 번 만든다.
        private readonly Dictionary<CellPos, List<CellPos>> _facesOf = new Dictionary<CellPos, List<CellPos>>();
        private Func<string, RoomFinishDefinition> _find;
        private RoomFill _fill;
        private string _loadedFrameId = "";
        private string _band = "";

        public bool Attached => Frame != null;
        public HouseBuildFrame Frame { get; private set; }
        /// <summary>바뀔 때마다 오른다(적용, 읽기, 붙이기). 미리 보기가 낡았는지 본다.</summary>
        public int Version { get; private set; }

        /// <summary>고른 띠 꾸밈 id(기록 그대로 — 빈 글이면 프레임 기본, 데이터에 없는 id도 그대로 들고 있다).</summary>
        public string Band => _band;

        /// <summary>
        /// 보이는 띠 색 줄 이름(시트 좌표표 bands의 줄): 기록의 꾸밈이 띠 자리 정의로 있으면 그 Art, 아니면(빈 글·데이터에 없는 id·다른 자리) 프레임 기본.
        /// 붙지 않았으면 빈 글.
        /// </summary>
        public string BandColors
        {
            get
            {
                if (!Attached) return string.Empty;
                var def = _band.Length > 0 ? _find(_band) : null;
                return def != null && IsBandSlot(def.Slot) ? def.Art : Frame.DefaultBand;
            }
        }

        public void Load(HouseBuildSaveData data)
        {
            _records.Clear();
            _loadedFrameId = data?.FrameId ?? string.Empty;
            _band = data?.Band ?? string.Empty;
            if (data?.Finishes != null)
                foreach (var cell in data.Finishes)
                {
                    if (cell == null) continue;
                    var pos = new CellPos(cell.X, cell.Y);
                    if (!_records.TryGetValue(pos, out var rec))
                        _records[pos] = rec = new Record();
                    // 같은 칸이 두 번 나오면 비지 않은 값이 이긴다(기록을 버리지 않는다)
                    if (!string.IsNullOrEmpty(cell.Wall)) rec.Wall = cell.Wall;
                    if (!string.IsNullOrEmpty(cell.Floor)) rec.Floor = cell.Floor;
                }
            Version++;
        }

        public void Attach(HouseBuildFrame frame, Func<string, RoomFinishDefinition> find)
        {
            Frame = frame ?? throw new ArgumentNullException(nameof(frame));
            _find = find ?? throw new ArgumentNullException(nameof(find));
            _fill = RoomFill.Build(frame.Tiles.Grid);
            _faceOwners.Clear();
            _facesOf.Clear();
            foreach (var cell in frame.Tiles.Cells)
                foreach (var part in cell.Parts)
                    if (IsFacePart(part))
                    {
                        var owner = OwnerOf(cell.Cell, part);
                        _faceOwners.Add(owner);
                        if (!_facesOf.TryGetValue(owner, out var faces)) _facesOf[owner] = faces = new List<CellPos>(4);
                        if (!faces.Contains(cell.Cell)) faces.Add(cell.Cell);
                    }
            Version++;
        }

        /// <summary>그 꾸밈을 쓸 수 있는가: 해금 토큰이 비었거나 얻었다. 미리 보기의 잠김 판단과 화면의 잠김 표시가 이 하나를 쓴다. 꾸밈이 null이면 false.</summary>
        public static bool IsUnlocked(RoomFinishDefinition finish, FurnitureUnlockProgress unlocks)
            => finish != null && (finish.UnlockToken.Length == 0 || (unlocks != null && unlocks.IsUnlocked(finish.UnlockToken)));

        public string WallAt(CellPos floorCell) => Visible(RoomFinishSlots.Wall, floorCell);
        public string FloorAt(CellPos floorCell) => Visible(RoomFinishSlots.Floor, floorCell);

        public HouseFinishPreview Preview(string finishId, string tool, CellPos pointer, FurnitureUnlockProgress unlocks,
            Func<IReadOnlyList<ItemAmount<string>>, bool> canPay)
            => Preview(finishId, tool, pointer, f => IsUnlocked(f, unlocks), canPay);

        /// <summary>
        /// 해금 판단을 받는 미리 보기. 같이 쓰는 집의 방은 해금을 요청한 사람에게 맡긴다(<c>_ =&gt; true</c> — 가구 놓기와 같다).
        /// 이유 순서: 붙지 않음 → 정의 없음 → 대상 없음(벽지는 덮는 칸에 정면 주인 칸이 없을 때도) → 잠김 → 바뀌는 칸 0 → 돈.
        /// </summary>
        public HouseFinishPreview Preview(string finishId, string tool, CellPos pointer, Func<RoomFinishDefinition, bool> isUnlocked,
            Func<IReadOnlyList<ItemAmount<string>>, bool> canPay)
        {
            if (!Attached)
                return HouseFinishPreview.Refused(finishId, string.Empty, tool, HouseFinishReasons.NotInHouse);

            var def = string.IsNullOrEmpty(finishId) ? null : _find(finishId);
            if (def == null || !IsKnownSlot(def.Slot))
                return Make(finishId, def?.Slot, tool, false, default, null, null, null, HouseFinishReasons.UnknownFinish);
            string slot = def.Slot;

            if (!TryTarget(pointer, out var target))
                return Make(finishId, slot, tool, false, default, null, null, null, HouseFinishReasons.NoTarget);

            IReadOnlyList<CellPos> cells;
            if (string.Equals(tool, HouseFinishTools.Room, StringComparison.Ordinal))
                cells = _fill.RoomOf(target);
            else if (string.Equals(tool, HouseFinishTools.Brush, StringComparison.Ordinal))
                cells = new[] { target };
            else
                return Make(finishId, slot, tool, false, default, null, null, null, HouseFinishReasons.NoTarget);

            bool wallSlot = string.Equals(slot, RoomFinishSlots.Wall, StringComparison.Ordinal);
            var changed = new List<CellPos>(cells.Count);
            int owners = 0;
            foreach (var c in cells)
            {
                if (wallSlot && !_faceOwners.Contains(c)) continue;
                owners++;
                if (!string.Equals(Visible(slot, c), def.Id, StringComparison.Ordinal))
                    changed.Add(c);
            }
            var cost = CostFor(def, changed.Count);

            string reason = null;
            if (owners == 0)
                reason = HouseFinishReasons.NoTarget; // 벽지를 바를 정면이 없다("이미 이 꾸밈"이 아니다)
            else if (isUnlocked == null || !isUnlocked(def))
                reason = HouseFinishReasons.Locked;
            else if (changed.Count == 0)
                reason = HouseFinishReasons.NothingToChange;
            else if (canPay == null || !canPay(cost))
                reason = HouseFinishReasons.NotEnoughMoney;

            return Make(def.Id, slot, tool, true, target, cells, changed, cost, reason, wallSlot ? FaceCellsOf(changed) : null);
        }

        /// <summary>
        /// 띠 색 한 번의 미리 보기(집 하나에 하나 — 칸이 없다: Cells·ChangedCells·HighlightCells는 빈 목록, 대상 없음).
        /// 이유 순서: 붙지 않음(NotInHouse) → 정의 없음·띠 자리가 아님(UnknownFinish) → 잠김 → 보이는 띠 색 줄과 같은 그림(NothingToChange) → 돈.
        /// 비용은 정의의 Cost 한 번(바뀌는 것이 없으면 빈 목록).
        /// </summary>
        public HouseFinishPreview PreviewBand(string finishId, FurnitureUnlockProgress unlocks, Func<IReadOnlyList<ItemAmount<string>>, bool> canPay)
            => PreviewBand(finishId, f => IsUnlocked(f, unlocks), canPay);

        /// <summary>해금 판단을 받는 띠 미리 보기(<see cref="Preview(string, string, CellPos, Func{RoomFinishDefinition, bool}, Func{IReadOnlyList{ItemAmount{string}}, bool})"/>와 같은 뜻).</summary>
        public HouseFinishPreview PreviewBand(string finishId, Func<RoomFinishDefinition, bool> isUnlocked, Func<IReadOnlyList<ItemAmount<string>>, bool> canPay)
        {
            if (!Attached)
                return HouseFinishPreview.Refused(finishId, RoomFinishSlots.Band, string.Empty, HouseFinishReasons.NotInHouse);
            var def = string.IsNullOrEmpty(finishId) ? null : _find(finishId);
            if (def == null || !IsBandSlot(def.Slot))
                return Make(finishId, def?.Slot, string.Empty, false, default, null, null, null, HouseFinishReasons.UnknownFinish);

            bool same = string.Equals(def.Art, BandColors, StringComparison.Ordinal);
            var cost = CostFor(def, same ? 0 : 1);
            string reason = null;
            if (isUnlocked == null || !isUnlocked(def)) reason = HouseFinishReasons.Locked;
            else if (same) reason = HouseFinishReasons.NothingToChange;
            else if (canPay == null || !canPay(cost)) reason = HouseFinishReasons.NotEnoughMoney;
            return Make(def.Id, def.Slot, string.Empty, false, default, null, null, cost, reason);
        }

        // 바뀌는 주인 칸들이 바라보는 정면 벽 칸(겹치지 않게, 주인 순서).
        private IReadOnlyList<CellPos> FaceCellsOf(List<CellPos> owners)
        {
            if (owners.Count == 0) return Array.Empty<CellPos>();
            var seen = new HashSet<CellPos>();
            var result = new List<CellPos>(owners.Count * 2);
            foreach (var owner in owners)
                if (_facesOf.TryGetValue(owner, out var faces))
                    foreach (var c in faces)
                        if (seen.Add(c)) result.Add(c);
            return result;
        }

        /// <summary>
        /// 적용할 수 있고 낡지 않은 미리 보기만 적용한다(메모리만). 아니면 false·무변경. 던지지 않는다.
        /// 띠 미리 보기(<see cref="PreviewBand"/>)는 띠 기록을 그 꾸밈 id로 바꾼다 — 그림이 프레임 기본 줄과 같으면 빈 글.
        /// </summary>
        public bool Apply(HouseFinishPreview preview)
        {
            if (preview == null || !Attached || !preview.CanApply || preview.StateVersion != Version)
                return false;
            if (IsBandSlot(preview.Slot)) return ApplyBand(preview.FinishId);
            if (preview.ChangedCells.Count == 0)
                return false;
            if (!IsKnownSlot(preview.Slot))
                return false;
            string value = string.Equals(preview.FinishId, DefaultFor(preview.Slot), StringComparison.Ordinal) ? string.Empty : preview.FinishId;
            bool wallSlot = string.Equals(preview.Slot, RoomFinishSlots.Wall, StringComparison.Ordinal);
            foreach (var c in preview.ChangedCells)
            {
                if (!_records.TryGetValue(c, out var rec))
                {
                    if (value.Length == 0) continue;
                    _records[c] = rec = new Record();
                }
                if (wallSlot) rec.Wall = value;
                else rec.Floor = value;
                if (rec.IsEmpty) _records.Remove(c);
            }
            Version++;
            return true;
        }

        private bool ApplyBand(string finishId)
        {
            var def = string.IsNullOrEmpty(finishId) ? null : _find(finishId);
            if (def == null || !IsBandSlot(def.Slot) || string.Equals(def.Art, BandColors, StringComparison.Ordinal)) return false;
            _band = string.Equals(def.Art, Frame.DefaultBand, StringComparison.Ordinal) ? string.Empty : def.Id;
            Version++;
            return true;
        }

        public HouseBuildSaveData ToSaveData()
        {
            var keys = new List<CellPos>(_records.Keys);
            keys.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            var cells = new HouseFinishCellSaveData[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                var rec = _records[keys[i]];
                cells[i] = new HouseFinishCellSaveData { X = keys[i].X, Y = keys[i].Y, Wall = rec.Wall, Floor = rec.Floor };
            }
            return new HouseBuildSaveData
            {
                Version = HouseBuildSaveData.KnownVersion,
                FrameId = Attached ? Frame.FrameId : _loadedFrameId,
                Band = _band,
                Finishes = cells,
            };
        }

        private HouseFinishPreview Make(string finishId, string slot, string tool, bool hasTarget, CellPos target,
            IReadOnlyList<CellPos> cells, IReadOnlyList<CellPos> changed, IReadOnlyList<ItemAmount<string>> cost, string reason,
            IReadOnlyList<CellPos> highlight = null) =>
            new HouseFinishPreview(finishId, slot, tool, hasTarget, target, cells, changed, cost, reason, Version, highlight);

        private static bool IsBandSlot(string slot) => string.Equals(slot, RoomFinishSlots.Band, StringComparison.Ordinal);

        private static bool IsKnownSlot(string slot) =>
            string.Equals(slot, RoomFinishSlots.Wall, StringComparison.Ordinal) || string.Equals(slot, RoomFinishSlots.Floor, StringComparison.Ordinal);

        private string DefaultFor(string slot) =>
            string.Equals(slot, RoomFinishSlots.Wall, StringComparison.Ordinal) ? Frame.DefaultWall : Frame.DefaultFloor;

        /// <summary>보이는 값: 기록이 비었거나, 정의가 없거나, 정의의 자리가 다르면 프레임 기본. 붙지 않았으면 빈 글.</summary>
        private string Visible(string slot, CellPos cell)
        {
            if (!Attached) return string.Empty;
            string fallback = DefaultFor(slot);
            if (!_records.TryGetValue(cell, out var rec)) return fallback;
            string id = string.Equals(slot, RoomFinishSlots.Wall, StringComparison.Ordinal) ? rec.Wall : rec.Floor;
            if (id.Length == 0) return fallback;
            var def = _find(id);
            return def != null && string.Equals(def.Slot, slot, StringComparison.Ordinal) ? id : fallback;
        }

        /// <summary>가리킨 칸 → 대상 바닥 칸. 바닥이면 그 칸, 벽이면 그 칸 출처표의 첫 face·narrow 부분의 주인 칸.</summary>
        private bool TryTarget(CellPos pointer, out CellPos target)
        {
            target = default;
            var grid = Frame.Tiles.Grid;
            int v = grid.At(pointer.X, pointer.Y);
            if (v == RoomCells.Floor)
            {
                target = pointer;
                return true;
            }
            if (v != RoomCells.Wall) return false;
            var tiles = Frame.Tiles.At(pointer.X, pointer.Y);
            if (tiles == null) return false;
            foreach (var part in tiles.Parts)
                if (IsFacePart(part))
                {
                    target = OwnerOf(pointer, part);
                    return true;
                }
            return false;
        }

        private static bool IsFacePart(TilePart part) =>
            part.HasOwner && (string.Equals(part.Source, TileSources.Face, StringComparison.Ordinal)
                              || string.Equals(part.Source, TileSources.Narrow, StringComparison.Ordinal));

        private static CellPos OwnerOf(CellPos cell, TilePart part) => new CellPos(cell.X + part.Owner.X, cell.Y + part.Owner.Y);

        /// <summary>
        /// 꾸밈 한 번의 비용: 벽지·바닥은 칸당 비용 × 바뀌는 칸 수(아이템별로 합친 것, 정의 순서), 띠는 바뀌면(changedCells &gt; 0) 정의의 비용 한 번.
        /// 바뀌는 것이 없으면 빈 목록. 미리 보기와 같이 쓰는 집의 지불이 이 하나를 쓴다.
        /// </summary>
        public static IReadOnlyList<ItemAmount<string>> CostFor(RoomFinishDefinition finish, int changedCells)
        {
            if (finish == null) throw new ArgumentNullException(nameof(finish));
            int cells = IsBandSlot(finish.Slot) ? Math.Min(changedCells, 1) : changedCells;
            if (cells <= 0 || finish.Cost.Count == 0) return Array.Empty<ItemAmount<string>>();
            var items = new List<string>(finish.Cost.Count);
            var counts = new List<int>(finish.Cost.Count);
            foreach (var amount in finish.Cost)
            {
                int i = items.IndexOf(amount.Item);
                if (i < 0)
                {
                    items.Add(amount.Item);
                    counts.Add(amount.Count * cells);
                }
                else counts[i] += amount.Count * cells;
            }
            var result = new ItemAmount<string>[items.Count];
            for (int i = 0; i < result.Length; i++) result[i] = new ItemAmount<string>(items[i], counts[i]);
            return result;
        }
    }
}
