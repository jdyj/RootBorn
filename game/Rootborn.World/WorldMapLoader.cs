using System;
using System.Collections.Generic;
using System.IO;
using Rootborn.Core.World;
using Rootborn.World.Ldtk;

namespace Rootborn.World
{
    /// <summary>읽어 들인 맵: Core 월드(충돌·문·도착 지점), 그릴 목록, 원본 레벨, 그리지 못한 타일셋, 부지에 얹은 건물의 문 프레임.</summary>
    public sealed class LoadedMap
    {
        public MapEntry Entry;
        public LdtkLevel Level;
        public WorldMap World;
        public MapPicture Picture;
        public readonly List<string> Problems = new List<string>();
        /// <summary>부지·건물 데이터의 문제(맵 검사가 "부지: "로 보인다). 문제가 있는 부지는 건너뛰고 나머지는 얹는다.</summary>
        public readonly List<string> LotProblems = new List<string>();
        /// <summary>문 프레임이 있는 건물 문들.</summary>
        public readonly List<BuildingDoorArt> DoorArt = new List<BuildingDoorArt>();
        /// <summary>만들 때 읽은 부지 단계(맵 목록의 부지 순서). 지금 단계와 다르면 다시 만든다(WorldMapLoader.IsStale).</summary>
        public int[] LotLevels = Array.Empty<int>();

        /// <summary>그 문의 문 프레임(없으면 null). 매 프레임 불러도 새 객체를 만들지 않는다.</summary>
        public BuildingDoorArt DoorArtFor(DoorArea door)
        {
            if (door == null) return null;
            for (int i = 0; i < DoorArt.Count; i++) if (ReferenceEquals(DoorArt[i].Door, door)) return DoorArt[i];
            return null;
        }
    }

    /// <summary>건물 문 하나의 열리는 그림: 어느 문, 프레임(닫힘 → 열림), 건물 그림이 Picture.Sorted의 몇 번째인가(프레임은 그 바로 위에 그린다).</summary>
    public sealed class BuildingDoorArt
    {
        public readonly DoorArea Door;
        public readonly IReadOnlyList<SpriteDraw> Frames;
        public int SortedIndex { get; internal set; }

        public BuildingDoorArt(DoorArea door, IReadOnlyList<SpriteDraw> frames, int sortedIndex)
        {
            Door = door ?? throw new ArgumentNullException(nameof(door));
            Frames = frames ?? throw new ArgumentNullException(nameof(frames));
            SortedIndex = sortedIndex;
        }

        /// <summary>열림 진행도(0~1, Core WorldSession.OpeningProgress) → 프레임 번호. 진행도를 프레임 수만큼 같은 길이로 나눈다.</summary>
        public int FrameAt(float progress)
        {
            int n = Frames.Count;
            if (n <= 1) return 0;
            int i = (int)Math.Floor(progress * n);
            return i < 0 ? 0 : i >= n ? n - 1 : i;
        }
    }

    /// <summary>
    /// 맵 목록과 LDtk 파일로 맵을 만든다. LDtk 프로젝트와 맵은 한 번 읽으면 기억해 둔다(문을 오갈 때 다시 읽지 않는다).
    /// 칸 크기는 Collision IntGrid 층(없으면 타일 층)의 격자에서, 칸 수는 레벨 픽셀 크기에서 얻는다.
    /// 부지(maps.json lots)가 있는 맵은 지금 단계의 건물(buildings.json)을 얹고, 부지의 단계가 바뀌면 다시 만든다(LDtk 레벨은 다시 읽지 않는다).
    /// </summary>
    public sealed class WorldMapLoader : IWorldMapSource
    {
        private readonly string _repoRoot;
        private readonly Dictionary<string, LdtkProject> _projects = new Dictionary<string, LdtkProject>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, LoadedMap> _maps = new Dictionary<string, LoadedMap>(StringComparer.OrdinalIgnoreCase);

