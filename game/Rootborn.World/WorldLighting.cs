using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Rootborn.Core.World;

namespace Rootborn.World
{
    /// <summary>
    /// 빛 지점 하나(Unity LocalLightController + 점 조명 Light2D): 자리(레벨 픽셀), 반경, 안 반경(여기까지 최대 밝기), 색, 때별 세기.
    /// 표에 없는 때는 꺼짐(0).
    /// </summary>
    public sealed class PointLightDef
    {
        /// <summary>Unity LocalLightController의 _lerpSpeed 기본값.</summary>
        public const float DefaultLerpSpeed = 6f;

        public readonly string Name;
        public readonly float X, Y, Radius, InnerRadius, LerpSpeed;
        public readonly LightColor Color;
        public readonly bool Temporary;
        public readonly string Reason;
        private readonly Dictionary<string, float> _intensity;

        public PointLightDef(string name, float x, float y, float radius, float innerRadius, LightColor color, IDictionary<string, float> intensityByTime,
            float lerpSpeed = DefaultLerpSpeed, bool temporary = false, string reason = null)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("빛 이름이 비었다", nameof(name));
            if (radius <= 0f) throw new ArgumentOutOfRangeException(nameof(radius), name + ": 반경은 양수");
            if (innerRadius < 0f || innerRadius > radius) throw new ArgumentOutOfRangeException(nameof(innerRadius), name + ": 안 반경은 0~반경");
            if (lerpSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(lerpSpeed), name + ": 따라가는 속도는 양수");
            Name = name; X = x; Y = y; Radius = radius; InnerRadius = innerRadius; Color = color; LerpSpeed = lerpSpeed;
            Temporary = temporary; Reason = reason ?? string.Empty;
            _intensity = new Dictionary<string, float>(StringComparer.Ordinal);
            if (intensityByTime != null)
                foreach (KeyValuePair<string, float> p in intensityByTime)
                {
                    if (p.Value < 0f) throw new ArgumentOutOfRangeException(nameof(intensityByTime), name + ": 세기는 0 이상");
                    _intensity[p.Key] = p.Value;
                }
        }

