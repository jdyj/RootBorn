using System;
using System.Collections.Generic;
using Rootborn.Core.World;

namespace Rootborn.Core.Building
{
    /// <summary>픽셀 출처 종류 하나: 이름(시험 표의 src), 값을 담는 필드(shade 또는 piece), 주인 칸이 있는가.
    /// 게임은 조합할 때 이름 → 처리 표로 고른다. 종류를 더하는 일 = <see cref="TileSources"/>에 한 줄 + 그 종류를 내는 규칙.</summary>
    public sealed class TileSourceKind
    {
        public const string ShadeField = "shade", PieceField = "piece";

        internal TileSourceKind(string name, string field, bool hasOwner)
        {
            Name = name;
            Field = field;
            HasOwner = hasOwner;
        }

        public string Name { get; }
        public string Field { get; }
        public bool HasOwner { get; }

        public override string ToString() => Name;
    }

    /// <summary>출처 종류 표(픽셀 키트 room_tiles.SOURCES, 시험 표 머리의 sources와 같다 — RoomTileVectorTests가 견준다).</summary>
    public static class TileSources
    {
        /// <summary>띠 칠: o 외곽선, d 안쪽 그늘, f 면, l 안쪽 빛.</summary>
        public const string Band = "band";
        /// <summary>꽉 찬 정면 조각 face:&lt;k&gt;[:&lt;lrct&gt;](늘인 정면은 l·r 없는 꼴). 주인 = 그 벽 면이 마주 보는 바닥.</summary>
        public const string Face = "face";
        /// <summary>좁은 정면 조각 face:&lt;k&gt;:lr[c]@&lt;x0&gt;-&lt;x1&gt;.</summary>
        public const string Narrow = "narrow";
        /// <summary>바닥: 값은 그 칸의 Ground 열쇠(floor, floor:shade, floor:shade:&lt;x0&gt;-&lt;x1&gt;).</summary>
        public const string Floor = "floor";
        /// <summary>문지방 조각 sill:&lt;n|e|w&gt;:&lt;a&gt;-&lt;b&gt;(벽지와 무관한 나무).</summary>
        public const string Sill = "sill";
        /// <summary>정면 문짝 조각 door.</summary>
        public const string Door = "door";

        private static readonly List<TileSourceKind> Kinds = new List<TileSourceKind>
        {
            new TileSourceKind(Band, TileSourceKind.ShadeField, false),
            new TileSourceKind(Face, TileSourceKind.PieceField, true),
            new TileSourceKind(Narrow, TileSourceKind.PieceField, true),
            new TileSourceKind(Floor, TileSourceKind.ShadeField, true),
            new TileSourceKind(Sill, TileSourceKind.PieceField, false),
            new TileSourceKind(Door, TileSourceKind.PieceField, false),
        };

        private static readonly Dictionary<string, TileSourceKind> ByName = Index();

        public static IReadOnlyList<TileSourceKind> All => Kinds;

        /// <summary>이름으로 찾는다. 모르는 이름은 KeyNotFoundException.</summary>
        public static TileSourceKind Find(string name) =>
            ByName.TryGetValue(name ?? "", out var k) ? k : throw new KeyNotFoundException("모르는 출처 종류 " + name);

        private static Dictionary<string, TileSourceKind> Index()
        {
            var d = new Dictionary<string, TileSourceKind>(Kinds.Count, StringComparer.Ordinal);
            foreach (var k in Kinds)
                d.Add(k.Name, k);
            return d;
        }
    }

    /// <summary>칸 안 픽셀 출처표의 한 부분(README "방 만들기 — 시험 표"). 한 칸의 부분들은 겹치지 않고, 합집합은 그 칸의 불투명 픽셀 전부다.</summary>
    public sealed class TilePart
    {
        internal TilePart(TileSourceKind kind, string value, bool hasOwner, CellPos owner, IReadOnlyList<PixelRect> rects)
        {
            if (kind.HasOwner != hasOwner)
                throw new InvalidOperationException($"{kind.Name}: 주인(own)이 {(kind.HasOwner ? "있어야" : "없어야")} 한다");
            Kind = kind;
            Shade = kind.Field == TileSourceKind.ShadeField ? value : null;
            Piece = kind.Field == TileSourceKind.PieceField ? value : null;
            HasOwner = hasOwner;
            Owner = hasOwner ? owner : new CellPos(0, 0);
            Rects = rects;
            Pixels = PixelRects.Text(rects);
        }

        public TileSourceKind Kind { get; }
        /// <summary>출처 종류 이름(<see cref="TileSources"/>의 상수).</summary>
        public string Source => Kind.Name;
        /// <summary>band의 칠(o/d/f/l)이나 floor의 Ground 열쇠. 해당 없으면 null.</summary>
        public string Shade { get; }
        /// <summary>face·narrow·sill·door의 조각 이름. 해당 없으면 null.</summary>
        public string Piece { get; }
        public bool HasOwner { get; }
        /// <summary>꾸밈 주인 바닥 칸의 이 칸에 대한 상대 위치(y 아래로 +). 주인이 없는 종류는 (0, 0).</summary>
        public CellPos Owner { get; }
        public IReadOnlyList<PixelRect> Rects { get; }
        /// <summary>Rects의 정규형 글(Python _encode(픽셀, 0, 32)).</summary>
        public string Pixels { get; }
    }

    /// <summary>칸 하나의 출처표.</summary>
    public sealed class CellTiles
    {
        internal CellTiles(CellPos cell, string kind, IReadOnlyList<TilePart> parts)
        {
            Cell = cell;
            Kind = kind;
            Parts = parts;
        }

        public CellPos Cell { get; }
        /// <summary>칸의 종류(Python classify 값): floor, floor:shade, cap, face:&lt;k&gt;[:&lt;lrct&gt;](좁은 정면 옆에서 끝 표시를 떼기 전).</summary>
        public string Kind { get; }
        /// <summary>부분 목록. 순서: 출처 이름, 값, 주인(없는 것 먼저, dx 다음 dy), 픽셀 글 — 글은 ordinal(시험 표와 같다).</summary>
        public IReadOnlyList<TilePart> Parts { get; }
    }
}
