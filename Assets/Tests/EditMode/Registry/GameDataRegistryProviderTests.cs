using NUnit.Framework;
using Rootborn.Game.Common;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Registry
{
    public sealed class GameDataRegistryProviderTests
    {
        [TearDown]
        public void TearDown()
        {
            GameDataRegistryProvider.ClearOverrideForTests();
            GameDataRegistryProvider.ResetCacheForTests();
        }

        [Test]
        public void REGISTRY_PROVIDER_EDIT_001_OverrideRegistryIsReturnedAndCached()
        {
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();

            GameDataRegistryProvider.SetOverrideForTests(registry);

            Assert.IsTrue(GameDataRegistryProvider.TryGet(out var first));
            Assert.AreSame(registry, first);
            Assert.AreSame(registry, GameDataRegistryProvider.Require());
        }

        [Test]
        public void REGISTRY_PROVIDER_EDIT_002_ClearOverrideRemovesTestRegistry()
        {
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            GameDataRegistryProvider.SetProviderForTests(new EmptyProvider());
            GameDataRegistryProvider.SetOverrideForTests(registry);
            Assert.IsTrue(GameDataRegistryProvider.TryGet(out _));

            GameDataRegistryProvider.ClearOverrideForTests();
            GameDataRegistryProvider.ResetCacheForTests();
            GameDataRegistryProvider.SetProviderForTests(new EmptyProvider());

            Assert.IsFalse(GameDataRegistryProvider.TryGet(out var cleared));
            Assert.IsNull(cleared);
        }

        [Test]
        public void REGISTRY_PROVIDER_EDIT_003_RequireThrowsWhenNoRegistrySourceExists()
        {
            GameDataRegistryProvider.ClearOverrideForTests();
            GameDataRegistryProvider.SetProviderForTests(new EmptyProvider());
            GameDataRegistryProvider.ResetCacheForTests();
            GameDataRegistryProvider.SetProviderForTests(new EmptyProvider());

            var exception = Assert.Throws<System.InvalidOperationException>(() => GameDataRegistryProvider.Require());
            StringAssert.Contains("GameDataRegistry", exception.Message);
        }

        private sealed class EmptyProvider : IGameDataRegistryProvider
        {
            public bool TryGet(out GameDataRegistry registry)
            {
                registry = null;
                return false;
            }

            public GameDataRegistry Require()
            {
                throw new System.InvalidOperationException("empty provider");
            }
        }
    }
}
