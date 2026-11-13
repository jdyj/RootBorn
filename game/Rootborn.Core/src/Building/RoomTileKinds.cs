using System.Text;

namespace Rootborn.Core.Building
{
    /// <summary>칸의 종류(픽셀 키트 room_tiles의 _kinds·classify를 옮긴 것).
    /// 벽 칸은 아래로 faceHeight칸 안에 바닥이 나오고 그 사이가 모두 벽이면 정면 k(1이 바닥 바로 위), 아니면 윗면.
    /// 정면 열쇠는 "face:&lt;k&gt;[:&lt;lrct&gt;]" — l 왼쪽 끝, r 오른쪽 끝, c 위가 윗면(천장 밑 그늘), t 위가 열림(벽이 아님).</summary>
    internal static class RoomTileKinds
    {
        // 칸 표시 값: 밖 -1, 바닥 0, 윗면 -2, 정면은 k(1 이상).
        internal const int Out = -1, Floor = 0, Cap = -2;
        internal const string CapKey = "cap";
        internal const string FloorKey = "floor", FloorShadeKey = "floor:shade";

        internal static int[] Tags(RoomGrid grid, int faceHeight)
        {
            int w = grid.Columns, h = grid.Rows;
            var tags = new int[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int v = grid.At(x, y), tag = Out;
                    if (v == RoomCells.Floor)
                        tag = Floor;
                    else if (v == RoomCells.Wall)
                    {
                        tag = Cap;
                        for (int d = 1; d <= faceHeight; d++)
                        {
                            int below = grid.At(x, y + d);
                            if (below == RoomCells.Floor)
                            {
                                tag = d;
                                break;
                            }
                            if (below != RoomCells.Wall)
                                break;
                        }
                    }
                    tags[y * w + x] = tag;
                }
            return tags;
        }

        internal static int TagAt(int[] tags, int w, int h, int x, int y) =>
            x >= 0 && y >= 0 && x < w && y < h ? tags[y * w + x] : Out;

        /// <summary>칸마다 classify 값(밖은 null).</summary>
        internal static string[] Classify(int[] tags, int w, int h)
        {
            var keys = new string[tags.Length];
            var sb = new StringBuilder(12);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int k = tags[y * w + x];
                    if (k == Out)
                        continue;
                    if (k == Floor)
                    {
                        keys[y * w + x] = TagAt(tags, w, h, x, y - 1) > 0 ? FloorShadeKey : FloorKey;
                        continue;
                    }
                    if (k == Cap)
                    {
                        keys[y * w + x] = CapKey;
                        continue;
                    }
                    sb.Clear();
                    int left = TagAt(tags, w, h, x - 1, y), right = TagAt(tags, w, h, x + 1, y);
                    if (left != k && left != Cap) sb.Append('l');       // 같은 높이 정면이면 이어지고, 윗면이면 그쪽이 선을 친다
                    if (right != k && right != Cap) sb.Append('r');
                    int above = TagAt(tags, w, h, x, y - 1);
                    if (above == Cap) sb.Append('c');
                    else if (above <= 0) sb.Append('t');                 // 밖이거나 바닥(벽이 아님)
                    keys[y * w + x] = sb.Length > 0 ? "face:" + k + ":" + sb : "face:" + k;
                }
            return keys;
        }

        /// <summary>정면 열쇠에서 끝 표시(l, r)를 뺀 것 — 옆으로 늘여 그릴 때 쓴다.</summary>
        internal static string StripLeftRight(string faceKey)
        {
            var parts = faceKey.Split(':');
            string flags = parts.Length == 3 ? parts[2].Replace("l", "").Replace("r", "") : "";
            return flags.Length > 0 ? "face:" + parts[1] + ":" + flags : "face:" + parts[1];
        }

        /// <summary>정면 열쇠에 끝 표시가 양쪽(l·r) 다 있는가(좁은 정면 후보).</summary>
        internal static bool HasBothEnds(string faceKey)
        {
            var parts = faceKey.Split(':');
            return parts.Length == 3 && parts[2].IndexOf('l') >= 0 && parts[2].IndexOf('r') >= 0;
        }
    }
}
