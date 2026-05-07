using NUnit.Framework;
using Rootborn.Game.Family;
using Rootborn.Game.Player;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Family
{
    public sealed class PlayerPrefabCharacterPartTests
    {
        [Test]
        public void PlayerPrefab_HasCharacterPartComposerAnimatorAndRequiredPartRenderers()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
            Assert.IsNotNull(prefab);
            Assert.IsNotNull(prefab.GetComponent<CharacterPartComposer>());
            Assert.IsNotNull(prefab.GetComponent<CharacterPartAnimator>());
            Assert.IsNotNull(prefab.GetComponent<FishingAnimationController>());
            Assert.AreEqual(Vector3.one, prefab.transform.localScale, "16x16 part-composed characters must render at 1x scale.");
            var rootRenderer = prefab.GetComponent<SpriteRenderer>();
            Assert.IsTrue(rootRenderer == null || !rootRenderer.enabled || rootRenderer.sprite == null,
                "Player prefab root renderer must not display a complete single-character sprite.");

            AssertPart(prefab, "Part_body");
            AssertPart(prefab, "Part_eyes");
            AssertPart(prefab, "Part_hair");
            AssertPart(prefab, "Part_outfit");
            AssertPart(prefab, "Part_accessory");
            var toolRenderer = AssertPart(prefab, "Part_tool");

            var controller = prefab.GetComponent<PlayerController>();
            Assert.IsNotNull(controller);
            var serialized = new SerializedObject(controller);
            Assert.AreSame(toolRenderer, serialized.FindProperty("_toolRenderer").objectReferenceValue);
        }

        private static SpriteRenderer AssertPart(GameObject prefab, string childName)
        {
            var child = prefab.transform.Find(childName);
            Assert.IsNotNull(child, childName);
            var renderer = child.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(renderer, childName);
            return renderer;
        }
    }
}
