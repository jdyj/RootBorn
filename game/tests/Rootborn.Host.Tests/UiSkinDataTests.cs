using System.IO;
using NUnit.Framework;
using Rootborn.Host.Rendering;
using Rootborn.Ui;

namespace Rootborn.Host.Tests
{
    /// <summary>스킨·그림 좌표를 실제 data/ui, data/atlases와 원본 PNG에서 읽는지(창 없음, 자동 테스트).</summary>
    public sealed class UiSkinDataTests
    {
        private static string Root => HostOptions.Resolve(new[] { "--sample" }).RepoRoot;

        [Test]
        public void Urban_PanelButtonSlot_ComeFromTheThemeAndAtlas()
        {
            UiSkins skins = UiSkins.Urban(Root, new SpriteAtlases(Root));
            SkinSource panel = skins.Get(UiSkin.Panel);
            Assert.That(panel.Texture, Is.EqualTo("Assets/Art/UI/UrbanMolding/urban-molding-atlas.png"));
            Assert.That(panel.Stretch, Is.True);
            // urban_panel (8,8) 96x96, 테두리 18
            Assert.That((panel.Slices.ColX(0), panel.Slices.ColX(1), panel.Slices.ColX(2), panel.Slices.ColX(3)), Is.EqualTo((8, 26, 86, 104)));
            Assert.That((panel.Slices.RowY(0), panel.Slices.RowY(3)), Is.EqualTo((8, 104)));
            // urban_button (112,8) 96x24, 테두리 6
            SkinSource button = skins.Get(UiSkin.Button);
            Assert.That((button.Slices.ColX(0), button.Slices.ColX(1), button.Slices.ColX(3), button.Slices.RowY(0), button.Slices.RowY(3)), Is.EqualTo((112, 118, 208, 8, 32)));
            Assert.That(button.TextColor, Is.EqualTo(new UiColor(244, 237, 218)));
            // urban_slot (112,48) 48x48
            SkinSource slot = skins.Get(UiSkin.Slot);
            Assert.That((slot.Slices.ColX(0), slot.Slices.ColX(3), slot.Slices.RowY(0), slot.Slices.RowY(3)), Is.EqualTo((112, 160, 48, 96)));
            Assert.That(skins.Get(UiSkin.Dialogue), Is.SameAs(panel), "Unity: 버튼·칸이 아닌 것은 패널 그림");
            Assert.That(skins.Get(UiSkin.Field), Is.SameAs(slot), "글자 칸은 테두리가 얇은 칸 그림(Unity 판은 흰 바탕)");
        }

        [Test]
        public void Urban_OneSourcePixel_IsTwoLogicalUnits()
        {
            SkinSource panel = UiSkins.Urban(Root, new SpriteAtlases(Root)).Get(UiSkin.Panel);
            Assert.That(panel.Scale(ScreenFit.For(1920, 1080)), Is.EqualTo(2));
            Assert.That(panel.Scale(ScreenFit.For(3840, 2160)), Is.EqualTo(4));
        }

        [Test]
        public void Style2_KeepsTheMeasuredCoordinates_AndDialogueIsTheRoundedLightBox()
        {
            UiSkins skins = UiSkins.Style2();
            Assert.That(skins.Get(UiSkin.Panel).Slices, Is.SameAs(UiSkins.Style2CommonPanel));
            Assert.That(skins.Get(UiSkin.Dialogue).Slices, Is.SameAs(UiSkins.Style2RoundedLight));
            Assert.That(skins.Get(UiSkin.Slot).Slices, Is.SameAs(UiSkins.Style2RoundedDark));
            Assert.That(File.Exists(Path.Combine(Root, UiSkins.Style2Sheet)), Is.True);
        }

        [Test]
        public void Images_ResolveThemeIcons_AtlasNames_AndWholeFiles()
        {
            var atlases = new SpriteAtlases(Root);
            var images = new ImageCatalog(atlases, UrbanTheme.Load(Root));

            Assert.That(images.TryResolve("icon:chef", default, out string texture, out PixelRect rect), Is.True);
            Assert.That(texture, Is.EqualTo("Assets/Art/UI/UrbanMolding/urban-life-icons-atlas.png"));
            Assert.That(rect, Is.EqualTo(new PixelRect(4, 60, 48, 48)));

            Assert.That(images.TryResolve("Assets/Art/UI/UrbanMolding/folder.png#folder", default, out texture, out rect), Is.True);
            Assert.That(rect, Is.EqualTo(new PixelRect(0, 0, 64, 64)));

            Assert.That(images.TryResolve("Assets/Art/UI/UrbanMolding/folder.png", new PixelRect(1, 2, 3, 4), out texture, out rect), Is.True);
            Assert.That(rect, Is.EqualTo(new PixelRect(1, 2, 3, 4)), "명령의 원본 칸이 먼저다");

            Assert.That(images.TryResolve("icon:no-such-icon", default, out _, out _), Is.False);
            Assert.That(images.TryResolve("Assets/x.png#missing", default, out _, out _), Is.False);
        }

        [Test]
        public void EveryThemeIcon_HasAnAtlasEntry_AndItsPngExists()
        {
            var atlases = new SpriteAtlases(Root);
            UrbanTheme theme = UrbanTheme.Load(Root);
            Assert.That(theme.Icons.Count, Is.GreaterThan(20));
            foreach (var pair in theme.Icons)
            {
                Assert.That(atlases.TryFind(pair.Value.Path, pair.Value.Name, out _), Is.True, pair.Key + " → " + pair.Value);
                Assert.That(File.Exists(atlases.FullPath(pair.Value.Path)), Is.True, pair.Value.Path);
            }
        }

        [Test]
        public void UiThemeOption_DefaultsToUrban_AndAcceptsStyle2()
        {
            Assert.That(HostOptions.Resolve(new[] { "--sample" }).UiTheme, Is.EqualTo(SkinTheme.Urban));
            Assert.That(HostOptions.Resolve(new[] { "--sample", "--ui-theme", "style2" }).UiTheme, Is.EqualTo(SkinTheme.Style2));
            Assert.That(() => HostOptions.Resolve(new[] { "--sample", "--ui-theme", "x" }), Throws.InvalidOperationException);
        }
    }
}
