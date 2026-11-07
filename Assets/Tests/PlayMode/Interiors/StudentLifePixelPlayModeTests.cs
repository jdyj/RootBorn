using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Rootborn.Game.Managers;
using Rootborn.Game.Placement;
using Rootborn.Game.Save;
using Rootborn.UI.Interiors;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.PlayMode.Interiors
{
    public sealed class StudentLifePixelPlayModeTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private static readonly PropCase[] Props =
        {
            new PropCase("studentlife.snack.delivery.crate", "StudentLife_snack_delivery_crate_Tile", 1, "snack-crate"),
            new PropCase("studentlife.toy.kitchen", "StudentLife_toy_kitchen_Tile", 2, "toy-kitchen")
        };

        private SaveSlotMetadata _previousSave;
        private string _previousSaveRoot;
        private string _runId;
        private string _saveRoot;
        private Mouse _mouse;
        private Keyboard _keyboard;
        private static string EvidenceRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../output/student-life-integration-20261027/evidence/student-life-playmode"));
        private string Evidence => Path.Combine(EvidenceRoot, _runId);

        public override void Setup()
        {
            base.Setup();
            _previousSave = ActiveSaveContext.Metadata;
            var rootField = typeof(SaveService).GetField("s_rootDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(rootField, "Preserve the caller's save-root override rather than resetting it blindly.");
            _previousSaveRoot = (string)rootField.GetValue(null);
            _runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            _saveRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../output/student-life-integration-20261027/saves-playmode", _runId));
            Directory.CreateDirectory(_saveRoot);
            Directory.CreateDirectory(Evidence);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "studentlife-qa-bootstrap", DisplayName = "Student-life art QA" });
            _mouse = InputSystem.AddDevice<Mouse>();
            _keyboard = InputSystem.AddDevice<Keyboard>();
        }

        public override void TearDown()
        {
            ActiveSaveContext.Set(_previousSave);
            SaveService.SetRootDirectoryForTests(_previousSaveRoot);
            base.TearDown();
        }

        [UnitySetUp]
        public IEnumerator EnableRealUiInputForPlayerFlow()
        {
            // The assembly guard otherwise replaces the real UI module with a no-op PassiveInputModule.
            // Buttons have a separate runtime pointer router, but InputField focus needs the real UI module.
            InputSystemUiModulePlayModeTestGuard.UseRealInputSystemForCurrentTest();
            yield return null;
        }

        [UnityTest]
        public IEnumerator StudentLifeAssets_AddressableAsyncLoadAndRelease()
        {
            yield return Await(Managers.BootstrapAsync(), "Managers bootstrap");
            var resource = Managers.Resource;
            Assert.IsNotNull(resource);
            const string label = "StudentLifePixels";
            const string spriteAddress = "sprites/student-life/sprites/old-town-ledger";
            var addresses = new List<string> { spriteAddress };
            foreach (var prop in Props)
            {
                addresses.Add("data/furniture/" + prop.StableId.Replace('.', '-'));
                addresses.Add("tiles/student-life/" + prop.TileName.ToLowerInvariant().Replace('_', '-'));
            }
            try
            {
                var pack = resource.LoadByLabelAsync<Object>(label);
                yield return Await(pack, "StudentLifePixels label load");
                Assert.IsNotNull(pack.Result);
                Assert.IsTrue(pack.Result.All(asset => asset != null));
                // Packed catalogs can expose Sprite and Texture2D for the same PNG.
                Assert.AreEqual(12, pack.Result.Select(asset => asset.name).Distinct().Count(), "8 image assets + 2 furniture definitions + 2 tiles.");
                Assert.AreEqual(8, pack.Result.Where(asset => asset is Sprite || asset is Texture2D).Select(asset => asset.name).Distinct().Count());
                Assert.AreEqual(2, pack.Result.OfType<FurnitureDefinition>().Count());
                Assert.AreEqual(2, pack.Result.OfType<Tile>().Count());

                var ledger = resource.LoadAsync<Sprite>(spriteAddress);
                yield return Await(ledger, "Ledger sprite load");
                Assert.IsNotNull(ledger.Result);
                Assert.AreEqual("old_town_ledger", ledger.Result.name);
                Assert.AreEqual(FilterMode.Point, ledger.Result.texture.filterMode);
                Assert.AreEqual(48f, ledger.Result.pixelsPerUnit);
                foreach (var prop in Props)
                {
                    string definitionAddress = "data/furniture/" + prop.StableId.Replace('.', '-');
                    string tileAddress = "tiles/student-life/" + prop.TileName.ToLowerInvariant().Replace('_', '-');
                    var definition = resource.LoadAsync<FurnitureDefinition>(definitionAddress);
                    yield return Await(definition, prop.StableId + " definition load");
                    Assert.IsNotNull(definition.Result);
                    Assert.AreEqual(prop.StableId, definition.Result.StableId);
                    Assert.AreEqual(prop.FootprintWidth, definition.Result.FootprintCells.Count);
                    var tile = resource.LoadAsync<Tile>(tileAddress);
                    yield return Await(tile, prop.StableId + " tile load");
                    Assert.IsNotNull(tile.Result);
                    Assert.AreEqual(prop.TileName, tile.Result.name);
                    Assert.IsNotNull(tile.Result.sprite);
                    Assert.AreEqual(Tile.ColliderType.None, tile.Result.colliderType, "Floor occupancy owns blocking; tall art must not become a full-height collider.");
                }
                File.WriteAllText(Path.Combine(Evidence, "addressables-load.txt"),
                    "loader=Managers.Resource.LoadAsync / LoadByLabelAsync\nlabel=" + label + "\nlogicalAssetCount=12\nloadedObjectCount=" + pack.Result.Count +
                    "\nimages=8\ndefinitions=2\ntiles=2\naddresses=" + string.Join("\n", addresses));
            }
            finally
            {
                foreach (string address in addresses) resource.Release(address);
                resource.Release("label:" + label);
            }
            foreach (string address in addresses) Assert.IsNull(resource.Load<Object>(address), "Explicit release must evict the load cache: " + address);
        }

        private static IEnumerator Await(Task task, string operation)
        {
            float deadline = Time.realtimeSinceStartup + 60f;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(task.IsCompleted, operation + " timed out.");
            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
            Assert.IsFalse(task.IsCanceled, operation + " was cancelled.");
        }

        private readonly struct PropCase
        {
            public readonly string StableId;
            public readonly string TileName;
            public readonly int FootprintWidth;
            public readonly string EvidenceName;
            public PropCase(string stableId, string tileName, int footprintWidth, string evidenceName)
            {
                StableId = stableId;
                TileName = tileName;
                FootprintWidth = footprintWidth;
                EvidenceName = evidenceName;
            }
        }
    }
}
