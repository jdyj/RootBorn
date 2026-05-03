using Rootborn.Game.Generation;
using Rootborn.Game.Heir;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.Generation
{
    public sealed class GenerationTransitionPanel : MonoBehaviour
    {
        [SerializeField] private GenerationManager _manager;
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _title;
        [SerializeField] private Text _traits;
        [SerializeField] private Button _continueButton;

        private void Awake()
        {
            if (_root != null) _root.SetActive(false);
            if (_continueButton != null) _continueButton.onClick.AddListener(Hide);
        }

        private void OnEnable()
        {
            if (_manager != null) _manager.OnGenerationChanged += HandleChanged;
        }

        private void OnDisable()
        {
            if (_manager != null) _manager.OnGenerationChanged -= HandleChanged;
        }

        private void HandleChanged(GenerationProfile prev, GenerationProfile next, HeirData heir)
        {
            if (_root != null) _root.SetActive(true);
            if (_title != null) _title.text = $"세대 교체 → 제 {next.GenerationIndex} 세대";
            if (_traits != null && heir != null)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < heir.Traits.Count; i++)
                {
                    sb.AppendLine($"- {heir.Traits[i].DisplayKey}");
                }
                _traits.text = sb.ToString();
            }
        }

        private void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }
    }
}
