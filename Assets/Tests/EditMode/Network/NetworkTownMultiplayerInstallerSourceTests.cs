using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Network
{
    public sealed class NetworkTownMultiplayerInstallerSourceTests
    {
        [Test]
        public void MULTI_DIRECT_003B_TownPlayableBaselineReinforcesEveryScenePlayer()
        {
            const string sourcePath = "Assets/Scripts/Game/Bootstrap/TownPlayableBaselineRuntimeInstaller.cs";

            Assert.IsTrue(File.Exists(sourcePath), "Town multiplayer clients can have multiple Player roots; baseline setup must not reinforce only the first Player.");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("CollectPlayerRoots", source);
            StringAssert.Contains("foreach (var candidate in players)", source);
            StringAssert.Contains("ReinforcePlayer(live)", source);
            StringAssert.Contains("ConfigureLayeredPlayer(live)", source);
            StringAssert.Contains("EnsurePlayerInteractionRouter(player)", source);
        }

        [Test]
        public void MULTI_DIRECT_003C_TownStudentLifeProgressIsInstalledForEveryScenePlayer()
        {
            const string sourcePath = "Assets/Scripts/Game/Bootstrap/TownStudentLifeRuntimeInstaller.cs";

            Assert.IsTrue(File.Exists(sourcePath), "Every connected Player needs StudentLifeProgressComponent so direct day-end interaction can request shared world time.");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("CollectPlayerRoots", source);
            StringAssert.Contains("foreach (var candidate in players)", source);
            StringAssert.Contains("EnsureProgress(candidate)", source);
            StringAssert.Contains("RestoreProgress(candidate, scene)", source);
        }
    }
}
