using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernUiSourceAuditTests
    {
        [Test]
        public void StatusHud_FirstScopeUiDoesNotUseSingleSpriteSlicedPanels()
        {
            string source = File.ReadAllText("Assets/Scripts/UI/HUD/StatusHud.cs");
            StringAssert.DoesNotContain("Image.Type.Sliced", source);
            StringAssert.DoesNotContain("MakeDefaultSlotGO", source);
            StringAssert.DoesNotContain("new Color(0f, 0f, 0f, 0.4f)", source);
        }

        [Test]
        public void InventoryBookPanel_UsesTiledModernUiBackground()
        {
            string source = File.ReadAllText("Assets/Scripts/UI/HUD/StatusHud.cs");
            StringAssert.Contains("BookPanel", source);
            StringAssert.Contains("ModernUiTileImage", source);
            StringAssert.DoesNotContain("bookSprite = ModernHudSprite", source);
        }

        [Test]
        public void InventoryControls_DoNotScaleSingleButtonOrRibbonSprites()
        {
            string source = File.ReadAllText("Assets/Scripts/UI/HUD/StatusHud.cs");
            StringAssert.DoesNotContain("smallBtnSprite = ModernHudSprite", source);
            StringAssert.DoesNotContain("itemsRibbonSprite = ModernHudSprite", source);
            StringAssert.DoesNotContain("var sprite = ModernHudSprite(ribbonKey)", source);
        }

        [Test]
        public void FarmHudController_WiresSettingsPanelToggle()
        {
            string source = File.ReadAllText("Assets/Scripts/UI/HUD/StatusHud.cs");
            StringAssert.Contains("SettingsPanel", source);
            StringAssert.Contains("<Keyboard>/escape", source);
            StringAssert.Contains("ToggleSettings", source);
        }

        [Test]
        public void ReconstructionAudit_DocumentsReplacedDeferredAndVerification()
        {
            string audit = File.ReadAllText("docs/art/modern-ui-reconstruction-audit.md");
            StringAssert.Contains("## Replaced", audit);
            StringAssert.Contains("## Deferred", audit);
            StringAssert.Contains("## Verification", audit);
        }
    }
}
