using NUnit.Framework;
using Rootborn.UI.Modern;
using Rootborn.UI.Objectives;
using UnityEngine;

namespace Rootborn.Tests.EditMode.UI.Objectives
{
    public sealed class ObjectiveJournalRuntimeProviderTests
    {
        [Test]
        public void ModernUiPanelAutoInstaller_InstallsRuntimeObjectiveProviderOnCanvasHost()
        {
            var canvasGo = new GameObject("ObjectiveJournalRuntimeProviderFixture", typeof(RectTransform), typeof(Canvas));
            try
            {
                ModernUiPanelAutoInstaller.InstallOnCanvas(canvasGo.GetComponent<Canvas>(), null);

                var provider = canvasGo.GetComponent<ObjectiveJournalRuntimeProvider>();
                var journal = canvasGo.GetComponentInChildren<ObjectiveJournalPanel>(true);
                var hud = canvasGo.GetComponentInChildren<TrackedObjectiveHud>(true);

                Assert.IsNotNull(provider, "Objective Journal must have one runtime provider installed by the common UI installer.");
                Assert.IsNotNull(journal, "Objective Journal panel must still be installed by the common UI installer.");
                Assert.IsNotNull(hud, "TrackedObjectiveHud must still be the single always-visible objective HUD owner.");
                Assert.AreSame(journal, provider.Panel);
                Assert.AreSame(hud, provider.TrackedHud);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasGo);
            }
        }
    }
}
