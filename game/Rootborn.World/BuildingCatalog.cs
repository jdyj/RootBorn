using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Rootborn.Core.World;

namespace Rootborn.World
{
    /// <summary>그림 안의 픽셀 사각형(PNG 왼쪽 위 기준).</summary>
    public readonly struct ImageRect : IEquatable<ImageRect>
    {
        public readonly int X, Y, W, H;

        public ImageRect(int x, int y, int w, int h)
        {
            X = x; Y = y; W = w; H = h;
        }

        public bool Equals(ImageRect o) => X == o.X && Y == o.Y && W == o.W && H == o.H;
        public override bool Equals(object obj) => obj is ImageRect o && Equals(o);
        public override int GetHashCode() => X ^ (Y * 31) ^ (W * 131) ^ (H * 1031);
        public override string ToString() => "[" + X + "," + Y + " " + W + "x" + H + "]";
    }

    /// <summary>
    /// 건물 한 단계의 겉모습: 그림(저장소 뿌리 기준 경로)과 그 안의 사각형, 막는 칸·문 칸(그림 왼쪽 위 칸 기준),
    /// 문 프레임(닫힘 → 열림, 같은 그림 안의 사각형 — 없어도 된다)과 그 프레임을 그릴 자리(그림 왼쪽 위 기준 픽셀).
    /// 칸 크기는 여기 적지 않는다(얹는 맵의 칸 크기로 나눈다).
    /// </summary>
    public sealed class BuildingLevelData
    {
        public string Texture;
        public ImageRect Src;
        public CellRect Solid;
        public CellPos DoorCell;
        public readonly List<ImageRect> DoorFrames = new List<ImageRect>();
        public int DoorAtX, DoorAtY;
    }

    /// <summary>건물 하나: id와 단계들(배열 순서가 단계 1, 2, 3 …).</summary>
    public sealed class BuildingData
    {
        public string Id;
        public readonly List<BuildingLevelData> Levels = new List<BuildingLevelData>();
    }

    /// <summary>
    /// 건물 목록(game/world/buildings.json). 건물은 맵에 칠하지 않고 데이터로 둔다 — 맵에는 부지(maps.json lots)만 있다.
    /// 값의 모양이 틀리면(빠진 칸, 숫자 아닌 값) 읽을 때 알린다. 칸 크기에 맞는지, 문 칸이 막는 칸 안인지는 맵에 얹을 때 문제로 모은다.
    /// </summary>
    public sealed class BuildingCatalog
    {
        public const string DefaultRelativePath = "game/world/buildings.json";

        public static BuildingCatalog Empty => new BuildingCatalog();

        public readonly List<BuildingData> Buildings = new List<BuildingData>();

        public BuildingData Find(string id)
        {
            foreach (BuildingData b in Buildings) if (string.Equals(b.Id, id, StringComparison.Ordinal)) return b;
            return null;
        }

        public static BuildingCatalog Load(string path) => Parse(File.ReadAllText(path), path);

        public static BuildingCatalog Parse(string json, string sourceName = "buildings.json")
        {
            using JsonDocument doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var catalog = new BuildingCatalog();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement b in doc.RootElement.GetProperty("buildings").EnumerateArray())
            {
                var building = new BuildingData { Id = Required(b, "id", sourceName) };
                if (!seen.Add(building.Id)) throw new InvalidDataException(sourceName + ": 건물 id가 겹친다: " + building.Id);
                foreach (JsonElement l in b.GetProperty("levels").EnumerateArray())
                {
                    JsonElement src = l.GetProperty("src"), solid = l.GetProperty("solid"), door = l.GetProperty("door"), cell = door.GetProperty("cell");
                    var level = new BuildingLevelData
                    {
                        Texture = Required(l, "texture", sourceName),
                        Src = new ImageRect(src[0].GetInt32(), src[1].GetInt32(), src[2].GetInt32(), src[3].GetInt32()),
                        Solid = new CellRect(solid[0].GetInt32(), solid[1].GetInt32(), solid[2].GetInt32(), solid[3].GetInt32()),
                        DoorCell = new CellPos(cell[0].GetInt32(), cell[1].GetInt32()),
                    };
                    if (door.TryGetProperty("frames", out JsonElement frames))
                    {
                        foreach (JsonElement f in frames.EnumerateArray())
                            level.DoorFrames.Add(new ImageRect(f[0].GetInt32(), f[1].GetInt32(), f[2].GetInt32(), f[3].GetInt32()));
                        if (level.DoorFrames.Count > 0)
                        {
                            if (!door.TryGetProperty("at", out JsonElement at)) throw new InvalidDataException(sourceName + ": 건물 " + building.Id + "의 문 프레임에 그릴 자리(at)가 없다");
                            level.DoorAtX = at[0].GetInt32();
                            level.DoorAtY = at[1].GetInt32();
                        }
                    }
                    building.Levels.Add(level);
                }
                catalog.Buildings.Add(building);
            }
            return catalog;
        }

        private static string Required(JsonElement e, string name, string source)
        {
            if (!e.TryGetProperty(name, out JsonElement v) || v.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(v.GetString()))
                throw new InvalidDataException(source + ": '" + name + "' 값이 없다");
            return v.GetString();
        }
    }
}
