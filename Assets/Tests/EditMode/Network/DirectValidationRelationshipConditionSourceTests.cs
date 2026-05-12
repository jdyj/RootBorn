using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Network
{
    public sealed class DirectValidationRelationshipConditionSourceTests
    {
        [Test]
        public void MULTI_DIRECT_RelationshipConditionRuntimeInstallerRetriesUntilNetworkTownObjectsAreReady()
        {
            const string sourcePath = "Assets/Scripts/Game/Bootstrap/TownRelationshipConditionRuntimeInstaller.cs";

            Assert.IsTrue(File.Exists(sourcePath), "Town relationship/condition installer must exist for direct multiplayer validation.");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("elapsed < 60f", source);
            StringAssert.Contains("relationship condition effects attached", source);
            StringAssert.Contains("RelationshipDeltaEffect", source);
            StringAssert.Contains("StatusDeltaEffect", source);
        }

        [Test]
        public void MULTI_DIRECT_RelationshipConditionRuntimeInstallerLogsAttachReadinessDiagnostics()
        {
            const string sourcePath = "Assets/Scripts/Game/Bootstrap/TownRelationshipConditionRuntimeInstaller.cs";

            Assert.IsTrue(File.Exists(sourcePath), "Town relationship/condition installer diagnostics must exist for direct multiplayer validation.");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("relationship condition installer started", source);
            StringAssert.Contains("relationship condition attach waiting", source);
            StringAssert.Contains("registryRelationships=", source);
            StringAssert.Contains("registryStatuses=", source);
            StringAssert.Contains("interactors=", source);
        }

        [Test]
        public void MULTI_DIRECT_RelationshipConditionRuntimeInstallerCollectsActivityInteractorsAcrossLoadedScenes()
        {
            const string sourcePath = "Assets/Scripts/Game/Bootstrap/TownRelationshipConditionRuntimeInstaller.cs";

            Assert.IsTrue(File.Exists(sourcePath), "Town relationship/condition installer must search all loaded scenes used by direct multiplayer validation.");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("CollectActivityInteractorsAcrossLoadedScenes", source);
            StringAssert.Contains("SceneManager.sceneCount", source);
            StringAssert.Contains("SceneManager.GetSceneAt", source);
        }

        [Test]
        public void MULTI_DIRECT_TownStudentLifeInstallerRebindsActivitiesAfterRegistryIsReady()
        {
            const string sourcePath = "Assets/Scripts/Game/Bootstrap/TownStudentLifeRuntimeInstaller.cs";

            Assert.IsTrue(File.Exists(sourcePath), "Town student life installer must rebind activities after data registry readiness.");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("elapsed < 60f", source);
            StringAssert.Contains("relationship condition activity objects rebound", source);
            StringAssert.Contains("StudyBasicsHasRelationshipConditionEffects", source);
            StringAssert.Contains("EnsureActivityObjects(scene)", source);
        }
    }
}
