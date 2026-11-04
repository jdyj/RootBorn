using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Localization;
using Rootborn.UI.StudentLife;
using UnityEngine;
namespace Rootborn.Tests.EditMode.UI.StudentLife
{
    public sealed class StudentDayEndPromptLocalizationTests
    {
        [Test] public void BedPrompt_UsesCatalogLabel()
        {
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            var catalog = ScriptableObject.CreateInstance<GameTextCatalog>(); var go = new GameObject("Bed");
            string old = GameText.Language;
            try
            {
                catalog.ConfigureJson("{\"entries\":[{\"key\":\"day-result.end-day\",\"ko\":\"Localized bedtime\",\"en\":\"Localized bedtime\"}]}");
                typeof(GameDataRegistry).GetField("_textCatalog",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(registry,catalog);
                GameDataRegistryProvider.SetOverrideForTests(registry); GameText.Language="en";
                Assert.That(go.AddComponent<StudentDayEndInteractor>().InteractionPrompt,Is.EqualTo("[E] Localized bedtime"));
            }
            finally { GameText.Language=old; GameDataRegistryProvider.ClearOverrideForTests(); GameDataRegistryProvider.ResetCacheForTests(); Object.DestroyImmediate(go); Object.DestroyImmediate(catalog); Object.DestroyImmediate(registry); }
        }
    }
}
