namespace Rootborn.Data.Builders
{
    /// <summary>
    /// 모든 영역의 등록을 부르는 유일한 곳. Host와 테스트는 GameDataLoader.Load(dir, RootbornBuilders.Register)만 쓴다.
    /// 게임을 돌릴 때는 실행 서비스를 넘기는 Register(b, services)를 쓴다(<see cref="RuleServices"/>).
    /// 새 종류 = Core 클래스 하나 + 영역 파일에 b.Add 한 줄.
    /// </summary>
    public static class RootbornBuilders
    {
        public static void Register(DefinitionBuilders b) => Register(b, null);

        public static void Register(DefinitionBuilders b, RuleServices services)
        {
            InventoryBuilders.Register(b);
            QuestBuilders.Register(b);
            StudentLifeBuilders.Register(b);
            DailyPlanBuilders.Register(b);
            CareerBuilders.Register(b);
            MinigameBuilders.Register(b);
            AdultCareerBuilders.Register(b);
            LocationBuilders.Register(b, services);
            DailyEventBuilders.Register(b);
            CampaignBuilders.Register(b);
            TownLifeBuilders.Register(b);
            ExplorationBuilders.Register(b);
            MilestoneBuilders.Register(b);
            CharacterBuilders.Register(b);
            TimeBuilders.Register(b);
            WorldStateBuilders.Register(b);
            EncyclopediaBuilders.Register(b);
            DiscoveryClueBuilders.Register(b);
            DialogueBuilders.Register(b);
            PlacementBuilders.Register(b);
            GatheringBuilders.Register(b);

            NotRules.Register(b);
            NotPortedYet.Register(b);
            NotUsedByGame.Register(b);
        }
    }
}
