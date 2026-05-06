using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.UI.MainMenu;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Save
{
    public sealed class SaveSlotSelectPanelTests
    {
        [Test]
        public void CreateMetadataForNewSlot_UsesProvidedCharacterSelectionAndSeeds()
        {
            var panel = new GameObject("SaveSlotSelectPanelTest").AddComponent<SaveSlotSelectPanel>();
            try
            {
                var character = new CharacterCustomization
                {
                    BodyVariant = 4,
                    HairVariant = 5,
                    OutfitVariant = 6,
                    DefaultFacing = CharacterCustomization.Facing.Left,
                };

                var metadata = panel.CreateMetadataForNewSlot("slot-1", character, 111, 222);

                Assert.AreEqual("slot-1", metadata.SlotId);
                Assert.AreEqual(111, metadata.WorldSeed);
                Assert.AreEqual(222, metadata.TileSeed);
                Assert.AreEqual(4, metadata.Character.BodyVariant);
                Assert.AreEqual(5, metadata.Character.HairVariant);
                Assert.AreEqual(6, metadata.Character.OutfitVariant);
                Assert.AreEqual(CharacterCustomization.Facing.Left, metadata.Character.DefaultFacing);
            }
            finally
            {
                Object.DestroyImmediate(panel.gameObject);
            }
        }

        [Test]
        public void FormatCharacterPreview_IncludesCharacterVariants()
        {
            var character = new CharacterCustomization { BodyVariant = 1, HairVariant = 2, OutfitVariant = 3 };
            var metadata = new SaveSlotMetadata { Character = character };

            string preview = InvokeFormatCharacterPreview(metadata);

            StringAssert.Contains("Body 1", preview);
            StringAssert.Contains("Hair 2", preview);
            StringAssert.Contains("Outfit 3", preview);
        }

        private static string InvokeFormatCharacterPreview(SaveSlotMetadata metadata)
        {
            var method = typeof(SaveSlotSelectPanel).GetMethod("FormatCharacterPreview", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return (string)method.Invoke(null, new object[] { metadata });
        }
    }
}
