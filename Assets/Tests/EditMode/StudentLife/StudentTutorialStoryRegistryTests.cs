using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.StudentLife;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class StudentTutorialStoryRegistryTests
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";
        private const string FirstGuidePath = "Assets/Data/NPCs/Npc_FirstGuide.asset";

        [Test]
        public void STUDENT_TUTORIAL_REGISTRY_001_DefaultTutorialStageAndAdvanceRuleAreRegisteredDataAssets()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            Assert.IsNotNull(registry, RegistryPath + " must exist.");

            var defaultStage = ReadProperty<TutorialStageDefinition>(registry, "DefaultTutorialStage");
            var stages = ReadProperty<TutorialStageDefinition[]>(registry, "TutorialStages");
            Assert.IsNotNull(defaultStage, "Registry must expose the first-day tutorial stage as data.");
            Assert.IsNotNull(stages, "Registry must expose all tutorial stages as data.");
            CollectionAssert.Contains(stages, defaultStage);
            Assert.AreEqual("tutorial.day1", defaultStage.Id);
            StringAssert.Contains("1", defaultStage.GuideText);
            StringAssert.StartsWith("Assets/Data/StudentLife/", AssetDatabase.GetAssetPath(defaultStage));

            var dayEndRule = registry.DefaultDayEndRule;
            Assert.IsNotNull(dayEndRule);
            bool hasStageAdvance = false;
            for (int i = 0; i < dayEndRule.Rules.Count; i++)
            {
                if (dayEndRule.Rules[i] is TutorialStageAdvanceDayEndRule)
                {
                    hasStageAdvance = true;
                    StringAssert.StartsWith("Assets/Data/StudentLife/", AssetDatabase.GetAssetPath(dayEndRule.Rules[i]));
                }
            }

            Assert.IsTrue(hasStageAdvance, "Default day-end rule must include a data asset that advances the tutorial/story stage.");
        }

        [Test]
        public void STUDENT_TUTORIAL_REGISTRY_002_NewProgressAppliesDefaultTutorialStageFromRegistry()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            var player = new GameObject("Player");
            try
            {
                var component = player.AddComponent<StudentLifeProgressComponent>();
                component.ConfigureForTests("slot-stage", "player-stage", 10, 10, 0, 8 * 60);

                MethodInfo method = typeof(StudentLifeProgressComponent).GetMethod("ApplyDefaultTutorialStage", BindingFlags.Instance | BindingFlags.Public);
                Assert.IsNotNull(method, "StudentLifeProgressComponent must apply registry default tutorial stage without hardcoded IDs.");
                method.Invoke(component, new object[] { registry });

                Assert.AreEqual("tutorial.day1", component.Progress.TutorialStageId);
                StringAssert.Contains("Day 1", component.Progress.NextGuideText);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void STUDENT_TUTORIAL_REGISTRY_003_FirstGuideUsesRegisteredDayTwoDialogueFromStageData()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            Assert.IsNotNull(registry, RegistryPath + " must exist.");

            var dayTwoStage = registry.TutorialStages.FirstOrDefault(stage => stage != null && stage.Id == "tutorial.day2");
            Assert.IsNotNull(dayTwoStage, "Day two tutorial stage must be registered as a data asset.");

            var firstGuide = AssetDatabase.LoadAssetAtPath<NpcDefinition>(FirstGuidePath);
            Assert.IsNotNull(firstGuide, FirstGuidePath + " must exist.");

            var dayTwoDialogue = firstGuide.StageDialogues.FirstOrDefault(dialogue => dialogue != null && dialogue.RequiredTutorialStage == dayTwoStage);
            Assert.IsNotNull(dayTwoDialogue, "First guide must resolve a day-two dialogue from stage data, not code branching.");
            CollectionAssert.Contains(registry.Dialogues, dayTwoDialogue, "Day-two dialogue must be registered in GameDataRegistry.");
            CollectionAssert.Contains(dayTwoDialogue.LineKeys, "dialogue.guide.day2");
            StringAssert.StartsWith("Assets/Data/Dialogue/", AssetDatabase.GetAssetPath(dayTwoDialogue));
        }

        private static T ReadProperty<T>(object target, string propertyName) where T : class
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property, "Missing public registry property " + propertyName + ".");
            return property.GetValue(target) as T;
        }
    }
}
