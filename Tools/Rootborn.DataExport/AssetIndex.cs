using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Rootborn.DataExport
{
    public sealed class SpriteDef
    {
        public string Name;
        /// <summary>Unity 좌표(왼쪽 아래 기준).</summary>
        public double X, Y, W, H;
        /// <summary>Unity 기준 pivot(0~1, y는 아래에서 위).</summary>
        public double PivotX, PivotY;
        public double BorderLeft, BorderBottom, BorderRight, BorderTop;
    }

    public sealed class SpriteSheet
    {
        public string Path;
        /// <summary>PNG 헤더에서 읽은 원본 크기. 읽지 못하면 0.</summary>
        public int Width, Height;
        public double PixelsPerUnit;
        public Dictionary<string, string> NameByFileId = new Dictionary<string, string>(StringComparer.Ordinal);
        public List<SpriteDef> Sprites = new List<SpriteDef>();
    }

    /// <summary>.meta의 guid로 파일을 찾고, 에셋 문서와 스프라이트 표를 읽어 둔다.</summary>
    public sealed class AssetIndex
    {
        private static readonly Regex GuidLine = new Regex(@"^guid: ([0-9a-f]{32})", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex MainObject = new Regex(@"mainObjectFileID: (-?\d+)", RegexOptions.Compiled);

        private readonly string _root;
        private readonly Dictionary<string, string> _pathByGuid = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<UnityDoc>> _docs = new Dictionary<string, List<UnityDoc>>(StringComparer.Ordinal);
        private readonly Dictionary<string, SpriteSheet> _sheets = new Dictionary<string, SpriteSheet>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _mainFileIds = new Dictionary<string, string>(StringComparer.Ordinal);

        public Dictionary<string, string> ParseErrors { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public AssetIndex(string root, params string[] scanDirs)
        {
            _root = root;
            var found = new ConcurrentBag<KeyValuePair<string, string>>();
            foreach (string dir in scanDirs)
            {
                string full = System.IO.Path.Combine(root, dir);
                if (!Directory.Exists(full)) continue;
                Parallel.ForEach(Directory.EnumerateFiles(full, "*.meta", SearchOption.AllDirectories), metaPath =>
                {
                    string head = ReadHead(metaPath, 256);
                    Match m = GuidLine.Match(head);
                    if (!m.Success) return;
                    string rel = System.IO.Path.GetRelativePath(root, metaPath).Replace('\\', '/');
                    found.Add(new KeyValuePair<string, string>(m.Groups[1].Value, rel.Substring(0, rel.Length - ".meta".Length)));
                });
            }
            // 같은 guid가 두 번 나오면 경로 순서로 앞선 것을 쓴다(결과가 실행마다 같도록).
            foreach (var pair in found.OrderBy(p => p.Value, StringComparer.Ordinal))
                if (!_pathByGuid.ContainsKey(pair.Key)) _pathByGuid.Add(pair.Key, pair.Value);
        }

        private static string ReadHead(string path, int count)
        {
            byte[] buffer = new byte[count];
            using (var stream = File.OpenRead(path))
            {
                int read = stream.Read(buffer, 0, count);
                return Encoding.UTF8.GetString(buffer, 0, read).Replace("\r\n", "\n");
            }
        }

        public string PathOf(string guid) => _pathByGuid.TryGetValue(guid, out string path) ? path : null;

        public string Full(string rel) => System.IO.Path.Combine(_root, rel);

        public bool Exists(string rel) => File.Exists(Full(rel));

        /// <summary>Unity YAML이 아니거나 읽지 못하면 null.</summary>
        public List<UnityDoc> Docs(string rel)
        {
            if (_docs.TryGetValue(rel, out var cached)) return cached;
            List<UnityDoc> docs = null;
            try
            {
                string text = File.ReadAllText(Full(rel)).Replace("\r\n", "\n");
                if (text.StartsWith("%YAML", StringComparison.Ordinal)) docs = UnityYaml.ParseDocs(text);
            }
            catch (Exception e) when (e is YamlDotNet.Core.YamlException || e is InvalidDataException || e is IOException)
            {
                ParseErrors[rel] = e.Message;
            }
            _docs[rel] = docs;
            return docs;
        }

        public string MainFileId(string rel)
        {
            if (_mainFileIds.TryGetValue(rel, out string cached)) return cached;
            string id = "11400000";
            string meta = Full(rel) + ".meta";
            if (File.Exists(meta))
            {
                Match m = MainObject.Match(File.ReadAllText(meta));
                if (m.Success) id = m.Groups[1].Value;
            }
            _mainFileIds[rel] = id;
            return id;
        }

        /// <summary>guid가 .cs를 가리키면 그 파일 이름(= 클래스 이름).</summary>
        public string ScriptClass(string guid)
        {
            string path = PathOf(guid);
            return path != null && path.EndsWith(".cs", StringComparison.Ordinal) ? System.IO.Path.GetFileNameWithoutExtension(path) : null;
        }

        /// <summary>이미지의 .meta가 TextureImporter가 아니면 null.</summary>
        public SpriteSheet Sheet(string rel)
        {
            if (_sheets.TryGetValue(rel, out var cached)) return cached;
            SpriteSheet sheet = null;
            try
            {
                sheet = ReadSheet(rel);
            }
            catch (Exception e) when (e is YamlDotNet.Core.YamlException || e is InvalidDataException || e is IOException)
            {
                ParseErrors[rel + ".meta"] = e.Message;
            }
            _sheets[rel] = sheet;
            return sheet;
        }

        private SpriteSheet ReadSheet(string rel)
        {
            string metaPath = Full(rel) + ".meta";
            if (!File.Exists(metaPath)) return null;
            var importer = (UnityYaml.ParseNode(File.ReadAllText(metaPath).Replace("\r\n", "\n")) as YMap)?.Get("TextureImporter") as YMap;
            if (importer == null) return null;

            var sheet = new SpriteSheet { Path = rel, PixelsPerUnit = Num(importer.Text("spritePixelsToUnits"), 100) };
            ReadPngSize(Full(rel), out sheet.Width, out sheet.Height);

            var spriteSheet = importer.Get("spriteSheet") as YMap;
            var sprites = spriteSheet?.Get("sprites") as YSeq;
            bool multiple = importer.Text("spriteMode") == "2";
            if (multiple && sprites != null)
            {
                foreach (YMap s in sprites.Items.OfType<YMap>())
                {
                    var rect = s.Get("rect") as YMap ?? new YMap();
                    var def = new SpriteDef
                    {
                        Name = s.Text("name"),
                        X = Num(rect.Text("x"), 0), Y = Num(rect.Text("y"), 0),
                        W = Num(rect.Text("width"), 0), H = Num(rect.Text("height"), 0),
                    };
                    SetPivot(def, s.Text("alignment"), s.Get("pivot") as YMap);
                    SetBorder(def, s.Get("border") as YMap);
                    sheet.Sprites.Add(def);
                    string internalId = s.Text("internalID");
                    if (!string.IsNullOrEmpty(internalId) && internalId != "0") sheet.NameByFileId[internalId] = def.Name;
                }
                // 오래된 형식(fileIDToRecycleName)과 새 형식(internalIDToNameTable, nameFileIdTable)을 모두 받는다.
                if (importer.Get("fileIDToRecycleName") is YMap recycle)
                    foreach (var e in recycle.Entries) sheet.NameByFileId[e.Key] = (e.Value as YScalar)?.Value;
                if (importer.Get("internalIDToNameTable") is YSeq table)
                    foreach (YMap row in table.Items.OfType<YMap>())
                        if (row.Get("first") is YMap first && first.Entries.Count > 0 && first.Entries[0].Value is YScalar id)
                            sheet.NameByFileId[id.Value] = row.Text("second");
                if (spriteSheet.Get("nameFileIdTable") is YMap names)
                    foreach (var e in names.Entries)
                        if (e.Value is YScalar id) sheet.NameByFileId[id.Value] = e.Key;
            }
            else
            {
                var def = new SpriteDef { Name = System.IO.Path.GetFileNameWithoutExtension(rel), W = sheet.Width, H = sheet.Height };
                SetPivot(def, importer.Text("alignment"), importer.Get("spritePivot") as YMap);
                SetBorder(def, importer.Get("spriteBorder") as YMap);
                sheet.Sprites.Add(def);
                sheet.NameByFileId["21300000"] = def.Name;
            }
            return sheet;
        }

        private static readonly double[][] AlignmentPivots =
        {
            new[] { 0.5, 0.5 }, new[] { 0.0, 1.0 }, new[] { 0.5, 1.0 }, new[] { 1.0, 1.0 }, new[] { 0.0, 0.5 },
            new[] { 1.0, 0.5 }, new[] { 0.0, 0.0 }, new[] { 0.5, 0.0 }, new[] { 1.0, 0.0 },
        };

        private static void SetPivot(SpriteDef def, string alignment, YMap custom)
        {
            int a = (int)Num(alignment, 0);
            if (a >= 0 && a < AlignmentPivots.Length)
            {
                def.PivotX = AlignmentPivots[a][0];
                def.PivotY = AlignmentPivots[a][1];
            }
            else
            {
                def.PivotX = Num(custom?.Text("x"), 0.5);
                def.PivotY = Num(custom?.Text("y"), 0.5);
            }
        }

        private static void SetBorder(SpriteDef def, YMap border)
        {
            if (border == null) return;
            def.BorderLeft = Num(border.Text("x"), 0);
            def.BorderBottom = Num(border.Text("y"), 0);
            def.BorderRight = Num(border.Text("z"), 0);
            def.BorderTop = Num(border.Text("w"), 0);
        }

        private static double Num(string text, double fallback)
            => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : fallback;

        private static void ReadPngSize(string fullPath, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (!File.Exists(fullPath)) return;
            byte[] b = new byte[24];
            using (var stream = File.OpenRead(fullPath))
                if (stream.Read(b, 0, 24) < 24) return;
            if (b[0] != 0x89 || b[1] != 0x50 || b[2] != 0x4E || b[3] != 0x47 || b[12] != 0x49 || b[13] != 0x48 || b[14] != 0x44 || b[15] != 0x52) return;
            width = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
            height = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
        }
    }
}
