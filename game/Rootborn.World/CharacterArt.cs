using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Rootborn.Core.Characters;
using Rootborn.Core.World;

namespace Rootborn.World
{
    /// <summary>그림 한 칸: PNG의 원본 사각형(왼쪽 위 원점)과 기준점(0~1, 왼쪽 위 원점. 발은 (0.5, 1)).</summary>
    public sealed class CharacterFrame
    {
        public readonly string Texture;
        public readonly int X, Y, W, H;
        public readonly float PivotX, PivotY;

        public CharacterFrame(string texture, int x, int y, int w, int h, float pivotX, float pivotY)
        {
            Texture = texture; X = x; Y = y; W = w; H = h; PivotX = pivotX; PivotY = pivotY;
        }
    }

    /// <summary>레이어 하나의 그림 시트: 4줄(아래, 왼쪽, 오른쪽, 위) × 6칸(걷기 3, 동작 2, 다침 1). Unity GrowingCharacterSheetDefinition.</summary>
    public sealed class CharacterSheet
    {
        public const int Rows = 4, Columns = 6, FrameCount = Rows * Columns;

        public readonly string Id;
        public readonly IReadOnlyList<CharacterFrame> Frames;

        public CharacterSheet(string id, IReadOnlyList<CharacterFrame> frames)
        {
            Id = id;
            Frames = frames;
        }

        public CharacterFrame Frame(int row, int column) =>
            row < 0 || row >= Rows || column < 0 || column >= Columns ? null : Frames[row * Columns + column];
    }

    /// <summary>
    /// 레이어 캐릭터의 그림 데이터: 그림 시트(GrowingCharacterSheetDefinition), 성장 단계의 몸 시트, 옷의 단계별 시트, 옛 겉모습.
    /// 정의 로더는 이것들을 "규칙이 아님"으로 두므로 여기서 data/characters와 좌표표(data/atlases)를 직접 읽는다.
    /// 무엇을 입을지(규칙)는 Core WearableRules가 정하고, 여기서는 id를 그림으로 바꾸기만 한다.
    /// </summary>
    public sealed class CharacterArt
    {
        public const string CharactersRelativePath = "data/characters";
        public const string AtlasesRelativePath = "data/atlases";

        private readonly Dictionary<string, CharacterSheet> _sheets = new Dictionary<string, CharacterSheet>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _bodyByStage = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, string>> _sheetByWearableStage = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        private readonly List<(string id, string body, string outfit)> _legacyLooks = new List<(string, string, string)>();

        public readonly List<string> Problems = new List<string>();

        public CharacterSheet Sheet(string id) => id != null && _sheets.TryGetValue(id, out CharacterSheet s) ? s : null;
        public CharacterSheet BodyFor(string stageId) => stageId != null && _bodyByStage.TryGetValue(stageId, out string id) ? Sheet(id) : null;

        public CharacterSheet WearableSheet(string wearableId, string stageId) =>
            wearableId != null && _sheetByWearableStage.TryGetValue(wearableId, out var byStage) && stageId != null && byStage.TryGetValue(stageId, out string id) ? Sheet(id) : null;

        public static CharacterArt Load(string repoRoot)
        {
            var art = new CharacterArt();
            string dir = Path.Combine(repoRoot, CharactersRelativePath);
            var atlases = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            var atlasDocs = new List<JsonDocument>();
            try
            {
                foreach (JsonElement s in Read(Path.Combine(dir, "GrowingCharacterSheetDefinition.json"), atlasDocs))
                {
                    string id = s.GetProperty("id").GetString();
                    var frames = new List<CharacterFrame>();
                    foreach (JsonElement f in s.GetProperty("frames").EnumerateArray())
                    {
                        string png = f.GetProperty("sprite").GetString(), name = f.GetProperty("name").GetString();
                        if (!atlases.TryGetValue(png, out JsonElement atlas))
                        {
                            string atlasPath = Path.Combine(repoRoot, AtlasesRelativePath, png + ".atlas.json");
                            JsonDocument doc = JsonDocument.Parse(File.ReadAllText(atlasPath));
                            atlasDocs.Add(doc);
                            atlases[png] = atlas = doc.RootElement;
                        }
                        if (!atlas.GetProperty("sprites").TryGetProperty(name, out JsonElement r))
                        {
                            art.Problems.Add(png + " 좌표표에 " + name + "이 없다");
                            frames.Add(null);
                            continue;
                        }
                        JsonElement pivot = r.GetProperty("pivot");
                        frames.Add(new CharacterFrame(Path.GetFullPath(Path.Combine(repoRoot, png)), r.GetProperty("x").GetInt32(), r.GetProperty("y").GetInt32(),
                            r.GetProperty("w").GetInt32(), r.GetProperty("h").GetInt32(), pivot.GetProperty("x").GetSingle(), pivot.GetProperty("y").GetSingle()));
                    }
                    if (frames.Count != CharacterSheet.FrameCount) { art.Problems.Add("그림 시트 " + id + "의 칸이 " + frames.Count + "개다(24개여야 한다)"); continue; }
                    art._sheets[id] = new CharacterSheet(id, frames);
                }

                foreach (JsonElement st in Read(Path.Combine(dir, "CharacterGrowthStageDefinition.json"), atlasDocs))
                    art._bodyByStage[st.GetProperty("id").GetString()] = RefId(st, "body");

                foreach (JsonElement w in Read(Path.Combine(dir, "WearableDefinition.json"), atlasDocs))
                {
                    var byStage = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (JsonElement a in w.GetProperty("stageArt").EnumerateArray())
                    {
                        string stage = RefId(a, "Stage"), sheet = RefId(a, "Sheet");
                        if (stage.Length > 0 && sheet.Length > 0) byStage[stage] = sheet;
                    }
                    art._sheetByWearableStage[w.GetProperty("id").GetString()] = byStage;
                }

                string legacy = Path.Combine(dir, "GrowingCharacterAppearanceDefinition.json");
                if (File.Exists(legacy))
                    foreach (JsonElement a in Read(legacy, atlasDocs))
                        art._legacyLooks.Add((a.GetProperty("id").GetString(), RefId(a, "body"), RefId(a, "outfit")));
            }
            finally
            {
                foreach (JsonDocument d in atlasDocs) d.Dispose();
            }
            return art;
        }

