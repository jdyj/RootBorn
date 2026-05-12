using Rootborn.Game.StudentLife;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public sealed class MilestoneHudPanel : MonoBehaviour
    {
        private GameObject _root;
        private Text _title;
        private Text _progress;
        private Text _recommendations;

        public string VisibleText => ((_title != null ? _title.text : string.Empty) + "\n" + (_progress != null ? _progress.text : string.Empty) + "\n" + (_recommendations != null ? _recommendations.text : string.Empty)).Trim();

        public static MilestoneHudPanel EnsureInScene(Canvas canvas)
        {
            var existing = Object.FindFirstObjectByType<MilestoneHudPanel>(FindObjectsInactive.Include);
            if (existing != null) return existing;
            var go = new GameObject("MilestoneHudPanel");
            go.transform.SetParent(canvas.transform, false);
            return go.AddComponent<MilestoneHudPanel>();
        }

        private void Awake()
        {
            BuildIfNeeded();
        }

        public void Show(MilestoneDefinition[] milestones, MilestoneProgress milestoneProgress)
        {
            Refresh(milestones, milestoneProgress, default);
        }

        public void Refresh(MilestoneDefinition[] milestones, MilestoneProgress milestoneProgress, MilestoneApplyResult applyResult)
        {
            if (milestones == null || milestones.Length == 0)
            {
                milestones = MilestoneRegistryResolver.ResolveMilestones(null);
            }
            if (milestoneProgress == null)
            {
                milestoneProgress = new MilestoneProgress("default", "player", milestones);
            }
            BuildIfNeeded();
            _title.text = "Goals";
            _progress.text = FormatProgress(milestones, milestoneProgress);
            _recommendations.text = FormatRecommendations(applyResult.RecommendedActionIds, milestones);
            _root.SetActive(true);
        }

        private static string FormatProgress(MilestoneDefinition[] milestones, MilestoneProgress milestoneProgress)
        {
            if (milestones == null || milestones.Length == 0) return "No active milestone";
            var text = string.Empty;
            for (int i = 0; i < milestones.Length; i++)
            {
                var milestone = milestones[i];
                if (milestone == null) continue;
                if (text.Length > 0) text += "\n";
                text += milestone.Id;
                var objectives = milestone.Objectives;
                int complete = 0;
                for (int j = 0; j < objectives.Length; j++)
                {
                    var objective = objectives[j];
                    if (objective == null) continue;
                    int value = milestoneProgress != null ? milestoneProgress.GetObjectiveProgress(milestone, objective) : 0;
                    if (value >= objective.RequiredValue) complete++;
                    text += " " + value + "/" + objective.RequiredValue;
                }
                if (milestoneProgress != null && milestoneProgress.IsCompleted(milestone)) text += " complete";
                else text += " objectives " + complete + "/" + milestone.RequiredObjectiveCount;
            }

            return string.IsNullOrEmpty(text) ? "No active milestone" : text;
        }

        private static string FormatRecommendations(string[] explicitRecommendations, MilestoneDefinition[] milestones)
        {
            var text = "Next";
            bool added = false;
            if (explicitRecommendations != null)
            {
                for (int i = 0; i < explicitRecommendations.Length; i++)
                {
                    if (string.IsNullOrEmpty(explicitRecommendations[i])) continue;
                    text += "\n" + explicitRecommendations[i];
                    added = true;
                }
            }

            if (!added && milestones != null)
            {
                for (int i = 0; i < milestones.Length; i++)
                {
                    var routes = milestones[i] != null ? milestones[i].Routes : null;
                    if (routes == null) continue;
                    for (int j = 0; j < routes.Length; j++)
                    {
                        string id = routes[j] != null ? routes[j].RecommendedActionId : string.Empty;
                        if (string.IsNullOrEmpty(id) || text.Contains(id)) continue;
                        text += "\n" + id;
                        added = true;
                    }
                }
            }

            return added ? text : text + "\nChoose a town activity";
        }

        private void BuildIfNeeded()
        {
            if (_root != null) return;
            _root = new GameObject("MilestoneHudRoot", typeof(RectTransform), typeof(ModernUiTileImage));
            _root.transform.SetParent(transform, false);
            var rt = (RectTransform)_root.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -24f);
            rt.sizeDelta = new Vector2(430f, 170f);
            var tile = _root.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);
            tile.Rebuild();
            _title = MakeText(rt, "Title", "Goals", new Vector2(18f, -18f), new Vector2(390f, 26f), 20, TextAnchor.UpperLeft);
            _progress = MakeText(rt, "Progress", string.Empty, new Vector2(18f, -50f), new Vector2(390f, 62f), 15, TextAnchor.UpperLeft);
            _recommendations = MakeText(rt, "Recommendations", string.Empty, new Vector2(18f, -113f), new Vector2(390f, 44f), 15, TextAnchor.UpperLeft);
        }

        private static Text MakeText(RectTransform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.25f, 0.18f, 0.12f, 1f);
            text.raycastTarget = false;
            return text;
        }
    }
}
