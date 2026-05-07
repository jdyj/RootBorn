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
    }
}
