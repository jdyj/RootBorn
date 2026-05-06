namespace Rootborn.Game.Quests
{
    public readonly struct RewardRuntimeContext
    {
        public readonly QuestLog QuestLog;

        public RewardRuntimeContext(QuestLog questLog)
        {
            QuestLog = questLog;
        }
    }
}
