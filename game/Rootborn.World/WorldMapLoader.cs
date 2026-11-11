using System;
using System.Collections.Generic;
using System.IO;
using Rootborn.Core.World;
using Rootborn.World.Ldtk;

namespace Rootborn.World
{
    /// <summary>읽어 들인 맵: Core 월드(충돌·문·도착 지점), 그릴 목록, 원본 레벨, 그리지 못한 타일셋.</summary>
    public sealed class LoadedMap
    {
        public MapEntry Entry;
        public LdtkLevel Level;
        public WorldMap World;
        public MapPicture Picture;
        public readonly List<string> Problems = new List<string>();
    }

    /// <summary>
    /// 맵 목록과 LDtk 파일로 맵을 만든다. LDtk 프로젝트와 맵은 한 번 읽으면 기억해 둔다(문을 오갈 때 다시 읽지 않는다).
    /// 칸 크기는 Collision IntGrid 층(없으면 타일 층)의 격자에서, 칸 수는 레벨 픽셀 크기에서 얻는다.
    /// </summary>
    public sealed class WorldMapLoader : IWorldMapSource
    {
        private readonly string _repoRoot;
        private readonly Dictionary<string, LdtkProject> _projects = new Dictionary<string, LdtkProject>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, LoadedMap> _maps = new Dictionary<string, LoadedMap>(StringComparer.OrdinalIgnoreCase);

        public readonly WorldCatalog Catalog;

        public WorldMapLoader(string repoRoot, WorldCatalog catalog)
        {
            _repoRoot = repoRoot ?? throw new ArgumentNullException(nameof(repoRoot));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public static WorldMapLoader FromRepo(string repoRoot) =>
            new WorldMapLoader(repoRoot, WorldCatalog.Load(Path.Combine(repoRoot, WorldCatalog.DefaultRelativePath)));

        WorldMap IWorldMapSource.Load(string mapId) => LoadMap(mapId).World;

        public LoadedMap LoadMap(string mapId)
        {
            if (_maps.TryGetValue(mapId ?? string.Empty, out LoadedMap cached)) return cached;
            MapEntry entry = Catalog.Find(mapId) ?? throw new InvalidOperationException("맵 목록에 없는 맵: " + mapId);
            if (_maps.TryGetValue(entry.Id, out cached)) return cached;
            string ldtkPath = Path.GetFullPath(Path.Combine(_repoRoot, entry.Ldtk));
            if (!_projects.TryGetValue(ldtkPath, out LdtkProject project))
                _projects[ldtkPath] = project = LdtkProject.Load(ldtkPath);
            LoadedMap map = Build(entry, project.LoadLevel(entry.Level));
            _maps[entry.Id] = map;
            return map;
        }

        public static LoadedMap Build(MapEntry entry, LdtkLevel level)
        {
            var map = new LoadedMap { Entry = entry, Level = level };
            var doors = new List<DoorArea>();
            CollisionInput input = CollisionInputOf(entry, level, doors, map.Problems);
            ICollisionRule rule = CollisionRules.Create(entry.CollisionRule, entry.CollisionLayers);
            map.World = new WorldMap(entry.Id, CollisionBaker.Bake(input, rule), doors, entry.Spawns);
            map.Picture = MapPicture.From(level);
            map.Problems.AddRange(map.Picture.Problems);
            return map;
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
                DoorArea door = DoorArea.FromEntity(e.Left, e.Top + entry.EntityRowShift * cell, e.Width, e.Height, where, cell, destination);
                doors.Add(door);
                if (destination.HasValue) exits.Add(door.Area);
            }
            return new CollisionInput(grid, layers, exits, painted);
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
