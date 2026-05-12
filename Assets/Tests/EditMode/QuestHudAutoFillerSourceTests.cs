using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode
{
    public sealed class QuestHudAutoFillerSourceTests
    {
        private const string SourcePath = "Assets/Scripts/UI/Quests/QuestHudAutoFiller.cs";

        [Test]
        public void EnsureQuestUi_BindsRegistryQuestsAndRewardContextIntoQuestLogPanel()
        {
            string source = File.ReadAllText(SourcePath);

            StringAssert.Contains("questPanel.Bind(questLog, registry != null ? registry.Quests : null", source);
            StringAssert.Contains("new RewardRuntimeContext(questLog", source);
        }
    }
}
