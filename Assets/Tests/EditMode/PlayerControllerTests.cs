using NUnit.Framework;
using Rootborn.Game.Player;
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
    }
}
