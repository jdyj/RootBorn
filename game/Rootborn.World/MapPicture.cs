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

        public static MapPicture From(LdtkLevel level)
        {
            var picture = new MapPicture();
            var sorted = new List<(SpriteDraw draw, int order)>();
            foreach (LdtkLayer layer in level.Layers)
            {
                if (!layer.Visible) continue;
                bool depth = layer.Tags.Contains(DepthSortTag);
                if (layer.Tiles.Count > 0)
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
