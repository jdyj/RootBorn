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

        [TestCase(true)][TestCase(false)]
        public void REGISTRY_PROVIDER_EDIT_004_PlayStartDropsPriorOverrideAndCachedProvider(bool useOverride)
        {
            var sentinel=ScriptableObject.CreateInstance<GameDataRegistry>();
            try{
                GameDataRegistryProvider.ClearOverrideForTests();GameDataRegistryProvider.SetProviderForTests(new FixedProvider(sentinel));
                if(useOverride)GameDataRegistryProvider.SetOverrideForTests(sentinel);
                Assert.That(GameDataRegistryProvider.Require(),Is.SameAs(sentinel));
                var reset=typeof(GameDataRegistryProvider).GetMethod("ResetForPlayMode",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                Assert.That(reset,Is.Not.Null,"Subsystem registration must clear stale test/editor registry state before each play session");
                reset.Invoke(null,null);GameDataRegistryProvider.TryGet(out var actual);
                Assert.That(actual,Is.Not.SameAs(sentinel));
                Assert.That(actual,Is.SameAs(Rootborn.Game.Managers.Managers.Data!=null?Rootborn.Game.Managers.Managers.Data.Registry:null));
            }finally{Object.DestroyImmediate(sentinel);}
        }
        private sealed class FixedProvider : IGameDataRegistryProvider
        {
            private readonly GameDataRegistry _value;public FixedProvider(GameDataRegistry value){_value=value;}
            public bool TryGet(out GameDataRegistry registry){registry=_value;return registry!=null;}public GameDataRegistry Require()=>_value;
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
