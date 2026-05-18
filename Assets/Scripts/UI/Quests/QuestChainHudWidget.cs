using Rootborn.Game.Quests;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.Quests
{
    public sealed class QuestChainHudWidget : MonoBehaviour
    {
        private GameObject _root;
        private Text _title;
        private Text _objective;
        private Text _nextAction;

        public string VisibleText => ((_title != null ? _title.text : string.Empty) + "\n" + (_objective != null ? _objective.text : string.Empty) + "\n" + (_nextAction != null ? _nextAction.text : string.Empty)).Trim();

        public static QuestChainHudWidget EnsureInScene(Canvas canvas)
        {
            if (canvas != null)
            {
                var existing = canvas.GetComponentInChildren<QuestChainHudWidget>(true);
                if (existing != null) return existing;
            }

            var go = new GameObject("QuestChainHudWidget", typeof(RectTransform));
            if (canvas != null) go.transform.SetParent(canvas.transform, false);
            return go.AddComponent<QuestChainHudWidget>();
        }

        private void Awake()
        {
            BuildIfNeeded();
        }

        public void Refresh(QuestChainDefinition[] chains, QuestChainLog log)
        {
            BuildIfNeeded();
            var tracked = FindTracked(chains, log) ?? FindCompleted(chains, log);
            if (tracked == null || log == null)
            {
                _title.text = string.Empty;
                _objective.text = string.Empty;
                _nextAction.text = string.Empty;
                _root.SetActive(false);
                return;
            }

            var state = log.GetState(tracked);
            _title.text = tracked.Id + "\n" + state;
            var step = tracked.Steps != null && tracked.Steps.Length > 0 ? tracked.Steps[0] : null;
            var objective = step != null && step.Objectives != null && step.Objectives.Length > 0 ? step.Objectives[0] : null;
            if (objective != null)
            {
                _objective.text = objective.DisplayKey + " " + log.GetObjectiveProgress(tracked, 0) + " / " + objective.RequiredCount;
                _nextAction.text = state == QuestChainState.Completed ? "Completed" : "Next\n" + objective.DisplayKey;
            }
            else
            {
                _objective.text = state.ToString();
                _nextAction.text = "Next\nOpen quest chain log";
            }

            _root.SetActive(true);
        }

        private static QuestChainDefinition FindTracked(QuestChainDefinition[] chains, QuestChainLog log)
        {
            if (chains == null || log == null) return null;
            for (int i = 0; i < chains.Length; i++)
            {
                var chain = chains[i];
                if (chain != null && log.IsTracked(chain)) return chain;
            }

            return null;
        }

        private static QuestChainDefinition FindCompleted(QuestChainDefinition[] chains, QuestChainLog log)
        {
            if (chains == null || log == null) return null;
            for (int i = 0; i < chains.Length; i++)
            {
                var chain = chains[i];
                if (chain != null && log.GetState(chain) == QuestChainState.Completed) return chain;
            }

            return null;
        }

        private void BuildIfNeeded()
        {
            if (_root != null) return;
            _root = new GameObject("QuestChainHudRoot", typeof(RectTransform), typeof(ModernUiTileImage));
            _root.transform.SetParent(transform, false);
            var rect = (RectTransform)_root.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -190f);
            rect.sizeDelta = new Vector2(360f, 128f);
            var tile = _root.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);
            tile.Rebuild();
            _title = MakeText(rect, "Title", string.Empty, new Vector2(16f, -14f), new Vector2(320f, 38f), 15);
            _objective = MakeText(rect, "Objective", string.Empty, new Vector2(16f, -54f), new Vector2(320f, 24f), 14);
            _nextAction = MakeText(rect, "NextAction", string.Empty, new Vector2(16f, -82f), new Vector2(320f, 36f), 13);
        }

        private static Text MakeText(RectTransform parent, string name, string value, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
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
    }
}
