using System.IO;
using NUnit.Framework;
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

        private static string MakeTempRoot()
        {
            var root = Path.Combine(Application.temporaryCachePath, "rootborn-save-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }
    }
}
