using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.Quests
{
    public sealed class NpcQuestSaveSlotIsolationTests
    {
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-npc-quest-slot-isolation", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            ActiveSaveContext.Clear();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            SaveService.SetRootDirectoryForTests(null);
            SaveSlotSelectPanel.SetSaveRootForTests(null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NPC_QUEST_006_SaveSlotAQuestAcceptanceDoesNotBleedIntoSaveSlotB()
        {
            var slotA = CreateMetadata("slot-0", 2026050811, 2026050812);
            var slotB = CreateMetadata("slot-1", 2026050813, 2026050814);

            ActiveSaveContext.Set(slotA);
            yield return LoadScene("Farm");
            yield return WaitForQuestRuntime(10f);

            var questLogPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
            var dialoguePanel = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
            var npc = Object.FindFirstObjectByType<NpcInteractor>(FindObjectsInactive.Include);
            var quest = npc.GetComponent<QuestProvider>().Quests[0];
            Assert.AreEqual(QuestState.NotStarted, questLogPanel.QuestLog.GetState(quest));

            npc.Interact();
            yield return null;
            Assert.IsTrue(dialoguePanel.Choose(0));
            yield return null;
            Assert.AreEqual(QuestState.Active, questLogPanel.QuestLog.GetState(quest));
            Assert.IsTrue(File.Exists(Path.Combine(_saveRoot, slotA.SlotId, "quest-log.json")));
            Assert.IsFalse(File.Exists(Path.Combine(_saveRoot, slotB.SlotId, "quest-log.json")));

            ActiveSaveContext.Set(slotB);
            yield return LoadScene("Farm");
            yield return WaitForQuestRuntime(10f);

            questLogPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
            npc = Object.FindFirstObjectByType<NpcInteractor>(FindObjectsInactive.Include);
            quest = npc.GetComponent<QuestProvider>().Quests[0];
            Assert.AreEqual(QuestState.NotStarted, questLogPanel.QuestLog.GetState(quest));

            ActiveSaveContext.Set(slotA);
            yield return LoadScene("Farm");
            yield return WaitForQuestRuntime(10f);

            questLogPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
            npc = Object.FindFirstObjectByType<NpcInteractor>(FindObjectsInactive.Include);
            quest = npc.GetComponent<QuestProvider>().Quests[0];
            Assert.AreEqual(QuestState.Active, questLogPanel.QuestLog.GetState(quest));
        }

        private SaveSlotMetadata CreateMetadata(string slotId, int worldSeed, int tileSeed)
        {
            var service = new SaveService(slotId, _saveRoot);
            var metadata = service.CreateUiMetadata(slotId, new CharacterCustomization(), worldSeed, tileSeed);
            service.SaveMetadata(metadata);
            return metadata;
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, sceneName + " should be present in BuildSettings.");
            while (!op.isDone) yield return null;
        }

        private static IEnumerator WaitForQuestRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var panel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
                var dialogue = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
                var npc = Object.FindFirstObjectByType<NpcInteractor>(FindObjectsInactive.Include);
                if (panel != null && panel.QuestLog != null && dialogue != null && npc != null && npc.GetComponent<QuestProvider>() != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("Quest runtime did not expose NPC, dialogue, and quest log within timeout.");
        }
    }
}
