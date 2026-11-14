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
        public const string SharedHouse = "ui.house-build.reason.shared-house";
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
            string reasonKey, int stateVersion)
        {
            FinishId = finishId ?? string.Empty;
            Slot = slot ?? string.Empty;
            Tool = tool ?? string.Empty;
            HasTarget = hasTarget;
            Target = hasTarget ? target : new CellPos(0, 0);
            Cells = cells ?? NoCells;
            ChangedCells = changedCells ?? NoCells;
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
    /// 짓는 집의 꾸밈 상태: 바닥 칸마다 벽지·바닥 기록, 미리 보기, 적용(메모리만). 꾸밈 id로 분기하지 않는다 — 정의는 <c>find</c>로 찾는다.
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
        private Func<string, RoomFinishDefinition> _find;
        private RoomFill _fill;
        private string _loadedFrameId = "";
        private string _band = "";

        public bool Attached => Frame != null;
        public HouseBuildFrame Frame { get; private set; }
        /// <summary>바뀔 때마다 오른다(적용, 읽기, 붙이기). 미리 보기가 낡았는지 본다.</summary>
        public int Version { get; private set; }

        /// <summary>보이는 띠 색 줄 이름: 기록이 비면 프레임 기본(붙지 않았으면 빈 글).</summary>
        public string Band => _band.Length > 0 ? _band : Frame?.DefaultBand ?? string.Empty;

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
            foreach (var cell in frame.Tiles.Cells)
                foreach (var part in cell.Parts)
                    if (IsFacePart(part))
                        _faceOwners.Add(OwnerOf(cell.Cell, part));
            Version++;
        }

        public string WallAt(CellPos floorCell) => Visible(RoomFinishSlots.Wall, floorCell);
        public string FloorAt(CellPos floorCell) => Visible(RoomFinishSlots.Floor, floorCell);

        public HouseFinishPreview Preview(string finishId, string tool, CellPos pointer, FurnitureUnlockProgress unlocks,
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
            foreach (var c in cells)
            {
                if (wallSlot && !_faceOwners.Contains(c)) continue;
                if (!string.Equals(Visible(slot, c), def.Id, StringComparison.Ordinal))
                    changed.Add(c);
            }
            var cost = CostFor(def, changed.Count);

            string reason = null;
            if (def.UnlockToken.Length > 0 && (unlocks == null || !unlocks.IsUnlocked(def.UnlockToken)))
                reason = HouseFinishReasons.Locked;
            else if (changed.Count == 0)
                reason = HouseFinishReasons.NothingToChange;
            else if (canPay == null || !canPay(cost))
                reason = HouseFinishReasons.NotEnoughMoney;

            return Make(def.Id, slot, tool, true, target, cells, changed, cost, reason);
        }

        /// <summary>적용할 수 있고 낡지 않은 미리 보기만 적용한다(메모리만). 아니면 false·무변경. 던지지 않는다.</summary>
        public bool Apply(HouseFinishPreview preview)
        {
            if (preview == null || !Attached || !preview.CanApply || preview.StateVersion != Version || preview.ChangedCells.Count == 0)
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
            IReadOnlyList<CellPos> cells, IReadOnlyList<CellPos> changed, IReadOnlyList<ItemAmount<string>> cost, string reason) =>
            new HouseFinishPreview(finishId, slot, tool, hasTarget, target, cells, changed, cost, reason, Version);

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

        private static IReadOnlyList<ItemAmount<string>> CostFor(RoomFinishDefinition def, int cells)
        {
            if (cells == 0 || def.Cost.Count == 0) return Array.Empty<ItemAmount<string>>();
            var items = new List<string>(def.Cost.Count);
            var counts = new List<int>(def.Cost.Count);
            foreach (var amount in def.Cost)
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
