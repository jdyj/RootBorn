using NUnit.Framework;
using Rootborn.UI.Modern;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernUiStatusPanelTests
    {
        [Test]
        public void Show_BuildsTiledStudentStatusCardsAndCloseWithoutFakePortraitOrHealth()
        {
            var type = System.Type.GetType("Rootborn.UI.Modern.ModernUiStatusPanel, Rootborn.UI");
            Assert.IsNotNull(type, "Missing Rootborn.UI.Modern.ModernUiStatusPanel.");

            var host = new GameObject("ModernUiStatusPanelFixture", typeof(RectTransform));
            try
            {
                var panel = (Component)host.AddComponent(type);
                panel.GetType().GetMethod("Show").Invoke(panel, null);

                Assert.IsTrue((bool)panel.GetType().GetProperty("IsVisible").GetValue(panel));
                Assert.IsNotNull(host.GetComponent<ModernUiTileImage>(), "Status needs a coherent outer molding frame.");
                foreach (string name in new[] { "EnergyCard", "FocusCard", "StressCard", "StatusCloseButton" }) AssertChildHasTiles(host.transform, name);
                Assert.IsNull(host.transform.Find("PortraitFrame"), "Do not retain the empty placeholder portrait.");
                Assert.IsNull(host.transform.Find("HealthGauge"), "Read actual student vitals instead of a fake health gauge.");
                Assert.GreaterOrEqual((int)panel.GetType().GetProperty("TileCount").GetValue(panel), 5);

            }
            finally
            {
                host.GetComponent<ModernUiStatusPanel>().Hide();
                Object.DestroyImmediate(host);
            }
        }

        private static void AssertChildHasTiles(Transform root, string name)
        {
            var child = root.Find(name);
            Assert.IsNotNull(child, "Missing " + name + ".");
            var tiles = child.GetComponent<ModernUiTileImage>();
            Assert.IsNotNull(tiles, name + " must use ModernUiTileImage.");
            Assert.Greater(tiles.TileCount, 0, name + " must build 16x16 tiles.");
        }
    }
}
