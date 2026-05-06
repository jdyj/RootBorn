using Rootborn.Game.Quests;
using UnityEngine;

namespace Rootborn.Game.Dialogue
{
    [DisallowMultipleComponent]
    public sealed class QuestProvider : MonoBehaviour
    {
        [SerializeField] private QuestDefinition[] _quests = System.Array.Empty<QuestDefinition>();

        public QuestDefinition[] Quests => _quests;

        public void Bind(QuestDefinition[] quests)
        {
            _quests = quests ?? System.Array.Empty<QuestDefinition>();
        }
    }
}
