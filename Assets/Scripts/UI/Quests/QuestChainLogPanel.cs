using System;
using System.Collections.Generic;
using Rootborn.Game.Quests;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.Quests
{
    public sealed class QuestChainLogPanel : MonoBehaviour
    {
        private const int MaxVisibleChainCards = 8;
        private const string CompletionRewardId = "completion";

        private QuestChainDefinition[] _chains = Array.Empty<QuestChainDefinition>();
        private QuestChainLog _log;
        private RewardRuntimeContext _rewardContext;
        private string _careerFilter = string.Empty;
        private string _selectedChainId = string.Empty;
        private QuestChainState? _stateFilter;
        private Action _onChanged;
        private GameObject _root;
        private Text _careerText;
        private Text _stateText;
        private Text _listText;
        private Text _detailText;
        private Button _acceptButton;
        private Button _trackButton;
        private Button _claimRewardButton;
        private readonly Button[] _chainButtons = new Button[MaxVisibleChainCards];
        private readonly Text[] _chainButtonLabels = new Text[MaxVisibleChainCards];

        public string VisibleText => ((_careerText != null ? _careerText.text : string.Empty) + "\n" + (_stateText != null ? _stateText.text : string.Empty) + "\n" + (_listText != null ? _listText.text : string.Empty) + "\n" + (_detailText != null ? _detailText.text : string.Empty)).Trim();

        public static QuestChainLogPanel EnsureInScene(Canvas canvas)
        {
            if (canvas != null)
            {
                var existing = canvas.GetComponentInChildren<QuestChainLogPanel>(true);
                if (existing != null) return existing;
            }

            var go = new GameObject("QuestChainLogPanel", typeof(RectTransform));
            if (canvas != null) go.transform.SetParent(canvas.transform, false);
            return go.AddComponent<QuestChainLogPanel>();
        }

        private void Awake()
        {
            BuildIfNeeded();
            Hide();
        }

        public void Bind(QuestChainDefinition[] chains, QuestChainLog log, Action onChanged)
        {
            Bind(chains, log, onChanged, default);
        }

        public void Bind(QuestChainDefinition[] chains, QuestChainLog log, Action onChanged, RewardRuntimeContext rewardContext)
        {
            _chains = chains ?? Array.Empty<QuestChainDefinition>();
            _log = log;
            _rewardContext = rewardContext;
            _onChanged = onChanged;
            BuildIfNeeded();
            Refresh();
        }

        public void Show(QuestChainDefinition[] chains, QuestChainLog log)
        {
            Bind(chains, log, null, default);
            _root.SetActive(true);
        }

        public void Open()
        {
            BuildIfNeeded();
            Refresh();
            _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        public void Refresh()
        {
            BuildIfNeeded();
            _careerText.text = "Career: " + (string.IsNullOrEmpty(_careerFilter) ? "All" : _careerFilter);
            _stateText.text = "State: " + (_stateFilter.HasValue ? _stateFilter.Value.ToString() : "All");

            var rows = new List<string>();
            var visible = new List<QuestChainDefinition>();
            for (int i = 0; i < _chains.Length; i++)
            {
                var chain = _chains[i];
                if (!IsVisible(chain)) continue;
                var state = _log != null ? _log.GetState(chain) : QuestChainState.Available;
                rows.Add(chain.Id + " | " + state + " | " + chain.RelatedCareerInterestId);
                visible.Add(chain);
            }

            var selected = SelectVisibleChain(visible);
            string detail = selected != null ? BuildDetail(selected, _log != null ? _log.GetState(selected) : QuestChainState.Available) : string.Empty;
            _listText.text = string.Join("\n", rows.ToArray());
            _detailText.text = detail;
            RefreshChainButtons(visible, selected);
            RefreshButtons(selected);
        }

        public void SelectCareerFilter(string careerInterestId)
        {
            _careerFilter = careerInterestId ?? string.Empty;
            Refresh();
        }

        public void SelectStateFilter(QuestChainState state)
        {
            _stateFilter = state;
            Refresh();
        }

        public void ClearStateFilter()
        {
            _stateFilter = null;
            Refresh();
        }

        private void AcceptSelected()
        {
            var selected = SelectVisibleChain();
            if (selected == null || _log == null) return;
            if (_log.Accept(selected))
            {
                Refresh();
                _onChanged?.Invoke();
            }
        }

        private void TrackSelected()
        {
            var selected = SelectVisibleChain();
            if (selected == null || _log == null) return;
            if (_log.Track(selected))
            {
                Refresh();
                _onChanged?.Invoke();
            }
        }

        private void ClaimSelectedReward()
        {
            var selected = SelectVisibleChain();
            if (selected == null || _log == null) return;
            if (_log.ClaimCompletionRewards(selected, in _rewardContext))
            {
                Refresh();
                _onChanged?.Invoke();
            }
        }

        private void SelectChain(string chainId)
        {
            _selectedChainId = chainId ?? string.Empty;
            Refresh();
        }

        private QuestChainDefinition SelectVisibleChain()
        {
            var visible = new List<QuestChainDefinition>();
            for (int i = 0; i < _chains.Length; i++) if (IsVisible(_chains[i])) visible.Add(_chains[i]);
            return SelectVisibleChain(visible);
        }

        private QuestChainDefinition SelectVisibleChain(List<QuestChainDefinition> visible)
        {
            if (visible == null || visible.Count == 0) return null;
            if (!string.IsNullOrEmpty(_selectedChainId))
            {
                for (int i = 0; i < visible.Count; i++)
                {
                    var chain = visible[i];
                    if (chain != null && chain.Id == _selectedChainId) return chain;
                }
            }

            _selectedChainId = visible[0] != null ? visible[0].Id : string.Empty;
            return visible[0];
        }

        private bool IsVisible(QuestChainDefinition chain)
        {
            if (chain == null) return false;
            var state = _log != null ? _log.GetState(chain) : QuestChainState.Available;
            if (!string.IsNullOrEmpty(_careerFilter) && chain.RelatedCareerInterestId != _careerFilter) return false;
            if (_stateFilter.HasValue && state != _stateFilter.Value) return false;
            return true;
        }

        private void RefreshChainButtons(List<QuestChainDefinition> visible, QuestChainDefinition selected)
        {
            for (int i = 0; i < _chainButtons.Length; i++)
            {
                var button = _chainButtons[i];
                var label = _chainButtonLabels[i];
                button.onClick.RemoveAllListeners();
                if (visible != null && i < visible.Count && visible[i] != null)
                {
                    var chain = visible[i];
                    var state = _log != null ? _log.GetState(chain) : QuestChainState.Available;
                    string chainId = chain.Id;
                    button.name = "QuestChainCard_" + SanitizeName(chainId);
                    button.interactable = true;
                    button.gameObject.SetActive(true);
                    button.GetComponent<Image>().color = chain == selected ? new Color(0.82f, 0.66f, 0.36f, 1f) : new Color(0.62f, 0.50f, 0.30f, 1f);
                    label.text = chain.Id + "\n" + state;
                    button.onClick.AddListener(() => SelectChain(chainId));
                }
                else
                {
                    button.name = "QuestChainCard_Empty_" + i;
                    label.text = string.Empty;
                    button.gameObject.SetActive(false);
                }
            }
        }

        private void RefreshButtons(QuestChainDefinition selected)
        {
            if (_acceptButton == null || _trackButton == null || _claimRewardButton == null) return;
            var state = selected != null && _log != null ? _log.GetState(selected) : QuestChainState.Available;
            _acceptButton.interactable = selected != null && state == QuestChainState.Available;
            _trackButton.interactable = selected != null && (state == QuestChainState.Active || state == QuestChainState.Paused || state == QuestChainState.Tracked);
            _claimRewardButton.interactable = selected != null && _log != null && _log.CanClaimCompletionRewards(selected, in _rewardContext);
        }

        private string BuildDetail(QuestChainDefinition chain, QuestChainState state)
        {
            string text = chain.Id + "\n" + state;
            var step = chain.Steps != null && chain.Steps.Length > 0 ? chain.Steps[0] : null;
            var objective = step != null && step.Objectives != null && step.Objectives.Length > 0 ? step.Objectives[0] : null;
            if (objective != null && _log != null) text += "\n" + objective.DisplayKey + " " + _log.GetObjectiveProgress(chain, 0) + " / " + objective.RequiredCount;
            if (state == QuestChainState.Blocked && _log != null) text += "\n" + _log.GetBlockedReasonId(chain) + "\n" + _log.GetBlockedSummary(chain);
            if (state == QuestChainState.Completed && _log != null) text += "\nReward: " + (_log.IsRewardClaimed(chain, CompletionRewardId) ? "Claimed" : "Ready");
            return text;
        }

        private void BuildIfNeeded()
        {
            if (_root != null) return;
            _root = new GameObject("QuestChainLogRoot", typeof(RectTransform), typeof(ModernUiTileImage));
            _root.transform.SetParent(transform, false);
            var rect = (RectTransform)_root.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(760f, 420f);
            var tile = _root.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);
            tile.Rebuild();
            _careerText = MakeText(rect, "CareerFilter", string.Empty, new Vector2(-350f, 170f), new Vector2(180f, 28f), 15);
            _stateText = MakeText(rect, "StateFilter", string.Empty, new Vector2(-350f, 135f), new Vector2(180f, 28f), 15);
            _listText = MakeText(rect, "ChainList", string.Empty, new Vector2(-120f, 150f), new Vector2(260f, 300f), 14);
            _detailText = MakeText(rect, "ChainDetail", string.Empty, new Vector2(170f, 150f), new Vector2(320f, 260f), 14);
            for (int i = 0; i < _chainButtons.Length; i++)
            {
                var button = MakeButton(rect, "QuestChainCard_Empty_" + i, string.Empty, new Vector2(-120f, 130f - i * 40f), new Vector2(260f, 36f));
                _chainButtons[i] = button;
                _chainButtonLabels[i] = button.GetComponentInChildren<Text>(true);
                button.gameObject.SetActive(false);
            }
            _acceptButton = MakeButton(rect, "QuestChainAcceptButton", "Accept", new Vector2(70f, -160f), new Vector2(105f, 38f));
            _trackButton = MakeButton(rect, "QuestChainTrackButton", "Track", new Vector2(190f, -160f), new Vector2(105f, 38f));
            _claimRewardButton = MakeButton(rect, "QuestChainClaimRewardButton", "Claim", new Vector2(310f, -160f), new Vector2(105f, 38f));
            _acceptButton.onClick.AddListener(AcceptSelected);
            _trackButton.onClick.AddListener(TrackSelected);
            _claimRewardButton.onClick.AddListener(ClaimSelectedReward);
        }

        private static Text MakeText(RectTransform parent, string name, string value, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.UpperLeft;
            text.color = new Color(0.25f, 0.18f, 0.12f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private static Button MakeButton(RectTransform parent, string name, string label, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.72f, 0.58f, 0.32f, 1f);
            var text = MakeText(rect, "Label", label, Vector2.zero, size, 14);
            text.alignment = TextAnchor.MiddleCenter;
            return go.GetComponent<Button>();
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "empty";
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++) if (!char.IsLetterOrDigit(chars[i])) chars[i] = '_';
            return new string(chars);
        }
    }
}
