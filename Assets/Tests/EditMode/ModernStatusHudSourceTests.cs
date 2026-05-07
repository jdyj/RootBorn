using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernStatusHudSourceTests
    {
        private const string StatusHudPath = "Assets/Scripts/UI/HUD/StatusHud.cs";
        private const string CharacterHudOverlayPath = "Assets/Scripts/UI/HUD/FarmCharacterHudOverlayInstaller.cs";

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
    }
}
