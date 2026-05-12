using Rootborn.Game.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.Quests
{
    [DisallowMultipleComponent]
    public sealed class QuestRewardButton : MonoBehaviour
    {
        [SerializeField] private Button _button;

        private QuestLog _questLog;
        private QuestDefinition _quest;
        private RewardRuntimeContext _context;

        private void Awake()
        {
            EnsureButton();
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleButtonClicked);
            }
        }

        public void Bind(QuestLog questLog, QuestDefinition quest, RewardRuntimeContext context)
        {
            _questLog = questLog;
            _quest = quest;
            _context = context;
            EnsureButton();
            Refresh();
        }

        public void Refresh()
        {
            EnsureButton();
            if (_button != null)
            {
                _button.interactable = _questLog != null && _questLog.CanClaimReward(_quest, in _context);
            }
        }

        public bool Click()
        {
            bool claimed = _questLog != null && _questLog.ClaimReward(_quest, in _context);
            Refresh();
            return claimed;
        }

        private void EnsureButton()
        {
            if (_button == null)
            {
                _button = GetComponent<Button>();
            }

            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleButtonClicked);
                _button.onClick.AddListener(HandleButtonClicked);
            }
        }

        private void HandleButtonClicked()
        {
            Click();
        }
    }
}
