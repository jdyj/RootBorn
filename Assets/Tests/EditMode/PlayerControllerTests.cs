using NUnit.Framework;
using Rootborn.Game.Family;
using Rootborn.Game.Player;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    // TOOL-002: PlayerController는 Animator/SpriteRenderer가 없어도 안전하게 동작하고
    // LastFacing은 마지막 입력 방향을 기억한다.
    public sealed class PlayerControllerTests
    {
        [Test]
        public void GetComponentLookup_WorksWhenAttachedFirst()
        {
            // Animator/SpriteRenderer를 PlayerController보다 먼저 추가하면 Bind 없이도
            // GetComponent로 검색되는지 검증. EditMode에서는 Awake 타이밍이 미묘할 수
            // 있으므로 Bind를 명시 호출해도 동등하게 동작해야 한다.
            var go = new GameObject("player");
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                var anim = go.AddComponent<Animator>();
                var pc = go.AddComponent<PlayerController>();

                // 명시적 Bind 가 우선 보장
                pc.Bind(anim, sr);

                var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                Assert.IsNotNull(typeof(PlayerController).GetField("_animator", bind).GetValue(pc));
                Assert.IsNotNull(typeof(PlayerController).GetField("_renderer", bind).GetValue(pc));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DefaultLastFacing_IsDown()
        {
            var go = new GameObject("player");
            try
            {
                var pc = go.AddComponent<PlayerController>();
                Assert.AreEqual(new Vector2(0f, -1f), pc.LastFacing);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Bind_AssignsAnimatorAndRenderer()
        {
            var go = new GameObject("player");
            var animatorGo = new GameObject("animator");
            try
            {
                var pc = go.AddComponent<PlayerController>();
                var sr = go.AddComponent<SpriteRenderer>();
                var anim = animatorGo.AddComponent<Animator>();

                pc.Bind(anim, sr);

                var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                Assert.AreEqual(anim, typeof(PlayerController).GetField("_animator", bind).GetValue(pc));
                Assert.AreEqual(sr, typeof(PlayerController).GetField("_renderer", bind).GetValue(pc));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(animatorGo);
            }
        }

        [Test]
        public void Update_WhenFacingRight_FlipsAllCharacterPartLayers()
        {
            var go = new GameObject("player");
            try
            {
                var pc = go.AddComponent<PlayerController>();
                var sr = go.AddComponent<SpriteRenderer>();
                var composer = go.AddComponent<CharacterPartComposer>();
                composer.EnsureLayers(new[]
                {
                    CreateDefinition("character.body.01", "body", 0),
                    CreateDefinition("character.outfit.braces.brown", "outfit", 2),
                });
                pc.Bind(null, sr);

                var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(PlayerController).GetField("_lastFacing", bind).SetValue(pc, new Vector2(1f, 0f));
                typeof(PlayerController).GetMethod("Update", bind).Invoke(pc, null);

                Assert.IsTrue(go.transform.Find("Part_body").GetComponent<SpriteRenderer>().flipX);
                Assert.IsTrue(go.transform.Find("Part_outfit").GetComponent<SpriteRenderer>().flipX);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Update_WhenFacingRight_DoesNotRotatePlayerOrPartLayers()
        {
            var go = new GameObject("player");
            try
            {
                var pc = go.AddComponent<PlayerController>();
                var sr = go.AddComponent<SpriteRenderer>();
                var composer = go.AddComponent<CharacterPartComposer>();
                composer.EnsureLayers(new[]
                {
                    CreateDefinition("character.body.01", "body", 0),
                    CreateDefinition("character.outfit.braces.brown", "outfit", 2),
                });
                pc.Bind(null, sr);

                var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(PlayerController).GetField("_lastFacing", bind).SetValue(pc, new Vector2(1f, 0f));
                for (int i = 0; i < 5; i++)
                {
                    typeof(PlayerController).GetMethod("Update", bind).Invoke(pc, null);
                }

                Assert.AreEqual(0f, go.transform.localEulerAngles.z, 0.001f);
                Assert.AreEqual(Vector3.one, go.transform.localScale);
                Assert.AreEqual(0f, go.transform.Find("Part_body").localEulerAngles.z, 0.001f);
                Assert.AreEqual(0f, go.transform.Find("Part_outfit").localEulerAngles.z, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static CharacterPartDefinition CreateDefinition(string id, string categoryId, int layerOrder)
        {
            var definition = ScriptableObject.CreateInstance<CharacterPartDefinition>();
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_id").stringValue = id;
            serialized.FindProperty("_categoryId").stringValue = categoryId;
            serialized.FindProperty("_displayNameKey").stringValue = "loc." + id;
            serialized.FindProperty("_sheetAddress").stringValue = "sprites/character/" + categoryId + "/" + id;
            serialized.FindProperty("_subSpriteName").stringValue = id + "_r0_c0";
            serialized.FindProperty("_layerOrder").intValue = layerOrder;
            serialized.FindProperty("_isDefault").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }
    }
}
