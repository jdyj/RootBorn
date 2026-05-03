using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode
{
    // GEN-001 smoke: GameBootstrap이 None 모드에서 정상 시작하고 AppConfig를 노출한다.
    public sealed class BootSmokeTest
    {
        [UnityTest]
        public IEnumerator Bootstrap_PopulatesConfig()
        {
            var go = new GameObject("bootstrap");
            try
            {
                go.AddComponent<GameBootstrap>();
                yield return null;
                Assert.IsNotNull(GameBootstrap.Config);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
