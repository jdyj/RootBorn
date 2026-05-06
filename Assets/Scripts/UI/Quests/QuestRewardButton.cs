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

        public void Bind(QuestLog questLog, QuestDefinition quest, RewardRuntimeContext context)
        {
            _questLog = questLog;
            _quest = quest;
            _context = context;
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
        }
    }
}
