using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class ExplorationInteractionChoiceE2EScenarioTests : InputTestFixture
    {
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-exploration-choice-e2e", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            EnsureEventSystem();
            yield return null;
        }

        [UnityTearDown]
        public new IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            SaveService.SetRootDirectoryForTests(null);
            LogAssert.ignoreFailingMessages = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator EXPLORATION_CHOICE_E2E_005_011_OpenChoosePersistAndDedupeThroughPromptAndButton()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            var trait = ScriptableObject.CreateInstance<TraitDefinition>();
            var risk = ScriptableObject.CreateInstance<ExplorationRiskPolicyDefinition>();
            var itemOutcome = ScriptableObject.CreateInstance<ExplorationItemGrantOutcome>();
            var traitOutcome = ScriptableObject.CreateInstance<ExplorationTraitDeltaOutcome>();
            var choice = ScriptableObject.CreateInstance<ExplorationChoiceDefinition>();
            var locked = ScriptableObject.CreateInstance<ExplorationChoiceDefinition>();
            var interaction = ScriptableObject.CreateInstance<ExplorationInteractionDefinition>();
            ExplorationTraitThresholdCondition impossible = null;
            try
            {
                ConfigureItem(item, "item.e2e-token", 99);
                trait.ConfigureForTests("trait.e2e-insight", "trait.e2e-insight");
                risk.ConfigureForTests(1, 1, 0, null, null);
                itemOutcome.ConfigureForTests(item, 1, false);
                traitOutcome.ConfigureForTests(trait, 1);
                choice.ConfigureForTests("choice.e2e.inspect", "Inspect", "Inspect", System.Array.Empty<ExplorationConditionBase>(), new ExplorationOutcomeBase[] { itemOutcome, traitOutcome }, risk, false, "item and insight");
                impossible = ScriptableObject.CreateInstance<ExplorationTraitThresholdCondition>();
                impossible.ConfigureForTests(trait, 99, "requires more curiosity");
                locked.ConfigureForTests("choice.e2e.locked", "Locked", "Locked", new ExplorationConditionBase[] { impossible }, System.Array.Empty<ExplorationOutcomeBase>(), null, false, "requires more curiosity");
                interaction.ConfigureForTests("exploration.e2e.crate", "E2E Crate", "Crate", new Vector2(1.5f, 0f), 1f, false, 0, null, new[] { choice, locked });

                var player = new GameObject("Player", typeof(PlayerInput), typeof(PlayerController), typeof(PlayerInteractionRouter), typeof(GatherInteractor), typeof(StudentLifeProgressComponent), typeof(PlayerInventory));
                player.transform.position = Vector3.zero;
                var student = player.GetComponent<StudentLifeProgressComponent>();
                student.ConfigureForTests("slot-exploration-e2e", "player-e2e", 10, 10, 0, 0);
                var inventory = player.GetComponent<PlayerInventory>();
                inventory.Bind(null);
                player.GetComponent<GatherInteractor>().BindInventory(inventory);
                var point = new GameObject("ExplorationE2EPoint", typeof(BoxCollider2D), typeof(ExplorationInteractionPointInteractor));
                point.transform.position = new Vector3(1.5f, 0f, 0f);
                point.GetComponent<BoxCollider2D>().isTrigger = true;
                var interactor = point.GetComponent<ExplorationInteractionPointInteractor>();
                interactor.Bind(interaction);

                yield return WalkPlayerWithKeyboardTo(player, keyboard, point.transform.position, 0.3f);
                player.GetComponent<PlayerInteractionRouter>().RefreshPromptNow();
                Assert.IsTrue(player.GetComponent<PlayerInteractionRouter>().PromptVisible, "EXPLORATION-CHOICE-E2E-004 failed: prompt not visible.");
                yield return PressInteractKey(keyboard);
                var panel = Object.FindFirstObjectByType<ExplorationChoicePanel>(FindObjectsInactive.Include);
                Assert.IsNotNull(panel, "EXPLORATION-CHOICE-E2E-005 failed: choice UI not created.");
                Assert.IsTrue(panel.IsOpen, "EXPLORATION-CHOICE-E2E-005 failed: choice UI not open.");
                Assert.GreaterOrEqual(panel.Model.Choices.Length, 2, "EXPLORATION-CHOICE-E2E-006 failed: expected at least two choices.");

                yield return ClickButton(FindButton(panel.transform, "ChoiceButton_choice.e2e.inspect"));
                Assert.AreEqual(ExplorationChoiceResultKind.Applied, interactor.LastResult.Kind, "EXPLORATION-CHOICE-E2E-007 failed: button click did not apply choice. Result text: " + panel.ResultText);
                Assert.AreEqual(1, inventory.Inventory.CountOf(item), "EXPLORATION-CHOICE-E2E-008 failed: item result missing.");
                Assert.AreEqual(1, student.Progress.GetTraitValue(trait), "EXPLORATION-CHOICE-E2E-008 failed: trait result missing.");
                Assert.IsTrue(panel.ResultText.Contains("item.e2e-token"), "EXPLORATION-CHOICE-E2E-008 failed: result UI missing item result.");

                var restored = ExplorationProgress.FromSaveData(panel.Progress.ToSaveData());
                Assert.IsTrue(restored.GetRecord(interaction.Id).Completed, "EXPLORATION-CHOICE-E2E-009 failed: completion did not persist through save data.");
                yield return ClickButton(FindButton(panel.transform, "ChoiceButton_choice.e2e.inspect"));
                Assert.AreEqual(1, inventory.Inventory.CountOf(item), "EXPLORATION-CHOICE-E2E-010 failed: duplicate item reward granted.");
            }
            finally
            {
                Object.DestroyImmediate(impossible);
                Object.DestroyImmediate(interaction);
                Object.DestroyImmediate(choice);
                Object.DestroyImmediate(locked);
                Object.DestroyImmediate(traitOutcome);
                Object.DestroyImmediate(itemOutcome);
                Object.DestroyImmediate(risk);
                Object.DestroyImmediate(trait);
                Object.DestroyImmediate(item);
            }
        }

        private IEnumerator WalkPlayerWithKeyboardTo(GameObject player, Keyboard keyboard, Vector3 targetPosition, float tolerance)
        {
            Key activeKey = Key.None;
            for (int i = 0; i < 180 && Vector3.Distance(player.transform.position, targetPosition) > tolerance; i++)
            {
                Vector3 delta = targetPosition - player.transform.position;
                Key key = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? (delta.x >= 0f ? Key.D : Key.A) : (delta.y >= 0f ? Key.W : Key.S);
                if (activeKey != key)
                {
                    if (activeKey != Key.None) Release(ControlFor(keyboard, activeKey));
                    Press(ControlFor(keyboard, key));
                    activeKey = key;
                }
                InputSystem.Update();
                yield return null;
                yield return new WaitForFixedUpdate();
            }
            if (activeKey != Key.None) Release(ControlFor(keyboard, activeKey));
            InputSystem.Update();
            yield return new WaitForFixedUpdate();
            Assert.LessOrEqual(Vector3.Distance(player.transform.position, targetPosition), tolerance);
        }

        private IEnumerator PressInteractKey(Keyboard keyboard)
        {
            Press(keyboard.eKey);
            InputSystem.Update();
            yield return null;
            yield return new WaitForFixedUpdate();
            Release(keyboard.eKey);
            InputSystem.Update();
            yield return null;
        }

        private static IEnumerator ClickButton(Button button)
        {
            Assert.IsNotNull(button);
            Assert.IsTrue(button.interactable, button.name + " must be interactable.");
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        private static Button FindButton(Transform root, string name)
        {
            var child = FindChild(root, name);
            Assert.IsNotNull(child, name + " missing.");
            return child.GetComponent<Button>();
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = FindChild(root.GetChild(i), name);
                if (child != null) return child;
            }
            return null;
        }

        private static KeyControl ControlFor(Keyboard keyboard, Key key)
        {
            if (key == Key.A) return keyboard.aKey;
            if (key == Key.D) return keyboard.dKey;
            if (key == Key.W) return keyboard.wKey;
            if (key == Key.S) return keyboard.sKey;
            return keyboard.eKey;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<Rootborn.Game.Common.PassiveInputModule>();
        }

        private static void ConfigureItem(ItemDefinition item, string id, int maxStack)
        {
            var serialized = new UnityEditor.SerializedObject(item);
            serialized.FindProperty("_id").stringValue = id;
            serialized.FindProperty("_displayKey").stringValue = id;
            serialized.FindProperty("_maxStack").intValue = maxStack;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