        /// <summary>
        /// 옛 겉모습 id → 그 옷을 그리는 옷 id(Unity WearableLookResolver.LegacyAppearanceMap): 옛 겉모습의 몸이 단계의 몸과 같고,
        /// 그 단계에서 옷의 시트가 옛 겉모습의 옷 시트와 같은 것. 옛 옷 선택을 한 번 옮길 때만 쓴다.
        /// </summary>
        public Dictionary<string, string> LegacyAppearanceMap()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (id, body, outfit) in _legacyLooks)
            {
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(outfit)) continue;
                foreach (var stage in _bodyByStage)
                {
                    if (stage.Value != body) continue;
                    foreach (var wearable in _sheetByWearableStage)
                        if (wearable.Value.TryGetValue(stage.Key, out string sheet) && sheet == outfit) { map[id] = wearable.Key; break; }
                }
            }
            return map;
        }

        private static IEnumerable<JsonElement> Read(string path, List<JsonDocument> keep)
        {
            JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            keep.Add(doc);
            return doc.RootElement.EnumerateArray();
        }

        private static string RefId(JsonElement e, string name) =>
            e.TryGetProperty(name, out JsonElement r) && r.ValueKind == JsonValueKind.Object && r.TryGetProperty("ref", out JsonElement id) ? id.GetString() ?? string.Empty : string.Empty;
    }

    /// <summary>그릴 겉모습: 단계, 몸 시트, 옷 시트(그리는 순서), 네트워크용 짧은 코드.</summary>
    public sealed class CharacterLook
    {
        public readonly string StageId;
        public readonly CharacterSheet Body;
        public readonly IReadOnlyList<CharacterSheet> Layers;
        public readonly string Code;

        private CharacterLook(string stageId, CharacterSheet body, IReadOnlyList<CharacterSheet> layers, string code)
        {
            StageId = stageId; Body = body; Layers = layers; Code = code;
        }

        /// <summary>Unity WearableLookResolver.ResolveStage: 칸마다 무엇을 입을지는 Core WearableRules.ResolveLayers가 정한다. 단계가 없으면 null.</summary>
        public static CharacterLook Resolve(CharacterArt art, WearableCatalog catalog, WearableState state, string stageId, string facilityId)
        {
            CharacterSheet body = art.BodyFor(stageId);
            if (body == null) return null;
            var layers = new List<CharacterSheet>();
            var ids = new List<string>();
            foreach (WearableLayer layer in WearableRules.ResolveLayers(catalog, state ?? new WearableState(), stageId, facilityId ?? string.Empty))
            {
                CharacterSheet sheet = art.WearableSheet(layer.WearableId, stageId);
                if (sheet == null) continue;
                layers.Add(sheet);
                ids.Add(layer.WearableId);
            }
            return new CharacterLook(stageId, body, layers, WearableLookCode.Encode(stageId, ids));
        }
    }

    /// <summary>
    /// 어느 칸을 그릴지(Unity GrowingCharacterVisualView.Advance): 줄은 보는 방향(아래 0, 왼쪽 1, 오른쪽 2, 위 3),
    /// 걷는 동안 칸은 초당 8번 {1, 2, 1, 0}을 돈다. 멈추면 서 있는 칸(1). 시간은 Advance에 넘긴 값만큼만 흐른다.
    /// </summary>
    public sealed class CharacterAnimator
    {
        public const int RowDown = 0, RowLeft = 1, RowRight = 2, RowUp = 3, StandingColumn = 1;
        public const float WalkFramesPerSecond = 8f;
        private static readonly int[] WalkCycle = { 1, 2, 1, 0 };

        private int _walkIndex = -1;
        private float _walkTimer;

        public int Row { get; private set; } = RowDown;
        public int Column { get; private set; } = StandingColumn;

        public void Advance(bool moving, Facing facing, float dt)
        {
            Row = facing == Facing.Left ? RowLeft : facing == Facing.Right ? RowRight : facing == Facing.Up ? RowUp : RowDown;
            if (!moving)
            {
                _walkIndex = -1;
                _walkTimer = 0f;
                Column = StandingColumn;
                return;
            }
            _walkTimer += Math.Max(0f, dt);
            const float step = 1f / WalkFramesPerSecond;
            while (_walkTimer >= step - 0.0001f)
            {
                _walkTimer -= step;
                _walkIndex = (_walkIndex + 1) % WalkCycle.Length;
            }
            Column = WalkCycle[Math.Max(_walkIndex, 0)];
        }
    }
}
