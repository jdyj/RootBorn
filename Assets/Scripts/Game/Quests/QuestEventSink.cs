namespace Rootborn.Game.Quests
{
    public interface IQuestEventSink
    {
        void Record(in QuestEvent questEvent);
    }
}
