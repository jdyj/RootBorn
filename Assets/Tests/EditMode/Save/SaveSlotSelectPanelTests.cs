using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.UI.MainMenu;
using UnityEngine;
using UnityEngine.UI;

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

        [Test]
        public void BuildCharacterPreviewImage_CreatesVisibleImageFromCharacterVariants()
        {
            var parent = new GameObject("Card", typeof(RectTransform));
            try
            {
                var character = new CharacterCustomization { BodyVariant = 1, HairVariant = 2, OutfitVariant = 3 };
                var metadata = new SaveSlotMetadata { Character = character };

                InvokeBuildCharacterPreviewImage(parent.transform, metadata);

                var preview = parent.transform.Find("CharacterPreviewImage");
                Assert.IsNotNull(preview);
                var image = preview.GetComponent<Image>();
                Assert.IsNotNull(image);
                Assert.Greater(image.color.a, 0f);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        private static string InvokeFormatCharacterPreview(SaveSlotMetadata metadata)
        {
            var method = typeof(SaveSlotSelectPanel).GetMethod("FormatCharacterPreview", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return (string)method.Invoke(null, new object[] { metadata });
        }

        private static void InvokeBuildCharacterPreviewImage(Transform parent, SaveSlotMetadata metadata)
        {
            var method = typeof(SaveSlotSelectPanel).GetMethod("BuildCharacterPreviewImage", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            method.Invoke(null, new object[] { parent, metadata });
        }
    }
}
