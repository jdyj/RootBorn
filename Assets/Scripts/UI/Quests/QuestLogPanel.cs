using Rootborn.Game.Quests;
using UnityEngine;

namespace Rootborn.UI.Quests
{
    [DisallowMultipleComponent]
    public sealed class QuestLogPanel : MonoBehaviour
    {
        private QuestLog _questLog;

        public QuestLog QuestLog => _questLog;

        public void Bind(QuestLog questLog)
        {
            _questLog = questLog;
        }
    }
}
