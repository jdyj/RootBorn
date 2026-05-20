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
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name, failure);
        }

        public static IEnumerator ClickButton(Button button)
        {
            Assert.IsNotNull(button);
            Assert.IsTrue(button.gameObject.activeInHierarchy, button.name + " must be visible for player-facing click.");
            Assert.IsTrue(button.interactable, button.name + " must be interactable for player-facing click.");
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            yield return null;
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
            Key activeKey = Key.None;
            for (int i = 0; i < maxFrames && Vector3.Distance(player.transform.position, targetPosition) > tolerance; i++)
            {
                Vector3 delta = targetPosition - player.transform.position;
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
            Assert.LessOrEqual(Vector3.Distance(player.transform.position, targetPosition), tolerance, failure);
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