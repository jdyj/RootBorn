using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Family;
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
        public void CreateMetadataForSelectedCharacter_UsesPanelSelectionAndCopiesIt()
        {
            var panel = new GameObject("SaveSlotSelectPanelTest").AddComponent<SaveSlotSelectPanel>();
            try
            {
                panel.SetSelectedCharacterSelection(2, 3, 4, CharacterCustomization.Facing.Right);

                var metadata = panel.CreateMetadataForSelectedCharacter("slot-2", 100, 200);
                panel.SetSelectedCharacterSelection(8, 9, 10, CharacterCustomization.Facing.Up);

                Assert.AreEqual("slot-2", metadata.SlotId);
                Assert.AreEqual(100, metadata.WorldSeed);
                Assert.AreEqual(200, metadata.TileSeed);
                Assert.AreEqual(2, metadata.Character.BodyVariant);
                Assert.AreEqual(3, metadata.Character.HairVariant);
                Assert.AreEqual(4, metadata.Character.OutfitVariant);
                Assert.AreEqual(CharacterCustomization.Facing.Right, metadata.Character.DefaultFacing);
            }
            finally
            {
                Object.DestroyImmediate(panel.gameObject);
            }
        }

        [Test]
        public void CreateMetadataForSelectedCharacter_CopiesSelectedAppearanceParts()
        {
            var panel = new GameObject("SaveSlotSelectPanelTest").AddComponent<SaveSlotSelectPanel>();
            try
            {
                panel.SetSelectedAppearancePart("body", "character.body.01");
                panel.SetSelectedAppearancePart("eyes", "character.eyes.blue");

                var metadata = panel.CreateMetadataForSelectedCharacter("slot-2", 100, 200);
                panel.SetSelectedAppearancePart("body", "character.body.02");

                Assert.AreEqual("character.body.01", metadata.Appearance.GetSelectedPartId("body"));
                Assert.AreEqual("character.eyes.blue", metadata.Appearance.GetSelectedPartId("eyes"));
            }
            finally
            {
                Object.DestroyImmediate(panel.gameObject);
            }
        }

        [Test]
        public void Show_BuildsCharacterSelectionControls()
        {
            var panel = new GameObject("SaveSlotSelectPanelTest").AddComponent<SaveSlotSelectPanel>();
            try
            {
                panel.Show();

                Assert.IsNotNull(GameObject.Find("CharacterSelectionPanel"));
                Assert.IsNotNull(GameObject.Find("BodyNextButton"));
                Assert.IsNotNull(GameObject.Find("HairNextButton"));
                Assert.IsNotNull(GameObject.Find("OutfitNextButton"));
                Assert.IsNotNull(GameObject.Find("EyesNextButton"));
                Assert.IsNotNull(GameObject.Find("AccessoryNextButton"));
            }
            finally
            {
                panel.Hide();
                Object.DestroyImmediate(panel.gameObject);
                DestroyIfFound("SaveSlotCanvas");
                DestroyIfFound("EventSystem");
            }
        }

        [Test]
        public void FormatCharacterPreview_IncludesCharacterVariants()
        {
            var character = new CharacterCustomization { BodyVariant = 1, HairVariant = 2, OutfitVariant = 3 };
            var metadata = new SaveSlotMetadata { Character = character };
            metadata.Appearance.SetSelectedPart("body", "character.body.01");

            string preview = InvokeFormatCharacterPreview(metadata);

            StringAssert.Contains("Body 1", preview);
            StringAssert.Contains("Hair 2", preview);
            StringAssert.Contains("Outfit 3", preview);
            StringAssert.Contains("character.body.01", preview);
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

        private static void DestroyIfFound(string name)
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
