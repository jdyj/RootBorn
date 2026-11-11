using Rootborn.Core.Quests;

namespace Rootborn.Core.WorldState
{
    /// <summary>
    /// 월드 상태 사용·발견 단서·해석이 이어지는 퀘스트를 여는 방법. 아직 시작하지 않은 퀘스트만 기록에 넣고 받는다.
    /// Unity 판은 매번 QuestLog.AddQuest로 다시 넣어 진행 중이거나 끝난 퀘스트의 진행도를 지웠다.
    /// </summary>
    public static class FollowUpQuests
    {
        /// <summary>기록이 없으면 true(원본처럼 열었다고 본다). 이미 시작한 퀘스트면 false.</summary>
        public static bool TryStart(QuestLog questLog, QuestDefinition quest)
        {
            if (quest == null) return false;
            if (questLog == null) return true;
            if (questLog.GetState(quest.Id) != QuestStage.NotStarted) return false;
            questLog.AddQuest(quest);
            questLog.Accept(quest);
            return true;
        }
    }
}
