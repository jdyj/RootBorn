using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.TownConcept
{
    public sealed class TownDefaultFlowSourceAuditTests
    {
        [Test]
        public void MainMenuAndBootstrap_DefaultPlayableSceneIsTown()
        {
            AssertDefaultTownScene("Assets/Scripts/UI/MainMenu/ModeSelectPanel.cs");
            AssertDefaultTownScene("Assets/Scripts/UI/MainMenu/SaveSlotSelectPanel.cs");
            AssertDefaultTownScene("Assets/Scripts/Game/Bootstrap/GameBootstrap.cs");
        }

        [Test]
        public void ModernUiPanelAutoInstaller_RunsOnlyOnTownScene()
        {
            string source = File.ReadAllText("Assets/Scripts/UI/Modern/ModernUiPanelAutoInstaller.cs");

            StringAssert.Contains("TownSceneName", source);
            StringAssert.Contains("\"Town\"", source);
            StringAssert.DoesNotContain("FarmSceneName", source);
            StringAssert.DoesNotContain("\"Farm\"", source);
        }

        private static void AssertDefaultTownScene(string path)
        {
            string source = File.ReadAllText(path);

            StringAssert.Contains("\"Town\"", source, path + " should name Town as the default playable scene.");
            StringAssert.DoesNotContain("_farmScene = \"Farm\"", source, path + " should not default to Farm.");
            StringAssert.DoesNotContain("LoadScene(_farmScene)", source, path + " should not load Farm through the old default field.");
        }
    }
}