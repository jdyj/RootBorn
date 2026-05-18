using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.WorldState
{
    public sealed class WorldStateRuntimeWiringTests
    {
        [Test]
        public void WORLD_STATE_EDIT_006_DialogueQuestRewardContextLoadsAndSavesWorldStateProgress()
        {
            const string path = "Assets/Scripts/UI/Quests/QuestHudAutoFiller.cs";
            Assert.IsTrue(File.Exists(path), path + " must exist.");
            string source = File.ReadAllText(path);

            StringAssert.Contains("WorldStateProgressPersistence.LoadOrCreate", source, "WORLD-STATE-EDIT-006 failed: dialogue quest UI must load world-state progress for reward context.");
            StringAssert.Contains("WorldStateProgressPersistence.Save", source, "WORLD-STATE-EDIT-006 failed: dialogue quest UI must save world-state progress after successful reward choices.");
            StringAssert.Contains("worldStateProgress", source, "WORLD-STATE-EDIT-006 failed: dialogue reward context must carry WorldStateProgress.");
        }
    }
}
