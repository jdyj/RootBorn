using System.Collections;
using Rootborn.Core.Network;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Localization;
using Rootborn.Network.Session;
using Rootborn.UI.Modern;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.MainMenu
{
    /// <summary>
    /// Main menu "Join game": the player types the server address and port, connects, and sees why it failed
    /// (for example a full server). Once connected the server's scene sync moves the client into the world.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class JoinGamePanel : MonoBehaviour
    {
        public enum JoinState
        {
            Idle,
            Connecting,
            Connected,
            Failed,
        }

        // Unity Transport waits 60 attempts of 1 s by default; a player at the menu should hear back sooner.
        private const int ConnectAttempts = 10;
        private static readonly Color Ink = new Color32(48, 71, 83, 255);

        private GameObject _root;
        private InputField _hostField;
        private InputField _portField;
        private Button _connectButton;
        private Text _statusLabel;
        private NetworkManager _subscribed;
        private Coroutine _connecting;

        public JoinState State { get; private set; } = JoinState.Idle;
        public string StatusText => _statusLabel != null ? _statusLabel.text : string.Empty;
        public bool IsShown => _root != null && _root.activeSelf;

        /// <summary>The panel lives in the menu scene: it goes away with the menu when the client enters the world.</summary>
        public static JoinGamePanel EnsureInScene(Transform parent)
        {
            var existing = parent.GetComponentInChildren<JoinGamePanel>(true);
            if (existing != null) return existing;
            var go = new GameObject("JoinGamePanel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<JoinGamePanel>();
        }

        public void Show()
        {
            if (_root == null) Build();
            var config = GameBootstrap.Config;
            if (State != JoinState.Connecting)
            {
                if (config != null)
                {
                    _hostField.text = config.JoinIp;
                    _portField.text = config.Port.ToString();
                }

                SetStatus(JoinState.Idle, Local("ui.join.hint", "호스트에게 받은 주소와 포트를 입력하세요.", "Enter the address and port from the host."));
            }

            transform.SetAsLastSibling();
            _root.SetActive(true);
        }

        public void Hide()
        {
            // Leaving while connecting cancels the attempt; a finished failure has already shut the client down.
            if (State == JoinState.Connecting) CancelConnecting();
            if (_root != null) _root.SetActive(false);
        }

        private void Connect()
        {
            if (State == JoinState.Connecting || State == JoinState.Connected) return;
            var error = JoinAddress.TryParse(_hostField.text, _portField.text, out var address);
            if (error != JoinAddressError.None)
            {
                SetStatus(JoinState.Failed, ErrorText(error));
                return;
            }

            var nm = NetworkManager.Singleton;
            var config = GameBootstrap.Config;
            if (nm == null || config == null)
            {
                SetStatus(JoinState.Failed, Local("ui.join.unavailable", "네트워크를 준비하지 못했습니다. 게임을 다시 시작해 주세요.", "Networking is not ready. Please restart the game."));
                return;
            }

            config.Mode = SessionMode.Client;
            config.JoinIp = address.Host;
            config.Port = address.Port;
            _connecting = StartCoroutine(ConnectRoutine(nm, config));
        }

        private IEnumerator ConnectRoutine(NetworkManager nm, AppConfig config)
        {
            SetStatus(JoinState.Connecting, string.Format(Local("ui.join.connecting", "{0}:{1}에 접속하는 중...", "Connecting to {0}:{1}..."), config.JoinIp, config.Port));
            // A failed attempt shuts the client down over a frame or two; starting again before that is refused.
            while (nm != null && nm.ShutdownInProgress) yield return null;
            if (nm == null) yield break;
            var transport = nm.GetComponent<UnityTransport>();
            if (transport != null) transport.MaxConnectAttempts = ConnectAttempts;
            Subscribe(nm);
            var start = NetworkBootstrap.StartSessionAsync(config);
            while (!start.IsCompleted) yield return null;
            _connecting = null;
            if (State == JoinState.Connecting && !nm.IsClient)
            {
                Unsubscribe();
                SetStatus(JoinState.Failed, UnreachableText());
            }
        }

        private void CancelConnecting()
        {
            if (_connecting != null) StopCoroutine(_connecting);
            _connecting = null;
            Unsubscribe();
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsClient && !nm.IsServer) nm.Shutdown();
            SetStatus(JoinState.Idle, string.Empty);
        }

        private void Subscribe(NetworkManager nm)
        {
            Unsubscribe();
            _subscribed = nm;
            nm.OnClientConnectedCallback += HandleConnected;
            nm.OnClientDisconnectCallback += HandleDisconnected;
        }

        private void Unsubscribe()
        {
            if (_subscribed == null) return;
            _subscribed.OnClientConnectedCallback -= HandleConnected;
            _subscribed.OnClientDisconnectCallback -= HandleDisconnected;
            _subscribed = null;
        }

        private void HandleConnected(ulong id)
        {
            if (_subscribed == null || id != _subscribed.LocalClientId) return;
            SetStatus(JoinState.Connected, Local("ui.join.connected", "접속했습니다. 마을로 들어가는 중...", "Connected. Entering the town..."));
        }

        private void HandleDisconnected(ulong id)
        {
            string reason = _subscribed != null ? _subscribed.DisconnectReason : string.Empty;
            Unsubscribe();
            SetStatus(JoinState.Failed, SessionCapacity.IsServerFull(reason)
                ? Local("ui.join.full", "서버가 가득 찼습니다. 자리가 나면 다시 시도하세요.", "The server is full. Try again when a seat opens.")
                : UnreachableText());
        }

        private void SetStatus(JoinState state, string text)
        {
            State = state;
            if (_statusLabel != null) _statusLabel.text = text;
            if (_connectButton != null) _connectButton.interactable = state != JoinState.Connecting && state != JoinState.Connected;
        }

        private static string UnreachableText() => Local("ui.join.unreachable", "접속하지 못했습니다. 주소와 포트, 호스트가 켜져 있는지 확인하세요.", "Could not connect. Check the address, the port and that the host is running.");

        private static string ErrorText(JoinAddressError error)
        {
            switch (error)
            {
                case JoinAddressError.MissingHost: return Local("ui.join.missing-host", "서버 주소를 입력하세요.", "Enter the server address.");
                case JoinAddressError.InvalidHost: return Local("ui.join.invalid-host", "주소는 192.168.0.10 같은 IP 형식으로 입력하세요.", "Enter the address as an IP, like 192.168.0.10.");
                default: return Local("ui.join.invalid-port", "포트는 1~65535 사이 숫자로 입력하세요.", "Enter a port number from 1 to 65535.");
            }
        }

        private void Build()
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            _root = new GameObject("JoinGameRoot", typeof(RectTransform), typeof(Image));
            _root.transform.SetParent(transform, false);
            var rootRect = (RectTransform)_root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
            var shade = _root.GetComponent<Image>();
            shade.color = new Color32(211, 216, 212, 255);
            shade.raycastTarget = true; // the mode buttons underneath stay out of reach while this is open

            var frame = new GameObject("JoinGameFrame", typeof(RectTransform));
            frame.transform.SetParent(_root.transform, false);
            var frameRect = (RectTransform)frame.transform;
            frameRect.anchorMin = frameRect.anchorMax = frameRect.pivot = new Vector2(0.5f, 0.5f);
            frameRect.sizeDelta = new Vector2(900f, 560f);
            StyleSurface(frame);

            MakeText(frame.transform, "Title", Local("ui.join.title", "다른 사람 게임에 접속", "Join a game"), new Vector2(0f, 214f), new Vector2(780f, 56f), 34, TextAnchor.MiddleLeft);
            MakeText(frame.transform, "HostLabel", Local("ui.join.host", "서버 주소", "Server address"), new Vector2(-250f, 110f), new Vector2(260f, 48f), 22, TextAnchor.MiddleLeft);
            _hostField = MakeField(frame.transform, "JoinHostField", new Vector2(130f, 110f), new Vector2(460f, 58f), InputField.ContentType.Standard, 15);
            MakeText(frame.transform, "PortLabel", Local("ui.join.port", "포트", "Port"), new Vector2(-250f, 30f), new Vector2(260f, 48f), 22, TextAnchor.MiddleLeft);
            _portField = MakeField(frame.transform, "JoinPortField", new Vector2(130f, 30f), new Vector2(460f, 58f), InputField.ContentType.IntegerNumber, 5);
            _statusLabel = MakeText(frame.transform, "JoinStatus", string.Empty, new Vector2(0f, -70f), new Vector2(780f, 96f), 20, TextAnchor.MiddleLeft);
            _connectButton = MakeButton(frame.transform, "JoinConnectButton", Local("ui.join.connect", "접속", "Connect"), new Vector2(150f, -200f), Connect);
            MakeButton(frame.transform, "JoinBackButton", Local("ui.join.back", "돌아가기", "Back"), new Vector2(-150f, -200f), Hide);
            _root.SetActive(false);
        }

        private static InputField MakeField(Transform parent, string name, Vector2 position, Vector2 size, InputField.ContentType contentType, int limit)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = Color.white;
            var text = MakeText(go.transform, "Text", string.Empty, Vector2.zero, size - new Vector2(28f, 10f), 22, TextAnchor.MiddleLeft);
            var field = go.GetComponent<InputField>();
            field.targetGraphic = image;
            field.textComponent = text;
            field.contentType = contentType;
            field.lineType = InputField.LineType.SingleLine;
            field.characterLimit = limit;
            return field;
        }

        private static Button MakeButton(Transform parent, string name, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(260f, 66f);
            go.GetComponent<Image>().raycastTarget = true;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            var text = MakeText(go.transform, "Label", label, Vector2.zero, Vector2.zero, 22, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(14f, 5f);
            text.rectTransform.offsetMax = new Vector2(-14f, -5f);
            StyleSurface(go);
            return button;
        }

        private static Text MakeText(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Ink;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static void StyleSurface(GameObject go)
        {
            var skin = go.GetComponent<ModernUiTileImage>();
            if (skin == null) skin = go.AddComponent<ModernUiTileImage>();
            skin.SetRecipe(ModernUiRecipes.CommonPanel);
            skin.Rebuild();
        }

        private static string Local(string key, string korean, string english)
        {
            string value = GameText.Get(key);
            return value == key ? (GameText.Language == "ko" ? korean : english) : value;
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
