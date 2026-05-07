using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Family;
using Rootborn.Game.Managers;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.UI.MainMenu;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode
{
    public sealed class CharacterPartCompositionPlayModeTests
    {
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ActiveSaveContext.Clear();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-character-parts-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            DestroyIfFound("Player");
            DestroyIfFound("[Resources]");
            DestroyIfFound("[Grid]");
            DestroyIfFound("[FarmAutoFiller]");
            DestroyIfFound("SaveSlotSelectPanel");
            DestroyIfFound("SaveSlotCanvas");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            SaveSlotSelectPanel.SetSaveRootForTests(null);
            DestroyIfFound("Player");
            DestroyIfFound("[Resources]");
            DestroyIfFound("[Grid]");
            DestroyIfFound("[FarmAutoFiller]");
            DestroyIfFound("SaveSlotSelectPanel");
            DestroyIfFound("SaveSlotCanvas");
            if (!string.IsNullOrEmpty(_saveRoot) && Directory.Exists(_saveRoot))
            {
                Directory.Delete(_saveRoot, true);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CHAR_PART_004_SelectionPreviewUsesLayeredPartSprites()
        {
            yield return Managers.BootstrapAsync().AsIEnumerator();
            var panel = SaveSlotSelectPanel.EnsureInScene();
            panel.Show();
            yield return null;

            ClickButtonNamed("BodyNextButton");
            ClickButtonNamed("EyesNextButton");
            ClickButtonNamed("HairNextButton");
            ClickButtonNamed("OutfitNextButton");
            ClickButtonNamed("AccessoryNextButton");
            yield return null;

            var selectionPanel = GameObject.Find("CharacterSelectionPanel");
            Assert.IsNotNull(selectionPanel);
            AssertPreviewPart(selectionPanel.transform, "PreviewPart_body", "Body_1_r0_c0");
            AssertPreviewPart(selectionPanel.transform, "PreviewPart_eyes", "Eyes_Blue_r0_c0");
            AssertPreviewPart(selectionPanel.transform, "PreviewPart_hair", "Hairstyle_Short_Blonde_r0_c0");
            AssertPreviewPart(selectionPanel.transform, "PreviewPart_outfit", "Outfit_Braces_Brown_r0_c0");
            AssertPreviewPart(selectionPanel.transform, "PreviewPart_accessory", "Accessory_Bamboo_Hat_Brown_r0_c0");
        }

        [UnityTest]
        public IEnumerator CHAR_PART_004_NewSlotUiSavesSelectedAppearanceBeforeFarmEntry()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            var panel = SaveSlotSelectPanel.EnsureInScene();
            panel.Show();
            yield return null;

            ClickButtonNamed("BodyNextButton");
            ClickButtonNamed("EyesNextButton");
            ClickButtonNamed("HairNextButton");
            ClickButtonNamed("OutfitNextButton");
            ClickButtonNamed("AccessoryNextButton");
            ClickButtonNamed("NewGameButton");
            yield return WaitForScene("Farm", 10f);

            var service = new SaveService("slot-0", _saveRoot);
            var metadata = service.LoadMetadata("slot-0");
            Assert.IsNotNull(metadata);
            Assert.AreEqual("character.body.01", metadata.Appearance.GetSelectedPartId("body"));
            Assert.AreEqual("character.eyes.blue", metadata.Appearance.GetSelectedPartId("eyes"));
            Assert.AreEqual("character.hair.short.blonde", metadata.Appearance.GetSelectedPartId("hair"));
            Assert.AreEqual("character.outfit.braces.brown", metadata.Appearance.GetSelectedPartId("outfit"));
            Assert.AreEqual("character.accessory.bamboo.brown", metadata.Appearance.GetSelectedPartId("accessory"));
        }

        [UnityTest]
        public IEnumerator CHAR_PART_005_FarmPlayerRestoresSavedLayeredAppearance()
        {
            var metadata = MakeSavedAppearanceMetadata();
            ActiveSaveContext.Set(metadata);

            yield return LoadFarmAndBootstrap();
            yield return WaitForPartSprites();

            var player = GameObject.Find("Player");
            AssertSavedLayeredPlayer(player);
        }

        [UnityTest]
        public IEnumerator CHAR_PART_005_FarmPlayerLayeredRenderScreenshot_WritesEvidence()
        {
            var metadata = MakeSavedAppearanceMetadata();
            ActiveSaveContext.Set(metadata);

            yield return LoadFarmAndBootstrap();
            yield return WaitForPartSprites();
            yield return new WaitForEndOfFrame();

            var player = GameObject.Find("Player");
            AssertSavedLayeredPlayer(player);
            Assert.GreaterOrEqual(CountVisiblePartRenderers(player), 5);

            string screenshotPath = CapturePlayerScreenshot("character-layered-player.png");
            Assert.IsTrue(File.Exists(screenshotPath), "Expected screenshot at " + screenshotPath);
            Assert.Greater(CountVisiblePixels(screenshotPath), 1000);
        }

        [UnityTest]
        public IEnumerator CHAR_PART_006_FarmPlayerRightMotionKeepsLayeredCharacterUpright()
        {
            var metadata = MakeSavedAppearanceMetadata();
            ActiveSaveContext.Set(metadata);

            yield return LoadFarmAndBootstrap();
            yield return WaitForPartSprites();

            var player = GameObject.Find("Player");
            AssertSavedLayeredPlayer(player);
            Assert.AreEqual(Vector3.one, player.transform.localScale, "16x16 part-composed player must stay at 1x scale in Farm.");
            Assert.AreEqual(0f, player.transform.localEulerAngles.z, 0.001f, "Farm player must not spin when facing right.");

            var rb = player.GetComponent<Rigidbody2D>();
            Assert.IsNotNull(rb, "Farm player must have Rigidbody2D for real in-game movement.");
            Assert.IsTrue((rb.constraints & RigidbodyConstraints2D.FreezeRotation) == RigidbodyConstraints2D.FreezeRotation,
                "Farm player Rigidbody2D must freeze rotation so right movement cannot spin the sprite.");

            var controller = player.GetComponent<PlayerController>();
            Assert.IsNotNull(controller);
            SetPrivateField(controller, "_lastFacing", new Vector2(1f, 0f));
            InvokePrivate(controller, "Update");

            Assert.AreEqual(0f, player.transform.localEulerAngles.z, 0.001f, "Right-facing controller update must not rotate the player root.");
            AssertPartUprightAndFlipped(player, "Part_body");
            AssertPartUprightAndFlipped(player, "Part_eyes");
            AssertPartUprightAndFlipped(player, "Part_hair");
            AssertPartUprightAndFlipped(player, "Part_outfit");
            AssertPartUprightAndFlipped(player, "Part_accessory");
        }

        [UnityTest]
        public IEnumerator CHAR_PART_007_EquippedToolSpriteStaysVisibleWhileFacingRight()
        {
            var metadata = MakeSavedAppearanceMetadata();
            ActiveSaveContext.Set(metadata);

            yield return LoadFarmAndBootstrap();
            yield return WaitForPartSprites();
            yield return Managers.Resource.LoadSubSpriteAsync("sprites/player/tool/axe-side", "Axe_Side_0").AsIEnumerator();

            var player = GameObject.Find("Player");
            AssertSavedLayeredPlayer(player);
            var inventory = player.GetComponent<PlayerInventory>();
            Assert.IsNotNull(inventory);
            if (inventory.FindById("StoneAxe") == null)
            {
                Assert.IsTrue(inventory.TryAddById("StoneAxe", 1), "StoneAxe item definition must be registered for held-tool rendering.");
            }
            var axe = inventory.FindById("StoneAxe");
            Assert.IsNotNull(axe, "Held-tool rendering test must be able to put StoneAxe in the player inventory.");

            var controller = player.GetComponent<PlayerController>();
            Assert.IsNotNull(controller);
            InvokePrivate(controller, "Update");
            inventory.EquipTool(axe);
            yield return null;

            SetPrivateField(controller, "_lastFacing", new Vector2(1f, 0f));
            InvokePrivate(controller, "Update");

            var toolRenderer = AssertToolRenderer(player);
            Assert.IsTrue(toolRenderer.enabled, "Equipped tool renderer must remain enabled while the player is facing/moving right.");
            Assert.IsNotNull(toolRenderer.sprite, "Equipped tool renderer must keep a sprite while the player is facing/moving right.");
            Assert.AreEqual("Axe_Side_0", toolRenderer.sprite.name);
            Assert.IsTrue(toolRenderer.flipX, "Right-facing tool sprite must flip with the character instead of rotating.");
            AssertPartUprightAndFlipped(player, "Part_body");
            AssertPartUprightAndFlipped(player, "Part_outfit");
        }

        private static SaveSlotMetadata MakeSavedAppearanceMetadata()
        {
            var metadata = new SaveSlotMetadata
            {
                SlotId = "character-parts-playmode",
                DisplayName = "character-parts-playmode",
                CreatedAtUtcTicks = 1,
                UpdatedAtUtcTicks = 1,
                WorldSeed = 2026050701,
                TileSeed = 2026050702,
            };
            metadata.Appearance.SetSelectedPart("body", "character.body.02");
            metadata.Appearance.SetSelectedPart("eyes", "character.eyes.brown");
            metadata.Appearance.SetSelectedPart("hair", "character.hair.short.brown_dark");
            metadata.Appearance.SetSelectedPart("outfit", "character.outfit.braces.green");
            metadata.Appearance.SetSelectedPart("accessory", "character.accessory.straw.black");
            return metadata;
        }

        private static void AssertSavedLayeredPlayer(GameObject player)
        {
            Assert.IsNotNull(player);
            var composer = player.GetComponent<CharacterPartComposer>();
            Assert.IsNotNull(composer);
            Assert.IsNotNull(player.GetComponent<CharacterPartAnimator>());
            var rootRenderer = player.GetComponent<SpriteRenderer>();
            Assert.IsTrue(rootRenderer == null || !rootRenderer.enabled || rootRenderer.sprite == null,
                "Player root renderer must not display a complete single-character sprite when part layers are active.");

            AssertLayerSprite(player, "Part_body", "Body_2_r0_c0");
            AssertLayerSprite(player, "Part_eyes", "Eyes_Brown_r0_c0");
            AssertLayerSprite(player, "Part_hair", "Hairstyle_Short_Brown_Dark_r0_c0");
            AssertLayerSprite(player, "Part_outfit", "Outfit_Braces_Green_r0_c0");
            AssertLayerSprite(player, "Part_accessory", "Accessory_Straw_Hat_Black_r0_c0");
        }

        private static IEnumerator LoadFarmAndBootstrap()
        {
            var asyncLoad = SceneManager.LoadSceneAsync("Farm");
            while (!asyncLoad.isDone) yield return null;

            var bootstrapTask = Managers.BootstrapAsync();
            float elapsed = 0f;
            while (!bootstrapTask.IsCompleted && elapsed < 10f)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
            Assert.IsTrue(bootstrapTask.IsCompleted, "Managers.BootstrapAsync did not complete within timeout.");

            var go = new GameObject("[FarmAutoFiller]");
            var filler = go.AddComponent<FarmAutoFiller>();
            filler.FillIfEmpty();
        }

        private static IEnumerator WaitForPartSprites()
        {
            float elapsed = 0f;
            while (elapsed < 10f)
            {
                yield return null;
                elapsed += Time.deltaTime;
                var player = GameObject.Find("Player");
                if (player == null) continue;
                var body = player.transform.Find("Part_body");
                var accessory = player.transform.Find("Part_accessory");
                var bodyRenderer = body != null ? body.GetComponent<SpriteRenderer>() : null;
                var accessoryRenderer = accessory != null ? accessory.GetComponent<SpriteRenderer>() : null;
                if (bodyRenderer != null && bodyRenderer.sprite != null && accessoryRenderer != null && accessoryRenderer.sprite != null)
                {
                    yield break;
                }
            }
        }

        private static IEnumerator WaitForScene(string sceneName, float timeout)
        {
            float elapsed = 0f;
            while (SceneManager.GetActiveScene().name != sceneName && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name);
        }

        private static void ClickButtonNamed(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, name);
            var button = go.GetComponent<Button>();
            Assert.IsNotNull(button, name);
            button.onClick.Invoke();
        }

        private static void AssertPreviewPart(Transform root, string childName, string spriteName)
        {
            var child = root.Find("CharacterPreviewImage/" + childName);
            Assert.IsNotNull(child, childName);
            var image = child.GetComponent<Image>();
            Assert.IsNotNull(image, childName);
            Assert.IsNotNull(image.sprite, childName);
            Assert.AreEqual(spriteName, image.sprite.name, childName);
        }

        private static void AssertLayerSprite(GameObject player, string childName, string spriteName)
        {
            var child = player.transform.Find(childName);
            Assert.IsNotNull(child, childName);
            var renderer = child.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(renderer, childName);
            Assert.IsNotNull(renderer.sprite, childName);
            Assert.AreEqual(spriteName, renderer.sprite.name, childName);
        }

        private static void AssertPartUprightAndFlipped(GameObject player, string childName)
        {
            var child = player.transform.Find(childName);
            Assert.IsNotNull(child, childName);
            Assert.AreEqual(0f, child.localEulerAngles.z, 0.001f, childName + " must not rotate when facing right.");
            var renderer = child.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(renderer, childName);
            Assert.IsTrue(renderer.flipX, childName + " must flip horizontally when facing right.");
        }

        private static SpriteRenderer AssertToolRenderer(GameObject player)
        {
            var child = player.transform.Find("Part_tool");
            Assert.IsNotNull(child, "Part_tool");
            var renderer = child.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(renderer, "Part_tool renderer");
            Assert.AreEqual(0f, child.localEulerAngles.z, 0.001f, "Part_tool must not rotate when facing right.");
            return renderer;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            method.Invoke(target, null);
        }

        private static int CountVisiblePartRenderers(GameObject player)
        {
            int count = 0;
            foreach (var renderer in player.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (!renderer.name.StartsWith("Part_")) continue;
                if (renderer.enabled && renderer.sprite != null) count++;
            }
            return count;
        }

        private static string CapturePlayerScreenshot(string fileName)
        {
            int width = Mathf.Max(1, Screen.width);
            int height = Mathf.Max(1, Screen.height);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            string directory = "Builds/Logs/character-parts";
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, fileName);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.Destroy(texture);
            return path;
        }

        private static int CountVisiblePixels(string screenshotPath)
        {
            var bytes = File.ReadAllBytes(screenshotPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(bytes);
            int count = 0;
            foreach (var pixel in texture.GetPixels32())
            {
                if (pixel.a > 0 && (pixel.r > 4 || pixel.g > 4 || pixel.b > 4))
                {
                    count++;
                }
            }

            Object.Destroy(texture);
            return count;
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

    internal static class TaskEnumeratorExtensions
    {
        public static IEnumerator AsIEnumerator(this System.Threading.Tasks.Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }
            Assert.IsFalse(task.IsFaulted, task.Exception != null ? task.Exception.ToString() : string.Empty);
        }
    }
}