        /// <summary>그 때의 세기. 표에 없으면 0(꺼짐).</summary>
        public float IntensityAt(string timeSlot) => timeSlot != null && _intensity.TryGetValue(timeSlot, out float v) ? v : 0f;
    }

    /// <summary>화면에 그릴 빛 하나(지금 세기를 곱한 색). 그리기는 Host.</summary>
    public readonly struct VisibleLight
    {
        public readonly float X, Y, Radius, InnerRadius, R, G, B;

        public VisibleLight(float x, float y, float radius, float innerRadius, float r, float g, float b)
        {
            X = x; Y = y; Radius = radius; InnerRadius = innerRadius; R = r; G = g; B = b;
        }
    }

    /// <summary>
    /// 월드의 빛 데이터(game/world/lights.json): 바깥 맵의 때별 밝기와 맵마다 놓인 빛 지점. 형식은 그 파일 머리의 주석.
    /// 때 이름은 Core 시간대 문자열(LocationStateTimeSlots) 그대로 표의 키로 쓴다 — 때 종류를 코드에 닫지 않는다.
    /// </summary>
    public sealed class WorldLightingData
    {
        public const string DefaultRelativePath = "game/world/lights.json";
        public const float DefaultAmbientLerpSpeed = 2f;

        private readonly Dictionary<string, LightColor> _ambient = new Dictionary<string, LightColor>(StringComparer.Ordinal);
        private readonly Dictionary<string, PointLightDef[]> _lights = new Dictionary<string, PointLightDef[]>(StringComparer.Ordinal);
        private readonly List<PointLightDef> _temporary = new List<PointLightDef>();

        public float AmbientLerpSpeed { get; private set; } = DefaultAmbientLerpSpeed;

        /// <summary>임시로 옮겨 둔 빛(맵을 새로 그리면 바꾼다).</summary>
        public IReadOnlyList<PointLightDef> Temporary => _temporary;

        /// <summary>빛이 있는 맵 id.</summary>
        public IEnumerable<string> MapIds => _lights.Keys;

        /// <summary>바깥 맵의 그 때 화면 색(색 × 세기). 표에 없으면 false.</summary>
        public bool TryGetAmbient(string timeSlot, out LightColor tint)
        {
            if (timeSlot != null && _ambient.TryGetValue(timeSlot, out tint)) return true;
            tint = LightColor.White;
            return false;
        }

        /// <summary>그 맵의 빛 지점(없으면 빈 목록).</summary>
        public IReadOnlyList<PointLightDef> LightsOf(string mapId) =>
            mapId != null && _lights.TryGetValue(mapId, out PointLightDef[] list) ? list : Array.Empty<PointLightDef>();

        public static WorldLightingData Load(string path) => Parse(File.ReadAllText(path), path);

        public static WorldLightingData Parse(string json, string sourceName = "lights.json")
        {
            using JsonDocument doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            JsonElement root = doc.RootElement;
            var data = new WorldLightingData();
            if (root.TryGetProperty("ambient", out JsonElement ambient))
            {
                if (ambient.TryGetProperty("lerpSpeed", out JsonElement speed))
                {
                    data.AmbientLerpSpeed = speed.GetSingle();
                    if (data.AmbientLerpSpeed <= 0f) throw new InvalidDataException(sourceName + ": ambient.lerpSpeed는 양수");
                }
                if (ambient.TryGetProperty("byTime", out JsonElement byTime))
                    foreach (JsonProperty slot in byTime.EnumerateObject())
                    {
                        string where = sourceName + " ambient " + slot.Name;
                        LightColor color = Color(slot.Value, where);
                        float intensity = slot.Value.GetProperty("intensity").GetSingle();
                        if (intensity < 0f) throw new InvalidDataException(where + ": intensity는 0 이상");
                        data._ambient[slot.Name] = color.Times(intensity);
                    }
            }
            if (root.TryGetProperty("maps", out JsonElement maps))
                foreach (JsonProperty map in maps.EnumerateObject())
                {
                    var list = new List<PointLightDef>();
                    foreach (JsonElement l in map.Value.EnumerateArray())
                    {
                        string name = l.TryGetProperty("name", out JsonElement n) ? n.GetString() : null;
                        string where = sourceName + " " + map.Name + " " + name;
                        if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException(sourceName + " " + map.Name + ": 빛에 name이 없다");
                        JsonElement at = l.GetProperty("at");
                        if (at.GetArrayLength() != 2) throw new InvalidDataException(where + ": at은 [x, y]");
                        var intensity = new Dictionary<string, float>(StringComparer.Ordinal);
                        if (l.TryGetProperty("intensityByTime", out JsonElement byTime))
                            foreach (JsonProperty p in byTime.EnumerateObject()) intensity[p.Name] = p.Value.GetSingle();
                        bool temporary = l.TryGetProperty("temporary", out JsonElement t) && t.ValueKind == JsonValueKind.True;
                        string reason = l.TryGetProperty("reason", out JsonElement r) ? r.GetString() : null;
                        if (temporary && string.IsNullOrWhiteSpace(reason)) throw new InvalidDataException(where + ": 임시 빛에는 reason을 적는다");
                        PointLightDef def;
                        try
                        {
                            def = new PointLightDef(name, at[0].GetSingle(), at[1].GetSingle(), l.GetProperty("radius").GetSingle(),
                                l.TryGetProperty("innerRadius", out JsonElement inner) ? inner.GetSingle() : 0f, Color(l, where), intensity,
                                l.TryGetProperty("lerpSpeed", out JsonElement s) ? s.GetSingle() : PointLightDef.DefaultLerpSpeed, temporary, reason);
                        }
                        catch (ArgumentException e) { throw new InvalidDataException(where + ": " + e.Message, e); }
                        list.Add(def);
                        if (temporary) data._temporary.Add(def);
                    }
                    data._lights[map.Name] = list.ToArray();
                }
            return data;
        }

        private static LightColor Color(JsonElement e, string where)
        {
            JsonElement c = e.GetProperty("color");
            if (c.ValueKind != JsonValueKind.Array || c.GetArrayLength() != 3) throw new InvalidDataException(where + ": color는 [r, g, b]");
            float r = c[0].GetSingle(), g = c[1].GetSingle(), b = c[2].GetSingle();
            if (r < 0f || g < 0f || b < 0f) throw new InvalidDataException(where + ": color는 0 이상");
            return new LightColor(r, g, b);
        }
    }

    /// <summary>
    /// 바깥 맵의 지금 화면 색: 때가 정한 목표를 t = 1 - exp(-속도 × dt)씩 따라간다(Unity LightingManager와 같은 따라가기). 표에 없는 때는 흰색.
    /// </summary>
    public sealed class AmbientLight
    {
        private readonly WorldLightingData _data;

        public AmbientLight(WorldLightingData data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public LightColor Tint { get; private set; } = LightColor.White;

        /// <summary>목표로 바로 맞춘다(맵에 들어선 순간).</summary>
        public void Snap(string timeSlot)
        {
            _data.TryGetAmbient(timeSlot, out LightColor target);
            Tint = target;
        }

        public void Update(string timeSlot, float dt)
        {
            _data.TryGetAmbient(timeSlot, out LightColor target);
            float t = 1f - (float)Math.Exp(-_data.AmbientLerpSpeed * Math.Max(0f, dt));
            Tint = LightColor.Lerp(Tint, target, t);
        }
    }

    /// <summary>
    /// 지금 맵의 빛 지점들의 세기(때를 따라간다)와 화면에 들어온 빛 고르기. 맵에 들어설 때만 배열을 만들고, 프레임마다는 새 객체를 만들지 않는다.
    /// </summary>
    public sealed class MapLights
    {
        /// <summary>이보다 어두운 빛은 그리지 않는다(꺼진 것으로 본다).</summary>
        public const float Off = 0.001f;

        private IReadOnlyList<PointLightDef> _defs = Array.Empty<PointLightDef>();
        private float[] _intensity = Array.Empty<float>();

        public int Count => _defs.Count;

        /// <summary>맵에 들어섰다: 그 맵의 빛으로 바꾸고 세기를 지금 때에 바로 맞춘다.</summary>
        public void Enter(IReadOnlyList<PointLightDef> lights, string timeSlot)
        {
            _defs = lights ?? Array.Empty<PointLightDef>();
            if (_intensity.Length != _defs.Count) _intensity = new float[_defs.Count];
            for (int i = 0; i < _defs.Count; i++) _intensity[i] = _defs[i].IntensityAt(timeSlot);
        }

        public void Update(string timeSlot, float dt)
        {
            float step = Math.Max(0f, dt);
            for (int i = 0; i < _defs.Count; i++)
            {
                PointLightDef d = _defs[i];
                float t = 1f - (float)Math.Exp(-d.LerpSpeed * step);
                _intensity[i] += (d.IntensityAt(timeSlot) - _intensity[i]) * t;
            }
        }

        /// <summary>보이는 사각형에 닿는 켜진 빛만 into에 채운다(먼저 비운다).</summary>
        public void CollectVisible(AreaRect view, List<VisibleLight> into)
        {
            into.Clear();
            for (int i = 0; i < _defs.Count; i++)
            {
                float k = _intensity[i];
                if (k < Off) continue;
                PointLightDef d = _defs[i];
                if (d.X + d.Radius <= view.X || d.X - d.Radius >= view.Right || d.Y + d.Radius <= view.Y || d.Y - d.Radius >= view.Bottom) continue;
                into.Add(new VisibleLight(d.X, d.Y, d.Radius, d.InnerRadius, d.Color.R * k, d.Color.G * k, d.Color.B * k));
            }
        }
    }
}
