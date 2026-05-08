namespace Rootborn.Game.ModernSociety
{
    public struct ModernSocietyStats
    {
        public int Energy;
        public int Money;
        public int Anxiety;
        public int Knowledge;
        public int Relationships;

        public ModernSocietyStats(int energy, int money, int anxiety, int knowledge, int relationships)
        {
            Energy = energy;
            Money = money;
            Anxiety = anxiety;
            Knowledge = knowledge;
            Relationships = relationships;
        }
    }
}
