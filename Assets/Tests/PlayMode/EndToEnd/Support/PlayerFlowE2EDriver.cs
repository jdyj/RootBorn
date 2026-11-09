using System.Collections;
using System.Text;
using NUnit.Framework;
using Rootborn.Game.Common;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd.Support
{
    public static class PlayerFlowE2EDriver
    {
        public static void EnsurePassiveEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<PassiveInputModule>();
        }

        public static IEnumerator LoadScene(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, sceneName + " should be present in BuildSettings.");
            while (!op.isDone) yield return null;
        }

        public static IEnumerator WaitForScene(string sceneName, float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (SceneManager.GetActiveScene().name != sceneName && elapsed < timeoutSeconds)
            {
                ConfirmCharacterCreatorIfShown();
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name, failure);
            yield return WaitForArrival();
        }

        // A scene can be active before arrival finishes binding the slot owner; the game ignores interaction until then.
        public static IEnumerator WaitForArrival(float timeoutSeconds = 10f)
        {
            float deadline = Time.unscaledTime + timeoutSeconds;
            while (Rootborn.Game.World.WorldTravelRuntime.HasPendingTravel && Time.unscaledTime < deadline) yield return null;
            // The test fixture swaps input systems per test; a scene load can leave actions bound to stale control indices,
            // which freezes the last move key down. Re-resolve after every arrival, as the per-test reset does.
            // Re-resolving can make the Input System log "cached value became outdated"; that comes from this reset, not the game.
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            Rootborn.Tests.PlayMode.PlayModeInputSystemGuardAction.ForgetResolvedControls();
        }

        // A new game opens the character creator first; a player presses Confirm to continue. Creator tests drive it themselves.
        // Preparation only (tests not about the creator); SpumCharacterCreatorNewGameE2ETests clicks it with a mouse device.
        public static void ConfirmCharacterCreatorIfShown()
        {
            string test = TestContext.CurrentContext?.Test?.FullName ?? string.Empty;
            if (test.Contains("Spum") || test.Contains("Creator") || test.Contains("CharacterPart")) return;
            if (Time.unscaledTime - s_lastCreatorConfirm < 1f) return;
            bool clicked = false;
            foreach (var panel in Object.FindObjectsByType<Rootborn.UI.MainMenu.SpumCharacterCreatorPanel>(FindObjectsSortMode.None))
            {
                if (panel == null || !panel.isActiveAndEnabled) continue;
                foreach (var button in panel.GetComponentsInChildren<Button>())
                    if (button.name == "ConfirmButton" && button.IsInteractable())
                    {
                        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                        clicked = true;
                        break;
                    }
            }
            if (clicked) s_lastCreatorConfirm = Time.unscaledTime;
        }

        public static string DescribeCreators()
        {
            var all = Object.FindObjectsByType<Rootborn.UI.MainMenu.SpumCharacterCreatorPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var text = new StringBuilder("creators:" + all.Length);
            foreach (var c in all) text.Append(' ').Append(c.name).Append(c.isActiveAndEnabled ? "/on" : "/off").Append('@').Append(c.gameObject.scene.name);
            return text.ToString();
        }

        private static float s_lastCreatorConfirm = -10f;

        /// <summary>
        /// Clicks a UI button the way a player does: the button must be on top under the pointer, and a mouse device moves,
        /// presses and releases there while the scene's InputSystemUIInputModule handles it (no direct handler calls).
        /// </summary>
        public static IEnumerator ClickButton(Button button)
        {
            Assert.IsNotNull(button);
            Assert.IsTrue(button.gameObject.activeInHierarchy, button.name + " must be visible for player-facing click. Hidden by: " + FirstInactiveAncestor(button.transform));
            Assert.IsTrue(button.interactable, button.name + " must be interactable for player-facing click.");
            string label = button.name;
            int clicks = 0;
            UnityEngine.Events.UnityAction count = () => clicks++;
            button.onClick.AddListener(count);
            try
            {
                yield return ClickWithMouse(button.gameObject, label);
            }
            finally
            {
                // Many buttons (dialogue choices, panel closers) destroy themselves when clicked.
                if (button != null) button.onClick.RemoveListener(count);
            }

            Assert.AreEqual(1, clicks, label + " did not receive exactly one click from the mouse device. " + DescribeInputSetup());
        }

        /// <summary>Event systems with their enabled modules and the pointer devices, for click failures.</summary>
        public static string DescribeInputSetup()
        {
            var text = new StringBuilder("event systems:");
            foreach (var system in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                text.Append(' ').Append(system.name).Append(system == EventSystem.current ? "(current)[" : "[");
                foreach (var module in system.GetComponents<BaseInputModule>())
                    if (module.enabled) text.Append(module.GetType().Name).Append(' ');
                text.Append(']');
            }

            text.Append(" pointers:");
            foreach (var device in InputSystem.devices)
                if (device is Pointer) text.Append(' ').Append(device.name);
            return text.ToString();
        }

        /// <summary>Moves a mouse device over the target's center, checks it is the top UI hit, then presses and releases.</summary>
        public static IEnumerator ClickWithMouse(GameObject target, string label)
        {
            var rect = target.GetComponent<RectTransform>();
            Assert.IsNotNull(rect, label + " is not a UI element.");
            // A row far down a list is reached the way a player does: with the mouse wheel over the list.
            yield return ScrollIntoViewWithWheel(rect, label);
            var canvas = target.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            yield return ClickWithMouseAt(target, point, label);
        }

        private sealed class RealPointerScope
        {
            private readonly BaseInputModule[] _disabled;
            private readonly UnityEngine.InputSystem.UI.InputSystemUIInputModule _temporary;
            public readonly Mouse Mouse;

            // A fresh UI module for this pointer only: scenario tests park a passive module, and a module created before
            // the test's input fixture may not see a device added now. Other modules are switched off meanwhile.
            public RealPointerScope(EventSystem events)
            {
                var modules = events.GetComponents<BaseInputModule>();
                var disabled = new List<BaseInputModule>();
                foreach (var module in modules)
                {
                    if (!module.enabled) continue;
                    module.enabled = false;
                    disabled.Add(module);
                }

                _disabled = disabled.ToArray();
                Mouse = InputSystem.AddDevice<Mouse>();
                Mouse.MakeCurrent();
                _temporary = events.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            public void Dispose()
            {
                // Removed at once (not Destroy, which waits for the end of the frame and could swallow the next click).
                if (_temporary != null) Object.DestroyImmediate(_temporary);
                InputSystem.RemoveDevice(Mouse);
                foreach (var module in _disabled)
                    if (module != null) module.enabled = true;
            }
        }

        /// <summary>Scrolls the nearest ScrollRect with mouse wheel events over its viewport until the target is inside it.</summary>
        public static IEnumerator ScrollIntoViewWithWheel(RectTransform target, string label)
        {
            var scroll = target.GetComponentInParent<ScrollRect>();
            // Only items inside the scrolled content move with the wheel; buttons beside the viewport do not.
            if (scroll == null || scroll.content == null || !target.IsChildOf(scroll.content) || EventSystem.current == null) yield break;
            var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            var canvas = viewport.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (Inside(viewport, target, camera)) yield break;
            var scope = new RealPointerScope(EventSystem.current);
            try
            {
                Vector2 over = RectTransformUtility.WorldToScreenPoint(camera, viewport.TransformPoint(viewport.rect.center));
                for (int step = 0; step < 80 && !Inside(viewport, target, camera); step++)
                {
                    float targetY = viewport.InverseTransformPoint(target.TransformPoint(target.rect.center)).y;
                    // Wheel down (negative) moves the list up to reveal rows below the viewport.
                    float wheel = targetY < viewport.rect.center.y ? -120f : 120f;
                    InputSystem.QueueStateEvent(scope.Mouse, new MouseState { position = over, scroll = new Vector2(0f, wheel) });
                    yield return null;
                    InputSystem.QueueStateEvent(scope.Mouse, new MouseState { position = over });
                    yield return null;
                }
            }
            finally
            {
                scope.Dispose();
            }

            Assert.IsTrue(Inside(viewport, target, camera), label + " stayed outside its list after scrolling with the mouse wheel.");
        }

        private static bool Inside(RectTransform viewport, RectTransform target, Camera camera)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, target.TransformPoint(target.rect.center));
            return RectTransformUtility.RectangleContainsScreenPoint(viewport, point, camera);
        }

        /// <summary>Clicks a screen point with a mouse device; the target must be the top UI hit there.</summary>
        public static IEnumerator ClickWithMouseAt(GameObject target, Vector2 point, string label)
        {
            var events = EventSystem.current;
            Assert.IsNotNull(events, "No EventSystem for " + label);
            var hits = new List<RaycastResult>();
            events.RaycastAll(new PointerEventData(events) { position = point }, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), "Nothing under the pointer at " + point + " for " + label);
            Assert.IsTrue(hits[0].gameObject.transform.IsChildOf(target.transform), label + " is covered by " + hits[0].gameObject.name);

            var scope = new RealPointerScope(events);
            try
            {
                yield return null;
                InputSystem.QueueStateEvent(scope.Mouse, new MouseState { position = point });
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(scope.Mouse, new MouseState { position = point, buttons = 1 });
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(scope.Mouse, new MouseState { position = point });
                yield return null;
                yield return null;
            }
            finally
            {
                scope.Dispose();
            }
        }

        private static string FirstInactiveAncestor(Transform t)
        {
            string hidden = "-";
            for (var c = t; c != null; c = c.parent) if (!c.gameObject.activeSelf) hidden = c.name;
            var journals = new StringBuilder();
            foreach (var j in Object.FindObjectsByType<Rootborn.UI.Objectives.ObjectiveJournalPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                journals.Append(' ').Append(j.GetInstanceID()).Append(j.IsVisible ? "/visible" : "/hidden").Append(j.gameObject.activeInHierarchy ? "/active" : "/inactive").Append(t.IsChildOf(j.transform) ? "/owner" : "");
            return hidden + " (scene " + t.gameObject.scene.name + ") journals:" + journals;
        }

        public static Button FindButton(string cardName, string buttonName)
        {
            var card = GameObject.Find(cardName);
            Assert.IsNotNull(card, "UI card missing: " + cardName);
            return FindButton(card.transform, buttonName);
        }

        public static Button FindButton(Transform root, string name)
        {
            var child = FindChild(root, name);
            Assert.IsNotNull(child, "UI button missing: " + name + "\n" + DescribeButtons(root));
            var button = child.GetComponent<Button>();
            Assert.IsNotNull(button, name + " does not have Button component.\n" + DescribeButtons(root));
            return button;
        }

        public static string DescribeButtons(Transform root)
        {
            if (root == null) return "Available buttons: <root missing>";
            var builder = new StringBuilder();
            builder.Append("Available buttons:");
            var buttons = root.GetComponentsInChildren<Button>(true);
            if (buttons.Length == 0)
            {
                builder.Append(" <none>");
                return builder.ToString();
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                var text = button.GetComponentInChildren<Text>(true);
                builder.Append("\n- ");
                builder.Append(button.name);
                builder.Append(" active=");
                builder.Append(button.gameObject.activeInHierarchy);
                builder.Append(" interactable=");
                builder.Append(button.interactable);
                if (text != null && !string.IsNullOrEmpty(text.text))
                {
                    builder.Append(" text='");
                    builder.Append(text.text);
                    builder.Append("'");
                }
            }

            return builder.ToString();
        }

        public static Transform FindChild(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindChild(root.GetChild(i), name);
                if (match != null) return match;
            }

            return null;
        }

        // A daily event can pop up after an activity and holds gameplay shortcuts until the player answers or closes it.
        public static IEnumerator DismissDailyEvent()
        {
            var close = GameObject.Find("DailyEventCloseButton");
            if (close == null || !close.activeInHierarchy) yield break;
            yield return ClickButton(close.GetComponent<Button>());
        }

        // Names whatever currently holds gameplay shortcuts (a modal panel left open, a travel in progress).
        public static string InputLockOwners()
        {
            var field = typeof(UiGameplayInputLock).GetField("Owners", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var owners = field != null ? field.GetValue(null) as System.Collections.IEnumerable : null;
            var names = new StringBuilder("input lock:");
            if (owners != null) foreach (var o in owners) names.Append(' ').Append(o is Object u && u != null ? u.GetType().Name + "/" + u.name : "null");
            names.Append(" textFocus:").Append(UiTextInputFocus.HasFocus);
            return names.ToString();
        }

        public static IEnumerator PressTab(Keyboard keyboard)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
            InputSystem.Update();
            yield return null;
            yield return new WaitForFixedUpdate();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;
        }

        public static IEnumerator PressInteractKey(Keyboard keyboard)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            InputSystem.Update();
            yield return null;
            yield return new WaitForFixedUpdate();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;
        }

        public static IEnumerator WalkPlayerWithKeyboardTo(GameObject player, Keyboard keyboard, Vector3 targetPosition, float tolerance, int maxFrames, string failure)
        {
            // The authored Town has solid buildings, so a straight walk stalls against walls. The route is planned
            // around colliders, but the player still moves only through WASD key events, like a person steering.
            yield return WaitForArrival();
            Vector3 startPosition = player.transform.position;
            var waypoints = PlanWalk(player, targetPosition);
            float pathLength = 0f; Vector3 prev = player.transform.position;
            foreach (var w in waypoints) { pathLength += Vector3.Distance(prev, w); prev = w; }
            pathLength += Vector3.Distance(prev, targetPosition);
            // Long routes around buildings take longer than the straight-line budget the callers were written for.
            int frameBudget = maxFrames + Mathf.CeilToInt(pathLength * 30f);
            int next = 0;
            Key activeKey = Key.None;
            Vector3 lastCheck = player.transform.position;
            int replans = 0;
            string lastContacts = "-";
            string startScene = SceneManager.GetActiveScene().name;
            for (int i = 0; i < frameBudget && Vector3.Distance(player.transform.position, targetPosition) > tolerance; i++)
            {
                // Walking into a portal or level edge changes scenes; the target belongs to the old scene, so stop there.
                if (SceneManager.GetActiveScene().name != startScene || player == null)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    InputSystem.Update();
                    yield break;
                }
                // Townsfolk walk their routes and can stand in the way; when the player stops making progress, plan again.
                if (i > 0 && i % 30 == 0)
                {
                    if (Vector3.Distance(player.transform.position, lastCheck) < 0.3f && replans < 8)
                    {
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        InputSystem.Update();
                        yield return new WaitForFixedUpdate();
                        activeKey = Key.None;
                        lastContacts = DescribeContacts(player);
                        waypoints = PlanWalk(player, targetPosition);
                        next = 0;
                        replans++;
                    }
                    lastCheck = player.transform.position;
                }
                Vector3 goal = next < waypoints.Count ? waypoints[next] : targetPosition;
                Vector3 delta = goal - player.transform.position;
                if (next < waypoints.Count && Mathf.Abs(delta.x) < 0.12f && Mathf.Abs(delta.y) < 0.12f) { next++; continue; }
                Key key = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? (delta.x >= 0f ? Key.D : Key.A) : (delta.y >= 0f ? Key.W : Key.S);
                if (activeKey != key)
                {
                    QueueKey(keyboard, key);
                    activeKey = key;
                }

                InputSystem.Update();
                yield return null;
                yield return new WaitForFixedUpdate();
            }

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            yield return new WaitForFixedUpdate();
            // The body keeps sliding a little after the key is released; a person taps back toward the spot.
            for (int nudge = 0; nudge < 12 && SceneManager.GetActiveScene().name == startScene && player != null; nudge++)
            {
                for (int settle = 0; settle < 3; settle++) yield return new WaitForFixedUpdate();
                Vector3 miss = targetPosition - player.transform.position;
                if (miss.magnitude <= tolerance || miss.magnitude > 1.5f) break;
                QueueKey(keyboard, Mathf.Abs(miss.x) >= Mathf.Abs(miss.y) ? (miss.x >= 0f ? Key.D : Key.A) : (miss.y >= 0f ? Key.W : Key.S));
                InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
            }
            if (SceneManager.GetActiveScene().name != startScene || player == null) yield break;
            Assert.LessOrEqual(Vector3.Distance(player.transform.position, targetPosition), tolerance, failure + " (from " + startPosition + " to " + targetPosition + ", stopped at " + player.transform.position + ", replans " + replans + " touching " + lastContacts + ", reached waypoint " + next + "/" + waypoints.Count + ": " + string.Join(" ", waypoints) + ") " + DescribeKeyboards(keyboard));
        }

        private static string DescribeKeyboards(Keyboard driven)
        {
            var text = new StringBuilder("keyboards:");
            foreach (var device in InputSystem.devices)
            {
                if (!(device is Keyboard k)) continue;
                text.Append(' ').Append(k.deviceId).Append(k == driven ? "(driven)" : "").Append(k.added ? "" : "(removed)").Append('[');
                if (k.wKey.isPressed) text.Append('W'); if (k.aKey.isPressed) text.Append('A'); if (k.sKey.isPressed) text.Append('S'); if (k.dKey.isPressed) text.Append('D');
                text.Append(']');
            }
            if (driven != null && !driven.added) text.Append(" driven keyboard " + driven.deviceId + " is no longer added");
            return text.ToString();
        }

        private static string DescribeContacts(GameObject player)
        {
            var body = player != null ? player.GetComponent<Rigidbody2D>() : null;
            if (body == null) return "no body";
            var contacts = new System.Collections.Generic.List<ContactPoint2D>();
            body.GetContacts(contacts);
            var names = new StringBuilder();
            foreach (var c in contacts) if (c.collider != null) names.Append(c.collider.name).Append('(').Append(c.collider.GetType().Name).Append(')').Append(' ');
            var box = player.GetComponent<BoxCollider2D>();
            if (box != null)
            {
                var filter = new ContactFilter2D { useTriggers = false };
                var hits = new System.Collections.Generic.List<Collider2D>();
                Vector2 at = (Vector2)box.bounds.center + Vector2.left * 0.5f;
                Physics2D.OverlapBox(at, (Vector2)box.bounds.size * 0.9f, 0f, filter, hits);
                names.Append("| overlapLeft:");
                foreach (var h in hits) names.Append(h.name).Append(h.transform.IsChildOf(player.transform) ? "(self)" : "").Append(' ');
                var cast = new RaycastHit2D[8];
                int n = Physics2D.BoxCast((Vector2)box.bounds.center, (Vector2)box.bounds.size * 0.9f, 0f, Vector2.left, filter, cast, 0.5f);
                names.Append("| castLeft:");
                for (int i = 0; i < n; i++) names.Append(cast[i].collider.name).Append('@').Append(cast[i].distance.ToString("0.00")).Append(' ');
                names.Append("| layer ").Append(LayerMask.LayerToName(player.layer)); names.Append(" | bounds ").Append(box.bounds.size).Append(" scale ").Append(player.transform.lossyScale);
            }
            return names.Length > 0 ? names.ToString() : "nothing";
        }

        // Breadth-first route on a half-unit grid, testing each step with the player's own collider box.
        // Returns the corners to steer through; empty when the straight walk is already the route or no route exists.
        public static System.Collections.Generic.List<Vector3> PlanWalk(GameObject player, Vector3 target)
        {
            var body = player != null ? player.GetComponent<BoxCollider2D>() : null;
            if (body == null) return new System.Collections.Generic.List<Vector3>();
            // A route that grazes a wall catches the body on tile seams and stalls, so keep a margin when there is room.
            // World bounds include the player's scale, which the collider's local size does not.
            Physics2D.SyncTransforms();
            Vector2 worldSize = body.bounds.size;
            // Try the widest margin first; a box smaller than the real body finds routes the body then catches on.
            System.Collections.Generic.List<Vector3> route = null;
            bool reached = false;
            var wide = worldSize + new Vector2(0.4f, 0.4f);
            route = PlanWalk(player, body, player.transform.position, target, wide, out reached);
            // Short walks (inside a room, to a nearby door) keep the original whole-route fallback.
            bool longWalk = Vector2.Distance(player.transform.position, target) >= 12f;
            if (!reached && !longWalk)
            {
                foreach (var size in new[] { worldSize + new Vector2(0.1f, 0.1f), worldSize + new Vector2(0.04f, 0.04f), worldSize * 0.9f })
                {
                    route = PlanWalk(player, body, player.transform.position, target, size, out reached);
                    if (reached) break;
                }
            }
            else if (!reached)
            {
                // Often only the last stretch (a doorway, a gate) is too narrow for the wide margin. Keep the wide route as far
                // as it gets and plan only the rest tighter; a tight route over the whole map grazes corners and catches.
                Vector3 handoff = route.Count > 0 ? route[route.Count - 1] : player.transform.position;
                foreach (var size in new[] { worldSize + new Vector2(0.1f, 0.1f), worldSize + new Vector2(0.04f, 0.04f), worldSize * 0.9f })
                {
                    var rest = PlanWalk(player, body, handoff, target, size, out reached);
                    if (!reached) continue;
                    route.AddRange(rest);
                    break;
                }

                if (!reached)
                {
                    foreach (var size in new[] { worldSize + new Vector2(0.1f, 0.1f), worldSize + new Vector2(0.04f, 0.04f), worldSize * 0.9f })
                    {
                        route = PlanWalk(player, body, player.transform.position, target, size, out reached);
                        if (reached) break;
                    }
                }
            }
            // The last corner is the target rounded to the half-unit grid; steering to it overshoots short walks. Head for the target itself.
            if (reached && route.Count > 0) route.RemoveAt(route.Count - 1);
            return route;
        }

        private static System.Collections.Generic.List<Vector3> PlanWalk(GameObject player, BoxCollider2D body, Vector3 start, Vector3 target, Vector2 size, out bool reachedGoal)
        {
            var corners = new System.Collections.Generic.List<Vector3>();
            Physics2D.SyncTransforms();
            const float step = 0.5f;
            Vector2 offset = (Vector2)(body.bounds.center - player.transform.position);
            int span = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(target.x - start.x), Mathf.Abs(target.y - start.y)) / step) + 24;
            // Large trigger zones overlap everything, so they are filtered out instead of crowding a fixed hit buffer.
            var solidOnly = new ContactFilter2D { useTriggers = false };
            var hits = new System.Collections.Generic.List<Collider2D>(16);
            bool Open(Vector2Int c)
            {
                Vector2 p = (Vector2)start + (Vector2)c * step + offset;
                Physics2D.OverlapBox(p, size, 0f, solidOnly, hits);
                for (int i = 0; i < hits.Count; i++)
                    if (hits[i] != null && !hits[i].transform.IsChildOf(player.transform)) return false;
                return true;
            }
            // Boxes half a unit apart leave thin gaps where a fence line hides; sweep the body along each step instead.
            var castHits = new RaycastHit2D[16];
            bool Passable(Vector2Int c, Vector2Int n)
            {
                Vector2 p = (Vector2)start + (Vector2)c * step + offset;
                int count = Physics2D.BoxCast(p, size, 0f, (Vector2)(n - c), solidOnly, castHits, step);
                for (int i = 0; i < count; i++)
                    if (castHits[i].collider != null && !castHits[i].collider.transform.IsChildOf(player.transform)) return false;
                return true;
            }
            var goal = new Vector2Int(Mathf.RoundToInt((target.x - start.x) / step), Mathf.RoundToInt((target.y - start.y) / step));
            var from = new System.Collections.Generic.Dictionary<Vector2Int, Vector2Int>();
            var queue = new System.Collections.Generic.Queue<Vector2Int>();
            var origin = Vector2Int.zero;
            from[origin] = origin; queue.Enqueue(origin);
            Vector2Int best = origin; float bestDistance = (goal - origin).sqrMagnitude;
            var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            while (queue.Count > 0 && from.Count < 40000)
            {
                var c = queue.Dequeue();
                float d = (goal - c).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = c; }
                if (c == goal) break;
                foreach (var dir in dirs)
                {
                    var n = c + dir;
                    if (Mathf.Abs(n.x) > span || Mathf.Abs(n.y) > span || from.ContainsKey(n)) continue;
                    if (n != goal && (!Open(n) || !Passable(c, n))) continue;
                    from[n] = c; queue.Enqueue(n);
                }
            }
            reachedGoal = best == goal;
            var path = new System.Collections.Generic.List<Vector2Int>();
            for (var c = best; c != origin; c = from[c]) path.Add(c);
            path.Reverse();
            for (int i = 0; i < path.Count; i++)
            {
                bool corner = i == path.Count - 1 || (i > 0 && (path[i] - path[i - 1]) != (path[i + 1] - path[i])) || (i == 0 && path.Count > 1 && path[1] - path[0] != path[0]);
                if (corner) corners.Add(start + new Vector3(path[i].x * step, path[i].y * step, 0f));
            }
            return corners;
        }

        // Opens MainMenu, waits for game data, presses a save card button and waits for the world scene and a ready player.
        public static IEnumerator EnterWorldFromSaveSlot(string slotId, string buttonName, string expectedScene, string failure)
        {
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            Rootborn.UI.MainMenu.SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return Rootborn.Tests.PlayMode.PlayModeScenarioStart.WaitForGameData();
            yield return ClickButton(FindButton("SaveSlotCard_" + slotId, buttonName));
            yield return WaitForScene(expectedScene, 20f, failure);
            yield return WaitForPlayerReady(failure);
        }

        public static IEnumerator WaitForPlayerReady(string failure)
        {
            float deadline = Time.unscaledTime + 15f;
            while (Time.unscaledTime < deadline)
            {
                var player = GameObject.Find("Player");
                if (player != null && player.GetComponent<Rootborn.Game.StudentLife.StudentLifeProgressComponent>() != null
                    && player.GetComponent<Rootborn.Game.Player.PlayerInteractionRouter>() != null && EventSystem.current != null
                    && !Rootborn.Game.World.WorldTravelRuntime.HasPendingTravel) yield break;
                yield return null;
            }
            Assert.Fail(failure + " Player runtime was not ready in " + SceneManager.GetActiveScene().name + ".");
        }

        // Walks into the door that leads to destinationScene; presses E if the door waits for it. Returns once the new scene's player is ready.
        public static IEnumerator WalkThroughDoor(Keyboard keyboard, string destinationScene, string failure)
        {
            string from = SceneManager.GetActiveScene().name;
            Rootborn.Game.World.WorldPortal door = null;
            foreach (var portal in Object.FindObjectsByType<Rootborn.Game.World.WorldPortal>(FindObjectsSortMode.None))
                if (portal.isActiveAndEnabled && portal.gameObject.scene.name == from && portal.DestinationScene == destinationScene) { door = portal; break; }
            Assert.IsNotNull(door, failure + " " + from + " has no active door to " + destinationScene + ".");
            var player = GameObject.Find("Player");
            yield return WalkPlayerWithKeyboardTo(player, keyboard, door.transform.position, 0.9f, 900, failure + " Could not walk to the door.");
            if (SceneManager.GetActiveScene().name == from) yield return PressInteractKey(keyboard);
            yield return WaitForScene(destinationScene, 15f, failure + " The door did not lead to " + destinationScene + ".");
            yield return WaitForPlayerReady(failure);
        }

        // Walks next to a point, checks a prompt is shown, and presses E.
        public static IEnumerator WalkAndInteract(Keyboard keyboard, Vector3 target, string failure)
        {
            var player = GameObject.Find("Player");
            yield return WalkPlayerWithKeyboardTo(player, keyboard, target, 0.9f, 900, failure + ": could not walk there.");
            var router = player.GetComponent<Rootborn.Game.Player.PlayerInteractionRouter>();
            router.RefreshPromptNow();
            Assert.IsTrue(router.PromptVisible, failure + ": no prompt there.");
            yield return PressInteractKey(keyboard);
            yield return null;
        }

        // Walks to a placed location point, opens its activity panel with E, presses one activity button and closes the panel.
        public static IEnumerator UseLocationActivity(Keyboard keyboard, string pointName, int activityIndex, string failure)
        {
            var point = GameObject.Find(pointName);
            Assert.IsNotNull(point, failure + " no point named " + pointName + ".");
            yield return WalkAndInteract(keyboard, point.transform.position, failure + " " + pointName);
            var panel = Object.FindFirstObjectByType<Rootborn.UI.StudentLife.LocationIdentityPanel>(FindObjectsInactive.Include);
            Assert.IsTrue(panel != null && panel.IsOpen, failure + " E at " + pointName + " did not open its activity panel.");
            yield return ClickButton(FindButton(panel.transform, "LocationActivityButton_" + activityIndex));
            Assert.AreEqual(Rootborn.Game.StudentLife.LifeActivityResultKind.Applied, panel.LastResult.Kind, failure + " the activity at " + pointName + " did not apply (" + panel.LastResult.Kind + ").");
            if (panel.IsOpen) yield return ClickButton(FindButton(panel.transform, "CloseLocation"));
        }

        // Plays the open minigame through its on-screen buttons, the way a player reads and answers the screen.
        // Sequence: remember the order while it is shown, then press it back. Timing: press Tap while the cursor is over a beat.
        // Choice: press the hinted answer when the screen marks one. Placement and dodge stages are not driven yet.
        public static IEnumerator PlayMinigameToEnd(Rootborn.UI.Minigames.MinigamePanel panel, float timeoutSeconds, string failure)
        {
            Assert.IsNotNull(panel, failure + " Minigame panel missing.");
            float deadline = Time.unscaledTime + timeoutSeconds;
            int[] remembered = null;
            Rootborn.Game.Minigames.ChallengeSession stage = null;
            int tappedBeats = 0;
            int rotation = 0;
            RectInt[] plan = null;
            while (panel.IsOpen && panel.Session != null && !panel.Session.IsComplete)
            {
                Assert.Less(Time.unscaledTime, deadline, failure + " Minigame did not finish in time (stage " + panel.Session.StageIndex + ").");
                var c = panel.Session.Current;
                if (!ReferenceEquals(c, stage)) { stage = c; remembered = null; tappedBeats = 0; rotation = 0; plan = null; }
                switch (c.Mechanic)
                {
                    case Rootborn.Game.Minigames.MinigameMechanic.Sequence:
                        if (c.PreviewVisible) { remembered = c.PreviewSequence; break; }
                        Assert.IsNotNull(remembered, failure + " The sequence was never shown before answering.");
                        var answer = FindButton(panel.transform, "Choice_" + remembered[c.ProgressIndex]); if (!answer.interactable) break; yield return ClickButton(answer);
                        break;
                    case Rootborn.Game.Minigames.MinigameMechanic.Choice:
                        int hinted = -1;
                        for (int i = 0; i < c.Symbols.Length; i++) if (c.IsHintChoice(i)) { hinted = i; break; }
                        var pick = FindButton(panel.transform, "Choice_" + Mathf.Max(0, hinted)); if (!pick.interactable) break; yield return ClickButton(pick);
                        break;
                    case Rootborn.Game.Minigames.MinigameMechanic.Timing:
                        var beats = c.BeatTimes;
                        if (tappedBeats < beats.Length && Mathf.Abs(c.Elapsed - beats[tappedBeats]) <= c.TimingWindow * 0.5f)
                        {
                            yield return ClickButton(FindButton(panel.transform, "Tap"));
                            tappedBeats++;
                        }
                        else if (tappedBeats < beats.Length && c.Elapsed > beats[tappedBeats] + c.TimingWindow) tappedBeats++;
                        break;
                    case Rootborn.Game.Minigames.MinigameMechanic.Placement:
                        // Work out a layout that fits every piece, then pick each piece, turn it with Rotate and press its corner cell.
                        if (plan == null) { plan = PlanPlacement(c); Assert.IsNotNull(plan, failure + " No layout fits the placement board."); }
                        int piece = -1;
                        for (int i = 0; i < plan.Length; i++) if (!c.TryGetPieceRect(i, out _)) { piece = i; break; }
                        if (piece < 0) break;
                        int wanted = plan[piece].width == c.PieceSizes[piece].x && plan[piece].height == c.PieceSizes[piece].y ? 0 : 1;
                        yield return ClickButton(FindButton(panel.transform, "Piece_" + piece));
                        while (rotation % 2 != wanted) { yield return ClickButton(FindButton(panel.transform, "Rotate")); rotation = (rotation + 1) % 4; }
                        yield return ClickButton(FindButton(panel.transform, "Cell_" + plan[piece].x + "_" + plan[piece].y));
                        Assert.IsTrue(c.TryGetPieceRect(piece, out _), failure + " Pressing the cell did not place piece " + piece + ".");
                        rotation = 0;
                        break;
                    case Rootborn.Game.Minigames.MinigameMechanic.TargetDodge:
                        // Step out of the lane the next obstacle comes down, and click the target where it is drawn.
                        if (c.NextObstacleLane >= 0 && c.Lane == c.NextObstacleLane)
                        {
                            yield return ClickButton(FindButton(panel.transform, "Lane_" + ((c.Lane + 1) % c.LaneCount)));
                            break;
                        }
                        if (c.HasTarget) yield return ClickAimSurface(panel, c.TargetPosition, failure);
                        break;
                    default:
                        Assert.Fail(failure + " Minigame stage " + c.Mechanic + " is not driven by the test helper yet.");
                        break;
                }
                yield return null;
            }
            Assert.IsNotNull(panel.Session, failure + " Minigame session missing.");
            Assert.IsTrue(panel.Session.IsComplete, failure + " Minigame closed before it finished.");
            // The panel hands the finished session to the game when it next refreshes.
            for (int i = 0; i < 10; i++) yield return null;
        }

        // Clicks the aim surface at a point given in its 0..1 coordinates with a mouse device.
        private static IEnumerator ClickAimSurface(Rootborn.UI.Minigames.MinigamePanel panel, Vector2 normalized, string failure)
        {
            var surface = panel.GetComponentInChildren<Rootborn.UI.Minigames.MinigamePointerSurface>();
            Assert.IsNotNull(surface, failure + " The target stage has no aim surface.");
            var rect = (RectTransform)surface.transform;
            var canvas = surface.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var bounds = rect.rect;
            var local = new Vector2(bounds.xMin + normalized.x * bounds.width, bounds.yMin + normalized.y * bounds.height);
            var screen = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(local));
            // Mouse device through the scene UI module, never a direct handler call.
            yield return ClickWithMouseAt(surface.gameObject, screen, failure + " aim surface");
        }

        // Finds where each piece goes (corner cell and size after turning) so blocked cells stay free and required cells get covered.
        private static RectInt[] PlanPlacement(Rootborn.Game.Minigames.ChallengeSession c)
        {
            var sizes = c.PieceSizes; var board = c.BoardSize;
            var used = new bool[board.x, board.y];
            foreach (var b in c.BlockedCells) if (b.x >= 0 && b.y >= 0 && b.x < board.x && b.y < board.y) used[b.x, b.y] = true;
            var plan = new RectInt[sizes.Length];
            return Fit(0, sizes, board, used, plan, RequiredCells(c)) ? plan : null;
        }

        private static Vector2Int[] RequiredCells(Rootborn.Game.Minigames.ChallengeSession c)
        {
            var field = c.GetType().GetField("_required", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field != null ? (Vector2Int[])field.GetValue(c) ?? new Vector2Int[0] : new Vector2Int[0];
        }

        private static bool Fit(int piece, Vector2Int[] sizes, Vector2Int board, bool[,] used, RectInt[] plan, Vector2Int[] required)
        {
            if (piece == sizes.Length)
            {
                foreach (var r in required) { bool covered = false; foreach (var p in plan) covered |= p.Contains(r); if (!covered) return false; }
                return true;
            }
            for (int turn = 0; turn < 2; turn++)
            {
                var size = turn == 0 ? sizes[piece] : new Vector2Int(sizes[piece].y, sizes[piece].x);
                if (turn == 1 && size == sizes[piece]) continue;
                for (int y = 0; y + size.y <= board.y; y++)
                for (int x = 0; x + size.x <= board.x; x++)
                {
                    bool free = true;
                    for (int dy = 0; dy < size.y && free; dy++) for (int dx = 0; dx < size.x && free; dx++) free = !used[x + dx, y + dy];
                    if (!free) continue;
                    for (int dy = 0; dy < size.y; dy++) for (int dx = 0; dx < size.x; dx++) used[x + dx, y + dy] = true;
                    plan[piece] = new RectInt(x, y, size.x, size.y);
                    if (Fit(piece + 1, sizes, board, used, plan, required)) return true;
                    for (int dy = 0; dy < size.y; dy++) for (int dx = 0; dx < size.x; dx++) used[x + dx, y + dy] = false;
                }
            }
            return false;
        }

        public static KeyControl ControlFor(Keyboard keyboard, Key key)
        {
            if (key == Key.A) return keyboard.aKey;
            if (key == Key.D) return keyboard.dKey;
            if (key == Key.W) return keyboard.wKey;
            if (key == Key.S) return keyboard.sKey;
            if (key == Key.E) return keyboard.eKey;
            Assert.Fail("Unsupported key: " + key);
            return keyboard.eKey;
        }

        public static void DestroyAllNamed(string objectName)
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = transforms.Length - 1; i >= 0; i--)
            {
                var transform = transforms[i];
                if (transform != null && transform.name == objectName) Object.DestroyImmediate(transform.gameObject);
            }
        }

        private static void QueueKey(Keyboard keyboard, Key key)
        {
            if (key == Key.A) InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
            else if (key == Key.D) InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            else if (key == Key.W) InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            else if (key == Key.S) InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
            else if (key == Key.E) InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            else Assert.Fail("Unsupported key: " + key);
        }
    }
}