        public readonly WorldCatalog Catalog;
        /// <summary>건물 목록(buildings.json). 부지가 가리키는 건물을 여기서 찾는다.</summary>
        public readonly BuildingCatalog Buildings;
        /// <summary>부지의 단계를 읽는 종류들. 게임 한 판을 읽는 종류는 Host가 더한다.</summary>
        public readonly BuildingLevelKinds Levels;

        public WorldMapLoader(string repoRoot, WorldCatalog catalog, BuildingCatalog buildings = null, BuildingLevelKinds levels = null)
        {
            _repoRoot = repoRoot ?? throw new ArgumentNullException(nameof(repoRoot));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Buildings = buildings ?? BuildingCatalog.Empty;
            Levels = levels ?? BuildingLevelKinds.WithFixed();
        }

        /// <summary>저장소의 맵 목록과 건물 목록으로.</summary>
        public static WorldMapLoader FromRepo(string repoRoot, BuildingLevelKinds levels = null) =>
            new WorldMapLoader(repoRoot, WorldCatalog.Load(Path.Combine(repoRoot, WorldCatalog.DefaultRelativePath)),
                BuildingCatalog.Load(Path.Combine(repoRoot, BuildingCatalog.DefaultRelativePath)), levels);

        WorldMap IWorldMapSource.Load(string mapId) => LoadMap(mapId).World;

        public LoadedMap LoadMap(string mapId)
        {
            if (!_maps.TryGetValue(mapId ?? string.Empty, out LoadedMap cached))
            {
                MapEntry entry = Catalog.Find(mapId) ?? throw new InvalidOperationException("맵 목록에 없는 맵: " + mapId);
                if (!_maps.TryGetValue(entry.Id, out cached))
                {
                    string ldtkPath = Path.GetFullPath(Path.Combine(_repoRoot, entry.Ldtk));
                    if (!_projects.TryGetValue(ldtkPath, out LdtkProject project))
                        _projects[ldtkPath] = project = LdtkProject.Load(ldtkPath);
                    LoadedMap map = Build(entry, project.LoadLevel(entry.Level), _repoRoot, Buildings, Levels);
                    _maps[entry.Id] = map;
                    return map;
                }
            }
            if (!IsStale(cached)) return cached;
            LoadedMap rebuilt = Build(cached.Entry, cached.Level, _repoRoot, Buildings, Levels);
            _maps[cached.Entry.Id] = rebuilt;
            return rebuilt;
        }

        /// <summary>부지의 지금 단계가 만들 때와 다른가. 부지가 없는 맵은 언제나 false. 새 객체를 만들지 않는다(프레임마다 불러도 된다).</summary>
        public bool IsStale(LoadedMap map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            List<LotEntry> lots = map.Entry.Lots;
            if (lots.Count != map.LotLevels.Length) return true;
            for (int i = 0; i < lots.Count; i++)
            {
                Levels.TryLevelOf(lots[i].Level, out int now);
                if (now != map.LotLevels[i]) return true;
            }
            return false;
        }

        /// <summary>건물 목록 없이 만든다. 부지가 있으면 건물을 찾지 못한 문제로 남는다.</summary>
        public static LoadedMap Build(MapEntry entry, LdtkLevel level) => Build(entry, level, null, null, null);

        /// <summary>맵 하나를 만든다. repoRoot는 건물 그림 경로의 뿌리. buildings·levels가 없으면 빈 목록과 fixed만.</summary>
        public static LoadedMap Build(MapEntry entry, LdtkLevel level, string repoRoot, BuildingCatalog buildings, BuildingLevelKinds levels)
        {
            var map = new LoadedMap { Entry = entry, Level = level };
            var doors = new List<DoorArea>();
            CollisionInput input = CollisionInputOf(entry, level, doors, map.Problems);
            ICollisionRule rule = CollisionRules.Create(entry.CollisionRule, entry.CollisionOptions);
            CollisionGrid collision = Open(CollisionBaker.Bake(input, rule), entry.OpenCells);
            map.Picture = MapPicture.From(level);
            map.Problems.AddRange(map.Picture.Problems);
            if (entry.Lots.Count > 0)
                collision = PlaceLots(map, collision, doors, repoRoot, buildings ?? BuildingCatalog.Empty, levels ?? BuildingLevelKinds.WithFixed());
            map.World = new WorldMap(entry.Id, collision, doors, entry.Spawns);
            return map;
        }

