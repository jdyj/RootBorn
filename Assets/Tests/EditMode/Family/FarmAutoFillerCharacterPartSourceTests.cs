using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Family
{
    public sealed class FarmAutoFillerCharacterPartSourceTests
    {
        [Test]
        public void FarmAutoFiller_WiresCharacterPartComposerFromRegistryAndActiveSaveAppearance()
        {
            string source = File.ReadAllText("Assets/Scripts/Game/Bootstrap/FarmAutoFiller.cs");

            StringAssert.Contains("CharacterPartComposer", source);
            StringAssert.Contains("CharacterPartAnimator", source);
            StringAssert.Contains("registry.CharacterParts", source);
            StringAssert.Contains("ActiveSaveContext.Metadata", source);
            StringAssert.Contains("CharacterAppearance.ResolveWithDefaults", source);
            StringAssert.Contains("animator.Configure", source);
            StringAssert.Contains("BuildFrameSubSpriteName", source);
        }
    }
}
