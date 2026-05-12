using System.Collections;
using System.Text;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.World;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.TownConcept
{
    public sealed class TownSceneBootTests
    {
        [UnityTest]
        public IEnumerator TownScene_LoadsAsPlayableTownBaseline()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return WaitForTownInstallers();

            Scene active = SceneManager.GetActiveScene();
            Assert.AreEqual("Town", active.name);
            Assert.IsTrue(active.rootCount > 0, "Town scene should have root objects.");

            var player = GameObject.Find("Player");
            Assert.IsNotNull(player, "Town scene should contain a Player.");
            Assert.IsNotNull(player.GetComponent<PlayerController>(), "Town Player should keep movement enabled.");
            Assert.IsNotNull(player.GetComponent<PlayerInventory>(), "Town Player should keep inventory for UI binding.");
            Assert.IsNotNull(player.GetComponent<Rigidbody2D>(), "Town Player should have 2D physics for movement.");
            Assert.IsNotNull(player.GetComponent<Collider2D>(), "Town Player should have collision for movement.");
            Assert.GreaterOrEqual(CountVisiblePartSprites(player), 5, "Town Player should render a complete layered character part composition. " + DescribeSpriteRenderers(player));
            AssertRootRendererDisabled(player);

            var guideNpc = GameObject.Find("GuideNpc");
            Assert.IsNotNull(guideNpc, "Town scene should contain a visible GuideNpc, matching the town UI/NPC reference flow.");
            Assert.IsTrue(HasVisibleSpriteRenderer(guideNpc), "Town GuideNpc should render an enabled non-null SpriteRenderer in PlayMode. " + DescribeSpriteRenderers(guideNpc));
            AssertNpcUsesImportedCharacterArt(guideNpc);

            Assert.IsNotNull(Camera.main, "Town scene should contain a Main Camera.");
            Assert.IsNotNull(Camera.main.GetComponent<CameraFollow>(), "Town camera should follow the Player.");

            Assert.IsNotNull(FindComponentInActiveScene<EventSystem>(), "Town scene should contain an EventSystem for UI input.");
            var canvas = FindComponentInActiveScene<Canvas>();
            Assert.IsNotNull(canvas, "Town scene should contain a Canvas.");
            Assert.IsTrue(canvas.gameObject.activeInHierarchy, "Town Canvas must stay active while panels are hidden.");
            Assert.IsNotNull(canvas.transform.Find("TownHud"), "Town Canvas should expose a visible HUD root.");
            Assert.GreaterOrEqual(CountActiveUiSprites(), 1, "Town should render at least one active UI Image backed by imported Modern UI sprites.");

            var router = canvas.GetComponent<ModernUiPanelInputRouter>();
            var inventoryPanel = canvas.GetComponentInChildren<ModernUiInventoryPanel>(true);
            var statusPanel = canvas.GetComponentInChildren<ModernUiStatusPanel>(true);
            Assert.IsNotNull(router, "Town Canvas should install the Modern UI input router.");
            Assert.IsNotNull(inventoryPanel, "Town Canvas should install the inventory panel.");
            Assert.IsNotNull(statusPanel, "Town Canvas should install the status panel.");
            Assert.AreNotSame(canvas.gameObject, inventoryPanel.gameObject, "Inventory panel must not disable the Canvas root when hidden.");
            Assert.AreNotSame(canvas.gameObject, statusPanel.gameObject, "Status panel must not disable the Canvas root when hidden.");

            router.ToggleStatusPanel();
            Assert.IsTrue(statusPanel.IsVisible, "Tab/status panel path should work in Town.");
            Assert.IsTrue(canvas.gameObject.activeInHierarchy, "Town Canvas must stay active after opening status.");
            router.ToggleInventoryPanel();
            Assert.IsTrue(inventoryPanel.IsVisible, "Inventory panel path should work in Town.");
            Assert.IsFalse(statusPanel.IsVisible, "Opening inventory should hide status panel.");
            Assert.IsTrue(canvas.gameObject.activeInHierarchy, "Town Canvas must stay active after toggling inventory.");

            var grid = FindComponentInActiveScene<Grid>();
            Assert.IsNotNull(grid, "Town scene should contain a Grid for tile placement.");
            Assert.GreaterOrEqual(grid.GetComponentsInChildren<Tilemap>(true).Length, 2, "Town Grid should expose layered Tilemaps for ground and decoration/collision.");

            Assert.IsNotNull(GameObject.Find("TownSpawnPoint") != null ? GameObject.Find("TownSpawnPoint").GetComponent<WorldSpawnPoint>() : null, "Town should expose a spawn point.");
            Assert.GreaterOrEqual(CountTownVisualCues(), 2, "Town scene should show at least two town-life visual cues.");
        }

        private static IEnumerator WaitForTownInstallers()
        {
            for (int i = 0; i < 240; i++)
            {
                var canvas = FindComponentInActiveScene<Canvas>();
                var player = GameObject.Find("Player");
                var guideNpc = GameObject.Find("GuideNpc");
                if (player != null &&
                    CountVisiblePartSprites(player) >= 5 &&
                    guideNpc != null &&
                    HasVisibleSpriteRenderer(guideNpc) &&
                    canvas != null &&
                    canvas.GetComponent<ModernUiPanelInputRouter>() != null &&
                    canvas.GetComponentInChildren<ModernUiInventoryPanel>(true) != null &&
                    CountActiveUiSprites() >= 1)
                {
                    yield break;
                }

                yield return null;
            }
        }

        private static int CountTownVisualCues()
        {
            int count = 0;
            count += HasRenderer("TownApartment") ? 1 : 0;
            count += HasRenderer("TownStreet") ? 1 : 0;
            count += HasRenderer("TownShop") ? 1 : 0;
            count += HasRenderer("TownCommunityBoard") ? 1 : 0;
            return count;
        }

        private static bool HasRenderer(string name)
        {
            var go = GameObject.Find(name);
            return go != null && go.GetComponentInChildren<Renderer>(true) != null;
        }

        private static bool HasVisibleSpriteRenderer(GameObject root)
        {
            if (root == null)
            {
                return false;
            }

            var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (IsVisibleSpriteRenderer(renderers[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountVisiblePartSprites(GameObject root)
        {
            if (root == null)
            {
                return 0;
            }

            int count = 0;
            var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer.gameObject.name.StartsWith("Part_") && renderer.gameObject.name != "Part_tool" && IsVisibleSpriteRenderer(renderer))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountActiveUiSprites()
        {
            int count = 0;
            var images = Object.FindObjectsByType<Image>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < images.Length; i++)
            {
                var image = images[i];
                if (image != null && image.isActiveAndEnabled && image.sprite != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsVisibleSpriteRenderer(SpriteRenderer renderer)
        {
            return renderer != null &&
                renderer.gameObject.activeInHierarchy &&
                renderer.enabled &&
                renderer.sprite != null &&
                renderer.color.a > 0.01f &&
                renderer.bounds.size.sqrMagnitude > 0.01f;
        }

        private static void AssertRootRendererDisabled(GameObject player)
        {
            var rootRenderer = player.GetComponent<SpriteRenderer>();
            Assert.IsTrue(rootRenderer == null || !rootRenderer.enabled || rootRenderer.sprite == null,
                "Town layered player must not display the complete fallback root sprite when part composition is active.");
        }

        private static void AssertNpcUsesImportedCharacterArt(GameObject npc)
        {
            var renderer = npc.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(renderer, "GuideNpc renderer");
            Assert.IsNotNull(renderer.sprite, "GuideNpc sprite");
            Assert.IsNotNull(renderer.sprite.texture, "GuideNpc texture");
            Assert.IsNotEmpty(renderer.sprite.texture.name, "GuideNpc must use imported character art, not an unnamed generated Texture2D placeholder.");
            StringAssert.DoesNotContain("Generated", renderer.sprite.texture.name);
        }

        private static string DescribeSpriteRenderers(GameObject root)
        {
            if (root == null)
            {
                return "root=null";
            }

            var sb = new StringBuilder();
            var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            sb.Append("renderers=").Append(renderers.Length).Append(";");
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                sb.Append(' ')
                    .Append(renderer.gameObject.name)
                    .Append(" active=").Append(renderer.gameObject.activeInHierarchy)
                    .Append(" enabled=").Append(renderer.enabled)
                    .Append(" sprite=").Append(renderer.sprite != null ? renderer.sprite.name : "null")
                    .Append(" texture=").Append(renderer.sprite != null && renderer.sprite.texture != null ? renderer.sprite.texture.name : "null")
                    .Append(" bounds=").Append(renderer.bounds.size);
            }

            return sb.ToString();
        }

        private static T FindComponentInActiveScene<T>() where T : Component
        {
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var match = roots[i].GetComponentInChildren<T>(true);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}