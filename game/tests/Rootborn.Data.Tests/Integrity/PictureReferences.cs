using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Rootborn.Data.Tests.Integrity
{
    /// <summary>
    /// data/ 엔티티와 registry.json 안의 그림·파일 참조를 훑어 빈 곳을 모은다.
    /// - { "sprite": PNG, "name": 칸 }: PNG가 있고, 좌표표 data/atlases/&lt;PNG&gt;.atlas.json이 그 PNG를 가리키며 그 이름의 칸이 그림 안에 있다.
    /// - { "asset": 경로 }: PNG가 아니면 unity-file(게임이 읽지 않는 Unity 파일, Unity를 지울 때 지웠다). PNG면 파일이 있다.
    /// - { "unresolved": GUID }: 변환기가 풀지 못한 Unity 참조.
    /// PNG 크기는 머리에서만 읽고, PNG·좌표표는 한 번씩만 읽는다.
    /// </summary>
    internal sealed class PictureReferences
    {
        public static readonly string[] Kinds =
        {
            "unresolved", "missing-file", "unity-file", "missing-png", "not-png", "missing-atlas", "atlas-image", "missing-cell", "cell-outside",
        };

        private readonly DataScan _data;
        private readonly Dictionary<string, (bool Ok, int Width, int Height)> _png = new Dictionary<string, (bool, int, int)>(StringComparer.Ordinal);
        private readonly Dictionary<string, JsonElement?> _atlas = new Dictionary<string, JsonElement?>(StringComparer.Ordinal);

        public PictureReferences(DataScan data)
        {
            _data = data;
        }

        /// <summary>찾은 그림 참조({ sprite, name }) 수.</summary>
        public int SpriteCount { get; private set; }

        public List<DataGap> Find()
        {
            var gaps = new List<DataGap>();
            SpriteCount = 0;
            foreach (var (owner, node, path, obj) in _data.EntityObjects())
                Check(_data.Relative(node.File) + " " + owner + (path.Length == 0 ? "" : " " + path), obj, gaps);
            foreach (var (path, obj) in DataScan.Objects(_data.Registry, ""))
                Check("data/registry.json " + path, obj, gaps);
            return gaps;
        }

        public static bool Inside(JsonElement cell, int width, int height)
        {
            var x = cell.GetProperty("x").GetInt32();
            var y = cell.GetProperty("y").GetInt32();
            var w = cell.GetProperty("w").GetInt32();
            var h = cell.GetProperty("h").GetInt32();
            return x >= 0 && y >= 0 && w > 0 && h > 0 && x + w <= width && y + h <= height;
        }

        public static string Rect(JsonElement cell) => $"[{cell.GetProperty("x")}, {cell.GetProperty("y")}, {cell.GetProperty("w")}, {cell.GetProperty("h")}]";

        public bool PngAt(string full, out int width, out int height)
        {
            if (!_png.TryGetValue(full, out var size))
            {
                var ok = PngSize.TryRead(full, out var w, out var h);
                _png[full] = size = (ok, w, h);
            }

            width = size.Width;
            height = size.Height;
            return size.Ok;
        }

        private void Check(string where, JsonElement obj, List<DataGap> gaps)
        {
            if (obj.TryGetProperty("unresolved", out var guid))
            {
                gaps.Add(new DataGap("unresolved", where, "GUID " + guid));
                return;
            }

            if (DataScan.HasOnlyStrings(obj, "asset"))
            {
                var asset = obj.GetProperty("asset").GetString();
                // 그림이 아닌 파일(Unity 프리팹 등)은 게임이 읽지 않는다 — 파일이 있든 없든(Unity를 지운 뒤) 같은 종류로 센다.
                if (!asset.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) gaps.Add(new DataGap("unity-file", where, asset));
                else if (!File.Exists(_data.Full(asset))) gaps.Add(new DataGap("missing-file", where, asset));
                else if (!PngAt(_data.Full(asset), out _, out _)) gaps.Add(new DataGap("not-png", where, asset));
                return;
            }

            if (!DataScan.HasOnlyStrings(obj, "sprite", "name")) return;

            SpriteCount++;
            var png = obj.GetProperty("sprite").GetString();
            var name = obj.GetProperty("name").GetString();
            if (!File.Exists(_data.Full(png)))
            {
                gaps.Add(new DataGap("missing-png", where, png));
                return;
            }

            if (!PngAt(_data.Full(png), out var w, out var h))
            {
                gaps.Add(new DataGap("not-png", where, png));
                return;
            }

            var atlas = Atlas(png);
            if (atlas == null)
            {
                gaps.Add(new DataGap("missing-atlas", where, "data/atlases/" + png + ".atlas.json"));
                return;
            }

            if (DataScan.Str(atlas.Value, "image") != png) gaps.Add(new DataGap("atlas-image", where, png));
            if (!atlas.Value.TryGetProperty("sprites", out var sprites) || !sprites.TryGetProperty(name, out var cell))
                gaps.Add(new DataGap("missing-cell", where, $"{png}#{name}"));
            else if (!Inside(cell, w, h))
                gaps.Add(new DataGap("cell-outside", where, $"{png}#{name} {Rect(cell)} / PNG {w}x{h}"));
        }

        private JsonElement? Atlas(string png)
        {
            if (!_atlas.TryGetValue(png, out var atlas))
            {
                var path = Path.Combine(_data.DataDirectory, "atlases", png + ".atlas.json");
                _atlas[png] = atlas = File.Exists(path) ? DataScan.ReadJson(path) : (JsonElement?)null;
            }

            return atlas;
        }
    }
}
