using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Characters;
using Rootborn.Game.Characters.Spum;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Player;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class SpumNpcDialogueE2ETests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator SPUM_NPC_DIALOGUE_E2E_001_ActualInteractOpensDialogueAndKeepsVisualChildSeparateFromNpcRoot()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var player = new GameObject("Player");
            var npcRoot = new GameObject("TownNpc_Test");
            try
            {
                player.transform.position = Vector3.zero;
                player.AddComponent<PlayerInteractionRouter>().ConfigureForTests(2f);
                player.AddComponent<GatherInteractor>();
                player.AddComponent<StudentLifeProgressComponent>();

                var location = ScriptableObject.CreateInstance<LocationDefinition>();
                var catalog = ScriptableObject.CreateInstance<SpumPartCatalogDefinition>();
                var appearance = ScriptableObject.CreateInstance<SpumAppearanceDefinition>();
                var dialogue = ScriptableObject.CreateInstance<DialogueDefinition>();
                var npc = ScriptableObject.CreateInstance<NpcDefinition>();
                location.ConfigureForTests("location.test", "location.test.name", Vector2.zero, System.Array.Empty<DiscoveryDefinition>());
                appearance.ConfigureForTests("appearance.npc.test", "spum", catalog);
                dialogue.ConfigureForTests(new[] { "dialogue.npc.test.hello" }, System.Array.Empty<DialogueChoiceDefinition>(), System.Array.Empty<DialogueCondition>());
                npc.ConfigureForTests("npc.test", "npc.test.name", "npc.test.intro", location, System.Array.Empty<NpcRoleDefinition>(), dialogue, System.Array.Empty<NpcDialogueConditionBase>(), appearance);

                npcRoot.transform.position = new Vector3(0.75f, 0f, 0f);
                var collider = npcRoot.AddComponent<BoxCollider2D>();
                collider.isTrigger = true;
                var interactor = npcRoot.AddComponent<NpcInteractor>();
                interactor.Bind(npc);
                var visual = new GameObject("NpcVisual");
                visual.transform.SetParent(npcRoot.transform, false);
                var visualAdapter = visual.AddComponent<NpcCharacterVisualAdapter>();
                visualAdapter.Bind(npc.CharacterAppearance, null);

                yield return null;
                Press(keyboard.eKey);
                InputSystem.Update();
                for (int i = 0; i < 12 && interactor.Session.Current == null; i++)
                {
                    yield return null;
                }
                Release(keyboard.eKey);
                InputSystem.Update();

                Assert.IsNotNull(interactor.Session.Current, "Actual E-key interaction must open NPC dialogue.");
                Assert.AreSame(npcRoot.transform, interactor.InteractionTransform, "Dialogue and collision ownership must stay on the NPC root.");
                Assert.AreNotSame(npcRoot, visualAdapter.gameObject, "SPUM visual adapter must live on a child object, separate from the NPC collider/dialogue root.");
                Assert.IsNull(visualAdapter.GetComponent<Collider2D>(), "Visual child must not own the interaction collider.");
                Assert.AreSame(appearance, visualAdapter.AppearanceDefinition);
            }
            finally
            {
                Release(keyboard.eKey);
                InputSystem.Update();
                InputSystem.RemoveDevice(keyboard);
                Object.Destroy(player);
                Object.Destroy(npcRoot);
            }
        }
    }
}
