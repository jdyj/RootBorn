using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Network
{
    public sealed class NetworkWorldTimeHudSourceTests
    {
        [Test]
        public void MULTI_TIME_005_TownRuntimeInstallsVisibleNetworkTimeHud()
        {
            const string installerPath = "Assets/Scripts/UI/HUD/TownTimeHudRuntimeInstaller.cs";

            Assert.IsTrue(File.Exists(installerPath), "Town must install a visible TimeHud at runtime for direct multiplayer UI verification.");
            string source = File.ReadAllText(installerPath);

            StringAssert.Contains("RuntimeInitializeOnLoadMethod", source);
            StringAssert.Contains("Town", source);
            StringAssert.Contains("TimeHud", source);
            StringAssert.Contains("Day", source);
            StringAssert.Contains("[TownTimeCanvas]", source);
            StringAssert.Contains("CanvasScaler.ScaleMode.ScaleWithScreenSize", source);
        }
    }
}
