using System.Collections.Generic;
using Rootborn.Core.Network;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Localization;
using Rootborn.Network.Steam;
using Rootborn.UI.Modern;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.MainMenu
{
    /// <summary>
    /// Main menu "Join a friend": lists Steam friends who opened a room and joins one with a click. Steam's own
    /// "Join Game" and invites arrive here too. Players never type an address; without Steam the panel says so.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class JoinGamePanel : MonoBehaviour
    {
        public enum JoinState
        {
            SteamUnavailable,
            Idle,
            Connecting,
            Connected,
            Failed,
        }

        private const int MaxRows = 6;
        private static readonly Color Ink = new Color32(48, 71, 83, 255);

        private GameObject _root;
        private RectTransform _list;
        private Text _statusLabel;
        private Button _refreshButton;
        private readonly List<GameObject> _rows = new List<GameObject>(MaxRows);
        private NetworkManager _subscribed;

        public JoinState State { get; private set; } = JoinState.Idle;
        public string StatusText => _statusLabel != null ? _statusLabel.text : string.Empty;
        public bool IsShown => _root != null && _root.activeSelf;
        public int RoomRowCount => _rows.Count;

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
            transform.SetAsLastSibling();
            _root.SetActive(true);
            if (State != JoinState.Connecting && State != JoinState.Connected) Refresh();
        }

        public void Hide()
        {
            if (State == JoinState.Connecting) CancelConnecting();
            if (_root != null) _root.SetActive(false);
        }

        /// <summary>Joins a lobby picked in Steam (friends list, invite) or in the room list.</summary>
        public void JoinLobby(ulong lobbyId)
        {
            if (_root == null) Build();
            if (!SteamSessions.Available)
            {
                ShowSteamUnavailable();
                return;
            }

            if (State == JoinState.Connecting || State == JoinState.Connected) return;
            var nm = NetworkManager.Singleton;
            SetStatus(JoinState.Connecting, Local("ui.join.connecting", "친구의 방에 들어가는 중...", "Joining your friend's room..."));
            Subscribe(nm);
            SteamSessions.Join(lobbyId, GameBootstrap.Config, () =>
            {
                if (State != JoinState.Connecting) return;
                Unsubscribe();
                SetStatus(JoinState.Failed, UnreachableText());
            });
        }

        private void Refresh()
        {
            ClearRows();
            if (!SteamSessions.Available)
            {
                ShowSteamUnavailable();
                return;
            }

            _refreshButton.gameObject.SetActive(true);
            var rooms = SteamLobby.FriendRoomsNow();
            for (int i = 0; i < rooms.Count && i < MaxRows; i++) AddRow(rooms[i], i);
            SetStatus(JoinState.Idle, rooms.Count == 0
                ? Local("ui.join.empty", "지금 방을 연 친구가 없습니다. 친구가 호스트 모드로 방을 열면 여기에 보입니다.", "No friends have a room open. When a friend hosts, their room appears here.")
                : Local("ui.join.pick", "들어갈 친구의 방을 고르세요.", "Pick a friend's room to join."));
        }

        private void ShowSteamUnavailable()
        {
            ClearRows();
            if (_refreshButton != null) _refreshButton.gameObject.SetActive(false);
            SetStatus(JoinState.SteamUnavailable, Local("ui.join.no-steam", "Steam이 실행 중이 아닙니다. Steam을 켠 뒤 게임을 다시 시작하세요.", "Steam is not running. Start Steam, then restart the game."));
        }

        private void CancelConnecting()
        {
            Unsubscribe();
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsClient && !nm.IsServer) nm.Shutdown();
            SteamLobby.Leave();
            State = JoinState.Idle;
        }

        private void Subscribe(NetworkManager nm)
        {
            Unsubscribe();
            if (nm == null) return;
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
                ? Local("ui.join.full", "방이 가득 찼습니다. 자리가 나면 다시 시도하세요.", "The room is full. Try again when a seat opens.")
                : UnreachableText());
        }

        private void SetStatus(JoinState state, string text)
        {
            State = state;
            if (_statusLabel != null) _statusLabel.text = text;
            bool busy = state == JoinState.Connecting || state == JoinState.Connected;
            foreach (var row in _rows)
            {
                var button = row.GetComponentInChildren<Button>(true);
                if (button != null) button.interactable = !busy;
            }

            if (_refreshButton != null) _refreshButton.interactable = !busy;
        }

        private static string UnreachableText() => Local("ui.join.unreachable", "방에 들어가지 못했습니다. 친구가 아직 방을 열어 두었는지 확인하세요.", "Could not join. Check that your friend still has the room open.");

        private void AddRow(FriendRoom room, int index)
        {
            var row = new GameObject("FriendRoomRow", typeof(RectTransform));
            row.transform.SetParent(_list, false);
            var rect = (RectTransform)row.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -index * 64f);
            rect.sizeDelta = new Vector2(760f, 58f);
            string name = string.IsNullOrEmpty(room.Name) ? Local("ui.join.unnamed", "이름 없는 친구", "Unnamed friend") : room.Name;
            MakeText(row.transform, "FriendName", string.Format(Local("ui.join.room", "{0}의 방", "{0}'s room"), name), new Vector2(-130f, 0f), new Vector2(480f, 50f), 22, TextAnchor.MiddleLeft);
            ulong lobbyId = room.LobbyId;
            MakeButton(row.transform, "FriendRoomJoinButton", Local("ui.join.join", "참가", "Join"), new Vector2(270f, 0f), new Vector2(200f, 54f), () => JoinLobby(lobbyId));
            _rows.Add(row);
        }

        private void ClearRows()
        {
            foreach (var row in _rows)
                if (row != null) Destroy(row);
            _rows.Clear();
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
            frameRect.sizeDelta = new Vector2(900f, 720f);
            StyleSurface(frame);

            MakeText(frame.transform, "Title", Local("ui.join.title", "친구 게임 참가", "Join a friend"), new Vector2(0f, 300f), new Vector2(780f, 56f), 34, TextAnchor.MiddleLeft);
            var list = new GameObject("FriendRoomList", typeof(RectTransform));
            list.transform.SetParent(frame.transform, false);
            _list = (RectTransform)list.transform;
            // The list's top edge is where the first row sits.
            _list.anchorMin = _list.anchorMax = new Vector2(0.5f, 0.5f);
            _list.pivot = new Vector2(0.5f, 1f);
            _list.anchoredPosition = new Vector2(0f, 240f);
            _list.sizeDelta = new Vector2(780f, MaxRows * 64f);
            _statusLabel = MakeText(frame.transform, "JoinStatus", string.Empty, new Vector2(0f, -200f), new Vector2(780f, 72f), 20, TextAnchor.MiddleLeft);
            MakeButton(frame.transform, "JoinBackButton", Local("ui.join.back", "돌아가기", "Back"), new Vector2(-150f, -290f), new Vector2(260f, 66f), Hide);
            _refreshButton = MakeButton(frame.transform, "JoinRefreshButton", Local("ui.join.refresh", "새로고침", "Refresh"), new Vector2(150f, -290f), new Vector2(260f, 66f), Refresh);
            _root.SetActive(false);
        }

        private static Button MakeButton(Transform parent, string name, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
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
