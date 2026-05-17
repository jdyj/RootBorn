using Rootborn.Game.Housing;
using Rootborn.Game.Interiors;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.Housing
{
    public sealed class HouseUpgradePanel : MonoBehaviour
    {
        private const string DefaultStageAssetPath = "Assets/Data/Housing/UpgradeStages/HouseStage_ExpandedRoom_01.asset";

        private Text _summaryText;
        private Button _hireButton;
        private Button _directButton;
        private Button _closeButton;
        private HouseUpgradeStageDefinition _stage;
        private HouseStateSaveData _state;
        private HouseCurrencyWallet _wallet;
        private string _boundSaveSlot;
        private bool _saveHireOnClick;

        public bool HireButtonVisibleForTests => _hireButton != null && _hireButton.gameObject.activeSelf;
        public bool DirectButtonVisibleForTests => _directButton != null && _directButton.gameObject.activeSelf;
        public bool HireButtonInteractableForTests => _hireButton != null && _hireButton.interactable;
        public bool DirectButtonInteractableForTests => _directButton != null && _directButton.interactable;
        public string VisibleTextForTests => _summaryText != null ? _summaryText.text : string.Empty;

        public event System.Action<HouseUpgradeStageDefinition, HouseStateSaveData, HouseCurrencyWallet> HireRequested;
        public event System.Action<HouseUpgradeStageDefinition, HouseStateSaveData, HouseCurrencyWallet> DirectRequested;

        public static HouseUpgradePanel EnsureInScene(Canvas canvas)
        {
            var existing = FindFirstObjectByType<HouseUpgradePanel>(FindObjectsInactive.Include);
            if (existing != null)
            {
                return existing;
            }

            var root = new GameObject("HouseUpgradePanel", typeof(RectTransform), typeof(Image), typeof(HouseUpgradePanel));
            root.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(520f, 260f);
            root.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.92f);

            var panel = root.GetComponent<HouseUpgradePanel>();
            panel.Build();
            root.SetActive(false);
            return panel;
        }

        public void ShowForTests(HouseUpgradeStageDefinition stage, HouseStateSaveData state, HouseCurrencyWallet wallet, bool directEligible)
        {
            _saveHireOnClick = false;
            _boundSaveSlot = null;
            Show(stage, state, wallet, directEligible);
        }

        public void OpenDefaultOfferForTests(string saveSlot, bool directEligible)
        {
            var stage = LoadDefaultStageForTests();
            var state = HouseStatePersistence.Load(saveSlot);
            var balance = state.Currency != null ? state.Currency.Balance : 0;
            _boundSaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            _saveHireOnClick = true;
            Show(stage, state, new HouseCurrencyWallet(balance), directEligible);
        }

        public void Show(HouseUpgradeStageDefinition stage, HouseStateSaveData state, HouseCurrencyWallet wallet, bool directEligible)
        {
            if (_summaryText == null)
            {
                Build();
            }

            _stage = stage;
            _state = state;
            _wallet = wallet;
            gameObject.SetActive(true);

            bool hasStage = stage != null;
            bool canHire = hasStage && wallet != null && wallet.CanSpend(stage.HireCost);
            bool canDirect = hasStage && directEligible && wallet != null && wallet.CanSpend(stage.DirectCost);

            _summaryText.text = hasStage
                ? $"House Expansion\nHire: {stage.HireCost}\nDirect: {stage.DirectCost}\nBalance: {(wallet != null ? wallet.Balance : 0)}"
                : "No House expansion available.";

            _hireButton.gameObject.SetActive(hasStage);
            _hireButton.interactable = canHire;
            _directButton.gameObject.SetActive(hasStage && directEligible);
            _directButton.interactable = canDirect;
        }

        private static HouseUpgradeStageDefinition LoadDefaultStageForTests()
        {
#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<HouseUpgradeStageDefinition>(DefaultStageAssetPath);
            if (asset != null)
            {
                return asset;
            }
#endif
            return HouseUpgradeStageDefinition.CreateForTests(
                "house.stage.expanded_room.01",
                1,
                300,
                120,
                InteriorGenerationProfile.CreateExpandedOfficeForTests(),
                null,
                null);
        }

        private void Build()
        {
            _summaryText = MakeText(transform, "Summary", new Vector2(0f, 62f), new Vector2(460f, 110f), 22);
            _hireButton = MakeButton("HireConstructionButton", "Hire", new Vector2(-115f, -52f));
            _directButton = MakeButton("DirectConstructionButton", "Direct", new Vector2(115f, -52f));
            _closeButton = MakeButton("CloseHouseUpgradeButton", "Close", new Vector2(0f, -112f));
            _hireButton.onClick.AddListener(HandleHireClicked);
            _directButton.onClick.AddListener(HandleDirectClicked);
            _closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        }

        private void HandleHireClicked()
        {
            HireRequested?.Invoke(_stage, _state, _wallet);
            if (!_saveHireOnClick || _stage == null || _state == null || _wallet == null)
            {
                return;
            }

            var service = new HouseUpgradeService(new[] { _stage });
            var result = service.TryHire(_stage, _state, _wallet);
            if (result.Kind != HouseUpgradeResultKind.Applied)
            {
                Show(_stage, _state, _wallet, _directButton != null && _directButton.gameObject.activeSelf);
                return;
            }

            _state.Currency.Balance = _wallet.Balance;
            HouseStatePersistence.Save(_boundSaveSlot, _state);
            gameObject.SetActive(false);
        }

        private void HandleDirectClicked()
        {
            DirectRequested?.Invoke(_stage, _state, _wallet);
            if (!_saveHireOnClick || _stage == null || _state == null || _wallet == null)
            {
                return;
            }

            var context = new HouseUpgradeContext(_state, _wallet, null);
            var service = new HouseUpgradeService(new[] { _stage });
            if (!service.CanStartDirect(_stage, in context) || !_wallet.CanSpend(_stage.DirectCost))
            {
                Show(_stage, _state, _wallet, true);
                return;
            }

            _state.ActiveConstructionStageId = _stage.Id;
            _state.PlacedConstructionCells ??= System.Array.Empty<HouseConstructionCellSaveData>();
            HouseStatePersistence.Save(_boundSaveSlot, _state);
            gameObject.SetActive(false);
            SceneManager.LoadScene("House", LoadSceneMode.Single);
        }

        private Text MakeText(Transform parent, string name, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            return text;
        }

        private Button MakeButton(string name, string label, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(180f, 46f);
            rect.anchoredPosition = position;
            go.GetComponent<Image>().color = new Color(0.88f, 0.88f, 0.82f, 1f);

            var text = MakeText(go.transform, "Text", Vector2.zero, rect.sizeDelta, 18);
            text.text = label;
            text.color = Color.black;
            return go.GetComponent<Button>();
        }
    }
}