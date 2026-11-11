using System;
using System.IO;
using System.Text.Json;

namespace Rootborn.World
{
    /// <summary>
    /// 월드의 조작감·연출 수치(game/world/presentation.json): 카메라 따라가기, 클릭 행동, 문 열림 덮개, 구역 페이드. 형식은 그 파일 머리의 주석.
    /// 규칙이 아니다 — 세이브·진행과 무관하고, 그림이 바뀌어도 수치만 고치면 된다.
    /// </summary>
    public sealed class WorldPresentation
    {
        public const string DefaultRelativePath = "game/world/presentation.json";
        private static readonly LightColor Black = new LightColor(0f, 0f, 0f);

        public float CameraSmoothSeconds { get; private set; }
        public float ActionSeconds { get; private set; }
        public int ActionFirstColumn { get; private set; }
        public int ActionColumnCount { get; private set; }
        public float DoorWidthFrom { get; private set; }
        public float DoorWidthTo { get; private set; }
        public float DoorAlphaFrom { get; private set; }
        public float DoorAlphaTo { get; private set; }
        public float DoorWidthCells { get; private set; }
        public float DoorHeightCells { get; private set; }
        public float DoorLiftCells { get; private set; }
        public float DoorEdgeCells { get; private set; }
        public LightColor DoorFill { get; private set; }
        public LightColor DoorEdge { get; private set; }
        public LightColor AreaColor { get; private set; }
        public float AreaFadeAlpha { get; private set; }
        public bool TransitionsBlockInput { get; private set; }

        public static WorldPresentation Load(string path) => Parse(File.ReadAllText(path), path);

        public static WorldPresentation Parse(string json, string sourceName = "presentation.json")
        {
            using JsonDocument doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            JsonElement root = doc.RootElement;
            var p = new WorldPresentation();
            JsonElement camera = Section(root, "camera"), action = Section(root, "action"), door = Section(root, "door"), area = Section(root, "area");
            p.CameraSmoothSeconds = NonNegative(camera, "smoothSeconds", 0f, sourceName);
            p.ActionSeconds = NonNegative(action, "seconds", 0f, sourceName);
            p.ActionFirstColumn = (int)NonNegative(action, "firstColumn", 0f, sourceName);
            p.ActionColumnCount = Math.Max(1, (int)NonNegative(action, "columnCount", 1f, sourceName));
            p.DoorWidthFrom = NonNegative(door, "widthFrom", 1f, sourceName);
            p.DoorWidthTo = NonNegative(door, "widthTo", 1f, sourceName);
            p.DoorAlphaFrom = Unit(door, "alphaFrom", 1f, sourceName);
            p.DoorAlphaTo = Unit(door, "alphaTo", 1f, sourceName);
            p.DoorWidthCells = NonNegative(door, "widthCells", 1f, sourceName);
            p.DoorHeightCells = NonNegative(door, "heightCells", 1f, sourceName);
            p.DoorLiftCells = Number(door, "liftCells", 0f);
            p.DoorEdgeCells = NonNegative(door, "edgeCells", 0f, sourceName);
            p.DoorFill = Color(door, "fill", Black, sourceName);
            p.DoorEdge = Color(door, "edge", Black, sourceName);
            p.AreaColor = Color(area, "color", Black, sourceName);
            p.AreaFadeAlpha = Unit(area, "alpha", 0f, sourceName);
            p.TransitionsBlockInput = root.TryGetProperty("blocksInput", out JsonElement b) && b.ValueKind == JsonValueKind.True;
            return p;
        }

        private static JsonElement Section(JsonElement root, string name) => root.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.Object ? e : default;

        private static float Number(JsonElement section, string name, float fallback) =>
            section.ValueKind == JsonValueKind.Object && section.TryGetProperty(name, out JsonElement v) ? v.GetSingle() : fallback;

        private static float NonNegative(JsonElement section, string name, float fallback, string source)
        {
            float v = Number(section, name, fallback);
            if (v < 0f || float.IsNaN(v)) throw new InvalidDataException(source + ": " + name + "는 0 이상");
            return v;
        }

        private static float Unit(JsonElement section, string name, float fallback, string source)
        {
            float v = Number(section, name, fallback);
            if (v < 0f || v > 1f || float.IsNaN(v)) throw new InvalidDataException(source + ": " + name + "는 0~1");
            return v;
        }

        private static LightColor Color(JsonElement section, string name, LightColor fallback, string source)
        {
            if (section.ValueKind != JsonValueKind.Object || !section.TryGetProperty(name, out JsonElement c)) return fallback;
            if (c.ValueKind != JsonValueKind.Array || c.GetArrayLength() != 3) throw new InvalidDataException(source + ": " + name + "는 [r, g, b]");
            return new LightColor(c[0].GetSingle(), c[1].GetSingle(), c[2].GetSingle());
        }
    }
}
