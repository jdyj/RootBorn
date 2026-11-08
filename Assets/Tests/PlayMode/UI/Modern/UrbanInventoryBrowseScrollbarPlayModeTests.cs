using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.PlayMode.UI.Modern
{
    public sealed class UrbanInventoryBrowseScrollbarPlayModeTests
    {
        [UnityTest]
        public IEnumerator EmptyFilterKeepsScrollbarHiddenAcrossFramesAndRestoresBottomRowScrolling()
        {
            // A real screen canvas with a raycaster and an EventSystem: the tabs are clicked with a mouse device.
            var canvas = new GameObject("InventoryScrollbarFixture", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            if (UnityEngine.EventSystems.EventSystem.current == null)
                new GameObject("InventoryScrollbarEventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule)).transform.SetParent(canvas.transform, false);
            var root = new GameObject("Inventory", typeof(RectTransform), typeof(ModernUiInventoryPanel));
            root.transform.SetParent(canvas.transform, false);
            var player = new GameObject("InventoryScrollbarOwner", typeof(PlayerInventory));
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            try
            {
                SetField(item, "_id", "fixture.scrollbar.resource");
                SetField(item, "_displayKey", "Scrollbar resource");
                SetField(item, "_category", ItemCategory.Resource);
                SetField(item, "_maxStack", 1);
                var owner = player.GetComponent<PlayerInventory>();
                SetField(owner, "_inventory", new Inventory());
                owner.Inventory.Add(item, Inventory.MaxSlots);
                string before = JsonUtility.ToJson(owner.ToSaveData());
                var panel = root.GetComponent<ModernUiInventoryPanel>();
                panel.Bind(owner);
                panel.Show();
                var scroll = root.GetComponentsInChildren<ScrollRect>(true).Single();
                var pool = scroll.content.Cast<Transform>().Select(t => t.GetInstanceID()).ToArray();
                Assert.That(pool.Length, Is.EqualTo(Inventory.MaxSlots));
                yield return null;
                yield return null;
                Assert.That(scroll.verticalScrollbar.gameObject.activeInHierarchy, Is.True);

                // Refresh's immediate SetActive(false) is insufficient: ScrollRect owns visibility in LateUpdate.
                yield return Rootborn.Tests.PlayMode.EndToEnd.Support.PlayerFlowE2EDriver.ClickButton(root.transform.Find("Tab_1").GetComponent<Button>());
                yield return null;
                yield return null;
                Assert.That(scroll.verticalScrollbar.gameObject.activeInHierarchy, Is.False,
                    "An empty Tools category must remain free of a scrollbar after ScrollRect's frame updates.");
                Assert.That(scroll.content.gameObject.activeInHierarchy, Is.False);
                Assert.That(scroll.viewport.Find("EmptyState").gameObject.activeInHierarchy, Is.True);

                yield return Rootborn.Tests.PlayMode.EndToEnd.Support.PlayerFlowE2EDriver.ClickButton(root.transform.Find("Tab_0").GetComponent<Button>());
                yield return null;
                yield return null;
                Assert.That(scroll.verticalScrollbar.gameObject.activeInHierarchy, Is.True);
                var search = root.transform.Find("SearchField").GetComponent<InputField>();
                search.text = "no matching owned name";
                yield return null;
                yield return null;
                Assert.That(scroll.verticalScrollbar.gameObject.activeInHierarchy, Is.False,
                    "A no-match name query must also keep the scrollbar hidden after frame updates.");

                search.text = string.Empty;
                yield return null;
                yield return null;
                Assert.That(scroll.isActiveAndEnabled, Is.True);
                Assert.That(scroll.vertical, Is.True);
                Assert.That(scroll.verticalScrollbar.gameObject.activeInHierarchy, Is.True);
                Assert.That(scroll.content.gameObject.activeInHierarchy, Is.True);
                Assert.That(scroll.content.Cast<Transform>().Select(t => t.GetInstanceID()).ToArray(), Is.EqualTo(pool));
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition = 0f;
                yield return null;
                yield return null;
                var last = (RectTransform)scroll.content.GetChild(Inventory.MaxSlots - 1);
                var corners = new Vector3[4];
                last.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var point = scroll.viewport.InverseTransformPoint(corner);
                    Assert.That(point.x, Is.InRange(scroll.viewport.rect.xMin - .1f, scroll.viewport.rect.xMax + .1f));
                    Assert.That(point.y, Is.InRange(scroll.viewport.rect.yMin - .1f, scroll.viewport.rect.yMax + .1f));
                }
                Assert.That(JsonUtility.ToJson(owner.ToSaveData()), Is.EqualTo(before));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(item);
            }
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Fixture isolation seam changed: " + name);
            field.SetValue(target, value);
        }
    }
}
