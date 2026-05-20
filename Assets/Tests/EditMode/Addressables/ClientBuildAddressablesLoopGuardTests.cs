using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Addressables
{
    public sealed class ClientBuildAddressablesLoopGuardTests
    {
        private const string BuildScriptPath = "Assets/Scripts/Editor/BuildScripts/BuildScript.cs";

        [Test]
        public void CLIENT_BUILD_ADDRESSABLES_EDIT_001_ClientBuildUsesSingleManualContentBuildAndDisablesPlayerHook()
        {
            string source = File.ReadAllText(BuildScriptPath);
            int buildClientIndex = source.IndexOf("private static void BuildClient", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(buildClientIndex, 0, "BuildScript must keep a BuildClient implementation for client builds.");

            int buildAddressablesIndex = source.IndexOf("private static string BuildAddressables", buildClientIndex, System.StringComparison.Ordinal);
            Assert.Greater(buildAddressablesIndex, buildClientIndex, "BuildClient body boundary should be discoverable before BuildAddressables.");

            string buildClientBody = source.Substring(buildClientIndex, buildAddressablesIndex - buildClientIndex);

            StringAssert.Contains("BuildAddressables();", buildClientBody, "Client builds must explicitly build Addressables content once before player build.");
            StringAssert.Contains("BuildPlayerWithAddressablesHookDisabled", buildClientBody, "Client builds must disable the Addressables player-build hook around BuildPipeline.BuildPlayer.");
            StringAssert.Contains("CopyAddressablesRuntimeDataToPlayer", buildClientBody, "Client builds must copy generated Addressables runtime data into the built player's StreamingAssets/aa folder.");
        }

        [Test]
        public void CLIENT_BUILD_ADDRESSABLES_EDIT_002_PlayerBuildHookIsRestoredAfterClientBuild()
        {
            string source = File.ReadAllText(BuildScriptPath);

            StringAssert.Contains("AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer", source);
            StringAssert.Contains("BuildPipeline.BuildPlayer(opts)", source);
            StringAssert.Contains("finally", source, "BuildScript must restore Addressables player-build settings even if BuildPipeline fails.");
            StringAssert.Contains("settings.BuildAddressablesWithPlayerBuild = previousOption", source, "BuildScript must restore the previous Addressables player-build option after the client build.");
        }

        [Test]
        public void CLIENT_BUILD_ADDRESSABLES_EDIT_003_AddressablesRuntimeDataCopyRequiresSettingsJson()
        {
            string source = File.ReadAllText(BuildScriptPath);

            StringAssert.Contains("Path.Combine(runtimePath, \"settings.json\")", source, "Addressables content build must verify generated runtime settings before player build.");
            StringAssert.Contains("Path.Combine(streamingAssetsAaPath, \"settings.json\")", source, "Client build must verify copied StreamingAssets/aa settings before reporting success.");
            StringAssert.Contains("Directory.Delete(streamingAssetsAaPath, true)", source, "Client build must remove stale player Addressables runtime data before copying fresh output.");
            StringAssert.Contains("CopyDirectory(addressablesRuntimePath, streamingAssetsAaPath)", source, "Client build must recursively copy all generated Addressables runtime files.");
        }
    }
}
