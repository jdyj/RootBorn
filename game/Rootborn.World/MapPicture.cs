using System;
using System.Collections.Generic;
using Rootborn.World.Ldtk;

namespace Rootborn.World
{
    /// <summary>그림 한 장: 텍스처의 원본 사각형을 맵 픽셀 사각형에 그린다. SortY는 발 위치(앞뒤를 가릴 때만 쓴다).</summary>
    public readonly struct SpriteDraw
    {
        public readonly string Texture;
        public readonly int SrcX, SrcY, SrcW, SrcH, Flip;
        public readonly float X, Y, W, H, Alpha, SortY;

        public SpriteDraw(string texture, int srcX, int srcY, int srcW, int srcH, float x, float y, float w, float h, int flip, float alpha, float sortY)
        {
            Texture = texture; SrcX = srcX; SrcY = srcY; SrcW = srcW; SrcH = srcH;
            X = x; Y = y; W = w; H = h; Flip = flip; Alpha = alpha; SortY = sortY;
        }
    }

    /// <summary>
    /// 레벨을 그리는 순서. Unity 판처럼 타일 층은 모두 캐릭터 아래에 깐다(Unity 판 캐릭터 정렬값 10000 이상, 타일 층 0~1001).
    /// "ysort" 태그가 붙은 층의 그림만 캐릭터와 발 위치(SortY)로 앞뒤를 가린다(Unity 판 GroundDepthSort: 아래쪽에 선 것이 앞).
    /// 엔티티는 "art"나 "ysort" 태그가 붙은 층의 것만 그린다(문 같은 표시용 엔티티는 그리지 않는다).
    /// </summary>
    public sealed class MapPicture
    {
        public const string ArtTag = "art";
        public const string DepthSortTag = "ysort";

        public readonly List<SpriteDraw> Under = new List<SpriteDraw>();
        /// <summary>SortY 오름차순(같으면 원본 순서).</summary>
        public readonly List<SpriteDraw> Sorted = new List<SpriteDraw>();
        public readonly List<string> Problems = new List<string>();
        public readonly HashSet<string> Textures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 건너뛴 타일 층(<c>skipTileLayers</c>) 중 그리기 순서상 가장 아래 층이 <see cref="Under"/>에 들어갔을 자리. 그리는 쪽이 Under[0..ReplacedAt) →
        /// 대신 그리는 그림 → 나머지 순으로 그린다(짓는 집의 조합한 벽·바닥 — ComposedMapTiles). 그 층이 비었거나 숨겨져 있어도 자리는 정해진다
        /// (게임이 그 층을 대신 그리므로 편집기에서 비우거나 숨겨도 된다). 건너뛸 층이 레벨에 하나도 없으면 -1.
        /// </summary>
        public int ReplacedAt { get; private set; } = -1;

        /// <param name="skipTileLayers">타일을 그리지 않을 층 이름(그 층의 엔티티는 그대로). null이면 모두 그린다.</param>
        public static MapPicture From(LdtkLevel level, IReadOnlyCollection<string> skipTileLayers = null)
        {
            var picture = new MapPicture();
            var sorted = new List<(SpriteDraw draw, int order)>();
            foreach (LdtkLayer layer in level.Layers)
            {
                bool skip = skipTileLayers != null && Contains(skipTileLayers, layer.Name);
                if (skip && picture.ReplacedAt < 0) picture.ReplacedAt = picture.Under.Count;
                if (!layer.Visible) continue;
                bool depth = layer.Tags.Contains(DepthSortTag);
                if (!skip && layer.Tiles.Count > 0)
                {
                    if (layer.TilesetMissing || layer.TilesetPath == null)
                    {
                        picture.Problems.Add("층 " + layer.Name + "의 타일셋 그림이 저장소에 없어 그리지 않았다");
                        continue;
                    }
                    int g = layer.GridSize;
                    foreach (LdtkTile t in layer.Tiles)
                    {
                        float x = t.Px + layer.OffsetX, y = t.Py + layer.OffsetY;
                        var draw = new SpriteDraw(layer.TilesetPath, t.SrcX, t.SrcY, g, g, x, y, g, g, t.Flip, t.Alpha * layer.Opacity, y + g / 2f);
                        picture.Textures.Add(layer.TilesetPath);
                        if (depth) sorted.Add((draw, sorted.Count)); else picture.Under.Add(draw);
                    }
                }
                if (!depth && !layer.Tags.Contains(ArtTag)) continue;
                foreach (LdtkEntity e in layer.Entities)
                {
                    if (e.MissingTileset != null && picture.Problems.Count < 200)
                    {
                        string problem = "층 " + layer.Name + "의 엔티티 " + e.Name + " 그림 타일셋이 저장소에 없어 그리지 않았다: " + e.MissingTileset;
                        if (!picture.Problems.Contains(problem)) picture.Problems.Add(problem);
                    }
                    if (!e.HasTile) continue;
                    var draw = new SpriteDraw(e.TilesetPath, e.TileX, e.TileY, e.TileW, e.TileH, e.Left + layer.OffsetX, e.Top + layer.OffsetY, e.Width, e.Height, 0, layer.Opacity, e.Y + layer.OffsetY);
                    picture.Textures.Add(e.TilesetPath);
                    if (depth) sorted.Add((draw, sorted.Count)); else picture.Under.Add(draw);
                }
            }
            sorted.Sort((a, b) => a.draw.SortY != b.draw.SortY ? a.draw.SortY.CompareTo(b.draw.SortY) : a.order.CompareTo(b.order));
            foreach (var s in sorted) picture.Sorted.Add(s.draw);
            return picture;
        }

        private static bool Contains(IReadOnlyCollection<string> names, string name)
        {
            foreach (string n in names) if (string.Equals(n, name, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>앞뒤를 가리는 그림 하나를 정렬을 지키며 끼운다(같은 발 위치면 이미 있는 것 뒤 — 원본 순서). 넣은 자리를 돌려준다.</summary>
        public int AddSorted(SpriteDraw draw)
        {
            int at = ActorIndex(draw.SortY);
            Sorted.Insert(at, draw);
            if (draw.Texture != null) Textures.Add(draw.Texture);
            return at;
        }

        /// <summary>발 위치가 actorSortY인 캐릭터를 끼워 넣을 자리: 이 앞의 그림은 캐릭터보다 먼저(뒤에) 그린다. 같으면 캐릭터가 앞.</summary>
        public int ActorIndex(float actorSortY)
        {
            int lo = 0, hi = Sorted.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (Sorted[mid].SortY <= actorSortY) lo = mid + 1; else hi = mid;
            }
            return lo;
        }
    }
}
