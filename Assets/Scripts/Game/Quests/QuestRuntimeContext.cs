namespace Rootborn.Game.Quests
{
    public readonly struct QuestRuntimeContext
    {
        public readonly QuestLog QuestLog;

        public QuestRuntimeContext(QuestLog questLog)
        {
            QuestLog = questLog;
        }
    }
}
