using NUnit.Framework;
using Rootborn.Network.Steam;

namespace Rootborn.Tests.EditMode
{
    /// <summary>STEAM-001: Steam starts only for players; servers, batch runs and -nosteam keep it off.</summary>
    public sealed class SteamPlatformTests
    {
        [TestCase(false, "rootborn.exe", false)]
        [TestCase(true, "rootborn.exe", true)]
        [TestCase(false, "rootborn.exe -nosteam", true)]
        [TestCase(false, "rootborn.exe -mode server -port 7777", true)]
        [TestCase(false, "rootborn.exe -mode host", false)]
        [TestCase(false, "rootborn.exe -mode", false)]
        public void STEAM_001_ShouldSkip(bool batchMode, string commandLine, bool expected)
        {
            Assert.AreEqual(expected, SteamPlatform.ShouldSkip(commandLine.Split(' '), batchMode));
        }

        [Test]
        public void STEAM_002_DevelopmentAppIdIsValvesTestApp()
        {
            Assert.AreEqual(480u, SteamPlatform.DevelopmentAppId);
        }
    }
}
