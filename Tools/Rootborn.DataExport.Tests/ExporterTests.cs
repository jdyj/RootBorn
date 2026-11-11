using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace Rootborn.DataExport.Tests
{
    public sealed class ExporterTests
    {
        private static readonly string Quest = TempRepo.G(1), Reward = TempRepo.G(2), Furniture = TempRepo.G(3),
            Registry = TempRepo.G(4), Catalog = TempRepo.G(5);
        private static readonly string QuestA = TempRepo.G(0xA1), QuestB = TempRepo.G(0xA2), RewardItem = TempRepo.G(0xA3),
            Chair = TempRepo.G(0xA4), ChairTile = TempRepo.G(0xA5), QuestC = TempRepo.G(0xA6), RegistryAsset = TempRepo.G(0xA7),
            CatalogAsset = TempRepo.G(0xA8), Sheet = TempRepo.G(0xB1), ChairPng = TempRepo.G(0xB2), Prefab = TempRepo.G(0xC1);

        private TempRepo _repo;

        [SetUp]
        public void SetUp()
        {
            _repo = new TempRepo();
            _repo.Script("QuestDefinition", Quest);
            _repo.Script("ItemReward", Reward);
            _repo.Script("FurnitureDefinition", Furniture);
            _repo.Script("GameDataRegistry", Registry);
            _repo.Script("GameTextCatalog", Catalog);

            _repo.Png("Assets/Art/sheet.png", 32, 64, Sheet,
                "TextureImporter:\n  internalIDToNameTable:\n  - first:\n      213: 777\n    second: icon_b\n"
                + "  spriteMode: 2\n  alignment: 0\n  spritePivot: {x: 0.5, y: 0.5}\n  spritePixelsToUnits: 16\n"
                + "  spriteSheet:\n    sprites:\n"
                + "    - name: icon_a\n      rect:\n        x: 0\n        y: 48\n        width: 16\n        height: 16\n"
                + "      alignment: 0\n      pivot: {x: 0.5, y: 0.5}\n      border: {x: 0, y: 0, z: 0, w: 0}\n      internalID: -555\n"
                + "    - name: icon_b\n      rect:\n        x: 16\n        y: 0\n        width: 16\n        height: 24\n"
                + "      alignment: 7\n      pivot: {x: 0.5, y: 0}\n      border: {x: 1, y: 2, z: 3, w: 4}\n      internalID: 777\n"
                + "    nameFileIdTable:\n      icon_a: -555\n      icon_b: 777\n");
            _repo.Png("Assets/Art/chair.png", 16, 32, ChairPng,
                "TextureImporter:\n  spriteMode: 1\n  alignment: 0\n  spritePivot: {x: 0.5, y: 0.5}\n  spritePixelsToUnits: 32\n"
                + "  spriteSheet:\n    sprites: []\n");
            _repo.Write("Assets/Prefabs/Thing.prefab", "%YAML 1.1\n--- !u!1 &123\nGameObject:\n  m_Name: Thing\n");
            _repo.Meta("Assets/Prefabs/Thing.prefab", Prefab);

            _repo.Asset("Assets/Data/Quests/Quest_B.asset", QuestB, TempRepo.Mono(Quest, "Quest_B",
                "  _id: quest.b\n  _displayNameKey: quest.b.name\n  _count: 3\n  _ratio: 0.5\n  _code: 007\n"
                + "  _title: \"\\uC694\\uB9AC 12\"\n  _quotedNumber: '12'\n  _empty: \n  _tags: []\n"
                + "  _rewards:\n  - {fileID: 11400000, guid: " + RewardItem + ", type: 2}\n"
                + "  _icon: {fileID: 777, guid: " + Sheet + ", type: 3}\n"
                + "  _portrait: {fileID: 21300000, guid: " + ChairPng + ", type: 3}\n"
                + "  _prefab: {fileID: 123, guid: " + Prefab + ", type: 3}\n"
                + "  _none: {fileID: 0}\n"
                + "  _packedDigits: 6400000004000000\n  _packedExponent: 1e00000003000000\n  _packedOne: 64000000\n"
                + "  _bigCount: 1234567\n"
                + "  _nested:\n    _inner: 1\n    _cells:\n    - {x: 0, y: -1}\n"));
            _repo.Asset("Assets/Data/Quests/Quest_A.asset", QuestA, TempRepo.Mono(Quest, "Quest_A", "  _id: quest.a\n"));
            _repo.Asset("Assets/Data/Quests/Rewards/Reward_Item.asset", RewardItem,
                TempRepo.Mono(Reward, "Reward_Item", "  _amount: 2\n"));
            _repo.Asset("Assets/Data/Quests/Quest_C.asset", QuestC,
                TempRepo.Mono(Reward, "Sub_Reward", "  _amount: 9\n", "555"),
                TempRepo.Mono(Quest, "Quest_C", "  _id: quest.c\n  _rewards:\n  - {fileID: 555}\n"));

            _repo.Asset("Assets/Data/Interiors/Furniture/Furn_Chair.asset", Chair, TempRepo.Mono(Furniture, "Furn_Chair",
                "  _stableId: chair\n  _tileParts:\n  - _localCell: {x: 0, y: 0}\n"
                + "    _tile: {fileID: 11400000, guid: " + ChairTile + ", type: 2}\n"));
            _repo.Asset("Assets/Data/Interiors/Tiles/Chair_Tile.asset", ChairTile, TempRepo.Mono(TempRepo.TileScriptGuid, "Chair_Tile",
                "  m_Sprite: {fileID: 21300000, guid: " + ChairPng + ", type: 3}\n  m_Color: {r: 1, g: 1, b: 1, a: 1}\n"));

            _repo.Asset("Assets/Data/Registry/GameDataRegistry.asset", RegistryAsset, TempRepo.Mono(Registry, "GameDataRegistry",
                "  _quests:\n  - {fileID: 11400000, guid: " + QuestB + ", type: 2}\n  - {fileID: 11400000, guid: " + QuestA + ", type: 2}\n"
                + "  _mainQuest: {fileID: 11400000, guid: " + QuestA + ", type: 2}\n  _version: 2\n"));
            _repo.Asset("Assets/Localization/GameTextCatalog.asset", CatalogAsset, TempRepo.Mono(Catalog, "GameTextCatalog",
                "  _entries:\n  - key: z.last\n    ko: \"\\uB05D\"\n    en: End\n  - key: a.first\n    ko: \"\\uCC98\\uC74C\"\n    en: 'First: one'\n"));
        }

        [TearDown]
        public void TearDown() => _repo.Dispose();

        private ExportResult Run(bool allowUnresolved = false, string outDir = null)
            => Exporter.Run(new ExportOptions { RepoRoot = _repo.Root, OutDir = outDir ?? _repo.Out, AllowUnresolved = allowUnresolved });

        private JsonElement Json(string relOut) => JsonDocument.Parse(_repo.Read(relOut)).RootElement;

        private JsonElement Entity(string relOut, string id)
            => Json(relOut).EnumerateArray().Single(e => e.GetProperty("id").GetString() == id);

        [Test]
        public void CleanRepo_ExitsZero()
        {
            var result = Run();
            Assert.AreEqual(0, result.ExitCode, string.Join("\n", result.Messages));
        }

        [Test]
        public void Entity_HasTypeIdAssetAndFieldsWithoutLeadingUnderscore()
        {
            Run();
            var b = Entity("quests/QuestDefinition.json", "quest.b");

            Assert.AreEqual(new[] { "type", "id", "asset", "displayNameKey" }, b.EnumerateObject().Take(4).Select(p => p.Name).ToArray());
            Assert.AreEqual("QuestDefinition", b.GetProperty("type").GetString());
            Assert.AreEqual("Assets/Data/Quests/Quest_B.asset", b.GetProperty("asset").GetString());
            Assert.AreEqual("quest.b.name", b.GetProperty("displayNameKey").GetString());
            Assert.IsFalse(b.TryGetProperty("m_Name", out _));
            Assert.IsFalse(b.TryGetProperty("m_Script", out _));
        }

        [Test]
        public void Values_KeepNumbersAndStrings()
        {
            Run();
            var b = Entity("quests/QuestDefinition.json", "quest.b");

            Assert.AreEqual(3, b.GetProperty("count").GetInt32());
            Assert.AreEqual("0.5", b.GetProperty("ratio").GetRawText());
            Assert.AreEqual("007", b.GetProperty("code").GetString());
            Assert.AreEqual("요리 12", b.GetProperty("title").GetString());
            Assert.AreEqual("12", b.GetProperty("quotedNumber").GetString());
            Assert.AreEqual("", b.GetProperty("empty").GetString());
            Assert.AreEqual(0, b.GetProperty("tags").GetArrayLength());
            Assert.AreEqual(1, b.GetProperty("nested").GetProperty("inner").GetInt32());
            Assert.AreEqual(-1, b.GetProperty("nested").GetProperty("cells")[0].GetProperty("y").GetInt32());
            StringAssert.Contains("요리 12", _repo.Read("quests/QuestDefinition.json"), "한글은 이스케이프하지 않는다");
        }

        [Test]
        public void PackedIntArrays_AreStrings_EvenWhenTheyLookLikeNumbers()
        {
            // Unity는 int 배열을 원소마다 8글자(리틀엔디언 16진)로 묶어 따옴표 없이 쓴다. 숫자처럼 보여도 문자열로 낸다.
            Run();
            var b = Entity("quests/QuestDefinition.json", "quest.b");

            Assert.AreEqual(JsonValueKind.String, b.GetProperty("packedDigits").ValueKind);
            Assert.AreEqual("6400000004000000", b.GetProperty("packedDigits").GetString());
            Assert.AreEqual("1e00000003000000", b.GetProperty("packedExponent").GetString());
            Assert.AreEqual("64000000", b.GetProperty("packedOne").GetString());
            Assert.AreEqual(1234567, b.GetProperty("bigCount").GetInt32(), "길이가 8의 배수가 아닌 정수는 숫자 그대로");
        }

        [Test]
        public void References_ResolveByKind()
        {
            Run();
            var b = Entity("quests/QuestDefinition.json", "quest.b");

            Assert.AreEqual("{\"ref\":\"Reward_Item\",\"type\":\"ItemReward\"}", Compact(b.GetProperty("rewards")[0]));
            Assert.AreEqual("{\"sprite\":\"Assets/Art/sheet.png\",\"name\":\"icon_b\"}", Compact(b.GetProperty("icon")));
            Assert.AreEqual("{\"sprite\":\"Assets/Art/chair.png\",\"name\":\"chair\"}", Compact(b.GetProperty("portrait")));
            Assert.AreEqual("{\"asset\":\"Assets/Prefabs/Thing.prefab\"}", Compact(b.GetProperty("prefab")));
            Assert.AreEqual(JsonValueKind.Null, b.GetProperty("none").ValueKind);
        }

        [Test]
        public void SubAsset_IsExportedAndReferencedByLocalFileId()
        {
            Run();
            var c = Entity("quests/QuestDefinition.json", "quest.c");
            Assert.AreEqual("{\"ref\":\"Quest_C/Sub_Reward\",\"type\":\"ItemReward\"}", Compact(c.GetProperty("rewards")[0]));

            var sub = Entity("quests/ItemReward.json", "Quest_C/Sub_Reward");
            Assert.AreEqual(9, sub.GetProperty("amount").GetInt32());
            Assert.AreEqual("Assets/Data/Quests/Quest_C.asset", sub.GetProperty("asset").GetString());
        }

        [Test]
        public void Files_GroupByFirstFolderAndClass_SortedById()
        {
            Run();
            var ids = Json("quests/QuestDefinition.json").EnumerateArray().Select(e => e.GetProperty("id").GetString()).ToArray();
            Assert.AreEqual(new[] { "quest.a", "quest.b", "quest.c" }, ids);
            Assert.IsTrue(File.Exists(Path.Combine(_repo.Out, "interiors", "FurnitureDefinition.json")));
        }

        [Test]
        public void TileShell_IsNotExported_AndReferenceBecomesItsSprite()
        {
            var result = Run();
            Assert.IsFalse(File.Exists(Path.Combine(_repo.Out, "interiors", "Tile.json")));
            Assert.AreEqual(1, result.Report.GetProperty("summary").GetProperty("skippedTileShells").GetInt32());

            var chair = Entity("interiors/FurnitureDefinition.json", "chair");
            Assert.AreEqual("chair", chair.GetProperty("stableId").GetString());
            Assert.AreEqual("{\"sprite\":\"Assets/Art/chair.png\",\"name\":\"chair\"}",
                Compact(chair.GetProperty("tileParts")[0].GetProperty("tile")));
        }

        [Test]
        public void Atlas_ConvertsRectsToTopLeftUsingPngHeaderHeight()
        {
            Run();
            var atlas = Json("atlases/Assets/Art/sheet.png.atlas.json");

            Assert.AreEqual("Assets/Art/sheet.png", atlas.GetProperty("image").GetString());
            Assert.AreEqual(32, atlas.GetProperty("width").GetInt32());
            Assert.AreEqual(64, atlas.GetProperty("height").GetInt32());
            Assert.AreEqual(16, atlas.GetProperty("pixelsPerUnit").GetInt32());
            Assert.AreEqual("top-left", atlas.GetProperty("origin").GetString());
            var sprites = atlas.GetProperty("sprites");
            Assert.AreEqual("{\"x\":0,\"y\":0,\"w\":16,\"h\":16,\"pivot\":{\"x\":0.5,\"y\":0.5}}", Compact(sprites.GetProperty("icon_a")));
            // Unity y=0(아래) 높이 24 → 위 기준 y = 64 - 0 - 24 = 40. 아래 가운데 pivot은 위 기준 y=1.
            Assert.AreEqual("{\"x\":16,\"y\":40,\"w\":16,\"h\":24,\"pivot\":{\"x\":0.5,\"y\":1},\"border\":{\"left\":1,\"top\":4,\"right\":3,\"bottom\":2}}",
                Compact(sprites.GetProperty("icon_b")));
        }

        [Test]
        public void Atlas_SingleSpriteCoversWholeImage()
        {
            Run();
            var atlas = Json("atlases/Assets/Art/chair.png.atlas.json");
            Assert.AreEqual(32, atlas.GetProperty("pixelsPerUnit").GetInt32());
            Assert.AreEqual("{\"x\":0,\"y\":0,\"w\":16,\"h\":32,\"pivot\":{\"x\":0.5,\"y\":0.5}}", Compact(atlas.GetProperty("sprites").GetProperty("chair")));
        }

        [Test]
        public void Localization_WritesOneFilePerLanguageInOriginalOrder()
        {
            Run();
            var ko = Json("localization/ko.json");
            var en = Json("localization/en.json");

            Assert.AreEqual(new[] { "z.last", "a.first" }, ko.EnumerateObject().Select(p => p.Name).ToArray());
            Assert.AreEqual("처음", ko.GetProperty("a.first").GetString());
            Assert.AreEqual("First: one", en.GetProperty("a.first").GetString());
        }

        [Test]
        public void Registry_WritesIdsOnly_AndIsNotExportedAsEntity()
        {
            Run();
            var reg = Json("registry.json");

            Assert.AreEqual("[\"quest.b\",\"quest.a\"]", Compact(reg.GetProperty("quests")));
            Assert.AreEqual("quest.a", reg.GetProperty("mainQuest").GetString());
            Assert.AreEqual(2, reg.GetProperty("version").GetInt32());
            Assert.IsFalse(Directory.Exists(Path.Combine(_repo.Out, "registry")));
        }

        [Test]
        public void Report_ListsTypeCountsAndIdFallbacks()
        {
            var report = Run().Report;

            Assert.AreEqual(3, report.GetProperty("typeCounts").GetProperty("QuestDefinition").GetInt32());
            Assert.AreEqual(2, report.GetProperty("typeCounts").GetProperty("ItemReward").GetInt32());
            var fallbacks = report.GetProperty("idFromFileName").EnumerateArray().Select(e => e.GetString()).ToArray();
            CollectionAssert.AreEquivalent(new[] { "Assets/Data/Quests/Quest_C.asset#555", "Assets/Data/Quests/Rewards/Reward_Item.asset" }, fallbacks);
            Assert.AreEqual(0, report.GetProperty("unresolved").GetArrayLength());
            Assert.AreEqual(report.GetRawText(), _repo.Read("export-report.json").TrimEnd('\n'));
            CollectionAssert.Contains(report.GetProperty("files").EnumerateArray().Select(e => e.GetString()).ToArray(), "quests/QuestDefinition.json");
        }

        [Test]
        public void UnresolvedGuid_IsReported_AndExitCodeIsOneUnlessAllowed()
        {
            string dead = "deadbeefdeadbeefdeadbeefdeadbeef";
            _repo.Asset("Assets/Data/Quests/Quest_D.asset", TempRepo.G(0xD1), TempRepo.Mono(Quest, "Quest_D",
                "  _id: quest.d\n  _rewards:\n  - {fileID: 11400000, guid: " + dead + ", type: 2}\n"));

            var result = Run();

            Assert.AreEqual(1, result.ExitCode);
            var u = result.Report.GetProperty("unresolved")[0];
            Assert.AreEqual("Assets/Data/Quests/Quest_D.asset", u.GetProperty("asset").GetString());
            Assert.AreEqual("rewards[0]", u.GetProperty("field").GetString());
            Assert.AreEqual(dead, u.GetProperty("guid").GetString());
            Assert.AreEqual("guidNotFound", u.GetProperty("reason").GetString());
            Assert.AreEqual("{\"unresolved\":\"" + dead + "\",\"fileID\":\"11400000\"}",
                Compact(Entity("quests/QuestDefinition.json", "quest.d").GetProperty("rewards")[0]));
            Assert.AreEqual(0, Run(allowUnresolved: true).ExitCode);
        }

        [Test]
        public void MissingSpriteNameAndMissingPng_AreReported()
        {
            string gone = TempRepo.G(0xB9);
            _repo.Meta("Assets/Art/gone.png", gone, "TextureImporter:\n  spriteMode: 1\n  spritePixelsToUnits: 16\n");
            _repo.Asset("Assets/Data/Quests/Quest_E.asset", TempRepo.G(0xD2), TempRepo.Mono(Quest, "Quest_E",
                "  _id: quest.e\n  _icon: {fileID: 4242, guid: " + Sheet + ", type: 3}\n  _portrait: {fileID: 21300000, guid: " + gone + ", type: 3}\n"));

            var result = Run();

            Assert.AreEqual(1, result.ExitCode);
            var reasons = result.Report.GetProperty("unresolved").EnumerateArray().Select(e => e.GetProperty("reason").GetString()).ToArray();
            CollectionAssert.AreEquivalent(new[] { "spriteNotFound", "fileMissing" }, reasons);
            Assert.AreEqual("Assets/Art/gone.png", result.Report.GetProperty("missingPngs")[0].GetString());
        }

        [Test]
        public void DuplicateIdsWithinType_AreReported()
        {
            _repo.Asset("Assets/Data/Quests/Quest_A2.asset", TempRepo.G(0xD3), TempRepo.Mono(Quest, "Quest_A2", "  _id: quest.a\n"));

            var dup = Run().Report.GetProperty("duplicateIds")[0];

            Assert.AreEqual("QuestDefinition", dup.GetProperty("type").GetString());
            Assert.AreEqual("quest.a", dup.GetProperty("id").GetString());
            Assert.AreEqual(2, dup.GetProperty("assets").GetArrayLength());
        }

        [Test]
        public void FileNameIdsThatCollide_AreDisambiguatedByPathOrOrder()
        {
            // 폴더만 다른 같은 파일 이름, 한 파일 안 같은 이름의 하위 에셋. 둘 다 _id가 없다.
            _repo.Asset("Assets/Data/Quests/Chef/Reward_Same.asset", TempRepo.G(0xE1), TempRepo.Mono(Reward, "Reward_Same", "  _amount: 1\n"));
            _repo.Asset("Assets/Data/Quests/Soldier/Reward_Same.asset", TempRepo.G(0xE2), TempRepo.Mono(Reward, "Reward_Same", "  _amount: 2\n"));
            _repo.Asset("Assets/Data/Quests/Quest_G.asset", TempRepo.G(0xE3),
                TempRepo.Mono(Reward, "Sub", "  _amount: 3\n", "-71"),
                TempRepo.Mono(Reward, "Sub", "  _amount: 4\n", "72"),
                TempRepo.Mono(Quest, "Quest_G", "  _id: quest.g\n  _rewards:\n  - {fileID: 72}\n  - {fileID: 11400000, guid: " + TempRepo.G(0xE2) + ", type: 2}\n"));

            var result = Run();

            Assert.AreEqual(0, result.Report.GetProperty("duplicateIds").GetArrayLength());
            var ids = Json("quests/ItemReward.json").EnumerateArray().Select(e => e.GetProperty("id").GetString()).ToArray();
            CollectionAssert.AreEquivalent(new[]
            {
                "Quests/Chef/Reward_Same", "Quests/Soldier/Reward_Same", "Quest_G/Sub#1", "Quest_G/Sub#2", "Quest_C/Sub_Reward", "Reward_Item",
            }, ids);
            var g = Entity("quests/QuestDefinition.json", "quest.g");
            Assert.AreEqual("Quest_G/Sub#2", g.GetProperty("rewards")[0].GetProperty("ref").GetString());
            Assert.AreEqual("Quests/Soldier/Reward_Same", g.GetProperty("rewards")[1].GetProperty("ref").GetString());
            Assert.AreEqual(4, result.Report.GetProperty("idDisambiguated").GetArrayLength());
        }

        [Test]
        public void EmptyIdField_FallsBackToFileNameWithoutCollisionNoise()
        {
            _repo.Asset("Assets/Data/Quests/Quest_H.asset", TempRepo.G(0xE4), TempRepo.Mono(Quest, "Quest_H", "  _id: \n  _count: 1\n"));

            var result = Run();

            var h = Entity("quests/QuestDefinition.json", "Quest_H");
            Assert.IsFalse(h.TryGetProperty("_id", out _));
            Assert.AreEqual(0, result.Report.GetProperty("reservedFieldCollisions").GetArrayLength());
            CollectionAssert.Contains(result.Report.GetProperty("idFromFileName").EnumerateArray().Select(e => e.GetString()).ToArray(), "Assets/Data/Quests/Quest_H.asset");
        }

        [Test]
        public void Type_PrefersClassNameWrittenInAsset_WhenScriptFileNameDiffers()
        {
            // 파일 이름과 클래스 이름이 다른 스크립트가 있다(CareerCandidateCore.cs 안의 CareerCandidateDefinition).
            string doc = TempRepo.Mono(Reward, "Reward_Named", "  _amount: 1\n")
                .Replace("m_EditorClassIdentifier: ", "m_EditorClassIdentifier: Rootborn.Game::Rootborn.Game.Quests.RealRewardClass");
            _repo.Asset("Assets/Data/Quests/Reward_Named.asset", TempRepo.G(0xE5), doc);
            _repo.Asset("Assets/Data/Quests/Quest_I.asset", TempRepo.G(0xE6), TempRepo.Mono(Quest, "Quest_I",
                "  _id: quest.i\n  _rewards:\n  - {fileID: 11400000, guid: " + TempRepo.G(0xE5) + ", type: 2}\n"));

            var result = Run();

            Assert.AreEqual("Reward_Named", Json("quests/RealRewardClass.json")[0].GetProperty("id").GetString());
            Assert.AreEqual("RealRewardClass", Entity("quests/QuestDefinition.json", "quest.i").GetProperty("rewards")[0].GetProperty("type").GetString());
            Assert.AreEqual("RealRewardClass", result.Report.GetProperty("typeNameDiffersFromScriptFile").GetProperty("ItemReward")[0].GetString());
        }

        [Test]
        public void ReservedFieldName_IsKeptUnderOriginalNameAndReported()
        {
            _repo.Asset("Assets/Data/Quests/Quest_F.asset", TempRepo.G(0xD4), TempRepo.Mono(Quest, "Quest_F", "  _id: quest.f\n  _type: 3\n"));

            var result = Run();

            var f = Entity("quests/QuestDefinition.json", "quest.f");
            Assert.AreEqual("QuestDefinition", f.GetProperty("type").GetString());
            Assert.AreEqual(3, f.GetProperty("_type").GetInt32());
            Assert.AreEqual(1, result.Report.GetProperty("reservedFieldCollisions").GetArrayLength());
        }

        [Test]
        public void TwoRuns_ProduceIdenticalBytes()
        {
            string other = Path.Combine(_repo.Root, "data2");
            Run();
            Run(outDir: other);
            Run(); // 같은 폴더에 다시 써도 같다

            var first = Directory.GetFiles(_repo.Out, "*", SearchOption.AllDirectories).Select(p => Path.GetRelativePath(_repo.Out, p)).OrderBy(p => p, System.StringComparer.Ordinal).ToArray();
            var second = Directory.GetFiles(other, "*", SearchOption.AllDirectories).Select(p => Path.GetRelativePath(other, p)).OrderBy(p => p, System.StringComparer.Ordinal).ToArray();
            Assert.AreEqual(first, second);
            Assert.Greater(first.Length, 6);
            foreach (string rel in first)
            {
                byte[] a = File.ReadAllBytes(Path.Combine(_repo.Out, rel));
                Assert.AreEqual(a, File.ReadAllBytes(Path.Combine(other, rel)), rel);
                Assert.IsFalse(a.Contains((byte)'\r'), rel + ": 줄 끝은 LF");
                Assert.AreEqual((byte)'\n', a[a.Length - 1], rel);
            }
        }

        [Test]
        public void StaleOutputFromPreviousRun_IsRemoved()
        {
            Run();
            File.Delete(Path.Combine(_repo.Root, "Assets/Data/Interiors/Furniture/Furn_Chair.asset"));
            string unrelated = Path.Combine(_repo.Out, "hand-written.json");
            File.WriteAllText(unrelated, "{}");

            Run();

            Assert.IsFalse(File.Exists(Path.Combine(_repo.Out, "interiors", "FurnitureDefinition.json")));
            Assert.IsTrue(File.Exists(unrelated), "변환기가 만들지 않은 파일은 건드리지 않는다");
        }

        private static string Compact(JsonElement e) => JsonSerializer.Serialize(e, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }
}
