using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Family
{
    public sealed class CharacterPartPlayModeAuditSourceTests
    {
        private const string PlayModeTestPath = "Assets/Tests/PlayMode/CharacterPartCompositionPlayModeTests.cs";

        [Test]
        public void CharacterPartCompositionPlayMode_CapturesLayeredPlayerScreenshotEvidence()
        {
            Assert.IsTrue(File.Exists(PlayModeTestPath), PlayModeTestPath);
            string source = File.ReadAllText(PlayModeTestPath);
            StringAssert.Contains("CHAR_PART_005_FarmPlayerLayeredRenderScreenshot_WritesEvidence", source);
            StringAssert.Contains("CapturePlayerScreenshot", source);
            StringAssert.Contains("character-layered-player.png", source);
            StringAssert.Contains("CountVisiblePixels", source);
            StringAssert.Contains("CountVisiblePartRenderers", source);
            StringAssert.Contains("Builds/Logs/character-parts", source);
        }
    }
}