        /// <summary>
        /// 부지마다 지금 단계의 건물을 놓는다: 막는 칸을 충돌에 더하고, 목적지가 있으면 문 칸을 문으로, 건물 그림을 발 위치(건물 아래 끝)로 정렬해 끼운다.
        /// 목적지가 없는 건물은 배경이라 문 칸도 막는다. 문제가 있는 부지는 건너뛰고 LotProblems에 남긴다.
        /// </summary>
        private static CollisionGrid PlaceLots(LoadedMap map, CollisionGrid collision, List<DoorArea> doors, string repoRoot, BuildingCatalog buildings, BuildingLevelKinds levels)
        {
            MapEntry entry = map.Entry;
            MapGrid grid = collision.Grid;
            int cell = grid.CellSize;
            map.LotLevels = new int[entry.Lots.Count];
            var lots = new List<BuildingLot>(entry.Lots.Count);
            foreach (LotEntry lot in entry.Lots) lots.Add(lot.ToLot());
            map.LotProblems.AddRange(BuildingPlacement.CheckLots(lots, grid));
            var blocked = new List<CellPos>();
            var arts = new List<(BuildingDoorArt art, SpriteDraw building)>();
            for (int i = 0; i < entry.Lots.Count; i++)
            {
                LotEntry lot = entry.Lots[i];
                if (!levels.TryLevelOf(lot.Level, out int raw))
                    map.LotProblems.Add("부지 " + lot.Name + "의 단계 종류 " + lot.Level.Kind + "를 모른다(있는 것: " + string.Join(", ", levels.Names) + ") — 단계 1로 놓는다");
                map.LotLevels[i] = raw;
                BuildingData building = buildings.Find(lot.Building);
                if (building == null) { map.LotProblems.Add("부지 " + lot.Name + "의 건물 " + lot.Building + "이 건물 목록에 없다"); continue; }
                if (building.Levels.Count == 0) { map.LotProblems.Add("건물 " + building.Id + "에 단계가 없다"); continue; }
                int number = BuildingPlacement.ClampLevel(raw, building.Levels.Count);
                BuildingLevelData data = building.Levels[number - 1];
                string what = "부지 " + lot.Name + "의 건물 " + building.Id + " 단계 " + number;
                if (data.Src.W <= 0 || data.Src.H <= 0 || data.Src.W % cell != 0 || data.Src.H % cell != 0)
                {
                    map.LotProblems.Add(what + "의 그림 사각형 " + data.Src + "이 칸 크기 " + cell + "의 배수가 아니다");
                    continue;
                }
                BuildingFootprint footprint;
                try { footprint = new BuildingFootprint(data.Src.W / cell, data.Src.H / cell, data.Solid, data.DoorCell); }
                catch (ArgumentException e) { map.LotProblems.Add(what + ": " + e.Message); continue; }
                BuildingPlacementResult placed = BuildingPlacement.Place(lots[i], footprint);
                if (!placed.Ok) { map.LotProblems.Add(what + ": " + placed.Problem); continue; }

                blocked.AddRange(placed.Blocked);
                DoorArea door = null;
                if (lot.To.HasValue) doors.Add(door = new DoorArea(lot.Name, placed.DoorArea(cell), lot.To, lot.OpenSeconds ?? entry.DoorOpenSeconds));
                else blocked.Add(placed.Door);

                string texture = repoRoot == null ? null : Path.GetFullPath(Path.Combine(repoRoot, data.Texture));
                if (texture == null || !File.Exists(texture)) { map.LotProblems.Add(what + "의 그림이 저장소에 없어 그리지 않았다: " + data.Texture); continue; }
                float x = placed.TopLeft.X * cell, y = placed.TopLeft.Y * cell, bottom = y + data.Src.H;
                var picture = new SpriteDraw(texture, data.Src.X, data.Src.Y, data.Src.W, data.Src.H, x, y, data.Src.W, data.Src.H, 0, 1f, bottom);
                map.Picture.AddSorted(picture);
                if (door == null || data.DoorFrames.Count == 0) continue;
                var frames = new SpriteDraw[data.DoorFrames.Count];
                for (int f = 0; f < frames.Length; f++)
                {
                    ImageRect r = data.DoorFrames[f];
                    frames[f] = new SpriteDraw(texture, r.X, r.Y, r.W, r.H, x + data.DoorAtX, y + data.DoorAtY, r.W, r.H, 0, 1f, bottom);
                }
                var art = new BuildingDoorArt(door, frames, -1);
                map.DoorArt.Add(art);
                arts.Add((art, picture));
            }
            // 나중에 끼운 건물이 앞 번호를 밀므로 다 끼운 뒤에 자리를 찾는다.
            foreach (var (art, building) in arts) art.SortedIndex = IndexOf(map.Picture.Sorted, building);
            return blocked.Count == 0 ? collision : Block(collision, blocked);
        }

