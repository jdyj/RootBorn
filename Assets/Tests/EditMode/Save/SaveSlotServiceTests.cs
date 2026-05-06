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
                    BodyIndex = 1,
                    EyesIndex = 2,
                    HairstyleIndex = 3,
                    OutfitIndex = 4,
                    AccessoryIndex = 5,
                };

                var metadata = service.CreateMetadata("slot-0", character, 1234, 5678);
                service.SaveMetadata(metadata);

                var loaded = service.LoadMetadata("slot-0");
                Assert.IsNotNull(loaded);
                Assert.AreEqual("slot-0", loaded.SlotId);
                Assert.AreEqual(1234, loaded.WorldSeed);
                Assert.AreEqual(5678, loaded.TileSeed);
                Assert.AreEqual(1, loaded.Character.BodyIndex);
                Assert.AreEqual(5, loaded.Character.AccessoryIndex);
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
