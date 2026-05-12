using System.Reflection;
using NUnit.Framework;
using Rootborn.UI.Modern;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernUiPanelInputRouterTests
    {
        [Test]
        public void Router_ExposesPanelToggleMethods()
        {
            var type = System.Type.GetType("Rootborn.UI.Modern.ModernUiPanelInputRouter, Rootborn.UI");
            Assert.IsNotNull(type, "Missing Rootborn.UI.Modern.ModernUiPanelInputRouter.");

            Assert.IsNotNull(FindBindMethod(type, 2));
            Assert.IsNotNull(FindBindMethod(type, 3));
            Assert.IsNotNull(type.GetMethod("ToggleInventoryPanel", BindingFlags.Instance | BindingFlags.Public));
            Assert.IsNotNull(type.GetMethod("ToggleStatusPanel", BindingFlags.Instance | BindingFlags.Public));
            Assert.IsNotNull(type.GetMethod("ToggleSettingsPanel", BindingFlags.Instance | BindingFlags.Public));
        }

        [Test]
        public void Router_TogglesModernPanelsWithoutStackingThem()
        {
            var host = new GameObject("ModernUiPanelInputRouterFixture", typeof(RectTransform));
            try
            {
                var inventory = host.AddComponent<ModernUiInventoryPanel>();
                var status = host.AddComponent<ModernUiStatusPanel>();
                var settings = host.AddComponent<SettingsPanel>();
                var type = System.Type.GetType("Rootborn.UI.Modern.ModernUiPanelInputRouter, Rootborn.UI");
                Assert.IsNotNull(type, "Missing Rootborn.UI.Modern.ModernUiPanelInputRouter.");
                var router = (Component)host.AddComponent(type);

                FindBindMethod(type, 3).Invoke(router, new object[] { inventory, status, settings });
                type.GetMethod("ToggleInventoryPanel").Invoke(router, null);

                Assert.IsTrue(inventory.IsVisible);
                Assert.IsFalse(status.IsVisible);
                Assert.IsFalse(settings.IsVisible);

                type.GetMethod("ToggleStatusPanel").Invoke(router, null);

                Assert.IsFalse(inventory.IsVisible);
                Assert.IsTrue(status.IsVisible);
                Assert.IsFalse(settings.IsVisible);

                type.GetMethod("ToggleSettingsPanel").Invoke(router, null);

                Assert.IsFalse(inventory.IsVisible);
                Assert.IsFalse(status.IsVisible);
                Assert.IsTrue(settings.IsVisible);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static MethodInfo FindBindMethod(System.Type type, int parameterCount)
        {
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.Name == "Bind" && method.GetParameters().Length == parameterCount)
                {
                    return method;
                }
            }

            return null;
        }
    }
}
