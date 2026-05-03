using Rootborn.Game.Bootstrap;
using Rootborn.Game.Time;
using Rootborn.Network.Session;
using Rootborn.UI.MainMenu;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Editor.Tools
{
    public static class SceneSetup
    {
        private const string ScenesRoot = "Assets/Scenes";

        [MenuItem("Rootborn/Scene/Setup All Scenes")]
        public static void SetupAll()
        {
            if (!OneClickSetup.EnsureNotPlaying()) return;
            SetupBoot();
            SetupMainMenu();
            SetupHostLobby();
            SetupFarm();
            Debug.Log("[ROOTBORN] All 4 scenes set up. Open Boot.unity and Play.");
        }

        [MenuItem("Rootborn/Scene/Setup Boot")]
        public static void SetupBoot()
        {
            if (!OneClickSetup.EnsureNotPlaying()) return;
            var scene = OpenScene($"{ScenesRoot}/Boot.unity");
            EnsureBootstrapRoot(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("Rootborn/Scene/Setup MainMenu")]
        public static void SetupMainMenu()
        {
            if (!OneClickSetup.EnsureNotPlaying()) return;
            var scene = OpenScene($"{ScenesRoot}/MainMenu.unity");
            EnsureCamera(scene);
            EnsureEventSystem(scene);
            EnsureMainMenuCanvas();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("Rootborn/Scene/Setup HostLobby")]
        public static void SetupHostLobby()
        {
            if (!OneClickSetup.EnsureNotPlaying()) return;
            var scene = OpenScene($"{ScenesRoot}/HostLobby.unity");
            EnsureCamera(scene);
            EnsureEventSystem(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("Rootborn/Scene/Setup Farm")]
        public static void SetupFarm()
        {
            if (!OneClickSetup.EnsureNotPlaying()) return;
            var scene = OpenScene($"{ScenesRoot}/Farm.unity");
            EnsureCamera(scene);
            EnsureEventSystem(scene);
            EnsureGameClock();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static UnityEngine.SceneManagement.Scene OpenScene(string path)
        {
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        private static void EnsureBootstrapRoot(UnityEngine.SceneManagement.Scene scene)
        {
            if (GameObject.Find("[Bootstrap]") == null)
            {
                var root = new GameObject("[Bootstrap]");
                root.AddComponent<GameBootstrap>();
            }

            if (GameObject.Find("[NetworkManager]") == null)
            {
                var nm = new GameObject("[NetworkManager]");
                var manager = nm.AddComponent<NetworkManager>();
                var transport = nm.AddComponent<UnityTransport>();
                manager.NetworkConfig = new NetworkConfig
                {
                    NetworkTransport = transport,
                    ConnectionApproval = false
                };
                nm.AddComponent<NetworkBootstrap>();
            }
        }

        private static void EnsureCamera(UnityEngine.SceneManagement.Scene scene)
        {
            if (Camera.main != null) return;
            var go = new GameObject("Main Camera");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.backgroundColor = new Color(0.1f, 0.13f, 0.1f, 1f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void EnsureEventSystem(UnityEngine.SceneManagement.Scene scene)
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        private static void EnsureGameClock()
        {
            if (GameObject.Find("[GameClock]") != null) return;
            var go = new GameObject("[GameClock]");
            go.AddComponent<GameClock>();
        }

        private static void EnsureMainMenuCanvas()
        {
            if (GameObject.Find("MainMenuCanvas") != null) return;

            var canvasGo = new GameObject("MainMenuCanvas",
                typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler),
                typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var panelGo = new GameObject("ModeSelectPanel", typeof(RectTransform));
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelRect = (RectTransform)panelGo.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(400, 360);
            panelRect.anchoredPosition = Vector2.zero;
            var panel = panelGo.AddComponent<ModeSelectPanel>();

            var single = MakeButton("SinglePlayButton", panelGo.transform, "1세대 시작 (싱글)", 0);
            var host = MakeButton("HostButton", panelGo.transform, "호스트 모드", 1);
            var client = MakeButton("ClientButton", panelGo.transform, "클라이언트 접속", 2);
            var quit = MakeButton("QuitButton", panelGo.transform, "종료", 3);

            var so = new SerializedObject(panel);
            so.FindProperty("_singleButton").objectReferenceValue = single;
            so.FindProperty("_hostButton").objectReferenceValue = host;
            so.FindProperty("_clientButton").objectReferenceValue = client;
            so.FindProperty("_quitButton").objectReferenceValue = quit;
            so.FindProperty("_farmScene").stringValue = "Farm";
            so.FindProperty("_hostLobbyScene").stringValue = "HostLobby";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static UnityEngine.UI.Button MakeButton(string name, Transform parent, string label, int index)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(320, 64);
            rect.anchoredPosition = new Vector2(0, -20 - index * 80);

            var image = go.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.18f, 0.16f, 0.12f, 0.95f);
            var btn = go.AddComponent<UnityEngine.UI.Button>();

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<UnityEngine.UI.Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 22;
            text.color = new Color(0.95f, 0.92f, 0.85f, 1f);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return btn;
        }
    }
}
