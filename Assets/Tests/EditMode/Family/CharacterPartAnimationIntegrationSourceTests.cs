using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Family
{
    public sealed class CharacterPartAnimationIntegrationSourceTests
    {
        [Test]
        public void FarmAutoFiller_ConfiguresCharacterPartAnimatorWithCachedFrameResolver()
        {
            string source = File.ReadAllText("Assets/Scripts/Game/Bootstrap/FarmAutoFiller.cs");

            StringAssert.Contains("CharacterPartAnimator", source);
            StringAssert.Contains("animator.Configure", source);
            StringAssert.Contains("resource.GetCachedSubSprite(part.SheetAddress, subSpriteName)", source);
        }

        [Test]
        public void PlayerController_PushesMotionStateIntoCharacterPartAnimator()
        {
            string source = File.ReadAllText("Assets/Scripts/Game/Player/PlayerController.cs");

            StringAssert.Contains("CharacterPartAnimator", source);
            StringAssert.Contains("_partAnimator.SetMotion", source);
            StringAssert.Contains("_partAnimator.Tick", source);
        }

        [Test]
        public void PlayerController_PlaysEquippedToolCharacterPartAnimationClipOnAttack()
        {
            string source = File.ReadAllText("Assets/Scripts/Game/Player/PlayerController.cs");

            StringAssert.Contains("_activeCharacterPartAnimationClip", source);
            StringAssert.Contains("ToolById.TryGetValue", source);
            StringAssert.Contains("CharacterPartAnimationClip", source);
            StringAssert.Contains("_partAnimator.PlayClip", source);
        }

        [Test]
        public void PlayerController_RendersHeldToolOnDedicatedLayerWhenPartCharacterDisablesRootRenderer()
        {
            string source = File.ReadAllText("Assets/Scripts/Game/Player/PlayerController.cs");

            StringAssert.Contains("_toolRenderer", source);
            StringAssert.Contains("EnsureToolRenderer", source);
            StringAssert.Contains("Part_tool", source);
            StringAssert.Contains("_toolRenderer.sprite = s", source);
            StringAssert.DoesNotContain("_renderer.sprite = s", source);
        }

        [Test]
        public void PlayerController_KeepsHeldToolSpriteVisibleWhileWalkingWithoutAttack()
        {
            string source = File.ReadAllText("Assets/Scripts/Game/Player/PlayerController.cs");

            StringAssert.Contains("UpdateToolSprite();", source);
            StringAssert.Contains("var facing = _isAttacking ? _attackFacing : _lastFacing;", source);
            StringAssert.Contains("else\n            {\n                frame = 0;\n            }", source);
            StringAssert.Contains("_toolRenderer.enabled = true", source);
        }

        [Test]
        public void PlayerController_BindsEquipmentEventsEvenWhenInventoryWasFoundInAwake()
        {
            string source = File.ReadAllText("Assets/Scripts/Game/Player/PlayerController.cs");

            StringAssert.Contains("_inventoryEventsBound", source);
            StringAssert.DoesNotContain("if (_inventory != null) return;", source);
            StringAssert.Contains("_inventory.OnEquipmentChanged += OnEquipmentChanged", source);
            StringAssert.Contains("OnEquipmentChanged();", source);
        }
    }
}
