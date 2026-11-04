using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Game.Characters;
using Rootborn.Game.Characters.Growing;
using Rootborn.Game.Common;
using Rootborn.Network.Characters;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Characters
{
    public sealed class GrowingCharacterIntegrationTests
    {
        private readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in _objects) Object.DestroyImmediate(item);
            _objects.Clear();
        }

        private GrowingCharacterAppearanceDefinition Look(string id)
        {
            var look = ScriptableObject.CreateInstance<GrowingCharacterAppearanceDefinition>();
            _objects.Add(look);
            look.ConfigureForTests(id, null, null, null);
            return look;
        }

        [Test]
        public void GROW_NET_004_DespawnClearsTheViewSoRespawnBindsAgain()
        {
            var player = new GameObject("player");
            _objects.Add(player);
            var view = player.AddComponent<GrowingCharacterVisualView>();
            var sync = player.AddComponent<GrowingCharacterNetworkAppearanceSync>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var field = typeof(GrowingCharacterNetworkAppearanceSync).GetField("_view", flags);
            typeof(GrowingCharacterNetworkAppearanceSync).GetMethod("BindView", flags).Invoke(sync, new object[] { view });
            Assert.That(field.GetValue(sync), Is.EqualTo(view));
            sync.OnNetworkDespawn();
            Assert.That(field.GetValue(sync), Is.Null, "A kept view makes the next spawn skip binding.");
        }

        [Test]
        public void GROW_REMOTE_001_SmallSyncedStepStillWalksAndFacesThatWay()
        {
            var player = new GameObject("remote");
            _objects.Add(player);
            var view = player.AddComponent<GrowingCharacterVisualView>();
            view.IsRemote = true;
            var update = typeof(GrowingCharacterVisualView).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            player.transform.position += new Vector3(0f, 0.005f, 0f);
            update.Invoke(view, null);
            Assert.That(view.CurrentRow, Is.EqualTo(3));
        }

        [Test]
        public void GROW_REMOTE_002_SpawningAwayFromOriginIsNotAMove()
        {
            var player = new GameObject("remote");
            _objects.Add(player);
            player.transform.position = new Vector3(5f, 7f, 0f);
            var view = player.AddComponent<GrowingCharacterVisualView>();
            view.IsRemote = true;
            int before = view.CurrentRow;
            typeof(GrowingCharacterVisualView).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(view, null);
            Assert.That(view.CurrentRow, Is.EqualTo(before), "The first position is where it spawned, not a step.");
        }

        [Test]
        public void GROW_NET_001_FindByIdResolvesOnlyRegisteredLooks()
        {
            var looks = new CharacterAppearanceDefinition[] { null, Look("a"), Look("b") };
            Assert.That(GrowingCharacterNetworkAppearanceSync.FindById(looks, "b").Id, Is.EqualTo("b"));
            Assert.That(GrowingCharacterNetworkAppearanceSync.FindById(looks, "hacked"), Is.Null);
            Assert.That(GrowingCharacterNetworkAppearanceSync.FindById(looks, ""), Is.Null);
            Assert.That(GrowingCharacterNetworkAppearanceSync.FindById(null, "a"), Is.Null);
        }

        [Test]
        public void GROW_NET_002_PlayerPrefabCarriesTheSync()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
            Assert.That(prefab.GetComponent<GrowingCharacterNetworkAppearanceSync>(), Is.Not.Null);
        }

        [Test]
        public void GROW_NET_003_IdsFitTheNetworkString()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            foreach (var appearance in registry.CharacterAppearances)
            {
                if (appearance is GrowingCharacterAppearanceDefinition look)
                    Assert.That(System.Text.Encoding.UTF8.GetByteCount(look.Id), Is.LessThanOrEqualTo(61), look.Id);
            }
        }

        [Test]
        public void GROW_VIEW_009_ApplyAnnouncesTheNewLook()
        {
            var host = new GameObject("view");
            _objects.Add(host);
            var view = host.AddComponent<GrowingCharacterVisualView>();
            GrowingCharacterAppearanceDefinition seen = null;
            view.AppearanceChanged += look => seen = look;
            var a = Look("a");
            view.Apply(a);
            Assert.That(seen, Is.EqualTo(a));
        }

        [Test]
        public void GROW_PREFAB_001_VisualPrefabHasWiredLayersInDrawOrder()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GrowingCharacterRuntimeInstaller.PrefabPath);
            Assert.That(prefab, Is.Not.Null, "Run the character pack import.");
            var view = prefab.GetComponent<GrowingCharacterVisualView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.BodyRenderer, Is.Not.Null);
            Assert.That(view.OutfitRenderer, Is.Not.Null);
            Assert.That(view.HeadRenderer, Is.Not.Null);
            Assert.That(view.OutfitRenderer.sortingOrder, Is.GreaterThan(view.BodyRenderer.sortingOrder));
            Assert.That(view.HeadRenderer.sortingOrder, Is.GreaterThan(view.OutfitRenderer.sortingOrder));
            Assert.That(prefab.GetComponentInChildren<Collider2D>(true), Is.Null, "Visual prefab stays free of physics.");
        }

        [Test]
        public void GROW_PREFAB_002_LooksPointAtThePrefab()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            var look = GrowingCharacterRuntimeInstaller.FindDefault(registry);
            Assert.That(look.VisualPrefab, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(look.VisualPrefab), Is.EqualTo(GrowingCharacterRuntimeInstaller.PrefabPath));
        }
    }
}
