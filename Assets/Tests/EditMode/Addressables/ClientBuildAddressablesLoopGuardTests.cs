using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Addressables
{
    public sealed class ClientBuildAddressablesLoopGuardTests
    {
        // A destination missing from the player cannot be loaded: a new game could not even start.
        [NUnit.Framework.Test]
        public void ClientBuild_ContainsEveryTravelDestinationAndBootsFirst()
        {
            var scenes = Rootborn.Editor.BuildScripts.BuildScript.ClientScenePaths();
            NUnit.Framework.Assert.That(scenes[0], NUnit.Framework.Is.EqualTo("Assets/Scenes/Boot.unity"));
            NUnit.Framework.CollectionAssert.Contains(scenes, "Assets/Scenes/MainMenu.unity");
            var missing = new System.Collections.Generic.List<string>();
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:WorldDestinationDefinition"))
            {
                var destination = UnityEditor.AssetDatabase.LoadAssetAtPath<Rootborn.Game.World.WorldDestinationDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (System.Array.IndexOf(scenes, "Assets/Scenes/" + destination.SceneName + ".unity") < 0) missing.Add(destination.SceneName);
            }
            NUnit.Framework.Assert.That(missing, NUnit.Framework.Is.Empty, string.Join(", ", missing));
            foreach (string scene in scenes) NUnit.Framework.Assert.That(System.IO.File.Exists(scene), NUnit.Framework.Is.True, scene);
        }

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
            StringAssert.Contains("Path.Combine(streamingAssetsAaPath, \"settings.json\")", source, "Client and server builds must verify copied StreamingAssets/aa settings before reporting success.");
            StringAssert.Contains("Directory.Delete(streamingAssetsAaPath, true)", source, "Builds must remove stale player Addressables runtime data before copying fresh output.");
            StringAssert.Contains("CopyDirectory(addressablesRuntimePath, streamingAssetsAaPath)", source, "Builds must recursively copy all generated Addressables runtime files.");
        }

        [Test]
        public void SERVER_BUILD_ADDRESSABLES_EDIT_004_ServerBuildDisablesAddressablesPlayerHook()
        {
            string source = File.ReadAllText(BuildScriptPath);
            string buildServerBody = GetBuildServerBody(source);

            StringAssert.Contains("BuildPlayerWithAddressablesHookDisabled", buildServerBody, "Dedicated server builds must disable the Addressables player-build hook so server artifact generation cannot enter Addressables content build loops.");
            StringAssert.DoesNotContain("BuildPipeline.BuildPlayer(opts)", buildServerBody, "Dedicated server builds must not call BuildPipeline.BuildPlayer directly while Addressables player-build hooks may be enabled.");
        }

        [Test]
        public void SERVER_BUILD_ADDRESSABLES_EDIT_005_ServerBuildCopiesExistingClientAddressablesRuntimeData()
        {
            string source = File.ReadAllText(BuildScriptPath);
            string buildServerBody = GetBuildServerBody(source);

            StringAssert.DoesNotContain("BuildAddressables();", buildServerBody, "Dedicated server builds must not rebuild Addressables content; this previously stalled in Generate Bundle Maps.");
            StringAssert.Contains("CopyExistingClientAddressablesRuntimeDataToPlayer(outputPath)", buildServerBody, "Dedicated server builds must copy existing client Addressables runtime data into rootborn-server_Data/StreamingAssets/aa before smoke tests launch.");
            StringAssert.Contains("Builds/Client/Windows/rootborn_Data/StreamingAssets/aa", source, "Server builds must use the current client Addressables runtime data as the source for dedicated smoke artifacts.");
        }

        private static string GetBuildServerBody(string source)
        {
            int buildServerIndex = source.IndexOf("private static void BuildServer", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(buildServerIndex, 0, "BuildScript must keep a BuildServer implementation for dedicated server builds.");

            int ensureDirIndex = source.IndexOf("private static void EnsureDir", buildServerIndex, System.StringComparison.Ordinal);
            Assert.Greater(ensureDirIndex, buildServerIndex, "BuildServer body boundary should be discoverable before EnsureDir.");

            return source.Substring(buildServerIndex, ensureDirIndex - buildServerIndex);
        }
    }
}
