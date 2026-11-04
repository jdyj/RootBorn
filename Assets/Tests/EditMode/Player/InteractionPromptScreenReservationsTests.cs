using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.UI.Objectives;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.Player
{
    // Deliberately reflect the proposed service: the pre-implementation RED must
    // be an assertion about its absence, not a C# compile failure.
    public sealed class InteractionPromptScreenReservationsTests
    {
        private const string ServiceName = "Rootborn.Game.Player.InteractionPromptScreenReservations";
        private const float Epsilon = 0.015f;
        private readonly List<Object> _owned = new List<Object>();
        private readonly List<Vector2> _scratch = new List<Vector2>();

        private static Type Service()
        {
            var type = typeof(PlayerInteractionRouter).Assembly.GetType(ServiceName, false);
            Assert.That(type, Is.Not.Null, "Missing " + ServiceName + "; expected RED before production implementation.");
            return type;
        }

        private static MethodInfo Method(string name, params Type[] parameters)
        {
            var method = Service().GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, parameters, null);
            Assert.That(method, Is.Not.Null, "Missing agreed public API " + ServiceName + "." + name);
            return method;
        }

        private static void Register(Object owner, Func<Rect?> provider)
        {
            Method("Register", typeof(Object), typeof(Func<Rect?>)).Invoke(null, new object[] { owner, provider });
        }

        private static void Unregister(Object owner)
        {
            Method("Unregister", typeof(Object)).Invoke(null, new object[] { owner });
        }

        private static List<Rect> Collect()
        {
            var result = new List<Rect>();
            Method("CollectRects", typeof(List<Rect>)).Invoke(null, new object[] { result });
            return result;
        }

        private bool Fit(Rect prompt, Rect viewport, IReadOnlyList<Rect> blockers, float padding, out Rect fitted, out float scale)
        {
            var args = new object[] { prompt, viewport, blockers, padding, _scratch, default(Rect), 0f };
            var result = Method("TryFitHorizontal", typeof(Rect), typeof(Rect), typeof(IReadOnlyList<Rect>), typeof(float),
                typeof(List<Vector2>), typeof(Rect).MakeByRefType(), typeof(float).MakeByRefType()).Invoke(null, args);
            fitted = (Rect)args[5]; scale = (float)args[6];
            Assert.That(result, Is.TypeOf<bool>());
            return (bool)result;
        }

        private GameObject Own(string name, params Type[] components)
        {
            var go = new GameObject("PromptReservationFixture-" + name, components);
            _owned.Add(go);
            return go;
        }

        [TearDown]
        public void ReleaseOnlyOwnedObjectsAndReservations()
        {
            var type = typeof(PlayerInteractionRouter).Assembly.GetType(ServiceName, false);
            var unregister = type == null ? null : type.GetMethod("Unregister", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Object) }, null);
            for (int i = _owned.Count - 1; i >= 0; i--)
            {
                if (unregister != null) unregister.Invoke(null, new object[] { _owned[i] });
                if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            }
            _owned.Clear(); _scratch.Clear();
        }

        [Test]
        public void ApiLivesInGamePlayerAndResetsAtSubsystemRegistration()
        {
            var type = Service();
            Assert.That(type.IsPublic && type.IsAbstract && type.IsSealed, Is.True, "One public static service, not another MonoBehaviour.");
            Assert.That(type.Assembly, Is.SameAs(typeof(PlayerInteractionRouter).Assembly));
            Assert.That(Method("Register", typeof(Object), typeof(Func<Rect?>)).ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(Method("Unregister", typeof(Object)).ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(Method("CollectRects", typeof(List<Rect>)).ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Any(method =>
                method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false)
                    .Cast<RuntimeInitializeOnLoadMethodAttribute>().Any(attribute => attribute.loadType == RuntimeInitializeLoadType.SubsystemRegistration)), Is.True,
                "Reset stale static registrations at subsystem start. Do not invoke that global reset from this fixture.");
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                foreach (var parameter in method.GetParameters())
                    Assert.That(parameter.ParameterType.FullName ?? "", Does.Not.Contain("Rootborn.UI").And.Not.Contain("UnityEngine.UI"));
        }

        [TestCase(0f)]
        [TestCase(21.12f)]
        public void BothApproachPosesAvoidVisibleCard(float upwardScreenDelta)
        {
            // Regression geometry derived from prior screenshot/source scale;
            // the second pose is a scenario, not a claim it was already rendered.
            var prompt = new Rect(596f, 396.8f + upwardScreenDelta, 284f, 24f);
            var viewport = new Rect(0f, 0f, 1470f, 714f);
            var card = new Rect(823.8125f, 410.8125f, 490f, 153.125f);
            Assert.That(Fit(prompt, viewport, new[] { card }, 8f, out var fitted, out var scale), Is.True);
            Assert.That(scale, Is.EqualTo(1f).Within(Epsilon));
            Assert.That(fitted.center.y, Is.EqualTo(prompt.center.y).Within(Epsilon));
            Assert.That(fitted.xMax, Is.EqualTo(card.xMin - 8f).Within(Epsilon), "Nearest complete free interval is to the card's left.");
            AssertSafe(fitted, viewport, new[] { card }, 8f);
        }

        [Test]
        public void VerticallySeparateBlockerLeavesAlreadySafePromptUnchanged()
        {
            var prompt = new Rect(100f, 100f, 180f, 24f);
            Assert.That(Fit(prompt, new Rect(0f, 0f, 800f, 600f), new[] { new Rect(80f, 300f, 500f, 80f) }, 8f, out var fitted, out var scale), Is.True);
            AssertRect(fitted, prompt); Assert.That(scale, Is.EqualTo(1f));
        }

        [Test]
        public void NonzeroViewportOriginClampsXWithoutShrinkingOrChangingY()
        {
            var viewport = new Rect(100f, 50f, 500f, 300f);
            var prompt = new Rect(40f, 150f, 180f, 24f);
            Assert.That(Fit(prompt, viewport, Array.Empty<Rect>(), 8f, out var fitted, out var scale), Is.True);
            Assert.That(fitted.xMin, Is.EqualTo(108f).Within(Epsilon));
            Assert.That(fitted.center.y, Is.EqualTo(prompt.center.y).Within(Epsilon));
            Assert.That(scale, Is.EqualTo(1f)); AssertSafe(fitted, viewport, Array.Empty<Rect>(), 8f);
        }

        [Test]
        public void UnsortedOverlappingBlockersChooseNearestFullWidthIntervalBeforeShrink()
        {
            var prompt = new Rect(320f, 140f, 180f, 24f);
            var viewport = new Rect(0f, 0f, 800f, 400f);
            var blockers = new[] { new Rect(450f, 120f, 150f, 80f), new Rect(280f, 130f, 200f, 60f) };
            Assert.That(Fit(prompt, viewport, blockers, 8f, out var fitted, out var scale), Is.True);
            Assert.That(scale, Is.EqualTo(1f));
            Assert.That(fitted.xMax, Is.EqualTo(272f).Within(Epsilon));
            AssertSafe(fitted, viewport, blockers, 8f);
        }

        [Test]
        public void NarrowFreeIntervalUniformlyScalesBothDimensionsAndKeepsCenterY()
        {
            var prompt = new Rect(160f, 138f, 180f, 24f);
            var viewport = new Rect(0f, 0f, 500f, 300f);
            var blockers = new[] { new Rect(330f, 100f, 170f, 100f), new Rect(0f, 100f, 170f, 100f) };
            Assert.That(Fit(prompt, viewport, blockers, 8f, out var fitted, out var scale), Is.True);
            Assert.That(scale, Is.EqualTo(0.8f).Within(Epsilon));
            Assert.That(fitted.width, Is.EqualTo(144f).Within(Epsilon));
            Assert.That(fitted.height, Is.EqualTo(19.2f).Within(Epsilon));
            Assert.That(fitted.center, Is.EqualTo(prompt.center));
            AssertSafe(fitted, viewport, blockers, 8f);
        }

        [Test]
        public void FullCoverageReturnsFalseWithoutInventingVerticalOrHiddenFallback()
        {
            var prompt = new Rect(100f, 100f, 180f, 24f);
            var viewport = new Rect(0f, 0f, 500f, 300f);
            Assert.That(Fit(prompt, viewport, new[] { viewport }, 8f, out var fitted, out var scale), Is.False);
            AssertRect(fitted, prompt); Assert.That(scale, Is.EqualTo(1f));
        }

        [Test]
        public void VerticallyOutsideViewportReturnsFalseRatherThanChangingAuthoredY()
        {
            var prompt = new Rect(100f, -10f, 180f, 24f);
            Assert.That(Fit(prompt, new Rect(0f, 0f, 500f, 300f), Array.Empty<Rect>(), 8f, out var fitted, out var scale), Is.False);
            AssertRect(fitted, prompt); Assert.That(scale, Is.EqualTo(1f));
        }

        [Test]
        public void ReusingScratchAfterBlockerDisappearsRestoresOriginalRectangle()
        {
            var prompt = new Rect(320f, 140f, 180f, 24f);
            var viewport = new Rect(0f, 0f, 800f, 400f);
            Assert.That(Fit(prompt, viewport, new[] { new Rect(280f, 130f, 320f, 60f) }, 8f, out var blocked, out _), Is.True);
            Assert.That(blocked.x, Is.Not.EqualTo(prompt.x));
            Assert.That(Fit(prompt, viewport, Array.Empty<Rect>(), 8f, out var fitted, out var scale), Is.True);
            AssertRect(fitted, prompt); Assert.That(scale, Is.EqualTo(1f));
        }

        [Test]
        public void SameOwnerReplacementAndNullableLiveProviderDoNotAccumulateReservations()
        {
            var baseline = Collect(); var owner = Own("replace");
            var first = new Rect(91001f, 92001f, 17f, 19f);
            var second = new Rect(93001f, 94001f, 23f, 29f);
            Register(owner, () => first);
            Assert.That(Collect().Count, Is.EqualTo(baseline.Count + 1));
            Rect? current = second; Register(owner, () => current);
            var collected = Collect(); Assert.That(collected.Contains(second), Is.True); Assert.That(collected.Contains(first), Is.False);
            Assert.That(collected.Count, Is.EqualTo(baseline.Count + 1));
            current = null; Assert.That(Collect(), Is.EquivalentTo(baseline));
            current = first; Assert.That(Collect().Contains(first), Is.True);
            var reused = new List<Rect> { second };
            Method("CollectRects", typeof(List<Rect>)).Invoke(null, new object[] { reused });
            Assert.That(reused.Contains(second), Is.False, "Collect clears the caller's prior contents.");
            Unregister(owner); Unregister(owner); Assert.That(Collect(), Is.EquivalentTo(baseline));
        }

        [Test]
        public void UnregisterOneOwnerPreservesOtherOwnerAndAmbientReservations()
        {
            var baseline = Collect(); var first = Own("first"); var other = Own("other");
            var a = new Rect(91002f, 92002f, 17f, 19f); var b = new Rect(93002f, 94002f, 23f, 29f);
            Register(first, () => a); Register(other, () => b); Unregister(first);
            var remaining = Collect(); Assert.That(remaining.Contains(b), Is.True); Assert.That(remaining.Contains(a), Is.False);
            Assert.That(remaining.Count, Is.EqualTo(baseline.Count + 1));
            Unregister(other); Assert.That(Collect(), Is.EquivalentTo(baseline));
        }

        [Test]
        public void DestroyedUnityOwnerIsPrunedBeforeItsProviderRuns()
        {
            var baseline = Collect(); var owner = Own("destroyed"); int calls = 0;
            Register(owner, () => { calls++; return new Rect(95003f, 96003f, 31f, 37f); });
            Object.DestroyImmediate(owner);
            Assert.That(Collect(), Is.EquivalentTo(baseline)); Assert.That(calls, Is.Zero);
            Assert.That(Collect(), Is.EquivalentTo(baseline)); Assert.That(calls, Is.Zero);
        }

        [TestCase(RenderMode.ScreenSpaceOverlay, false)]
        [TestCase(RenderMode.ScreenSpaceCamera, false)]
        [TestCase(RenderMode.ScreenSpaceCamera, true)]
        public void ActualTrackedHudReservesProjectedRootOnlyWhileVisibleAndEnabled(RenderMode mode, bool nestedCanvas)
        {
            var baseline = Collect();
            var canvasObject = Own("canvas", typeof(RectTransform), typeof(Canvas));
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = mode;
            if (mode == RenderMode.ScreenSpaceCamera)
            {
                var cameraObject = Own("canvas-camera", typeof(Camera));
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);
                var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true;
                canvas.worldCamera = camera; canvas.planeDistance = 5f;
            }
            Transform hudParent = canvas.transform;
            if (nestedCanvas)
            {
                var nested = Own("nested-canvas", typeof(RectTransform), typeof(Canvas));
                nested.transform.SetParent(canvas.transform, false);
                hudParent = nested.transform;
            }
            var host = Own("tracked-host", typeof(RectTransform));
            host.transform.SetParent(hudParent, false);
            var hud = host.AddComponent<TrackedObjectiveHud>(); _owned.Add(hud);
            // This is an isolated EditMode fixture, not an automatic PlayMode lifecycle claim.
            InvokeOwnedLifecycle(hud, "OnEnable");
            Assert.That(Collect(), Is.EquivalentTo(baseline), "Enabled but not built/visible reserves nothing.");
            var item = new ObjectiveJournalItem("fixture.prompt.geometry", "Goals", "Tracked goal", "", "Active", "1 / 2", "Next step", false, 0);
            hud.Show(item); Canvas.ForceUpdateCanvases();
            var root = (RectTransform)host.transform.Find("TrackedObjectiveHudRoot");
            var expected = Project(root, canvas);
            AssertReservationAdded(baseline, expected);
            hud.Show(item); Canvas.ForceUpdateCanvases(); AssertReservationAdded(baseline, Project(root, canvas));
            hud.Hide(); Assert.That(Collect(), Is.EquivalentTo(baseline), "Hide deactivates the root without disabling the component.");
            hud.Show(item); Canvas.ForceUpdateCanvases(); AssertReservationAdded(baseline, Project(root, canvas));
            var visibility = root.GetComponent<CanvasGroup>(); visibility.alpha = 0f;
            Assert.That(Collect(), Is.EquivalentTo(baseline), "Fully hidden decoration does not reserve an invisible rectangle.");
            visibility.alpha = 1f; AssertReservationAdded(baseline, Project(root, canvas));
            hud.enabled = false; InvokeOwnedLifecycle(hud, "OnDisable"); Assert.That(Collect(), Is.EquivalentTo(baseline));
            hud.enabled = true; InvokeOwnedLifecycle(hud, "OnEnable"); Canvas.ForceUpdateCanvases(); AssertReservationAdded(baseline, Project(root, canvas));
            InvokeOwnedLifecycle(hud, "OnDestroy");
            Assert.That(Collect(), Is.EquivalentTo(baseline), "OnDestroy must remove this still-live owned provider, before destruction can hide/prune it.");
            Object.DestroyImmediate(host); Assert.That(Collect(), Is.EquivalentTo(baseline));
        }

        private void InvokeOwnedLifecycle(TrackedObjectiveHud hud, string name)
        {
            Assert.That(Application.isPlaying, Is.False, "Manual lifecycle simulation is confined to this isolated EditMode fixture.");
            Assert.That(_owned.Contains(hud), Is.True, "Never invoke lifecycle on a host or foreign component.");
            var method = typeof(TrackedObjectiveHud).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Missing actual HUD lifecycle method " + name);
            method.Invoke(hud, null);
        }

        private static Rect Project(RectTransform rect, Canvas canvas)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var effectiveCanvas = canvas.rootCanvas;
            var camera = effectiveCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : effectiveCanvas.worldCamera;
            var points = corners.Select(point => RectTransformUtility.WorldToScreenPoint(camera, point)).ToArray();
            return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
        }

        private static void AssertReservationAdded(List<Rect> baseline, Rect expected)
        {
            var actual = Collect(); Assert.That(actual.Count, Is.EqualTo(baseline.Count + 1));
            foreach (var value in baseline) Assert.That(actual, Does.Contain(value));
            Assert.That(actual.Any(value => Mathf.Abs(value.x - expected.x) < Epsilon && Mathf.Abs(value.y - expected.y) < Epsilon &&
                Mathf.Abs(value.width - expected.width) < Epsilon && Mathf.Abs(value.height - expected.height) < Epsilon), Is.True, "Reservation must match projected actual root corners.");
        }

        private static void AssertRect(Rect actual, Rect expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Epsilon)); Assert.That(actual.y, Is.EqualTo(expected.y).Within(Epsilon));
            Assert.That(actual.width, Is.EqualTo(expected.width).Within(Epsilon)); Assert.That(actual.height, Is.EqualTo(expected.height).Within(Epsilon));
        }

        private static void AssertSafe(Rect fitted, Rect viewport, IReadOnlyList<Rect> blockers, float padding)
        {
            Assert.That(fitted.xMin, Is.GreaterThanOrEqualTo(viewport.xMin + padding - Epsilon));
            Assert.That(fitted.xMax, Is.LessThanOrEqualTo(viewport.xMax - padding + Epsilon));
            Assert.That(fitted.yMin, Is.GreaterThanOrEqualTo(viewport.yMin + padding - Epsilon));
            Assert.That(fitted.yMax, Is.LessThanOrEqualTo(viewport.yMax - padding + Epsilon));
            for (int i = 0; i < blockers.Count; i++)
            {
                var blocker = blockers[i];
                bool separated = fitted.xMax <= blocker.xMin - padding + Epsilon || fitted.xMin >= blocker.xMax + padding - Epsilon ||
                    fitted.yMax <= blocker.yMin - padding + Epsilon || fitted.yMin >= blocker.yMax + padding - Epsilon;
                Assert.That(separated, Is.True, "Prompt intersects padded reservation " + i);
            }
        }
    }
}
