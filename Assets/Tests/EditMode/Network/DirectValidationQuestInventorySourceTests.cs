using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Network
{
    public sealed class DirectValidationQuestInventorySourceTests
    {
        [Test]
        public void MULTI_DIRECT_006A_QuestHudBindsToLocalOwnedNetworkPlayer()
        {
            const string sourcePath = "Assets/Scripts/UI/Quests/QuestHudAutoFiller.cs";

            Assert.IsTrue(File.Exists(sourcePath), "Quest HUD autofill must exist for direct quest/inventory validation.");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("FindLocalInputPlayerInScene", source);
            StringAssert.Contains("PlayerInteractionRouter", source);
            StringAssert.Contains("controller.enabled", source);
            StringAssert.Contains("router.enabled", source);
        }

        [Test]
        public void MULTI_DIRECT_006B_QuestPlayflowBinderUsesLocalOwnedNetworkPlayerAndLogsPlayerScopedChanges()
        {
            const string sourcePath = "Assets/Scripts/UI/Quests/QuestPlayflowRuntimeBinder.cs";

            Assert.IsTrue(File.Exists(sourcePath), "Quest playflow runtime binder must exist for quest/inventory direct validation.");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("FindLocalInputPlayerInScene", source);
            StringAssert.Contains("PlayerInteractionRouter", source);
            StringAssert.Contains("[ROOTBORN] Quest state changed player=", source);
            StringAssert.Contains("[ROOTBORN] Inventory changed player=", source);
            StringAssert.Contains("InventorySummary", source);
        }

        [Test]
        public void MULTI_DIRECT_006A_DialoguePanelSupportsKeyboardChoiceForDirectInputPath()
        {
            const string sourcePath = "Assets/Scripts/UI/Quests/DialoguePanel.cs";

            Assert.IsTrue(File.Exists(sourcePath), "Dialogue panel must support a keyboard selection path for direct executable validation.");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("Keyboard.current", source);
            StringAssert.Contains("ChooseFirstAvailableChoiceFromKeyboard", source);
            StringAssert.Contains("Choose(choiceIndex)", source);
        }

        [Test]
        public void MULTI_DIRECT_006A_AutoplayCanDriveQuestAcceptAndResourceGatherWithoutDirectStateMutation()
        {
            const string sourcePath = "Assets/Scripts/Game/Common/DirectValidationAutoplayInstaller.cs";

            Assert.IsTrue(File.Exists(sourcePath), "Direct validation autoplay must support quest/inventory executable validation.");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("-directValidationAutoplayQuestInventory", source);
            StringAssert.Contains("GuideNpc", source);
            StringAssert.Contains("QuestResource_00", source);
            StringAssert.Contains("InventorySnapshot(identity) == \"inventory=empty\"", source);
            Assert.IsFalse(source.Contains("QuestLog.Accept"), "Autoplay must accept quests through dialogue input, not direct QuestLog APIs.");
            Assert.IsFalse(source.Contains("Inventory.Add"), "Autoplay must gain items through resource interaction, not direct inventory mutation.");
        }

        [Test]
        public void MULTI_DIRECT_006A_AutoplayCreatesKeyboardDeviceForBatchmodeInputPath()
        {
            const string sourcePath = "Assets/Scripts/Game/Common/DirectValidationAutoplayInstaller.cs";

            Assert.IsTrue(File.Exists(sourcePath), "Direct validation autoplay must support executable batchmode validation.");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("EnsureKeyboardDevice", source);
            StringAssert.Contains("InputSystem.AddDevice<Keyboard>()", source);
        }

        [Test]
        public void MULTI_DIRECT_006B_AutoplayLogsReadOnlyInventorySnapshotAfterQuestInventoryPath()
        {
            const string sourcePath = "Assets/Scripts/Game/Common/DirectValidationAutoplayInstaller.cs";

            Assert.IsTrue(File.Exists(sourcePath), "Direct validation autoplay must expose read-only inventory evidence.");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("InventorySnapshot", source);
            StringAssert.Contains("autoplay inventory snapshot player=", source);
            StringAssert.Contains("ToSaveData()", source);
        }
    }
}
