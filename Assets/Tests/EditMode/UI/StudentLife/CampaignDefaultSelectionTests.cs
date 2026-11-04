using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using Rootborn.UI.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.UI.StudentLife
{
    public sealed class CampaignDefaultSelectionTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void RuntimeCampaignSelection_UsesExplicitDefault_AndPreservesLegacyFallback(bool hasDefault)
        {
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            var old = ScriptableObject.CreateInstance<CampaignDefinition>();
            var opening = ScriptableObject.CreateInstance<CampaignDefinition>();
            try
            {
                typeof(GameDataRegistry).GetField("_campaigns", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(registry, new[] { old, opening });
                var field = typeof(GameDataRegistry).GetField("_defaultStudentCampaign", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(field, Is.Not.Null, "Registry needs an explicit default campaign independent of array ordering.");
                field.SetValue(registry, hasDefault ? opening : null);
                GameDataRegistryProvider.SetOverrideForTests(registry);
                var resolve = typeof(CampaignRuntimeInstaller).GetMethod("ResolveCampaign", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(resolve, Is.Not.Null);
                Assert.That(resolve.Invoke(null, null), Is.SameAs(hasDefault ? opening : old));
            }
            finally
            {
                GameDataRegistryProvider.ClearOverrideForTests(); GameDataRegistryProvider.ResetCacheForTests();
                Object.DestroyImmediate(registry); Object.DestroyImmediate(old); Object.DestroyImmediate(opening);
            }
        }
    }
}
