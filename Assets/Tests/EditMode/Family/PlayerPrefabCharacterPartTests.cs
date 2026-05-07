using NUnit.Framework;
using Rootborn.Game.Family;
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
            var rootRenderer = prefab.GetComponent<SpriteRenderer>();
            Assert.IsTrue(rootRenderer == null || !rootRenderer.enabled || rootRenderer.sprite == null,
                "Player prefab root renderer must not display a complete single-character sprite.");

            AssertPart(prefab, "Part_body");
            AssertPart(prefab, "Part_eyes");
            AssertPart(prefab, "Part_hair");
            AssertPart(prefab, "Part_outfit");
            AssertPart(prefab, "Part_accessory");
        }

        private static void AssertPart(GameObject prefab, string childName)
        {
            var child = prefab.transform.Find(childName);
            Assert.IsNotNull(child, childName);
            Assert.IsNotNull(child.GetComponent<SpriteRenderer>(), childName);
        }
    }
}
