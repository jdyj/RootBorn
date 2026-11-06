using System.Collections;
using System.Text;
using NUnit.Framework;
using Rootborn.Game.Common;
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
        }

        // A new game opens the character creator first; a player presses Confirm to continue. Creator tests drive it themselves.
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

        public static IEnumerator ClickButton(Button button)
        {
            Assert.IsNotNull(button);
            Assert.IsTrue(button.gameObject.activeInHierarchy, button.name + " must be visible for player-facing click. Hidden by: " + FirstInactiveAncestor(button.transform));
            Assert.IsTrue(button.interactable, button.name + " must be interactable for player-facing click.");
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            yield return null;
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
            Assert.LessOrEqual(Vector3.Distance(player.transform.position, targetPosition), tolerance, failure + " (from " + startPosition + " to " + targetPosition + ", stopped at " + player.transform.position + ", replans " + replans + " touching " + lastContacts + ", reached waypoint " + next + "/" + waypoints.Count + ": " + string.Join(" ", waypoints) + ")");
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
                Vector2 at = (Vector2)player.transform.position + box.offset + Vector2.left * 0.5f;
                Physics2D.OverlapBox(at, box.size * 0.9f, 0f, filter, hits);
                names.Append("| overlapLeft:");
                foreach (var h in hits) names.Append(h.name).Append(h.transform.IsChildOf(player.transform) ? "(self)" : "").Append(' ');
                var cast = new RaycastHit2D[8];
                int n = Physics2D.BoxCast((Vector2)player.transform.position + box.offset, box.size * 0.9f, 0f, Vector2.left, filter, cast, 0.5f);
                names.Append("| castLeft:");
                for (int i = 0; i < n; i++) names.Append(cast[i].collider.name).Append('@').Append(cast[i].distance.ToString("0.00")).Append(' ');
                names.Append("| layer ").Append(LayerMask.LayerToName(player.layer));
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
            var roomy = PlanWalk(player, body, target, body.size + new Vector2(0.4f, 0.4f), out bool reached);
            return reached ? roomy : PlanWalk(player, body, target, body.size * 0.9f, out _);
        }

        private static System.Collections.Generic.List<Vector3> PlanWalk(GameObject player, BoxCollider2D body, Vector3 target, Vector2 size, out bool reachedGoal)
        {
            var corners = new System.Collections.Generic.List<Vector3>();
            Physics2D.SyncTransforms();
            const float step = 0.5f;
            Vector2 offset = body.offset;
            Vector3 start = player.transform.position;
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