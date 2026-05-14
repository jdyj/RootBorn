using NUnit.Framework;
using Rootborn.Game.Characters.Spum;
using Rootborn.Game.Dialogue;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Characters.Spum
{
    public sealed class SpumNpcAppearanceDefinitionTests
    {
        [Test]
        public void SPUM_NPC_APPEARANCE_001_NpcDefinitionExposesCharacterAppearanceDefinition()
        {
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            var npc = ScriptableObject.CreateInstance<NpcDefinition>();
            var catalog = ScriptableObject.CreateInstance<SpumPartCatalogDefinition>();
            var appearance = ScriptableObject.CreateInstance<SpumAppearanceDefinition>();
            try
            {
                location.ConfigureForTests("location.test", "location.test.name", Vector2.zero, System.Array.Empty<DiscoveryDefinition>());
                appearance.ConfigureForTests("appearance.npc.test", "spum", catalog);
                npc.ConfigureForTests(
                    "npc.test",
                    "npc.test.name",
                    "npc.test.intro",
                    location,
                    System.Array.Empty<NpcRoleDefinition>(),
                    null,
                    System.Array.Empty<NpcDialogueConditionBase>(),
                    appearance);

                Assert.AreSame(appearance, npc.CharacterAppearance, "NPC appearance must be data-driven through the NPC definition.");
            }
            finally
            {
                Object.DestroyImmediate(appearance);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(npc);
                Object.DestroyImmediate(location);
            }
        }
    }
}