        private static int IndexOf(List<SpriteDraw> sorted, SpriteDraw d)
        {
            for (int i = 0; i < sorted.Count; i++)
            {
                SpriteDraw s = sorted[i];
                if (s.Texture == d.Texture && s.X == d.X && s.Y == d.Y && s.SrcX == d.SrcX && s.SrcY == d.SrcY && s.W == d.W && s.H == d.H) return i;
            }
            return -1;
        }

        /// <summary>칸들을 막은 새 충돌(맵 밖에 열린 칸은 그대로).</summary>
        private static CollisionGrid Block(CollisionGrid baked, List<CellPos> cells)
        {
            MapGrid grid = baked.Grid;
            var blocked = new bool[grid.Count];
            for (int y = 0; y < grid.Rows; y++)
                for (int x = 0; x < grid.Columns; x++) blocked[grid.Index(x, y)] = baked.IsBlocked(x, y);
            foreach (CellPos c in cells) if (grid.Contains(c.X, c.Y)) blocked[grid.Index(c.X, c.Y)] = true;
            return new CollisionGrid(grid, blocked, baked.OpenOutside);
        }

        /// <summary>충돌 규칙에 넘길 내용: 타일 층, 칠한 Collision 층, 목적지가 있는 문의 상자. doors에는 모든 문을 담는다.</summary>
        public static CollisionInput CollisionInputOf(MapEntry entry, LdtkLevel level, List<DoorArea> doors, List<string> problems)
        {
            int cell = CellSizeOf(level);
            if (level.PixelWidth % cell != 0 || level.PixelHeight % cell != 0)
                problems.Add(level.Name + " 크기 " + level.PixelWidth + "x" + level.PixelHeight + "가 칸 " + cell + "로 나눠지지 않는다");
            var grid = new MapGrid(level.PixelWidth / cell, level.PixelHeight / cell, cell);

            var layers = new List<MapLayer>();
            int[] painted = null;
            foreach (LdtkLayer layer in level.Layers)
            {
                if (layer.Type == "IntGrid" && layer.Name == PaintedCollision.LayerName && layer.IntGrid != null)
                {
                    if (layer.GridSize == cell && layer.IntGrid.Length == grid.Count) painted = ShiftRows(layer.IntGrid, grid.Columns, entry.EntityRowShift);
                    else problems.Add("Collision 층 격자(" + layer.GridSize + ")가 맵 칸과 달라 쓰지 않았다");
                    continue;
                }
                if (layer.Tiles.Count == 0 || layer.GridSize != cell) continue;
                var occupied = new bool[grid.Count];
                foreach (LdtkTile t in layer.Tiles)
                {
                    int cx = (t.Px + layer.OffsetX) / cell, cy = (t.Py + layer.OffsetY) / cell;
                    if (grid.Contains(cx, cy)) occupied[grid.Index(cx, cy)] = true;
                }
                layers.Add(new MapLayer(layer.Name, occupied));
            }

            var routes = new DoorRoutes(entry.Doors);
            var exits = new List<AreaRect>();
            foreach (LdtkEntity e in level.Entities(entry.DoorEntity))
            {
                string where = e.Field(entry.DoorField);
                TravelTarget? destination = routes.Resolve(where, entry.SpawnField != null ? e.Field(entry.SpawnField) : null);
                DoorArea door = DoorArea.FromEntity(e.Left, e.Top + entry.EntityRowShift * cell, e.Width, e.Height, where, cell, destination, entry.DoorOpenSeconds);
                doors.Add(door);
                if (destination.HasValue) exits.Add(door.Area);
            }
            foreach (MapArea area in entry.Areas)
            {
                var door = new DoorArea(area.Name, area.Rect, area.Destination, area.OpenSeconds);
                doors.Add(door);
                exits.Add(door.Area);
            }
            return new CollisionInput(grid, layers, exits, painted);
        }

