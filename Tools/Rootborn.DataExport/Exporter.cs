using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Rootborn.DataExport
{
    public sealed class ExportOptions
    {
        public string RepoRoot;
        public string OutDir;
        public bool AllowUnresolved;
        public string DataDir = "Assets/Data";
        public string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";
        public string CatalogPath = "Assets/Localization/GameTextCatalog.asset";
        public string[] ScanDirs = { "Assets", "Packages" };
    }

    public sealed class ExportResult
    {
        public int ExitCode;
        public JsonElement Report;
        public List<string> Messages = new List<string>();
    }

    /// <summary>
    /// Unity 에셋(YAML)을 읽어 JSON으로 낸다. 엔티티 id나 클래스 이름으로 분기하지 않는다.
    /// 타입별 처리는 둘뿐이다: Unity 내장 Tile(스프라이트 껍데기)은 건너뛰고, 등록부·현지화 카탈로그는 경로로 지정받아 따로 쓴다.
    /// </summary>
    public sealed class Exporter
    {
        private const string ReportFile = "export-report.json";
        private const string TileScriptGuid = "0000000000000000e000000000000000";
        private const string TileScriptFileId = "13312";

        // _id가 없을 때 id로 쓰는 필드. 필드 이름 규약이지 엔티티별 분기가 아니다.
        private static readonly string[] IdFields = { "_id", "_stableId" };
        private static readonly HashSet<string> HeaderFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject",
            "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier",
        };
        private static readonly HashSet<string> ReservedFields = new HashSet<string>(StringComparer.Ordinal) { "type", "id", "asset" };
        private static readonly HashSet<string> RefKeys = new HashSet<string>(StringComparer.Ordinal) { "fileID", "guid", "type" };
        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".psd", ".tga", ".bmp", ".gif", ".tif", ".tiff",
        };
        private static readonly Regex BuiltinGuid = new Regex(@"^0{16}[def]0{15}$", RegexOptions.Compiled);
        // Unity가 int 배열을 묶어 쓴 값: 원소마다 8글자(리틀엔디언 16진), 따옴표 없음. 숫자로만 되거나 "1e0..."처럼
        // 지수 표기로 보여도 숫자가 아니라 문자열로 낸다. int 필드 하나는 8글자 배수 길이의 10진수가 될 일이 거의 없다(int32는 최대 10자리).
        private static readonly Regex PackedIntArray = new Regex(@"^(?:[0-9a-f]{8})+$", RegexOptions.Compiled);
        private static readonly Regex JsonNumber = new Regex(@"^-?(0|[1-9]\d*)(\.\d+)?([eE][+-]?\d+)?$", RegexOptions.Compiled);
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
        };

        private readonly ExportOptions _options;
        private readonly AssetIndex _index;
        private readonly string _dataPrefix;

        private readonly Dictionary<string, KeyValuePair<string, string>> _ids = new Dictionary<string, KeyValuePair<string, string>>(StringComparer.Ordinal);
        private readonly SortedSet<string> _referencedImages = new SortedSet<string>(StringComparer.Ordinal);
        private readonly SortedSet<string> _missingPngs = new SortedSet<string>(StringComparer.Ordinal);
        private readonly SortedSet<string> _refsOutsideData = new SortedSet<string>(StringComparer.Ordinal);
        private readonly SortedSet<string> _unresolvedScripts = new SortedSet<string>(StringComparer.Ordinal);
        private readonly JsonArray _unresolved = new JsonArray();
        private readonly JsonArray _collisions = new JsonArray();
        private readonly SortedDictionary<string, SortedSet<string>> _typeNameDiffers = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        private readonly SortedSet<string> _typeFromFileName = new SortedSet<string>(StringComparer.Ordinal);
        private int _builtinRefs;

        private Exporter(ExportOptions options)
        {
            _options = options;
            _dataPrefix = options.DataDir.TrimEnd('/') + "/";
            _index = new AssetIndex(options.RepoRoot, options.ScanDirs);
        }

        public static ExportResult Run(ExportOptions options) => new Exporter(options).Execute();

        private sealed class Entity
        {
            public string Type, Id, Asset, FileId, Folder;
            public JsonObject Json;
        }

        private ExportResult Execute()
        {
            var result = new ExportResult();
            var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var entities = new List<Entity>();
            var idFromFileName = new JsonArray();
            var nonScriptObjects = new JsonArray();
            int skippedTiles = 0, assetFiles = 0;

            string dataFull = Path.Combine(_options.RepoRoot, _options.DataDir);
            var assetPaths = Directory.Exists(dataFull)
                ? Directory.EnumerateFiles(dataFull, "*.asset", SearchOption.AllDirectories)
                    .Select(p => Path.GetRelativePath(_options.RepoRoot, p).Replace('\\', '/'))
                    .OrderBy(p => p, StringComparer.Ordinal).ToList()
                : new List<string>();

            JsonArray idDisambiguated = DisambiguateFileNameIds(assetPaths);

            foreach (string asset in assetPaths)
            {
                if (asset == _options.RegistryPath) continue;
                var docs = _index.Docs(asset);
                if (docs == null) { nonScriptObjects.Add(asset); continue; }
                assetFiles++;
                foreach (UnityDoc doc in docs)
                {
                    if (doc.ClassId != 114) { nonScriptObjects.Add(asset + "#" + doc.FileId); continue; }
                    if (IsTileShell(doc)) { skippedTiles++; continue; }

                    var id = IdOf(asset, doc);
                    bool main = IsMain(asset, doc, docs);
                    if (id.Value == "file") idFromFileName.Add(main ? asset : asset + "#" + doc.FileId);

                    string rest = asset.Substring(_dataPrefix.Length);
                    int slash = rest.IndexOf('/');
                    var entity = new Entity
                    {
                        Type = TypeOf(doc, asset), Id = id.Key, Asset = asset, FileId = doc.FileId,
                        Folder = slash < 0 ? "_root" : rest.Substring(0, slash).ToLowerInvariant(),
                    };
                    entity.Json = new JsonObject { ["type"] = entity.Type, ["id"] = entity.Id, ["asset"] = asset };
                    WriteFields(entity.Json, doc.Body, asset);
                    entities.Add(entity);
                }
            }

            foreach (var group in entities.GroupBy(e => e.Folder + "/" + e.Type + ".json", StringComparer.Ordinal))
            {
                var array = new JsonArray();
                foreach (Entity e in group.OrderBy(e => e.Id, StringComparer.Ordinal).ThenBy(e => e.Asset, StringComparer.Ordinal).ThenBy(e => e.FileId, StringComparer.Ordinal))
                    array.Add(e.Json);
                files[group.Key] = Serialize(array);
            }

            JsonObject registry = BuildRegistry(result);
            if (registry != null) files["registry.json"] = Serialize(registry);

            var duplicateKeys = new JsonArray();
            int localizationKeys = BuildLocalization(files, duplicateKeys, result);

            var outOfBounds = new JsonArray();
            var unreadableImages = new JsonArray();
            int atlases = 0;
            foreach (string image in _referencedImages)
            {
                SpriteSheet sheet = _index.Sheet(image);
                if (sheet == null || sheet.Width == 0) { unreadableImages.Add(image); continue; }
                files["atlases/" + image + ".atlas.json"] = Serialize(BuildAtlas(sheet, outOfBounds));
                atlases++;
            }

            var duplicateIds = new JsonArray();
            foreach (var group in entities.GroupBy(e => e.Type + "\n" + e.Id, StringComparer.Ordinal).Where(g => g.Count() > 1)
                .OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var assets = new JsonArray();
                foreach (Entity e in group) assets.Add(e.FileId == _index.MainFileId(e.Asset) ? e.Asset : e.Asset + "#" + e.FileId);
                duplicateIds.Add(new JsonObject { ["type"] = group.First().Type, ["id"] = group.First().Id, ["assets"] = assets });
            }

            var typeCounts = new JsonObject();
            foreach (var group in entities.GroupBy(e => e.Type, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
                typeCounts[group.Key] = group.Count();

            var parseErrors = new JsonArray();
            foreach (var e in _index.ParseErrors.OrderBy(e => e.Key, StringComparer.Ordinal))
                parseErrors.Add(new JsonObject { ["asset"] = e.Key, ["error"] = e.Value });

            var report = new JsonObject
            {
                ["summary"] = new JsonObject
                {
                    ["assetFiles"] = assetFiles,
                    ["entities"] = entities.Count,
                    ["skippedTileShells"] = skippedTiles,
                    ["atlases"] = atlases,
                    ["localizationKeys"] = localizationKeys,
                    ["unresolved"] = _unresolved.Count,
                    ["unresolvedScripts"] = _unresolvedScripts.Count,
                    ["typeFromScriptFileName"] = _typeFromFileName.Count,
                    ["parseErrors"] = parseErrors.Count,
                    ["idFromFileName"] = idFromFileName.Count,
                    ["duplicateIds"] = duplicateIds.Count,
                    ["missingPngs"] = _missingPngs.Count,
                    ["builtinRefs"] = _builtinRefs,
                    ["refsOutsideData"] = _refsOutsideData.Count,
                },
                ["typeCounts"] = typeCounts,
                ["unresolved"] = _unresolved,
                ["unresolvedScripts"] = ToArray(_unresolvedScripts),
                ["parseErrors"] = parseErrors,
                ["duplicateIds"] = duplicateIds,
                ["missingPngs"] = ToArray(_missingPngs),
                ["unreadableImages"] = unreadableImages,
                ["spriteRectOutOfBounds"] = outOfBounds,
                ["reservedFieldCollisions"] = _collisions,
                ["localizationDuplicateKeys"] = duplicateKeys,
                ["nonScriptObjects"] = nonScriptObjects,
                ["refsOutsideData"] = ToArray(_refsOutsideData),
                ["typeNameDiffersFromScriptFile"] = new JsonObject(_typeNameDiffers.Select(p => new KeyValuePair<string, JsonNode>(p.Key, ToArray(p.Value)))),
                ["idFromFileName"] = idFromFileName,
                ["idDisambiguated"] = idDisambiguated,
                ["files"] = ToArray(files.Keys),
            };
            string reportText = Serialize(report);

            WriteOutput(files, reportText);

            result.Report = JsonDocument.Parse(reportText).RootElement.Clone();
            result.Messages.Add(report["summary"].ToJsonString());
            bool failed = _unresolved.Count > 0 || _unresolvedScripts.Count > 0;
            result.ExitCode = parseErrors.Count > 0 ? 2 : failed && !_options.AllowUnresolved ? 1 : 0;
            return result;
        }

        // ---- 엔티티 ----

        private static bool IsTileShell(UnityDoc doc)
        {
            var script = doc.Body.Get("m_Script") as YMap;
            return script != null && script.Text("guid") == TileScriptGuid && script.Text("fileID") == TileScriptFileId;
        }

        private bool IsMain(string asset, UnityDoc doc, List<UnityDoc> docs)
            => docs.Count == 1 || doc.FileId == _index.MainFileId(asset);

        /// <summary>(id, 출처). 출처는 "_id" 같은 필드 이름이거나, 파일 이름에서 만들었으면 "file".</summary>
        private KeyValuePair<string, string> IdOf(string asset, UnityDoc doc)
        {
            string cacheKey = asset + "#" + doc.FileId;
            if (_ids.TryGetValue(cacheKey, out var cached)) return cached;

            KeyValuePair<string, string> id = default;
            foreach (string field in IdFields)
            {
                string value = doc.Body.Text(field);
                if (!string.IsNullOrEmpty(value)) { id = new KeyValuePair<string, string>(value, field); break; }
            }
            if (id.Key == null)
            {
                string fileName = Path.GetFileNameWithoutExtension(asset);
                var docs = _index.Docs(asset);
                string name = IsMain(asset, doc, docs) ? fileName : fileName + "/" + (string.IsNullOrEmpty(doc.Body.Text("m_Name")) ? doc.FileId : doc.Body.Text("m_Name"));
                id = new KeyValuePair<string, string>(name, "file");
            }
            _ids[cacheKey] = id;
            return id;
        }

        /// <summary>
        /// 파일 이름으로 만든 id가 같은 타입 안에서 겹치면 참조가 모호해진다. 겹친 것만 구분한다:
        /// 주 에셋은 Assets/Data 기준 경로(확장자 없이), 하위 에셋은 파일 안 순번(#1부터)을 붙인다.
        /// </summary>
        private JsonArray DisambiguateFileNameIds(List<string> assetPaths)
        {
            var groups = new Dictionary<string, List<KeyValuePair<string, UnityDoc>>>(StringComparer.Ordinal);
            foreach (string asset in assetPaths)
            {
                if (asset == _options.RegistryPath) continue;
                var docs = _index.Docs(asset);
                if (docs == null) continue;
                foreach (UnityDoc doc in docs)
                {
                    if (doc.ClassId != 114 || IsTileShell(doc)) continue;
                    string key = TypeOf(doc, asset) + "\n" + IdOf(asset, doc).Key;
                    if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<KeyValuePair<string, UnityDoc>>();
                    list.Add(new KeyValuePair<string, UnityDoc>(asset, doc));
                }
            }

            var changed = new JsonArray();
            foreach (var group in groups.Where(g => g.Value.Count > 1).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var orderInFile = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var member in group.Value)
                {
                    string asset = member.Key;
                    var id = IdOf(asset, member.Value);
                    if (id.Value != "file") continue;
                    string unique;
                    if (IsMain(asset, member.Value, _index.Docs(asset)))
                    {
                        unique = asset.Substring(_dataPrefix.Length, asset.Length - _dataPrefix.Length - ".asset".Length);
                    }
                    else
                    {
                        orderInFile.TryGetValue(asset, out int order);
                        orderInFile[asset] = ++order;
                        unique = id.Key + "#" + order;
                    }
                    _ids[asset + "#" + member.Value.FileId] = new KeyValuePair<string, string>(unique, "file");
                    changed.Add(new JsonObject { ["asset"] = asset + "#" + member.Value.FileId, ["id"] = unique });
                }
            }
            return changed;
        }

        /// <summary>
        /// 클래스 이름. Unity가 에셋에 적어 둔 이름(m_EditorClassIdentifier)이 있으면 그것을 쓰고,
        /// 없으면 m_Script guid가 가리키는 .cs 파일 이름을 쓴다. 파일 이름과 클래스 이름이 다른 스크립트가 있어서다.
        /// </summary>
        private string TypeOf(UnityDoc doc, string asset)
        {
            var script = doc.Body.Get("m_Script") as YMap;
            string guid = script?.Text("guid");
            string fileName = guid == null ? null : _index.ScriptClass(guid);

            string identifier = doc.Body.Text("m_EditorClassIdentifier");
            if (!string.IsNullOrEmpty(identifier))
            {
                int cut = Math.Max(identifier.LastIndexOf('.'), identifier.LastIndexOf(':'));
                string name = identifier.Substring(cut + 1);
                if (fileName != null && fileName != name)
                {
                    if (!_typeNameDiffers.TryGetValue(fileName, out var names)) _typeNameDiffers[fileName] = names = new SortedSet<string>(StringComparer.Ordinal);
                    names.Add(name);
                }
                return name;
            }
            if (fileName != null) { _typeFromFileName.Add(asset + "#" + doc.FileId); return fileName; }
            _unresolvedScripts.Add(asset + "#" + doc.FileId + " script " + (guid ?? "(none)"));
            return "UnknownScript";
        }

        private void WriteFields(JsonObject target, YMap body, string asset)
        {
            foreach (var entry in body.Entries)
            {
                if (HeaderFields.Contains(entry.Key)) continue;
                if (entry.Key == "_id" && entry.Value is YScalar) continue; // 위의 "id"가 이 값이다(비어 있으면 파일 이름)
                string name = StripUnderscore(entry.Key);
                if (ReservedFields.Contains(name) || target.ContainsKey(name))
                {
                    _collisions.Add(new JsonObject { ["asset"] = asset, ["field"] = entry.Key });
                    name = entry.Key;
                    if (target.ContainsKey(name)) continue;
                }
                target[name] = Convert(entry.Value, asset, name);
            }
        }

        private static string StripUnderscore(string key)
            => key.Length > 1 && key[0] == '_' ? key.Substring(1) : key;

        // ---- 값 ----

        private JsonNode Convert(YNode node, string asset, string field)
        {
            switch (node)
            {
                case YScalar scalar:
                    return scalar.Plain && JsonNumber.IsMatch(scalar.Value) && !PackedIntArray.IsMatch(scalar.Value)
                        ? JsonNode.Parse(scalar.Value) : JsonValue.Create(scalar.Value);
                case YSeq seq:
                    var array = new JsonArray();
                    for (int i = 0; i < seq.Items.Count; i++) array.Add(Convert(seq.Items[i], asset, field + "[" + i + "]"));
                    return array;
                case YMap map:
                    if (IsReference(map)) return ResolveReference(map, asset, asset, field);
                    var obj = new JsonObject();
                    foreach (var entry in map.Entries)
                    {
                        string name = StripUnderscore(entry.Key);
                        if (obj.ContainsKey(name)) name = entry.Key;
                        if (obj.ContainsKey(name)) continue;
                        obj[name] = Convert(entry.Value, asset, field + "." + name);
                    }
                    return obj;
                default:
                    return null;
            }
        }

        private static bool IsReference(YMap map)
            => map.Get("fileID") is YScalar && map.Entries.All(e => RefKeys.Contains(e.Key));

        /// <param name="context">참조가 적힌 파일(guid 없는 참조는 이 파일 안을 가리킨다).</param>
        /// <param name="asset">보고서에 적을 에셋.</param>
        private JsonNode ResolveReference(YMap reference, string context, string asset, string field)
        {
            string fileId = reference.Text("fileID");
            string guid = reference.Text("guid");
            if (guid == null)
                return fileId == "0" ? null : ResolveObject(context, fileId, null, asset, field);

            if (BuiltinGuid.IsMatch(guid)) // Unity 내장 리소스(unity default resources, unity_builtin_extra)
            {
                _builtinRefs++;
                return new JsonObject { ["builtin"] = guid, ["fileID"] = fileId };
            }

            string path = _index.PathOf(guid);
            if (path == null) return Unresolved(asset, field, guid, fileId, "guidNotFound", null);

            bool image = ImageExtensions.Contains(Path.GetExtension(path));
            if (!_index.Exists(path))
            {
                if (image) _missingPngs.Add(path);
                return Unresolved(asset, field, guid, fileId, "fileMissing", path);
            }
            if (image)
            {
                SpriteSheet sheet = _index.Sheet(path);
                if (sheet != null && sheet.NameByFileId.TryGetValue(fileId, out string spriteName))
                {
                    _referencedImages.Add(path);
                    return new JsonObject { ["sprite"] = path, ["name"] = spriteName };
                }
                if (fileId == "2800000") return new JsonObject { ["asset"] = path }; // 텍스처 자체
                return Unresolved(asset, field, guid, fileId, "spriteNotFound", path);
            }
            if (path.EndsWith(".asset", StringComparison.Ordinal))
                return ResolveObject(path, fileId, guid, asset, field);
            return new JsonObject { ["asset"] = path };
        }

        private JsonNode ResolveObject(string path, string fileId, string guid, string asset, string field)
        {
            var docs = _index.Docs(path);
            if (docs == null) return new JsonObject { ["asset"] = path };
            UnityDoc doc = docs.FirstOrDefault(d => d.FileId == fileId);
            if (doc == null) return Unresolved(asset, field, guid, fileId, "objectNotFound", path);
            if (doc.ClassId != 114) return new JsonObject { ["asset"] = path };

            if (IsTileShell(doc))
                return doc.Body.Get("m_Sprite") is YMap sprite ? ResolveReference(sprite, path, asset, field) : null;

            if (!path.StartsWith(_dataPrefix, StringComparison.Ordinal)) _refsOutsideData.Add(path);
            return new JsonObject { ["ref"] = IdOf(path, doc).Key, ["type"] = TypeOf(doc, path) };
        }

        private JsonNode Unresolved(string asset, string field, string guid, string fileId, string reason, string target)
        {
            var entry = new JsonObject { ["asset"] = asset, ["field"] = field, ["guid"] = guid, ["fileID"] = fileId, ["reason"] = reason };
            if (target != null) entry["target"] = target;
            _unresolved.Add(entry);
            return new JsonObject { ["unresolved"] = guid, ["fileID"] = fileId };
        }

        // ---- 등록부, 현지화, 좌표표 ----

        private JsonObject BuildRegistry(ExportResult result)
        {
            UnityDoc doc = MainDoc(_options.RegistryPath);
            if (doc == null) { result.Messages.Add("등록부 없음: " + _options.RegistryPath); return null; }
            var registry = new JsonObject();
            foreach (var entry in doc.Body.Entries)
            {
                if (HeaderFields.Contains(entry.Key)) continue;
                string name = StripUnderscore(entry.Key);
                registry[name] = IdsOnly(Convert(entry.Value, _options.RegistryPath, name));
            }
            return registry;
        }

        private static JsonNode IdsOnly(JsonNode node)
        {
            if (node is JsonObject obj && obj["ref"] is JsonValue id) return JsonValue.Create(id.GetValue<string>());
            if (node is JsonArray array)
            {
                var copy = new JsonArray();
                foreach (JsonNode item in array) copy.Add(IdsOnly(item));
                return copy;
            }
            return node?.DeepClone();
        }

        private int BuildLocalization(SortedDictionary<string, string> files, JsonArray duplicateKeys, ExportResult result)
        {
            UnityDoc doc = MainDoc(_options.CatalogPath);
            if (!(doc?.Body.Get("_entries") is YSeq entries)) { result.Messages.Add("현지화 카탈로그 없음: " + _options.CatalogPath); return 0; }

            var languages = new List<KeyValuePair<string, JsonObject>>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (YMap entry in entries.Items.OfType<YMap>())
            {
                string key = entry.Text("key");
                if (key == null) continue;
                if (!seen.Add(key)) { duplicateKeys.Add(key); continue; }
                foreach (var field in entry.Entries)
                {
                    if (field.Key == "key" || !(field.Value is YScalar text)) continue;
                    JsonObject table = languages.FirstOrDefault(l => l.Key == field.Key).Value;
                    if (table == null) languages.Add(new KeyValuePair<string, JsonObject>(field.Key, table = new JsonObject()));
                    table[key] = text.Value;
                }
            }
            foreach (var language in languages) files["localization/" + language.Key + ".json"] = Serialize(language.Value);
            return seen.Count;
        }

        private UnityDoc MainDoc(string asset)
        {
            if (!_index.Exists(asset)) return null;
            var docs = _index.Docs(asset);
            if (docs == null || docs.Count == 0) return null;
            return docs.FirstOrDefault(d => d.FileId == _index.MainFileId(asset)) ?? docs[0];
        }

        private static JsonObject BuildAtlas(SpriteSheet sheet, JsonArray outOfBounds)
        {
            var sprites = new JsonObject();
            foreach (SpriteDef s in sheet.Sprites.Where(s => s.Name != null).OrderBy(s => s.Name, StringComparer.Ordinal))
            {
                if (sprites.ContainsKey(s.Name)) continue;
                if (s.X < 0 || s.Y < 0 || s.X + s.W > sheet.Width || s.Y + s.H > sheet.Height)
                    outOfBounds.Add(sheet.Path + "#" + s.Name);
                // Unity 좌표는 왼쪽 아래 기준이다. 원본 PNG 높이로 위 기준으로 바꾼다.
                var sprite = new JsonObject
                {
                    ["x"] = Number(s.X), ["y"] = Number(sheet.Height - s.Y - s.H), ["w"] = Number(s.W), ["h"] = Number(s.H),
                    ["pivot"] = new JsonObject { ["x"] = Number(s.PivotX), ["y"] = Number(1 - s.PivotY) },
                };
                if (s.BorderLeft != 0 || s.BorderTop != 0 || s.BorderRight != 0 || s.BorderBottom != 0)
                    sprite["border"] = new JsonObject
                    {
                        ["left"] = Number(s.BorderLeft), ["top"] = Number(s.BorderTop),
                        ["right"] = Number(s.BorderRight), ["bottom"] = Number(s.BorderBottom),
                    };
                sprites[s.Name] = sprite;
            }
            return new JsonObject
            {
                ["image"] = sheet.Path,
                ["width"] = sheet.Width,
                ["height"] = sheet.Height,
                ["pixelsPerUnit"] = Number(sheet.PixelsPerUnit),
                ["origin"] = "top-left",
                ["sprites"] = sprites,
            };
        }

        private static JsonNode Number(double value)
            => value == Math.Floor(value) && Math.Abs(value) < 1e15 ? JsonValue.Create((long)value) : JsonValue.Create(value);

        // ---- 쓰기 ----

        private static JsonArray ToArray(IEnumerable<string> values)
        {
            var array = new JsonArray();
            foreach (string v in values) array.Add(v);
            return array;
        }

        private static string Serialize(JsonNode node) => node.ToJsonString(JsonOptions).Replace("\r\n", "\n") + "\n";

        private void WriteOutput(SortedDictionary<string, string> files, string reportText)
        {
            Directory.CreateDirectory(_options.OutDir);
            // 지난 실행이 만든 파일 중 이번에 만들지 않은 것만 지운다. 변환기가 만들지 않은 파일은 건드리지 않는다.
            string reportPath = Path.Combine(_options.OutDir, ReportFile);
            if (File.Exists(reportPath))
            {
                try
                {
                    using (JsonDocument previous = JsonDocument.Parse(File.ReadAllText(reportPath)))
                        if (previous.RootElement.TryGetProperty("files", out JsonElement old) && old.ValueKind == JsonValueKind.Array)
                            foreach (JsonElement item in old.EnumerateArray())
                            {
                                string rel = item.GetString();
                                if (rel == null || files.ContainsKey(rel) || rel.Contains("..") || Path.IsPathRooted(rel)) continue;
                                string stale = Path.Combine(_options.OutDir, rel);
                                if (File.Exists(stale)) File.Delete(stale);
                            }
                }
                catch (JsonException) { }
            }

            var encoding = new UTF8Encoding(false);
            foreach (var file in files) WriteIfChanged(Path.Combine(_options.OutDir, file.Key), encoding.GetBytes(file.Value));
            WriteIfChanged(reportPath, encoding.GetBytes(reportText));
        }

        private static void WriteIfChanged(string path, byte[] bytes)
        {
            if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
        }
    }
}
