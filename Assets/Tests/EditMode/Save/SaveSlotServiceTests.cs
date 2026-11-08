using System.IO;
using NUnit.Framework;
using Rootborn.Game.Family;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Save
{
    public sealed class SaveSlotServiceTests
    {
        [Test]
        public void ListUiSlots_ReturnsThreeEmptySlots()
        {
            var root = MakeTempRoot();
            try
            {
                var service = new SaveService("slot-0", root);
                var slots = service.ListUiSlots();
                Assert.AreEqual(3, slots.Count);
                Assert.AreEqual("slot-0", slots[0].SlotId);
                Assert.AreEqual("slot-1", slots[1].SlotId);
                Assert.AreEqual("slot-2", slots[2].SlotId);
                Assert.IsFalse(slots[0].Exists);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void SaveAndLoadMetadata_PreservesSeedsAndCharacter()
        {
            var root = MakeTempRoot();
            try
            {
                var service = new SaveService("slot-0", root);
                var character = new CharacterCustomization
                {
                    BodyVariant = 1,
                    HairVariant = 2,
                    OutfitVariant = 3,
                    DefaultFacing = CharacterCustomization.Facing.Up,
                };

                var metadata = service.CreateMetadata("slot-0", character, 1234, 5678);
                service.SaveMetadata(metadata);

                var loaded = service.LoadMetadata("slot-0");
                Assert.IsNotNull(loaded);
                Assert.AreEqual("slot-0", loaded.SlotId);
                Assert.AreEqual(1234, loaded.WorldSeed);
                Assert.AreEqual(5678, loaded.TileSeed);
                Assert.AreEqual(1, loaded.Character.BodyVariant);
                Assert.AreEqual(2, loaded.Character.HairVariant);
                Assert.AreEqual(3, loaded.Character.OutfitVariant);
                Assert.AreEqual(CharacterCustomization.Facing.Up, loaded.Character.DefaultFacing);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void SaveAndLoadMetadata_PreservesAppearancePartIds()
        {
            var root = MakeTempRoot();
            try
            {
                var service = new SaveService("slot-0", root);
                var metadata = service.CreateMetadata("slot-0", new CharacterCustomization(), 1234, 5678);
                metadata.Appearance.SetSelectedPart("body", "character.body.01");
                metadata.Appearance.SetSelectedPart("eyes", "character.eyes.blue");
                metadata.Appearance.SetSelectedPart("hair", "character.hair.short.blonde");
                metadata.Appearance.SetSelectedPart("outfit", "character.outfit.braces.brown");
                metadata.Appearance.SetSelectedPart("accessory", "character.accessory.bamboo.brown");

                service.SaveMetadata(metadata);

                var loaded = service.LoadMetadata("slot-0");
                Assert.IsNotNull(loaded);
                Assert.AreEqual("character.body.01", loaded.Appearance.GetSelectedPartId("body"));
                Assert.AreEqual("character.eyes.blue", loaded.Appearance.GetSelectedPartId("eyes"));
                Assert.AreEqual("character.hair.short.blonde", loaded.Appearance.GetSelectedPartId("hair"));
                Assert.AreEqual("character.outfit.braces.brown", loaded.Appearance.GetSelectedPartId("outfit"));
                Assert.AreEqual("character.accessory.bamboo.brown", loaded.Appearance.GetSelectedPartId("accessory"));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void LoadMetadata_LegacyMetadataWithoutAppearanceGetsEmptyAppearanceFallback()
        {
            var root = MakeTempRoot();
            try
            {
                var slotDir = Path.Combine(root, "slot-0");
                Directory.CreateDirectory(slotDir);
                File.WriteAllText(Path.Combine(slotDir, "metadata.json"),
                    "{\"SlotId\":\"slot-0\",\"DisplayName\":\"slot-0\",\"CreatedAtUtcTicks\":1,\"UpdatedAtUtcTicks\":1,\"WorldSeed\":11,\"TileSeed\":22,\"Character\":{\"_bodyVariant\":2}}"
                );

                var service = new SaveService("slot-0", root);
                var loaded = service.LoadMetadata("slot-0");

                Assert.IsNotNull(loaded);
                Assert.IsNotNull(loaded.Character);
                Assert.IsNotNull(loaded.Appearance);
                Assert.AreEqual(string.Empty, loaded.Appearance.GetSelectedPartId("body"));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void CreateUiMetadata_RejectsFourthUiSlotButAllowsCliMetadataThroughGeneralApi()
        {
            var root = MakeTempRoot();
            try
            {
                var service = new SaveService("slot-0", root);
                Assert.Throws<System.ArgumentException>(() => service.CreateUiMetadata("slot-3", new CharacterCustomization(), 1, 2));

                var cliMetadata = service.CreateMetadata("myfarm", new CharacterCustomization(), 1, 2);
                Assert.AreEqual("myfarm", cliMetadata.SlotId);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void CreateDeterministicMetadata_UsesStableSeedsForCliSlotFallback()
        {
            var root = MakeTempRoot();
            try
            {
                var service = new SaveService("myfarm", root);
                var a = service.CreateDeterministicMetadata("myfarm", new CharacterCustomization { BodyVariant = 2 });
                var b = service.CreateDeterministicMetadata("myfarm", new CharacterCustomization { BodyVariant = 2 });
                var c = service.CreateDeterministicMetadata("otherfarm", new CharacterCustomization { BodyVariant = 2 });

                Assert.AreEqual("myfarm", a.SlotId);
                Assert.AreEqual(a.WorldSeed, b.WorldSeed);
                Assert.AreEqual(a.TileSeed, b.TileSeed);
                Assert.AreNotEqual(a.WorldSeed, c.WorldSeed);
                Assert.AreEqual(2, a.Character.BodyVariant);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void DeleteSlot_RemovesMetadata()
        {
            var root = MakeTempRoot();
            try
            {
                var service = new SaveService("slot-0", root);
                service.SaveMetadata(service.CreateMetadata("slot-1", new CharacterCustomization(), 11, 22));
                Assert.IsTrue(service.DeleteSlot("slot-1"));
                Assert.IsNull(service.LoadMetadata("slot-1"));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void StartNewSlot_ClearsLeftoverFilesOfAStoryWhoseMetadataIsGone()
        {
            var root = MakeTempRoot();
            try
            {
                // An unreadable or missing metadata shows the slot as empty, but old progress files can still be there.
                var old = new SaveService("slot-1", root);
                old.WriteJson("wearables-local-player.json", "{\"legacyImported\":true}");
                old.WriteJson("student-life-progress.json", "{\"day\":9}");
                File.WriteAllText(Path.Combine(old.DirectoryPath, "metadata.json"), "not json");

                var service = new SaveService("slot-0", root);
                Assert.IsNull(service.LoadMetadata("slot-1"));
                Assert.IsTrue(service.StartNewSlot(service.CreateMetadata("slot-1", new CharacterCustomization(), 11, 22)));

                Assert.IsNotNull(service.LoadMetadata("slot-1"));
                Assert.AreEqual(new[] { "metadata.json" }, System.Array.ConvertAll(Directory.GetFiles(old.DirectoryPath), Path.GetFileName),
                    "A new story must not inherit the old story's outfit, progress or anything else.");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void StartNewSlot_NeverOverwritesAStoryThatStillLoads()
        {
            var root = MakeTempRoot();
            try
            {
                var service = new SaveService("slot-0", root);
                var existing = service.CreateMetadata("slot-1", new CharacterCustomization(), 11, 22);
                existing.DisplayName = "Kept";
                service.SaveMetadata(existing);
                new SaveService("slot-1", root).WriteJson("student-life-progress.json", "{\"day\":9}");

                Assert.IsFalse(service.StartNewSlot(service.CreateMetadata("slot-1", new CharacterCustomization(), 33, 44)));
                Assert.AreEqual("Kept", service.LoadMetadata("slot-1").DisplayName);
                Assert.AreEqual("{\"day\":9}", new SaveService("slot-1", root).ReadJson("student-life-progress.json"));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void CreateMetadata_RejectsUnsafeSlotId()
        {
            var root = MakeTempRoot();
            try
            {
                var service = new SaveService("slot-0", root);
                Assert.Throws<System.ArgumentException>(() => service.CreateMetadata("../bad", new CharacterCustomization(), 1, 2));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void WriteJson_ReplacesFileAtomicallyAndLeavesNoTemporaryFiles()
        {
            var root = MakeTempRoot();
            try
            {
                var service = new SaveService("slot-0", root);
                service.WriteJson("progress.json", "{\"v\":1}");
                string path = Path.Combine(service.DirectoryPath, "progress.json");
                using (var committed = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(committed))
                {
                    service.WriteJson("progress.json", "{\"v\":2}");
                    // In-place truncation would change this handle; an atomic replace leaves the old file intact.
                    Assert.AreEqual("{\"v\":1}", reader.ReadToEnd());
                }

                Assert.AreEqual("{\"v\":2}", service.ReadJson("progress.json"));
                Assert.AreEqual(1, Directory.GetFiles(service.DirectoryPath).Length);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static string MakeTempRoot()
        {
            var root = Path.Combine(Application.temporaryCachePath, "rootborn-save-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }
    }
}
