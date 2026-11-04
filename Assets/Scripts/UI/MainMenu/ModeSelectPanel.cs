using System;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Localization;
using Rootborn.Game.Save;
using Rootborn.Network.Session;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.MainMenu
{
    public sealed class ModeSelectPanel : MonoBehaviour
    {
        [SerializeField] private Button _singleButton;
        [SerializeField] private Button _hostButton;
        [SerializeField] private Button _clientButton;
        [SerializeField] private Button _quitButton;
        [SerializeField] private string _townScene = "Town";
        [SerializeField] private string _hostLobbyScene = "HostLobby";
        private static readonly Color Ink = new Color32(48, 71, 83, 255);
        private GameObject _ownedBackdrop;

        private void Awake()
        {
            DisableButtonLabelRaycasts(_singleButton);
            DisableButtonLabelRaycasts(_hostButton);
            DisableButtonLabelRaycasts(_clientButton);
            DisableButtonLabelRaycasts(_quitButton);
            if (_singleButton != null) _singleButton.onClick.AddListener(OnSingle);
            if (_hostButton != null) _hostButton.onClick.AddListener(OnHost);
            if (_clientButton != null) _clientButton.onClick.AddListener(OnClient);
            if (_quitButton != null) _quitButton.onClick.AddListener(OnQuit);
            ApplyPresentation();
        }

        private void Start() => ApplyPresentation();

        private void ApplyPresentation()
        {
            var rect = transform as RectTransform;
            if (rect == null) return;
            EnsureBackdrop();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1440f, 840f);
            var skin = GetComponent<ModernUiTileImage>();
            if (skin == null) skin = gameObject.AddComponent<ModernUiTileImage>();
            skin.SetRecipe(ModernUiRecipes.CommonPanel);
            skin.Rebuild();
            MakeDecoration("UrbanMenuTitle", "ROOTBORN", new Vector2(76f, -64f), new Vector2(650f, 64f), 38);
            MakeDecoration("UrbanMenuTagline", Local("ui.menu.tagline", "오늘의 작은 선택이\n내일의 나와 마을을 바꿉니다.", "Small choices today\nshape tomorrow's life and town."),
                new Vector2(76f, -154f), new Vector2(625f, 110f), 28);
            MakeDecoration("UrbanMenuSchoolCaption", Local("ui.menu.growth", "배움 · 체험 · 관계 · 마을 재건", "Learning · Discovery · Relationships · Renewal"),
                new Vector2(740f, -664f), new Vector2(620f, 44f), 23);

            var illustration = transform.Find("UrbanMenuSchool");
            if (illustration == null)
            {
                var go = new GameObject("UrbanMenuSchool", typeof(RectTransform), typeof(Image), typeof(UrbanUiIcon));
                go.transform.SetParent(transform, false);
                illustration = go.transform;
            }
            var imageRect = (RectTransform)illustration;
            imageRect.anchorMin = imageRect.anchorMax = new Vector2(0f, 1f);
            imageRect.pivot = new Vector2(0f, 1f);
            imageRect.anchoredPosition = new Vector2(730f, -202f);
            imageRect.sizeDelta = new Vector2(640f, 400f);
            var image = illustration.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            illustration.GetComponent<UrbanUiIcon>().Configure("school");

            StyleButton(_singleButton, 0, Local("ui.menu.single", "이야기 시작 · 싱글", "Start story · Single"));
            StyleButton(_hostButton, 1, Local("ui.menu.host", "호스트 모드", "Host game"));
            StyleButton(_clientButton, 2, Local("ui.menu.client", "클라이언트 접속", "Join game"));
            StyleButton(_quitButton, 3, Local("ui.menu.quit", "종료", "Quit"));
        }

        private void StyleButton(Button button, int index, string label)
        {
            if (button == null) return;
            var rect = (RectTransform)button.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(76f, -300f - index * 100f);
            rect.sizeDelta = new Vector2(454f, 66f);
            var rootImage = button.GetComponent<Image>();
            if (rootImage != null) rootImage.raycastTarget = true;
            var text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = label;
                text.fontSize = 24;
                text.alignment = TextAnchor.MiddleCenter;
                text.raycastTarget = false;
                text.supportRichText = false;
                text.rectTransform.anchorMin = Vector2.zero;
                text.rectTransform.anchorMax = Vector2.one;
                text.rectTransform.offsetMin = new Vector2(18f, 6f);
                text.rectTransform.offsetMax = new Vector2(-18f, -6f);
            }
            var skin = button.GetComponent<ModernUiTileImage>();
            if (skin == null) skin = button.gameObject.AddComponent<ModernUiTileImage>();
            skin.SetRecipe(ModernUiRecipes.CommonPanel);
            skin.Rebuild();
        }

        private void MakeDecoration(string name, string value, Vector2 position, Vector2 size, int fontSize)
        {
            var child = transform.Find(name);
            if (child == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Text));
                go.transform.SetParent(transform, false); child = go.transform;
            }
            var rect = (RectTransform)child;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = child.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value; text.fontSize = fontSize; text.color = Ink;
            text.alignment = TextAnchor.UpperLeft; text.raycastTarget = false; text.supportRichText = false;
        }

        private static string Local(string key, string korean, string english)
        {
            string value = GameText.Get(key);
            return value == key ? (GameText.Language == "ko" ? korean : english) : value;
        }

        private void EnsureBackdrop()
        {
            if (transform.parent == null || transform.parent.GetComponent<Canvas>() == null) return;
            if (transform.parent.Find("UrbanMenuBackdrop") != null) return;
            _ownedBackdrop = new GameObject("UrbanMenuBackdrop", typeof(RectTransform), typeof(Image));
            _ownedBackdrop.transform.SetParent(transform.parent, false);
            _ownedBackdrop.transform.SetAsFirstSibling();
            var rect = (RectTransform)_ownedBackdrop.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = _ownedBackdrop.GetComponent<Image>();
            image.color = new Color32(211, 216, 212, 255); image.raycastTarget = false;
        }

        private void OnDestroy()
        {
            if (_ownedBackdrop == null) return;
            _ownedBackdrop.SetActive(false);
            if (Application.isPlaying) Destroy(_ownedBackdrop); else DestroyImmediate(_ownedBackdrop);
        }

        private void OnSingle()
        {
            ApplyMode(SessionMode.Single);
            var slots = SaveSlotSelectPanel.EnsureInScene();
            slots.Show();
        }

        private async void OnHost()
        {
            ApplyMode(SessionMode.Host);
            EnsureSaveContext(GameBootstrap.Config);
            await NetworkBootstrap.StartSessionAsync(GameBootstrap.Config);
            SceneManager.LoadScene(_townScene);
        }

        private void OnClient()
        {
            ApplyMode(SessionMode.Client);
            SceneManager.LoadScene(_hostLobbyScene);
        }

        private void OnQuit() => Application.Quit();

        private static void DisableButtonLabelRaycasts(Button button)
        {
            if (button == null) return;
            var labels = button.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < labels.Length; i++) labels[i].raycastTarget = false;
        }

        private static void ApplyMode(SessionMode mode)
        {
            if (GameBootstrap.Config != null) GameBootstrap.Config.Mode = mode;
        }

        private static void EnsureSaveContext(AppConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.SaveSlot)) return;
            var existing = ActiveSaveContext.Metadata;
            if (existing != null && existing.SlotId == config.SaveSlot) return;
            ActiveSaveContext.Set(new SaveSlotMetadata
            {
                SlotId = config.SaveSlot, DisplayName = config.SaveSlot,
                CreatedAtUtcTicks = DateTime.UtcNow.Ticks, UpdatedAtUtcTicks = DateTime.UtcNow.Ticks,
            });
        }
    }
}