        /// <summary>맵 목록의 openCells를 연다(Unity 씬의 구운 충돌에 없던 칸).</summary>
        private static CollisionGrid Open(CollisionGrid baked, List<CellPos> openCells)
        {
            if (openCells == null || openCells.Count == 0) return baked;
            MapGrid grid = baked.Grid;
            var blocked = new bool[grid.Count];
            for (int y = 0; y < grid.Rows; y++)
                for (int x = 0; x < grid.Columns; x++) blocked[grid.Index(x, y)] = baked.IsBlocked(x, y);
            foreach (CellPos c in openCells) if (grid.Contains(c.X, c.Y)) blocked[grid.Index(c.X, c.Y)] = false;
            var outside = new List<CellPos>();
            for (int y = -4; y < grid.Rows + 4; y++) // 출구 상자가 맵 밖으로 몇 칸까지 연 칸
                for (int x = -4; x < grid.Columns + 4; x++)
                    if (!grid.Contains(x, y) && !baked.IsBlocked(x, y)) outside.Add(new CellPos(x, y));
            return new CollisionGrid(grid, blocked, outside);
        }

        private static int[] ShiftRows(int[] cells, int columns, int rows)
        {
            if (rows == 0) return cells;
            var shifted = new int[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                int j = i + rows * columns;
                if (j >= 0 && j < cells.Length) shifted[j] = cells[i];
            }
            return shifted;
        }

        /// <summary>칸 크기: Collision IntGrid 층이 있으면 그 격자, 없으면 타일이 가장 많은 타일 층의 격자.</summary>
        public static int CellSizeOf(LdtkLevel level)
        {
            LdtkLayer painted = level.Layer(PaintedCollision.LayerName);
            if (painted != null && painted.Type == "IntGrid") return painted.GridSize;
            int best = 0, size = 0;
            foreach (LdtkLayer layer in level.Layers)
                if (layer.Type == "Tiles" && layer.Tiles.Count >= best && layer.Tiles.Count > 0) { best = layer.Tiles.Count; size = layer.GridSize; }
            if (size > 0) return size;
            foreach (LdtkLayer layer in level.Layers) if (layer.Type == "Tiles") return layer.GridSize;
            throw new InvalidDataException(level.Name + "에 칸 크기를 알 수 있는 층이 없다");
        }
    }
}
