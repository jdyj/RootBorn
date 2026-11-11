namespace Rootborn.Data.Builders
{
    /// <summary>규칙이 아닌 종류: 그림·타일·화면·맵·캐릭터 겉모습 전용, 또는 Core가 id 문자열로만 받는 이름표.</summary>
    internal static class NotRules
    {
        private const string NameOnly = "화면 이름 문구만 있다. Core 규칙은 이 정의를 id 문자열로만 받는다";
        private const string MapGeneration = "맵·실내 생성과 타일 배치(월드·Host 몫)";
        private const string Look = "캐릭터 겉모습과 애니메이션";

        public static void Register(DefinitionBuilders b)
        {
            b.NotARule("TraitDefinition", NameOnly);
            b.NotARule("SkillDefinition", NameOnly);
            b.NotARule("StoryFlagDefinition", NameOnly);
            b.NotARule("OutsideSchoolActivityCategoryDefinition", NameOnly);

            b.NotARule("FacilityVisualDefinition", "시설 그림 주소와 그림 배치 칸");
            b.NotARule("GrowingCharacterAppearanceDefinition", Look);
            b.NotARule("GrowingCharacterSheetDefinition", Look);
            b.NotARule("CharacterPartAnimationClipDefinition", Look);
            b.NotARule("CharacterPartMotionDefinition", Look);
            b.NotARule("FishingAnimationDefinition", Look);
            b.NotARule("UrbanUiThemeDefinition", "UI 테마 그림");

            b.NotARule("LdtkTownMapDefinition", MapGeneration);
            b.NotARule("InteriorGenerationProfile", MapGeneration);
            b.NotARule("InteriorTileSetDefinition", MapGeneration);
            b.NotARule("NaturalPropSpawnDefinition", MapGeneration);
            b.NotARule("TerrainGenerationDefinition", MapGeneration);
            b.NotARule("TilePatternDefinition", MapGeneration);
            b.NotARule("TileVariantSetDefinition", MapGeneration);
            b.NotARule("PlaceableTileDefinition", MapGeneration);
        }
    }
}
