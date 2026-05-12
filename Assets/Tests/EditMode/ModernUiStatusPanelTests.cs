using NUnit.Framework;
using Rootborn.UI.Modern;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernUiStatusPanelTests
    {
        [Test]
        public void Show_BuildsTiledStatusWindowPortraitGaugesIconFrameAndButtons()
        {
            var type = System.Type.GetType("Rootborn.UI.Modern.ModernUiStatusPanel, Rootborn.UI");
            Assert.IsNotNull(type, "Missing Rootborn.UI.Modern.ModernUiStatusPanel.");

            var host = new GameObject("ModernUiStatusPanelFixture", typeof(RectTransform));
            try
            {
                var panel = (Component)host.AddComponent(type);
                panel.GetType().GetMethod("Show").Invoke(panel, null);

                Assert.IsTrue((bool)panel.GetType().GetProperty("IsVisible").GetValue(panel));
                AssertChildHasTiles(host.transform, "StatusTitleTab");
                AssertChildHasTiles(host.transform, "PortraitFrame");
                AssertChildHasTiles(host.transform, "StatusValueFrame");
                AssertChildHasTiles(host.transform, "EnergyGauge");
                AssertChildHasTiles(host.transform, "HealthGauge");
                AssertChildHasTiles(host.transform, "IconFrame");
                AssertChildHasTiles(host.transform, "StatusBottomButtons");
                Assert.GreaterOrEqual((int)panel.GetType().GetProperty("TileCount").GetValue(panel), 70);
            }
            finally
            {
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
