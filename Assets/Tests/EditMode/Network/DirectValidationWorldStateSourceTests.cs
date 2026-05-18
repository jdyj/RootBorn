using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Network
{
    public sealed class DirectValidationWorldStateSourceTests
    {
        [Test]
        public void MULTI_WORLDSTATE_010_BootstrapAppliesCommandLineSaveSlotToActiveSaveContext()
        {
            const string sourcePath = "Assets/Scripts/Game/Bootstrap/GameBootstrap.cs";

            Assert.IsTrue(File.Exists(sourcePath), "Executable multiplayer validation needs command-line save slots to reach runtime save-bound systems.");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("ActiveSaveContext.Set", source);
            StringAssert.Contains("Config.SaveSlot", source);
        }

        [Test]
        public void MULTI_WORLDSTATE_010_AutoplayCanClaimWorldStateQuestThroughInputPath()
        {
            const string sourcePath = "Assets/Scripts/Game/Common/DirectValidationAutoplayInstaller.cs";

            Assert.IsTrue(File.Exists(sourcePath), "Direct validation autoplay must exist for executable world-state validation.");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("-directValidationAutoplayWorldStateClaim", source);
            StringAssert.Contains("RunWorldStateClaimPath", source);
            StringAssert.Contains("GuideNpc", source);
            StringAssert.Contains("QuestResource_00", source);
            StringAssert.Contains("ChoiceButtons", source);
            StringAssert.Contains("ExecuteEvents.pointerClickHandler", source);
            StringAssert.Contains("WorldStateSnapshot", source);
            Assert.IsFalse(source.Contains("TryActivate("), "World-state direct validation must activate flags through quest reward input, not by mutating progress directly.");
        }

        [Test]
        public void MULTI_WORLDSTATE_010_LocationNpcInstallerSkipsInvalidSceneRootTraversal()
        {
            const string sourcePath = "Assets/Scripts/UI/StudentLife/LocationNpcRuntimeInstaller.cs";

            Assert.IsTrue(File.Exists(sourcePath), "Network scene transitions must not spam invalid-scene exceptions from location NPC runtime wiring.");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("scene.IsValid()", source);
            StringAssert.Contains("GetSafeScene()", source);
        }
        [Test]
        public void MULTI_WORLDSTATE_013_AutoplayCanObserveSharedWorldStateWithoutQuestMutation()
        {
            const string sourcePath = "Assets/Scripts/Game/Common/DirectValidationAutoplayInstaller.cs";

            Assert.IsTrue(File.Exists(sourcePath), "Direct validation autoplay must exist for executable world-state observation.");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("-directValidationAutoplayWorldStateObserve", source);
            StringAssert.Contains("RunWorldStateObservePath", source);
            StringAssert.Contains("WorldStateLogButton", source);
            StringAssert.Contains("WorldStateChange_", source);
            StringAssert.Contains("autoplay world-state snapshot player=", source);
            Assert.IsFalse(source.Contains("QuestLog.Accept"), "Autoplay must not accept quests through direct QuestLog APIs.");
            Assert.IsFalse(source.Contains("Inventory.Add"), "Autoplay must not gain items through direct inventory mutation.");
        }
    }
}
