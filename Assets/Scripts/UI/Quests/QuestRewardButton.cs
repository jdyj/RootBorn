using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.WorldState;
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
            if (claimed)
            {
                SaveQuestLog();
                SaveWorldStateProgress();
            }
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

        private void SaveQuestLog()
        {
            var metadata = ActiveSaveContext.Metadata;
            if (metadata == null || string.IsNullOrEmpty(metadata.SlotId) || _questLog == null) return;
            string playerId = _context.StudentLifeProgress != null ? _context.StudentLifeProgress.PlayerId : PlayerIdentity.DefaultPlayerId;
            new SaveService(metadata.SlotId).WriteJson(QuestLogFileNameFor(playerId), JsonUtility.ToJson(_questLog.ToSaveData(), true));
        }

        private void SaveWorldStateProgress()
        {
            if (_context.WorldStateProgress == null) return;
            WorldStateProgressPersistence.Save(_context.WorldStateProgress);
        }

        private static string QuestLogFileNameFor(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == PlayerIdentity.DefaultPlayerId) return "quest-log.json";
            return "quest-log-" + SanitizeFileName(playerId) + ".json";
        }

        private static string SanitizeFileName(string value)
        {
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++) if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_') chars[i] = '_';
            return new string(chars);
        }
    }
}
