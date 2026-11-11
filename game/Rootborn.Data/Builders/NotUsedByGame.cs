namespace Rootborn.Data.Builders
{
    /// <summary>
    /// 규칙이지만 지금 게임(Town·학생 생활)이 부르지 않아 옮기지 않은 종류. 근거는 얼린 Unity 판(태그 unity-playable-20261111)의
    /// 코드·씬·에셋을 찾아 본 결과다. 게임이 쓰게 되면 그 영역을 옮기고 여기서 뺀다.
    /// </summary>
    internal static class NotUsedByGame
    {
        public static void Register(DefinitionBuilders b)
        {
            b.NotUsedByGame("StatusEffectDefinition",
                "시간이 지나며 쌓이는 배고픔·피로·외로움(레거시 농장 상태). 쓰는 곳은 PlayerStatus뿐인데 어느 씬·프리팹에도 없다"
                + "(Assets/Scripts/Game/Status/PlayerStatus.cs, 씬에서 그 GUID 검색 0건). GameDataRegistry.Statuses를 읽는 코드도 없다."
                + " 학생 컨디션은 StatusDefinition이 맡는다");

            const string Knowledge = "지식 노드는 GatherInteractor가 자원을 칠 때만 평가하는데, Town에 있는 자원은 나무(Tree) 3개뿐이고"
                + " 유일한 트리거(HitGroundWithRockTrigger)는 바위(Rock)를 맨손으로 쳐야 한다. 바위 노드는 어느 씬·프리팹에도 없다"
                + "(Resource_Rock.asset GUID 검색 0건). Fire는 트리거가 없어 열리지 않는다. 지식을 가리키는 데이터"
                + "(KnowledgeUnlockedCondition, UnlockKnowledgeCompletionEffect, 도감 지식 규칙)도 data/에 없고 지식 진행은 저장되지 않는다";
            b.NotUsedByGame("KnowledgeNode", Knowledge);
            b.NotUsedByGame("HitGroundWithRockTrigger", Knowledge);

            b.NotUsedByGame("ToolDefinition",
                "게임 중 도구를 장착하는 길이 없다. PlayerInventory.EquipTool을 부르는 곳은 세이브의 EquippedToolItemId 되살리기"
                + "(PlayerInventory.LoadFromSaveData, AdultCareerRuntimeComponent)뿐이고 그 값을 정하는 화면이 없다. 그래서 자원은 늘 맨손으로 친다."
                + " 자원의 선호 도구는 id(ResourceNodeDefinition.PreferredToolId)로만 남겼다");
            b.NotUsedByGame("FishingAnimationEffect",
                "낚싯대(ToolDefinition FishingRod)의 효과로만 쓰이고 낚싯대를 장착할 길이 없다(ToolDefinition 참조). 내용도 캐릭터 애니메이션 재생뿐이다");
            b.NotUsedByGame("GatherResourceStep",
                "레시피의 만들기 단계. RecipeDefinition.AllStepsSatisfied·CraftAttemptState를 부르는 곳이 Crafting 폴더 밖에 없다."
                + " 게임은 레시피를 공개해 목표 저널에 이름과 힌트만 보인다");
            b.NotUsedByGame("HeirTrait",
                "레거시 세대 특성. 쓰는 곳은 Assets/Scripts/Game/Heir·Lineage·Family 안뿐이고 그 밖에서 그 코드나 GameDataRegistry.Traits를 부르는 곳이 없다");
        }
    }
}
