using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace Rootborn.Data.Tests.Integrity
{
    /// <summary>
    /// 데이터가 가리키는 파일이 저장소에 있는지 본다(자동 테스트). 변환기 보고서(data/export-report.json)가 알려 주던 것을
    /// Unity 없이 데이터만으로 확인한다.
    /// - data/ 엔티티와 registry.json의 그림 참조 { "sprite": PNG, "name": 칸 이름 }: PNG가 있고, 좌표표
    ///   data/atlases/&lt;PNG&gt;.atlas.json에 그 PNG와 그 이름의 칸이 있고, 칸이 그림 크기 안이다.
    /// - 파일 참조 { "asset": 경로 }: 파일이 있다. PNG가 아니면(Unity 프리팹 등) 알려진 빈 곳이어야 한다.
    /// - 풀지 못한 참조 { "unresolved": GUID }: 알려진 빈 곳이어야 한다.
    /// - 좌표표 자체, UI 테마(game/themes/*/theme.json), 맵 목록(game/world/maps.json)과 그 LDtk·레벨·타일셋,
    ///   상호작용(game/world/interactions.json)이 가리키는 정의 id.
    /// 그림 크기는 PNG 머리에서만 읽는다. 지금 걸리는 것은 데이터를 고치지 않고 KnownDataGaps에 고정한다.
    /// </summary>
    public sealed class FileReferenceTests
    {
        private static DataScan Data => DataScan.Real;

        private readonly PictureReferences _pictures = new PictureReferences(DataScan.Real);

        [Test]
        public void EveryPictureReferenceInData_PointsToAPngAndAnAtlasCellInsideIt()
        {
            var references = new PictureReferences(Data);
            var gaps = references.Find();

            TestContext.WriteLine($"그림 참조 {references.SpriteCount}개");
            Assert.That(references.SpriteCount, Is.GreaterThan(1000), "그림 참조를 찾는 방식이 데이터 모양과 어긋났다");
            GapAssert.OnlyKnown(gaps, KnownDataGaps.All, PictureReferences.Kinds);
        }

        [Test]
        public void ThePictureCheck_FindsEachKindOfGap()
        {
            using var dir = new TempDir();
            dir.Write("data/registry.json", @"{ ""groundSprite"": { ""sprite"": ""art/gone.png"", ""name"": ""gone"" } }");
            dir.Write("art/visual.prefab", "%YAML");
            dir.Write("art/fake.png", "not a png");
            File.WriteAllBytes(System.IO.Path.Combine(dir.Path, "art", "a.png"), PngHead(16, 16));
            File.WriteAllBytes(System.IO.Path.Combine(dir.Path, "art", "noatlas.png"), PngHead(8, 8));
            dir.Write("data/atlases/art/a.png.atlas.json",
                @"{ ""image"": ""art/a.png"", ""width"": 16, ""height"": 16, ""sprites"": { ""ok"": { ""x"": 0, ""y"": 0, ""w"": 16, ""h"": 16 }, ""wide"": { ""x"": 8, ""y"": 0, ""w"": 16, ""h"": 8 } } }");
            dir.Write("data/things/Thing.json", @"[{ ""type"": ""Thing"", ""id"": ""thing.1"",
                ""good"": { ""sprite"": ""art/a.png"", ""name"": ""ok"" },
                ""parts"": [ { ""tile"": { ""unresolved"": ""abc"", ""fileID"": ""1"" } } ],
                ""noName"": { ""sprite"": ""art/a.png"", ""name"": ""nope"" },
                ""outside"": { ""sprite"": ""art/a.png"", ""name"": ""wide"" },
                ""notPng"": { ""sprite"": ""art/fake.png"", ""name"": ""x"" },
                ""noAtlas"": { ""sprite"": ""art/noatlas.png"", ""name"": ""x"" },
                ""prefab"": { ""asset"": ""art/visual.prefab"" },
                ""missing"": { ""asset"": ""art/none.png"" } }]");

            var references = new PictureReferences(DataScan.Of(dir.Path));
            var found = references.Find().Select(g => g.Kind + " " + g.Where.Substring(g.Where.LastIndexOf(' ') + 1)).ToList();

            Assert.That(found, Is.EquivalentTo(new[]
            {
                "unresolved parts[0].tile", "missing-cell noName", "cell-outside outside", "not-png notPng", "missing-atlas noAtlas",
                "unity-file prefab", "missing-file missing", "missing-png groundSprite",
            }));
            Assert.That(references.SpriteCount, Is.EqualTo(6));
        }

        [Test]
        public void EveryAtlas_PointsToItsPngAndKeepsItsCellsInside()
        {
            var gaps = new List<DataGap>();
            var atlasDirectory = Path.Combine(Data.DataDirectory, "atlases");
            var files = Directory.GetFiles(atlasDirectory, "*.atlas.json", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var where = Data.Relative(file);
                var atlas = DataScan.ReadJson(file);
                var image = DataScan.Str(atlas, "image");
                var expected = Path.GetRelativePath(atlasDirectory, file).Replace('\\', '/');
                if (image == null || expected != image + ".atlas.json")
                {
                    gaps.Add(new DataGap("atlas-image", where, $"image '{image}'가 파일 이름과 맞지 않는다"));
                    continue;
                }

                if (!Png(image, out var w, out var h))
                {
                    gaps.Add(new DataGap("missing-png", where, image));
                    continue;
                }

                if (atlas.GetProperty("width").GetInt32() != w || atlas.GetProperty("height").GetInt32() != h)
                    gaps.Add(new DataGap("atlas-size", where, $"좌표표 {atlas.GetProperty("width")}x{atlas.GetProperty("height")}, PNG {w}x{h}"));
                foreach (var cell in atlas.GetProperty("sprites").EnumerateObject())
                    if (!PictureReferences.Inside(cell.Value, w, h))
                        gaps.Add(new DataGap("cell-outside", where + " " + cell.Name, PictureReferences.Rect(cell.Value) + $" / PNG {w}x{h}"));
            }

            TestContext.WriteLine($"좌표표 {files.Length}개");
            Assert.That(files.Length, Is.GreaterThan(1000));
            GapAssert.OnlyKnown(gaps, KnownDataGaps.All, "atlas-image", "missing-png", "atlas-size", "cell-outside");
        }

        [Test]
        public void EveryTheme_PointsToPngsCellsAndDefinitionsThatExist()
        {
            var gaps = new List<DataGap>();
            var themes = Directory.GetFiles(Path.Combine(Data.Root, "game", "themes"), "theme.json", SearchOption.AllDirectories);
            foreach (var file in themes)
            {
                var where = Data.Relative(file);
                var theme = DataScan.ReadJson(file);
                var folder = Path.GetDirectoryName(file);
                var icons = new HashSet<string>(StringComparer.Ordinal);
                foreach (var section in new[] { "skins", "icons" })
                {
                    if (!theme.TryGetProperty(section, out var entries)) continue;
                    foreach (var entry in entries.EnumerateObject())
                    {
                        if (section == "icons") icons.Add(entry.Name);
                        var at = $"{where} {section}.{entry.Name}";
                        var image = DataScan.Str(entry.Value, "image");
                        var full = image != null && image.StartsWith("repo:", StringComparison.Ordinal)
                            ? Data.Full(image.Substring("repo:".Length))
                            : Path.GetFullPath(Path.Combine(folder, image ?? ""));
                        if (image == null || !File.Exists(full))
                        {
                            gaps.Add(new DataGap("missing-png", at, image ?? "(image 없음)"));
                            continue;
                        }

                        if (!PngAt(full, out var w, out var h))
                        {
                            gaps.Add(new DataGap("not-png", at, image));
                            continue;
                        }

                        if (entry.Value.TryGetProperty("rect", out var rect))
                        {
                            var r = rect.EnumerateArray().Select(v => v.GetInt32()).ToArray();
                            if (r.Length != 4 || r[0] < 0 || r[1] < 0 || r[2] <= 0 || r[3] <= 0 || r[0] + r[2] > w || r[1] + r[3] > h)
                                gaps.Add(new DataGap("cell-outside", at, $"rect [{string.Join(", ", r)}] / PNG {w}x{h}"));
                        }
                    }
                }

                if (theme.TryGetProperty("definitionIcons", out var definitionIcons))
                {
                    foreach (var pair in definitionIcons.EnumerateObject())
                    {
                        if (!Data.Ids.Contains(pair.Name)) gaps.Add(new DataGap("missing-definition", $"{where} definitionIcons.{pair.Name}", "data/에 그 id가 없다"));
                        if (!icons.Contains(pair.Value.GetString()))
                            gaps.Add(new DataGap("missing-icon", $"{where} definitionIcons.{pair.Name}", $"테마에 아이콘 '{pair.Value.GetString()}'이 없다"));
                    }
                }
            }

            Assert.That(themes, Is.Not.Empty);
            GapAssert.OnlyKnown(gaps, KnownDataGaps.All, "missing-png", "not-png", "cell-outside", "missing-definition", "missing-icon");
        }

        [Test]
        public void EveryMap_PointsToAnLdtkLevelAndTilesetPngsInTheRepo()
        {
            var gaps = new List<DataGap>();
            var maps = DataScan.ReadJson(Path.Combine(Data.Root, "game", "world", "maps.json")).GetProperty("maps");
            var projects = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var map in maps.EnumerateArray())
            {
                var where = "game/world/maps.json " + DataScan.Str(map, "id");
                var ldtk = DataScan.Str(map, "ldtk");
                var level = DataScan.Str(map, "level");
                if (ldtk == null || !File.Exists(Data.Full(ldtk)))
                {
                    gaps.Add(new DataGap("missing-file", where, "LDtk " + ldtk));
                    continue;
                }

                projects.Add(ldtk);
                var project = DataScan.ReadJson(Data.Full(ldtk));
                var found = project.GetProperty("levels").EnumerateArray().Where(l => DataScan.Str(l, "identifier") == level).ToList();
                if (found.Count != 1)
                {
                    gaps.Add(new DataGap("missing-level", where, $"{ldtk}에 레벨 '{level}'이 {found.Count}개"));
                    continue;
                }

                var external = DataScan.Str(found[0], "externalRelPath");
                if (external != null && !File.Exists(Path.Combine(Path.GetDirectoryName(Data.Full(ldtk)), external)))
                    gaps.Add(new DataGap("missing-file", where, "레벨 파일 " + external));
            }

            var tilesets = 0;
            foreach (var ldtk in projects)
            {
                var full = Data.Full(ldtk);
                var project = DataScan.ReadJson(full);
                foreach (var tileset in project.GetProperty("defs").GetProperty("tilesets").EnumerateArray())
                {
                    var relPath = DataScan.Str(tileset, "relPath");
                    if (relPath == null) continue; // LDtk 내장 그림(그림 파일 없음)
                    tilesets++;
                    var where = $"{ldtk} tileset {DataScan.Str(tileset, "identifier")}";
                    var png = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(full), relPath));
                    if (!Data.InsideRepo(png))
                    {
                        gaps.Add(new DataGap("tileset-outside-repo", where, relPath));
                        continue;
                    }

                    if (!File.Exists(png))
                    {
                        gaps.Add(new DataGap("missing-png", where, relPath));
                        continue;
                    }

                    if (!PngAt(png, out var w, out var h))
                        gaps.Add(new DataGap("not-png", where, relPath));
                    else if (tileset.GetProperty("pxWid").GetInt32() != w || tileset.GetProperty("pxHei").GetInt32() != h)
                        gaps.Add(new DataGap("tileset-size", where, $"LDtk {tileset.GetProperty("pxWid")}x{tileset.GetProperty("pxHei")}, PNG {w}x{h}"));
                }
            }

            TestContext.WriteLine($"맵 {maps.GetArrayLength()}개, LDtk 프로젝트 {projects.Count}개, 타일셋 {tilesets}개");
            Assert.That(tilesets, Is.GreaterThan(0));
            GapAssert.OnlyKnown(gaps, KnownDataGaps.All, "missing-file", "missing-level", "tileset-outside-repo", "missing-png", "not-png", "tileset-size");
        }

        /// <summary>
        /// 상호작용 지점의 종류마다 id가 무엇을 가리키는가. 정의를 가리키는 종류는 그 종류의 정의가 data/에 있어야 한다.
        /// 점 이름일 뿐인 종류는 data/를 보지 않는다. 표에 없는 종류가 생기면 여기에 분류한다.
        /// </summary>
        private static readonly Dictionary<string, string> InteractionKindTargets = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["location-activity"] = "LocationDefinition",
            ["location-visit"] = "LocationDefinition",
            ["part-time-work"] = "PartTimeWorkDefinition",
            ["career-practice"] = "CareerPracticeDefinition",
            ["career-facility"] = "FacilityDefinition",
            ["exploration"] = "ExplorationInteractionDefinition",
            ["discovery"] = "DiscoveryDefinition",
            ["daily-plan-board"] = null, // 점 이름(그 맵의 일과판)
            ["student-day-end"] = null,  // 점 이름(하루 끝내기)
        };

        [Test]
        public void EveryInteraction_PointsToMapsAndDefinitionsThatExist()
        {
            var gaps = new List<DataGap>();
            var mapIds = new HashSet<string>(DataScan.ReadJson(Path.Combine(Data.Root, "game", "world", "maps.json")).GetProperty("maps")
                .EnumerateArray().Select(m => DataScan.Str(m, "id")), StringComparer.Ordinal);
            var maps = DataScan.ReadJson(Path.Combine(Data.Root, "game", "world", "interactions.json")).GetProperty("maps");
            var checkedIds = 0;
            foreach (var map in maps.EnumerateObject())
            {
                var where = "game/world/interactions.json " + map.Name;
                if (!mapIds.Contains(map.Name)) gaps.Add(new DataGap("missing-map", where, "maps.json에 그 맵이 없다"));
                if (map.Value.TryGetProperty("npcAnchors", out var anchors))
                    foreach (var anchor in anchors.EnumerateObject())
                    {
                        checkedIds++;
                        if (!Data.HasEntity("LocationDefinition", anchor.Name))
                            gaps.Add(new DataGap("missing-definition", where + " npcAnchors." + anchor.Name, "LocationDefinition이 없다"));
                    }

                if (!map.Value.TryGetProperty("things", out var things)) continue;
                var i = 0;
                foreach (var thing in things.EnumerateArray())
                {
                    var at = $"{where} things[{i++}]";
                    var kind = DataScan.Str(thing, "kind");
                    var id = DataScan.Str(thing, "id");
                    if (kind == null || !InteractionKindTargets.TryGetValue(kind, out var type))
                    {
                        gaps.Add(new DataGap("unknown-kind", at, $"종류 '{kind}'를 InteractionKindTargets에 분류하지 않았다"));
                        continue;
                    }

                    if (type == null) continue;
                    checkedIds++;
                    if (!Data.HasEntity(type, id)) gaps.Add(new DataGap("missing-definition", at, $"{kind} → {type}/{id}가 없다"));
                }
            }

            TestContext.WriteLine($"정의를 가리키는 상호작용 id {checkedIds}개");
            Assert.That(checkedIds, Is.GreaterThan(0));
            GapAssert.OnlyKnown(gaps, KnownDataGaps.All, "missing-map", "missing-definition", "unknown-kind");
        }

        [Test]
        public void PngSize_ReadsOnlyTheHeader()
        {
            using var dir = new TempDir();
            var path = System.IO.Path.Combine(dir.Path, "a.png");
            File.WriteAllBytes(path, PngHead(300, 2));
            Assert.That(PngSize.TryRead(path, out var w, out var h), Is.True);
            Assert.That((w, h), Is.EqualTo((300, 2)));
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            Assert.That(PngSize.TryRead(path, out _, out _), Is.False);
        }

        // PNG 서명 + IHDR 머리만(그림 본문 없음).
        private static byte[] PngHead(int width, int height)
        {
            return new byte[]
            {
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R',
                (byte)(width >> 24), (byte)(width >> 16), (byte)(width >> 8), (byte)width,
                (byte)(height >> 24), (byte)(height >> 16), (byte)(height >> 8), (byte)height,
            };
        }

        private bool Png(string repoRelative, out int width, out int height) => PngAt(Data.Full(repoRelative), out width, out height);

        private bool PngAt(string full, out int width, out int height) => _pictures.PngAt(full, out width, out height);
    }
}
