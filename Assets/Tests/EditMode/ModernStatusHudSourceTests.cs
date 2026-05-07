using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernStatusHudSourceTests
    {
        private const string StatusHudPath = "Assets/Scripts/UI/HUD/StatusHud.cs";
        private const string CharacterHudOverlayPath = "Assets/Scripts/UI/HUD/FarmCharacterHudOverlayInstaller.cs";
        private const string CharacterHudPlayModeTestPath = "Assets/Tests/PlayMode/FarmCharacterHudOverlayPlayModeTests.cs";

        [Test]
        public void StatusHud_UsesModernHudSpriteKeysInsteadOfLegacyUiSpriteAddresses()
        {
            string source = File.ReadAllText(StatusHudPath);
            StringAssert.Contains("ModernHudSpriteKeys", source);
            Assert.IsFalse(source.Contains("UISpriteAddresses."), "StatusHud still references legacy Pixelwood UI sprite addresses directly.");
        }

        [Test]
        public void StatusHud_DefinesModernHudSpriteResolver()
        {
            string source = File.ReadAllText(StatusHudPath);
            StringAssert.Contains("ModernHudSprite(ModernHudSpriteKey key)", source);
            StringAssert.Contains("SubSpr(key.SheetAddress, key.SubSpriteName)", source);
        }

        [Test]
        public void FarmHudOverlay_BuildsTopLeftLayeredCharacterThumbnail()
        {
            Assert.IsTrue(File.Exists(CharacterHudOverlayPath), CharacterHudOverlayPath);
            string source = File.ReadAllText(CharacterHudOverlayPath);
            StringAssert.Contains("CharacterThumbnailFrame", source);
            StringAssert.Contains("BuildCharacterThumbnail", source);
            StringAssert.Contains("CharacterAppearance.ResolveWithDefaults", source);
            StringAssert.Contains("PreviewSprite", source);
            StringAssert.Contains("HudPart_", source);
        }

        [Test]
        public void FarmHudOverlay_FollowsPixelwoodReferenceTopLeftHudStructure()
        {
            Assert.IsTrue(File.Exists(CharacterHudOverlayPath), CharacterHudOverlayPath);
            string source = File.ReadAllText(CharacterHudOverlayPath);
            StringAssert.Contains("TimeLabel", source);
            StringAssert.Contains("CurrencyLabel", source);
            StringAssert.Contains("HudSlot_Inventory", source);
            StringAssert.Contains("HudSlot_Health", source);
            StringAssert.Contains("HudSlot_Tool", source);
            StringAssert.DoesNotContain("HealthGauge", source);
            StringAssert.DoesNotContain("EnergyGauge", source);
            StringAssert.DoesNotContain("ToolGauge", source);
        }

        [Test]
        public void FarmHudOverlay_UsesModern16x16TileRecipesInsteadOfSolidPanels()
        {
            Assert.IsTrue(File.Exists(CharacterHudOverlayPath), CharacterHudOverlayPath);
            string source = File.ReadAllText(CharacterHudOverlayPath);
            StringAssert.Contains("Rootborn.UI.Modern", source);
            StringAssert.Contains("ModernUiTileImage", source);
            StringAssert.Contains("ModernUiRecipes.CommonPanel", source);
            StringAssert.Contains("TileBox", source);
            StringAssert.Contains("tileImage.Rebuild();", source);
        }

        [Test]
        public void FarmHudOverlay_BindsTimeAndDayLabelsToRuntimeStateWithoutCurrencyIdBranching()
        {
            Assert.IsTrue(File.Exists(CharacterHudOverlayPath), CharacterHudOverlayPath);
            string source = File.ReadAllText(CharacterHudOverlayPath);
            StringAssert.Contains("Rootborn.Game.Time", source);
            StringAssert.Contains("RefreshHudData", source);
            StringAssert.Contains("GameClock.Instance", source);
            StringAssert.Contains("DayProgress01", source);
            StringAssert.Contains("SetText(hudRoot, \"CurrencyLabel\", \"0G\")", source);
            StringAssert.DoesNotContain("FindCurrencyItem", source);
            StringAssert.DoesNotContain("item.Id ==", source);
        }

        [Test]
        public void FarmHudOverlay_PlayModeAuditCapturesTopLeftHudScreenshotEvidence()
        {
            Assert.IsTrue(File.Exists(CharacterHudPlayModeTestPath), CharacterHudPlayModeTestPath);
            string source = File.ReadAllText(CharacterHudPlayModeTestPath);
            StringAssert.Contains("FarmScene_TopLeftHudScreenshotAudit_WritesEvidence", source);
            StringAssert.Contains("CaptureHudScreenshot", source);
            StringAssert.Contains("farm-top-left-hud.png", source);
            StringAssert.Contains("CountVisiblePixels", source);
            StringAssert.Contains("CountTextOverflows", source);
            StringAssert.Contains("Builds/Logs/modern-ui", source);
        }

        [Test]
        public void FarmHudOverlay_PlayModeAuditWritesReferenceComparisonReport()
        {
            Assert.IsTrue(File.Exists(CharacterHudPlayModeTestPath), CharacterHudPlayModeTestPath);
            string source = File.ReadAllText(CharacterHudPlayModeTestPath);
            StringAssert.Contains("FarmScene_TopLeftHudReferenceComparison_WritesReport", source);
            StringAssert.Contains("docs/art/reference/pixelwood-reference-frame0-top-left.png", source);
            StringAssert.Contains("farm-top-left-hud-crop.png", source);
            StringAssert.Contains("farm-top-left-hud-reference-report.json", source);
            StringAssert.Contains("CaptureTopLeftCrop", source);
            StringAssert.Contains("WriteReferenceComparisonReport", source);
        }
    }
}
