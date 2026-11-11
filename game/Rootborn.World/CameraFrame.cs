using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Rootborn.Core.World;

namespace Rootborn.World
{
    /// <summary>
    /// 월드 카메라가 비추는 사각형(맵 픽셀). 경계 상자 안에서 따라가고, 경계가 화면보다 작은 축은 가운데에 둔다(Unity CameraFollow.ClampToBounds).
    /// 경계는 맵 크기에서 얻는다. 맵 목록에 따로 정한 경계(cameraBounds)가 있으면 맵 안의 부분만 쓴다 — 맵 밖은 비추지 않는다.
    /// </summary>
    public static class CameraFrame
    {
        /// <summary>카메라 경계: 맵 사각형, 또는 정한 경계와 맵이 겹치는 부분(겹치지 않거나 없으면 맵 사각형).</summary>
        public static AreaRect Bounds(float mapWidth, float mapHeight, AreaRect? authored)
        {
            var map = new AreaRect(0f, 0f, mapWidth, mapHeight);
            if (!authored.HasValue) return map;
            AreaRect a = authored.Value;
            float left = Math.Max(0f, a.X), top = Math.Max(0f, a.Y);
            float right = Math.Min(mapWidth, a.Right), bottom = Math.Min(mapHeight, a.Bottom);
            return right > left && bottom > top ? new AreaRect(left, top, right - left, bottom - top) : map;
        }

        /// <summary>
        /// 보이는 사각형(크기 viewW × viewH). 축마다 Core WorldCamera.Offset을 경계 원점 기준으로 쓴다(정수 픽셀로 반올림).
        /// 경계가 화면보다 크면 사각형은 언제나 경계 안이다.
        /// </summary>
        public static AreaRect View(float focusX, float focusY, AreaRect bounds, float viewW, float viewH)
        {
            float x = bounds.X + WorldCamera.Offset(focusX - bounds.X, bounds.W, viewW);
            float y = bounds.Y + WorldCamera.Offset(focusY - bounds.Y, bounds.H, viewH);
            return new AreaRect(x, y, viewW, viewH);
        }
    }

    /// <summary>
    /// 맵 목록(game/world/maps.json)의 선택 필드 cameraBounds: [x, y, 너비, 높이] 레벨 픽셀(그림 기준, 줄 밀기 없음).
    /// 없으면 맵 크기가 경계다. 맵 크기보다 좁게 비춰야 하는 맵에만 적는다(예: 그림 가장자리에 보이면 안 되는 칸이 있을 때).
    /// 맵 목록의 다른 값은 WorldCatalog가 읽는다. 이 필드만 여기서 따로 읽는다.
    /// </summary>
    public sealed class MapCameraBounds
    {
        private readonly Dictionary<string, AreaRect> _bounds = new Dictionary<string, AreaRect>(StringComparer.Ordinal);

        public bool TryGet(string mapId, out AreaRect bounds) => _bounds.TryGetValue(mapId ?? string.Empty, out bounds);

        public static MapCameraBounds Load(string mapsPath) => Parse(File.ReadAllText(mapsPath), mapsPath);

        public static MapCameraBounds Parse(string json, string sourceName = "maps.json")
        {
            using JsonDocument doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var result = new MapCameraBounds();
            if (!doc.RootElement.TryGetProperty("maps", out JsonElement maps)) return result;
            foreach (JsonElement m in maps.EnumerateArray())
            {
                if (!m.TryGetProperty("cameraBounds", out JsonElement b)) continue;
                string id = m.TryGetProperty("id", out JsonElement idElement) ? idElement.GetString() : null;
                if (string.IsNullOrWhiteSpace(id)) throw new InvalidDataException(sourceName + ": cameraBounds가 있는 맵에 id가 없다");
                if (b.ValueKind != JsonValueKind.Array || b.GetArrayLength() != 4)
                    throw new InvalidDataException(sourceName + " " + id + ": cameraBounds는 [x, y, 너비, 높이]");
                var rect = new AreaRect(b[0].GetSingle(), b[1].GetSingle(), b[2].GetSingle(), b[3].GetSingle());
                if (rect.W <= 0f || rect.H <= 0f) throw new InvalidDataException(sourceName + " " + id + ": cameraBounds의 너비·높이는 양수");
                result._bounds[id] = rect;
            }
            return result;
        }
    }
}
