using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace Rootborn.Tests.EditMode.TownConcept
{
    public sealed class TownConceptDomainMappingTests
    {
        [Test]
        public void DomainMapping_DocumentsTownReplacementForFarmLoop()
        {
            string mapping = File.ReadAllText("docs/art/town-concept-domain-mapping.md");

            StringAssert.Contains("City resident", mapping);
            StringAssert.Contains("Town scene", mapping);
            StringAssert.Contains("Activity progress", mapping);
            StringAssert.Contains("Neighbor request", mapping);
            StringAssert.DoesNotContain("Use farming as first loop", mapping);
        }

        [Test]
        public void TownSceneAsset_Exists()
        {
            Assert.IsTrue(File.Exists("Assets/Scenes/Town.unity"), "Town scene must exist before default flow migration.");
        }

        [Test]
        public void BuildSettings_PrioritizeTownBeforeLegacyFarm()
        {
            var scenes = EditorBuildSettings.scenes;
            int townIndex = FindSceneIndex(scenes, "Assets/Scenes/Town.unity");
            int farmIndex = FindSceneIndex(scenes, "Assets/Scenes/Farm.unity");

            Assert.GreaterOrEqual(townIndex, 0, "Town scene must be included in build settings.");
            Assert.GreaterOrEqual(farmIndex, 0, "Farm scene can remain only as a legacy scene after Town.");
            Assert.Less(townIndex, farmIndex, "Town should be ordered before legacy Farm in build settings.");
        }

        private static int FindSceneIndex(EditorBuildSettingsScene[] scenes, string path)
        {
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == path)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}