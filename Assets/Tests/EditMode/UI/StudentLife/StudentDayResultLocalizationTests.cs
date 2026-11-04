using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Localization;
using Rootborn.Game.StudentLife;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.UI;
namespace Rootborn.Tests.EditMode.UI.StudentLife
{
    public sealed class StudentDayResultLocalizationTests
    {
        [Test] public void DaySummary_LocalizesCatalogKeysAndRegistryActivityNames()
        {
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            var catalog = ScriptableObject.CreateInstance<GameTextCatalog>();
            var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            var go = new GameObject("ResultLocalization");
            string previousLanguage = GameText.Language;
            try
            {
                catalog.ConfigureJson("{\"entries\":[{\"key\":\"test.activity.name\",\"ko\":\"Localized activity\",\"en\":\"Localized activity\"},{\"key\":\"test.route\",\"ko\":\"Localized route\",\"en\":\"Localized route\"}]}");
                Set(activity,"_id","test.activity.id"); Set(activity,"_displayNameKey","test.activity.name");
                Set(registry,"_textCatalog",catalog); Set(registry,"_lifeActivities",new[] { activity });
                GameDataRegistryProvider.SetOverrideForTests(registry); GameText.Language = "en";
                var panel = go.AddComponent<StudentDayResultPanel>();
                panel.Show(null,new StudentDaySummary(1,new[] { activity.Id },new string[0],string.Empty));
                panel.AppendDailyPlanSummary(new DailyPlanDaySummary("test.route","test.activity.name",true,null,null));
                Assert.That(panel.ResultsTextForTests,Does.Contain("Localized route"));
                var activities = System.Array.Find(panel.GetComponentsInChildren<Text>(true),t => t.name == "Activities");
                Assert.That(activities.text,Does.Contain("Localized activity"));
                Assert.That(activities.text,Does.Not.Contain(activity.Id));
            }
            finally
            {
                GameText.Language = previousLanguage; GameDataRegistryProvider.ClearOverrideForTests(); GameDataRegistryProvider.ResetCacheForTests();
                Object.DestroyImmediate(go); Object.DestroyImmediate(activity); Object.DestroyImmediate(catalog); Object.DestroyImmediate(registry);
            }
        }
        private static void Set(object target,string field,object value) => target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    }
}
