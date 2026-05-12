using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.StudentLife;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class StudentTutorialStoryStageTests
    {
        [Test]
        public void STUDENT_TUTORIAL_001_ActivityCompletionDoesNotConstructStageFromActivityId()
        {
            var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            activity.ConfigureForTests(
                "activity.study-basics",
                "activity.study-basics",
                LifeActivityCategory.School,
                0,
                0,
                0,
                0,
                null,
                null);
            var progress = new StudentLifeProgress("slot-stage", "player-stage", 10, 10);
            var runner = new LifeActivityRunner();

            Assert.IsTrue(runner.TryPerform(activity, progress, "request-study", out _));

            Assert.AreEqual(string.Empty, progress.TutorialStageId, "Activity completion must not derive tutorial stages from activity IDs in code.");
            Assert.AreEqual(string.Empty, progress.ToSaveData().TutorialStageId);
        }

        [Test]
        public void STUDENT_TUTORIAL_002_DayEndStageRuleAdvancesFromDataAndIsIdempotent()
        {
            Type stageType = FindType("Rootborn.Game.StudentLife.TutorialStageDefinition");
            Type ruleType = FindType("Rootborn.Game.StudentLife.TutorialStageAdvanceDayEndRule");
            Assert.IsNotNull(stageType, "Tutorial stages must be ScriptableObject data.");
            Assert.IsNotNull(ruleType, "Stage advancement must be a generic day-end rule strategy.");

            var dayOne = (ScriptableObject)ScriptableObject.CreateInstance(stageType);
            var dayTwo = (ScriptableObject)ScriptableObject.CreateInstance(stageType);
            InvokeConfigureStageForTests(dayOne, "tutorial.day1", "tutorial.day1", "Start the first day.");
            InvokeConfigureStageForTests(dayTwo, "tutorial.day2", "tutorial.day2", "Meet the guide again.");

            var rule = (DayEndRuleBase)ScriptableObject.CreateInstance(ruleType);
            InvokeConfigureRuleForTests(rule, "tutorial.day1", dayTwo, "goal.talk-to-guide-day2");

            var progress = new StudentLifeProgress("slot-stage", "player-stage", 10, 10);
            InvokeProgressMethod(progress, "SetTutorialStageForTests", new object[] { "tutorial.day1" }, new[] { typeof(string) });

            rule.Apply(progress);
            rule.Apply(progress);

            Assert.AreEqual("tutorial.day2", progress.TutorialStageId);
            Assert.AreEqual("goal.talk-to-guide-day2", ReadStringProperty(progress, "NextObjectiveId"));
            Assert.AreEqual("Meet the guide again.", ReadStringProperty(progress, "NextGuideText"));
        }

        [Test]
        public void STUDENT_TUTORIAL_003_StudentLifeCoreDoesNotContainStageIdConstructionBranching()
        {
            string source = File.ReadAllText("Assets/Scripts/Game/StudentLife/StudentLifeCore.cs");

            StringAssert.DoesNotContain("BuildTutorialStageId", source);
            StringAssert.DoesNotContain("\"tutorial.\" +", source);
            StringAssert.DoesNotContain("activityId + \".completed\"", source);
        }

        [Test]
        public void STUDENT_TUTORIAL_004_ResultPanelShowsNextObjectiveAndGuideFromSummary()
        {
            var canvasObject = new GameObject("ResultCanvas", typeof(RectTransform), typeof(Canvas));
            var panelObject = new GameObject("ResultPanel");
            try
            {
                panelObject.transform.SetParent(canvasObject.transform, false);
                var panel = panelObject.AddComponent<StudentDayResultPanel>();
                var summary = new StudentDaySummary(
                    1,
                    new[] { "activity.study-basics" },
                    new[] { "activity.study-basics:+skill.basic-study=1" },
                    "home-entry",
                    "tutorial.day2",
                    "goal.talk-to-guide-day2",
                    "Meet the guide again.");

                panel.Show(null, summary);

                string rendered = CollectText(panel.transform);
                StringAssert.Contains("tutorial.day2", rendered);
                StringAssert.Contains("goal.talk-to-guide-day2", rendered);
                StringAssert.Contains("Meet the guide again.", rendered);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(panelObject);
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        private static string CollectText(Transform root)
        {
            var texts = root.GetComponentsInChildren<Text>(true);
            string result = string.Empty;
            for (int i = 0; i < texts.Length; i++)
            {
                result += "\n" + texts[i].text;
            }

            return result;
        }

        private static Type FindType(string fullName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static void InvokeConfigureStageForTests(ScriptableObject target, string id, string displayNameKey, string guideText)
        {
            MethodInfo method = target.GetType().GetMethod("ConfigureForTests", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(string), typeof(string), typeof(string) }, null);
            Assert.IsNotNull(method, target.GetType().Name + " must expose ConfigureForTests for data-driven tests.");
            method.Invoke(target, new object[] { id, displayNameKey, guideText });
        }

        private static void InvokeConfigureRuleForTests(DayEndRuleBase target, string fromStageId, ScriptableObject targetStage, string nextObjectiveId)
        {
            MethodInfo method = target.GetType().GetMethod("ConfigureForTests", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(string), targetStage.GetType(), typeof(string) }, null);
            Assert.IsNotNull(method, target.GetType().Name + " must expose ConfigureForTests for data-driven tests.");
            method.Invoke(target, new object[] { fromStageId, targetStage, nextObjectiveId });
        }

        private static void InvokeProgressMethod(StudentLifeProgress progress, string methodName, object[] args, Type[] parameterTypes)
        {
            MethodInfo method = typeof(StudentLifeProgress).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, null, parameterTypes, null);
            Assert.IsNotNull(method, "StudentLifeProgress must expose " + methodName + " for test-controlled setup only.");
            method.Invoke(progress, args);
        }

        private static string ReadStringProperty(StudentLifeProgress progress, string propertyName)
        {
            PropertyInfo property = typeof(StudentLifeProgress).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property, "StudentLifeProgress must expose " + propertyName + ".");
            return (string)property.GetValue(progress);
        }
    }
}
