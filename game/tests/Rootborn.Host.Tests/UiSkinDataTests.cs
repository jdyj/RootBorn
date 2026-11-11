using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rootborn.Host.Rendering;
using Rootborn.Ui;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// UI 테마 폴더(game/themes/&lt;이름&gt;/theme.json): 옮긴 두 테마가 예전과 같은 좌표를 내는지, 고르기, 빠진 것 알리기(창 없음, 자동 테스트).
    /// </summary>
    public sealed class UiSkinDataTests
    {
        private static string Root => HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
        private static string ThemeDir(string name) => Path.Combine(Root, "game", "themes", name);
        private static ThemeFolder Load(string name) => ThemeFolder.Load(ThemeDir(name), Root);
        private static string RepoPath(string relative) => Path.GetFullPath(Path.Combine(Root, relative));

        private static string _temp;

        [TearDown]
        public void TearDown()
        {
            if (_temp != null) try { Directory.Delete(_temp, true); } catch (IOException) { }
            _temp = null;
        }

        private static string TempDir()
        {
            _temp = Path.Combine(Path.GetTempPath(), "rootborn-theme-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temp);
            return _temp;
        }

        // ---------- 옮긴 테마가 예전과 같은가 ----------

        [Test]
        public void UrbanThemeFile_IsExactlyTheConversionOfTheOldData()
        {
            string expected = UrbanThemeConversion.ToText(UrbanThemeConversion.Urban(Root));
            string actual = File.ReadAllText(Path.Combine(ThemeDir("urban"), ThemeFolder.FileName)).Replace("\r\n", "\n");
            Assert.That(actual, Is.EqualTo(expected), "data/ui·data/atlases가 바뀌었으면 ThemeConversionWriter.WriteThemeFiles를 돌려 다시 쓴다");
        }

        [Test]
        public void Style2Icons_AreTheUrbanIcons_AsTheOldHostUsedThem()
        {
            JsonNode style2 = JsonNode.Parse(File.ReadAllText(Path.Combine(ThemeDir("style2"), ThemeFolder.FileName)));
            (JsonObject icons, JsonObject definitionIcons) = UrbanThemeConversion.Icons(Root);
            Assert.That(JsonNode.DeepEquals(style2["icons"], icons), Is.True);
            Assert.That(JsonNode.DeepEquals(style2["definitionIcons"], definitionIcons), Is.True);
        }

        [Test]
        public void Urban_PanelButtonSlot_DrawTheSameCoordinatesAsBefore()
        {
            UiSkins skins = Load("urban").Skins;
            SkinSource panel = skins.Get(UiSkin.Panel);
            Assert.That(panel.Texture, Is.EqualTo(RepoPath("Assets/Art/UI/UrbanMolding/urban-molding-atlas.png")));
            Assert.That(panel.Stretch, Is.True);
            // urban_panel (8,8) 96x96, 테두리 18
            Assert.That((panel.Slices.ColX(0), panel.Slices.ColX(1), panel.Slices.ColX(2), panel.Slices.ColX(3)), Is.EqualTo((8, 26, 86, 104)));
            Assert.That((panel.Slices.RowY(0), panel.Slices.RowY(1), panel.Slices.RowY(2), panel.Slices.RowY(3)), Is.EqualTo((8, 26, 86, 104)));
            Assert.That(panel.Padding, Is.EqualTo(new SkinPadding(36, 36, 36, 36)), "안쪽 여백 기본값 = 테두리 × 원본 1픽셀의 논리 크기");
            // urban_button (112,8) 96x24, 테두리 6
            SkinSource button = skins.Get(UiSkin.Button);
            Assert.That((button.Slices.ColX(0), button.Slices.ColX(1), button.Slices.ColX(3), button.Slices.RowY(0), button.Slices.RowY(3)), Is.EqualTo((112, 118, 208, 8, 32)));
            Assert.That(button.TextColor, Is.EqualTo(new UiColor(244, 237, 218)));
            // urban_slot (112,48) 48x48
            SkinSource slot = skins.Get(UiSkin.Slot);
            Assert.That((slot.Slices.ColX(0), slot.Slices.ColX(3), slot.Slices.RowY(0), slot.Slices.RowY(3)), Is.EqualTo((112, 160, 48, 96)));
            SkinSource dialogue = skins.Get(UiSkin.Dialogue), field = skins.Get(UiSkin.Field);
            Assert.That((dialogue.Texture, dialogue.Slices.ColX(1), dialogue.Slices.RowY(2)), Is.EqualTo((panel.Texture, 26, 86)), "Unity: 버튼·칸이 아닌 것은 패널 그림");
            Assert.That((field.Texture, field.Slices.ColX(0), field.Slices.RowY(3)), Is.EqualTo((slot.Texture, 112, 96)), "글자 칸은 테두리가 얇은 칸 그림");
        }

        [Test]
        public void Urban_OneSourcePixel_IsTwoLogicalUnits()
        {
            SkinSource panel = Load("urban").Skins.Get(UiSkin.Panel);
            Assert.That(panel.Scale(ScreenFit.For(1920, 1080)), Is.EqualTo(2));
            Assert.That(panel.Scale(ScreenFit.For(3840, 2160)), Is.EqualTo(4));
        }

        [Test]
        public void Style2_KeepsTheMeasuredCoordinates_AndDialogueIsTheRoundedLightBox()
        {
            UiSkins skins = Load("style2").Skins;
            static int[] Lines(SkinSource s) => new[] { s.Slices.ColX(0), s.Slices.ColX(1), s.Slices.ColX(2), s.Slices.ColX(3), s.Slices.RowY(0), s.Slices.RowY(1), s.Slices.RowY(2), s.Slices.RowY(3) };
            // 예전 코드의 실측값: 공통 패널 x11~37 y42~69(왼 5, 오 5, 위 6, 아래 5), 밝은 상자 x60~84 y235~260(왼 4, 위 5, 오 3, 아래 3), 어두운 상자 x12~36.
            int[] common = { 11, 16, 32, 37, 42, 48, 64, 69 }, light = { 60, 64, 81, 84, 235, 240, 257, 260 }, dark = { 12, 16, 33, 36, 235, 240, 257, 260 };
            Assert.That(Lines(skins.Get(UiSkin.Panel)), Is.EqualTo(common));
            Assert.That(Lines(skins.Get(UiSkin.Dialogue)), Is.EqualTo(light));
            Assert.That(Lines(skins.Get(UiSkin.Button)), Is.EqualTo(light));
            Assert.That(Lines(skins.Get(UiSkin.Field)), Is.EqualTo(light));
            Assert.That(Lines(skins.Get(UiSkin.Slot)), Is.EqualTo(dark));
            SkinSource panel = skins.Get(UiSkin.Panel);
            Assert.That((panel.Stretch, panel.LogicalPerPixel, panel.TextColor), Is.EqualTo((false, 0f, (UiColor?)null)), "가운데 반복, 화면 타일 배율");
            Assert.That(panel.Texture, Is.EqualTo(RepoPath("Assets/modernuserinterface-win/16x16/Modern_UI_Style_2.png")));
            Assert.That(File.Exists(panel.Texture), Is.True);
        }

        [Test]
        public void BothThemes_KeepTheDefaultTextColors()
        {
            foreach (string name in new[] { "urban", "style2" })
            {
                ThemeTextColors colors = Load(name).TextColors;
                Assert.That((colors.Ink, colors.InkMuted, colors.InkOnDark), Is.EqualTo((UiTheme.Ink, UiTheme.InkMuted, UiTheme.InkOnDark)), name);
            }
        }

        // ---------- 그림 id 풀기 ----------

        [Test]
        public void Images_ResolveThemeIcons_AtlasNames_AndWholeFiles()
        {
            var atlases = new SpriteAtlases(Root);
            var images = new ImageCatalog(atlases, Load("urban"));

            Assert.That(images.TryResolve("icon:chef", default, out string texture, out PixelRect rect), Is.True);
            Assert.That(texture, Is.EqualTo(RepoPath("Assets/Art/UI/UrbanMolding/urban-life-icons-atlas.png")));
            Assert.That(rect, Is.EqualTo(new PixelRect(4, 60, 48, 48)));

            Assert.That(images.TryResolve("Assets/Art/UI/UrbanMolding/folder.png#folder", default, out texture, out rect), Is.True);
            Assert.That(rect, Is.EqualTo(new PixelRect(0, 0, 64, 64)));

            Assert.That(images.TryResolve("Assets/Art/UI/UrbanMolding/folder.png", new PixelRect(1, 2, 3, 4), out texture, out rect), Is.True);
            Assert.That(rect, Is.EqualTo(new PixelRect(1, 2, 3, 4)), "명령의 원본 칸이 먼저다");

            Assert.That(images.TryResolve("icon:no-such-icon", default, out _, out _), Is.False);
            Assert.That(images.TryResolve("Assets/x.png#missing", default, out _, out _), Is.False);
        }

        [Test]
        public void IconForADefinition_UsesTheThemesDefinitionIcon_ElseTheFallbackKey()
        {
            var images = new ImageCatalog(new SpriteAtlases(Root), Load("urban"));
            images.TryResolve("icon:chef", default, out _, out PixelRect chef);
            images.TryResolve("icon:plan", default, out _, out PixelRect plan);
            // 테마 데이터: daily.practice.chef → chef. 테마가 정하지 않은 정의는 대체 키(plan).
            Assert.That(images.TryResolve("icon:plan@daily.practice.chef", default, out _, out PixelRect own), Is.True);
            Assert.That(own, Is.EqualTo(chef));
            Assert.That(images.TryResolve("icon:plan@no.such.definition", default, out _, out PixelRect fallback), Is.True);
            Assert.That(fallback, Is.EqualTo(plan));
        }

        [Test]
        public void EveryThemeIcon_PointsInsideAnExistingPng()
        {
            foreach (string name in new[] { "urban", "style2" })
            {
                ThemeFolder theme = Load(name);
                Assert.That(theme.Icons.Count, Is.GreaterThan(20), name);
                foreach (var pair in theme.Icons)
                {
                    (int w, int h)? size = PngSize.Read(pair.Value.Texture);
                    Assert.That(size, Is.Not.Null, pair.Key);
                    Assert.That(pair.Value.Rect.X + pair.Value.Rect.W <= size.Value.w && pair.Value.Rect.Y + pair.Value.Rect.H <= size.Value.h, Is.True, pair.Key);
                }
            }
        }

        // ---------- 고르기 ----------

        [Test]
        public void UiThemeOption_DefaultsToUrban_TakesAName_AFolder_OrTheEnvironment()
        {
            Assert.That(HostOptions.Resolve(new[] { "--sample" }).UiTheme.Name, Is.EqualTo("urban"));
            Assert.That(HostOptions.Resolve(new[] { "--sample", "--ui-theme", "style2" }).UiTheme.Name, Is.EqualTo("style2"));
            Assert.That(HostOptions.Resolve(new[] { "--sample", "--ui-theme", ThemeDir("style2") }).UiThemeDir, Is.EqualTo(Path.GetFullPath(ThemeDir("style2"))));
            string Env(string key) => key == HostOptions.UiThemeVariable ? "style2" : Environment.GetEnvironmentVariable(key);
            Assert.That(HostOptions.Resolve(new[] { "--sample" }, Env).UiTheme.Name, Is.EqualTo("style2"));
            Assert.That(HostOptions.Resolve(new[] { "--sample", "--ui-theme", "urban" }, Env).UiTheme.Name, Is.EqualTo("urban"), "인자가 환경 변수보다 먼저");
        }

        [Test]
        public void UnknownThemeFolder_StopsAtStart()
        {
            var e = Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--sample", "--ui-theme", "no-such-theme" }));
            Assert.That(e.Message, Does.Contain("no-such-theme"));
        }

        [Test]
        public void ThemeMissingSkinsOrIcons_StopsAtStart_ListingEveryMissingName()
        {
            string dir = TempDir();
            JsonNode theme = JsonNode.Parse(File.ReadAllText(Path.Combine(ThemeDir("urban"), ThemeFolder.FileName)));
            theme["skins"].AsObject().Remove(UiSkin.Dialogue);
            theme["icons"].AsObject().Remove("close");
            theme["icons"].AsObject().Remove("time");
            File.WriteAllText(Path.Combine(dir, ThemeFolder.FileName), theme.ToJsonString());

            var e = Assert.Throws<ThemeException>(() => HostOptions.Resolve(new[] { "--sample", "--ui-theme", dir }));
            Assert.That(e.Problems, Is.EquivalentTo(new[] { "빠진 스킨: dialogue", "빠진 아이콘: time", "빠진 아이콘: close" }));
            Assert.That(e.Message, Does.Contain("빠진 아이콘: close"));
        }

        [Test]
        public void ThemeWithMissingPngOrOutOfImageRect_StopsAtStart()
        {
            string dir = TempDir();
            JsonNode theme = JsonNode.Parse(File.ReadAllText(Path.Combine(ThemeDir("urban"), ThemeFolder.FileName)));
            theme["skins"]["panel"]["image"] = "no-such.png";
            theme["icons"]["close"]["rect"] = new JsonArray(0, 0, 9999, 10);
            theme["definitionIcons"]["some.definition"] = "no-such-icon";
            File.WriteAllText(Path.Combine(dir, ThemeFolder.FileName), theme.ToJsonString());

            var e = Assert.Throws<ThemeException>(() => ThemeFolder.Load(dir, Root));
            Assert.That(e.Problems, Has.Some.Contains("스킨 panel: 그림 파일이 없다"));
            Assert.That(e.Problems, Has.Some.Contains("아이콘 close: rect"));
            Assert.That(e.Problems, Has.Some.Contains("no-such-icon"));
        }

        // ---------- 필요한 스킨·아이콘 목록 ----------

        [Test]
        public void RequiredSkins_AreEveryUiSkinName()
        {
            Assert.That(ThemeRequirements.Skins, Is.EquivalentTo(new[] { UiSkin.Panel, UiSkin.Button, UiSkin.Slot, UiSkin.Field, UiSkin.Dialogue }));
        }

        /// <summary>화면 코드에 적힌 아이콘 키("icon:키", ScreenIcons.Key("키"), 목표 수첩 분류, 일과 카드 대체 키)가 모두 필수 목록에 있다.</summary>
        [Test]
        public void RequiredIcons_CoverEveryIconKeyWrittenInTheScreenCode()
        {
            string screens = Path.Combine(Root, "game", "Rootborn.Screens");
            var patterns = new[]
            {
                new Regex("\"icon:([A-Za-z0-9_-]+)\""),
                new Regex(@"ScreenIcons\.Key\(""([A-Za-z0-9_-]+)""\)"),
                new Regex(@"new JournalCategory\([^,]+,\s*""[^""]*"",\s*""([A-Za-z0-9_-]+)""\)"),
                new Regex(@"IconFallback\s*=\s*""([A-Za-z0-9_-]+)"""),
            };
            var found = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string file in Directory.EnumerateFiles(screens, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) || file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)) continue;
                string text = File.ReadAllText(file);
                foreach (Regex pattern in patterns)
                    foreach (Match m in pattern.Matches(text)) found.Add(m.Groups[1].Value);
            }
            Assert.That(found.Count, Is.GreaterThan(10), "훑기가 아이콘 키를 찾지 못했다(화면 코드 꼴이 바뀌었나)");
            Assert.That(found.Except(ThemeRequirements.Icons), Is.Empty, "ThemeRequirements.Icons에 더한다");
        }
    }

    /// <summary>예전 데이터에서 game/themes의 두 theme.json을 다시 쓴다. 손으로 돌린다(data/ui나 data/atlases가 바뀌었을 때).</summary>
    [Explicit("game/themes를 다시 쓴다")]
    public sealed class ThemeConversionWriter
    {
        [Test]
        public void WriteThemeFiles()
        {
            // 테마를 읽지 않고 저장소를 찾는다(옮기기 전에는 읽을 테마가 없다).
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "game", "Rootborn.sln"))) dir = dir.Parent;
            string root = dir?.FullName ?? throw new InvalidOperationException("저장소를 찾지 못했다");
            string themes = Path.Combine(root, "game", "themes");
            File.WriteAllText(Path.Combine(themes, "urban", ThemeFolder.FileName), UrbanThemeConversion.ToText(UrbanThemeConversion.Urban(root)));

            string style2Path = Path.Combine(themes, "style2", ThemeFolder.FileName);
            JsonObject style2 = JsonNode.Parse(File.ReadAllText(style2Path)).AsObject();
            (JsonObject icons, JsonObject definitionIcons) = UrbanThemeConversion.Icons(root);
            style2["icons"] = icons;
            style2["definitionIcons"] = definitionIcons;
            style2["textColors"] = UrbanThemeConversion.DefaultTextColors();
            File.WriteAllText(style2Path, UrbanThemeConversion.ToText(style2));
        }
    }
}
