using System;
using System.Collections.Generic;

namespace Rootborn.Core.World
{
    /// <summary>충돌 규칙에 넘기는 맵 내용: 타일 층, 출구 상자, 손으로 칠한 Collision 층(없으면 null).</summary>
    public sealed class CollisionInput
    {
        public readonly MapGrid Grid;
        public readonly IReadOnlyList<MapLayer> TileLayers;
        public readonly IReadOnlyList<AreaRect> ExitAreas;
        public readonly int[] Painted;

        public CollisionInput(MapGrid grid, IReadOnlyList<MapLayer> tileLayers, IReadOnlyList<AreaRect> exitAreas, int[] painted)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            TileLayers = tileLayers ?? Array.Empty<MapLayer>();
            ExitAreas = exitAreas ?? Array.Empty<AreaRect>();
            foreach (MapLayer layer in TileLayers)
                if (layer.Count != grid.Count) throw new ArgumentException("층 " + layer.Name + "의 칸 수가 맵과 다르다");
            if (painted != null && painted.Length != grid.Count) throw new ArgumentException("Collision 층의 칸 수가 맵과 다르다");
            Painted = painted;
        }

        /// <summary>
        /// 상자가 덮는 칸. 경계에서 0.01칸 안쪽까지만 친다(Unity 판 굽기와 같다: floor(가장자리 ± 0.01)).
        /// </summary>
        public static List<CellPos> CellsUnder(AreaRect area, int cellSize)
        {
            var cells = new List<CellPos>();
            int x0 = (int)Math.Floor(area.X / cellSize + 0.01f), x1 = (int)Math.Floor(area.Right / cellSize - 0.01f);
            int y0 = (int)Math.Floor(area.Y / cellSize + 0.01f), y1 = (int)Math.Floor(area.Bottom / cellSize - 0.01f);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    cells.Add(new CellPos(x, y));
            return cells;
        }
    }

    /// <summary>타일 층으로 막힌 칸을 정하는 방식 하나. 새 방식은 클래스 하나와 CollisionRules 등록 한 줄이면 된다.</summary>
    public interface ICollisionRule
    {
        /// <summary>blocked(맵 칸 수 길이)에 막힌 칸을 표시한다. 칠한 Collision 층은 규칙 뒤에 따로 덮는다.</summary>
        void Mark(CollisionInput input, bool[] blocked);
    }

    /// <summary>
    /// 맵 목록이 충돌 규칙에 넘기는 값. 비어 있는(null) 값은 규칙의 기본값을 쓴다. 맵마다 다른 층 이름은 여기로만 들어온다.
    /// </summary>
    public sealed class CollisionRuleOptions
    {
        /// <summary>collider-layers: 막는 층 이름.</summary>
        public readonly IReadOnlyList<string> Layers;
        /// <summary>interior-auto: 이름에 이 글자가 들어 있으면 바닥 층.</summary>
        public readonly IReadOnlyList<string> GroundKeys;
        /// <summary>interior-auto: 이름에 이 글자가 들어 있으면 벽 층.</summary>
        public readonly IReadOnlyList<string> SolidKeys;
        /// <summary>interior-auto: 이름이 이것으로 시작하면 가구 층(막지만 문 옆 바닥은 연다).</summary>
        public readonly IReadOnlyList<string> PropPrefixes;

        public CollisionRuleOptions(IReadOnlyList<string> layers = null, IReadOnlyList<string> groundKeys = null,
            IReadOnlyList<string> solidKeys = null, IReadOnlyList<string> propPrefixes = null)
        {
            Layers = layers;
            GroundKeys = groundKeys;
            SolidKeys = solidKeys;
            PropPrefixes = propPrefixes;
        }
    }

    /// <summary>
    /// 실내 자동 판정. Unity 판 "Bake Interior Exit Doors"(InteriorDoorPortalBaker.BakeCollision)를 옮겼다.
    /// - 이름에 바닥 글자(기본 Ground)가 든 층은 바닥(ForeGround도 이름 때문에 바닥으로 친다 — Unity 판 그대로).
    /// - 이름에 벽 글자(기본 Wall·Border)가 든 층과 가구 층(이름이 가구 앞부분 — 기본 Prop — 으로 시작)은 막는 층.
    ///   지금 맵의 가구 층 이름(Police·Kitchen·Shop…)은 맵 목록이 넘긴다.
    /// - 출구 상자가 덮는 칸은 열고, 그 둘레 8칸 중 벽이 아닌 바닥도 연다(문 앞 가구가 출구를 막지 않게).
    /// - 타일이 있는 범위에 한 칸 테두리를 더한 안에서, 막는 층이 없는 바닥과 열린 칸 말고는 모두 막는다.
    /// </summary>
    public sealed class InteriorAutoCollision : ICollisionRule
    {
        public static readonly string[] DefaultGroundKeys = { "Ground" };
        public static readonly string[] DefaultSolidKeys = { "Wall", "Border" };
        public static readonly string[] DefaultPropPrefixes = { "Prop" };

        private readonly string[] _groundKeys, _solidKeys, _propPrefixes;

        public InteriorAutoCollision(IReadOnlyList<string> groundKeys = null, IReadOnlyList<string> solidKeys = null, IReadOnlyList<string> propPrefixes = null)
        {
            _groundKeys = Copy(groundKeys, DefaultGroundKeys);
            _solidKeys = Copy(solidKeys, DefaultSolidKeys);
            _propPrefixes = Copy(propPrefixes, DefaultPropPrefixes);
        }

        private static string[] Copy(IReadOnlyList<string> values, string[] fallback)
        {
            if (values == null) return fallback;
            var copy = new List<string>(values.Count);
            foreach (string v in values) if (!string.IsNullOrEmpty(v)) copy.Add(v);
            return copy.ToArray();
        }

        private static bool ContainsAny(string name, string[] keys)
        {
            foreach (string key in keys) if (name.IndexOf(key, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        private static bool StartsWithAny(string name, string[] keys)
        {
            foreach (string key in keys) if (name.StartsWith(key, StringComparison.Ordinal)) return true;
            return false;
        }

        public void Mark(CollisionInput input, bool[] blocked)
        {
            MapGrid grid = input.Grid;
            var ground = new bool[grid.Count];
            var solid = new bool[grid.Count];
            var walls = new bool[grid.Count];
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (MapLayer layer in input.TileLayers)
            {
                bool isGround = ContainsAny(layer.Name, _groundKeys);
                bool isProp = StartsWithAny(layer.Name, _propPrefixes);
                bool isSolid = isProp || ContainsAny(layer.Name, _solidKeys);
                for (int i = 0; i < grid.Count; i++)
                {
                    if (!layer.Has(i)) continue;
                    int x = i % grid.Columns, y = i / grid.Columns;
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                    if (isGround) ground[i] = true;
                    if (isSolid) solid[i] = true;
                    if (isSolid && !isProp) walls[i] = true;
                }
            }
            if (minX == int.MaxValue) return; // 타일이 없는 맵

            var open = new HashSet<CellPos>();
            foreach (AreaRect exit in input.ExitAreas)
                foreach (CellPos cell in CollisionInput.CellsUnder(exit, grid.CellSize))
                    open.Add(cell);
            foreach (CellPos cell in new List<CellPos>(open))
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int x = cell.X + dx, y = cell.Y + dy;
                        if (!grid.Contains(x, y)) continue;
                        int i = grid.Index(x, y);
                        if (ground[i] && !walls[i]) open.Add(new CellPos(x, y));
                    }

            for (int y = minY - 1; y <= maxY + 1; y++)
                for (int x = minX - 1; x <= maxX + 1; x++)
                {
                    if (!grid.Contains(x, y)) continue; // 맵 밖은 CollisionGrid가 언제나 막는다
                    int i = grid.Index(x, y);
                    if (open.Contains(new CellPos(x, y)) || (ground[i] && !solid[i])) continue;
                    blocked[i] = true;
                }
        }
    }

    /// <summary>
    /// 정해 둔 층의 타일이 칸 전체를 막는다. Unity 판 마을은 건물·울타리 그림 층에 TilemapCollider2D를 달았고,
    /// 그 층의 타일은 모두 Grid(칸 전체) 충돌이었다.
    /// </summary>
    public sealed class ColliderLayersCollision : ICollisionRule
    {
        private readonly HashSet<string> _layers;

        public ColliderLayersCollision(IEnumerable<string> layerNames)
        {
            _layers = new HashSet<string>(layerNames ?? Array.Empty<string>(), StringComparer.Ordinal);
        }

        public void Mark(CollisionInput input, bool[] blocked)
        {
            foreach (MapLayer layer in input.TileLayers)
            {
                if (!_layers.Contains(layer.Name)) continue;
                for (int i = 0; i < blocked.Length; i++)
                    if (layer.Has(i)) blocked[i] = true;
            }
        }
    }

    /// <summary>LDtk IntGrid "Collision" 층: 1 막음, 2 열림, 그 밖(0 포함)은 규칙의 결과를 둔다.</summary>
    public static class PaintedCollision
    {
        public const string LayerName = "Collision";
        public const int Block = 1, Open = 2;

        public static void Apply(int[] painted, bool[] blocked)
        {
            if (painted == null) return;
            for (int i = 0; i < painted.Length; i++)
            {
                if (painted[i] == Block) blocked[i] = true;
                else if (painted[i] == Open) blocked[i] = false;
            }
        }
    }

    public static class CollisionBaker
    {
        /// <summary>규칙으로 막힌 칸을 정하고 칠한 Collision 층을 덮는다. 출구 상자가 맵 밖으로 나간 칸은 걸을 수 있게 둔다.</summary>
        public static CollisionGrid Bake(CollisionInput input, ICollisionRule rule)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            var blocked = new bool[input.Grid.Count];
            rule.Mark(input, blocked);
            PaintedCollision.Apply(input.Painted, blocked);
            var outside = new List<CellPos>();
            foreach (AreaRect exit in input.ExitAreas)
                foreach (CellPos cell in CollisionInput.CellsUnder(exit, input.Grid.CellSize))
                    if (!input.Grid.Contains(cell.X, cell.Y)) outside.Add(cell);
            return new CollisionGrid(input.Grid, blocked, outside);
        }
    }

    /// <summary>맵 목록 파일이 이름으로 고르는 충돌 규칙. 새 규칙은 여기에 한 줄 더한다.</summary>
    public static class CollisionRules
    {
        private static readonly Dictionary<string, Func<CollisionRuleOptions, ICollisionRule>> Factories =
            new Dictionary<string, Func<CollisionRuleOptions, ICollisionRule>>(StringComparer.Ordinal)
            {
                { "interior-auto", o => new InteriorAutoCollision(o.GroundKeys, o.SolidKeys, o.PropPrefixes) },
                { "collider-layers", o => new ColliderLayersCollision(o.Layers) },
            };

        public static IEnumerable<string> Names => Factories.Keys;

        public static ICollisionRule Create(string name, CollisionRuleOptions options)
        {
            if (name == null || !Factories.TryGetValue(name, out var factory))
                throw new ArgumentException("모르는 충돌 규칙: " + name + " (있는 것: " + string.Join(", ", Factories.Keys) + ")");
            return factory(options ?? new CollisionRuleOptions());
        }

        public static ICollisionRule Create(string name, IReadOnlyList<string> layers) => Create(name, new CollisionRuleOptions(layers));
    }
}
