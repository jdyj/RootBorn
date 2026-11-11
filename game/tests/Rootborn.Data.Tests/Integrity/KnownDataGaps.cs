using System.Collections.Generic;

namespace Rootborn.Data.Tests.Integrity
{
    /// <summary>
    /// 지금 데이터에 있는 빈 곳: 무엇(종류), 어디, 이유. 데이터를 고치지 않고 여기에 고정한다.
    /// 이 목록 밖의 새 빈 곳은 테스트를 실패시키고, 고쳐져 사라진 항목도 실패시킨다(목록에서 지운다).
    /// </summary>
    internal static class KnownDataGaps
    {
        // 변환 보고서(data/export-report.json) unresolved 12건: Unity 에셋이 가리키던 GUID의 파일이 저장소에 없다(guidNotFound).
        private const string DeskTileGone =
            "Unity 판에서도 이 책상 그림 타일 에셋이 저장소에 없었다. 가구 만들기 함수는 그 칸을 건너뛰어 그림 칸 없이 차지 칸만 갖는다(RealDataTests.Furniture_CarriesItsFootprintAndSale)";

        private const string OfficeTileGone =
            "실내 생성 타일셋(InteriorTileSetDefinition, 규칙이 아님 — 게임이 읽지 않는다)의 가구 타일 에셋이 Unity 판에서도 저장소에 없었다";

        private const string Town2Gone =
            "옛 마을2 LDtk 지도 정의(LdtkTownMapDefinition, 규칙이 아님 — 게임이 읽지 않는다). 가리키던 LDtk 프로젝트·합성 그림이 Unity 판에서도 저장소에 없었다. 지금 마을은 game/world/maps.json이 정한다";

        private const string CharacterPrefab =
            "Unity 판 캐릭터 프리팹. MonoGame 판은 이 필드를 읽지 않는다(겉모습은 그림 시트 id로, game/Rootborn.World/CharacterArt). Unity를 지우면 파일이 사라지므로 그때 데이터에서 필드를 지운다";

        public static readonly IReadOnlyList<KnownGap> All = new List<KnownGap>
        {
            new KnownGap("unresolved", "data/interiors/FurnitureDefinition.json FurnitureDefinition/desk.basic tileParts[0].tile", DeskTileGone),
            new KnownGap("unresolved", "data/interiors/FurnitureDefinition.json FurnitureDefinition/desk.large tileParts[0].tile", DeskTileGone),
            new KnownGap("unresolved", "data/interiors/FurnitureDefinition.json FurnitureDefinition/desk.large tileParts[1].tile", DeskTileGone),
            new KnownGap("unresolved", "data/interiors/InteriorTileSetDefinition.json InteriorTileSetDefinition/TileSetDefinition_NewPaletteOffice deskNorth", OfficeTileGone),
            new KnownGap("unresolved", "data/interiors/InteriorTileSetDefinition.json InteriorTileSetDefinition/TileSetDefinition_NewPaletteOffice deskEast", OfficeTileGone),
            new KnownGap("unresolved", "data/interiors/InteriorTileSetDefinition.json InteriorTileSetDefinition/TileSetDefinition_NewPaletteOffice chairNorth", OfficeTileGone),
            new KnownGap("unresolved", "data/interiors/InteriorTileSetDefinition.json InteriorTileSetDefinition/TileSetDefinition_NewPaletteOffice chairEast", OfficeTileGone),
            new KnownGap("unresolved", "data/interiors/InteriorTileSetDefinition.json InteriorTileSetDefinition/TileSetDefinition_NewPaletteOffice sofaNorth", OfficeTileGone),
            new KnownGap("unresolved", "data/interiors/InteriorTileSetDefinition.json InteriorTileSetDefinition/TileSetDefinition_NewPaletteOffice sofaEast", OfficeTileGone),
            new KnownGap("unresolved", "data/interiors/InteriorTileSetDefinition.json InteriorTileSetDefinition/TileSetDefinition_NewPaletteOffice sofaWest", OfficeTileGone),
            new KnownGap("unresolved", "data/locations/LdtkTownMapDefinition.json LdtkTownMapDefinition/Town2LdtkMapDefinition projectAsset", Town2Gone),
            new KnownGap("unresolved", "data/locations/LdtkTownMapDefinition.json LdtkTownMapDefinition/Town2LdtkMapDefinition compositeSprite", Town2Gone),

            // 그림이 아닌 Unity 파일을 가리키는 참조(파일은 지금 있다).
            new KnownGap("unity-file", "data/characters/GrowingCharacterAppearanceDefinition.json GrowingCharacterAppearanceDefinition/adult_short_brown_adult_casual visualPrefab", CharacterPrefab),
            new KnownGap("unity-file", "data/characters/GrowingCharacterAppearanceDefinition.json GrowingCharacterAppearanceDefinition/adult_short_brown_chef visualPrefab", CharacterPrefab),
            new KnownGap("unity-file", "data/characters/GrowingCharacterAppearanceDefinition.json GrowingCharacterAppearanceDefinition/adult_short_brown_interior visualPrefab", CharacterPrefab),
            new KnownGap("unity-file", "data/characters/GrowingCharacterAppearanceDefinition.json GrowingCharacterAppearanceDefinition/adult_short_brown_office visualPrefab", CharacterPrefab),
            new KnownGap("unity-file", "data/characters/GrowingCharacterAppearanceDefinition.json GrowingCharacterAppearanceDefinition/playground_short_brown_casual visualPrefab", CharacterPrefab),
            new KnownGap("unity-file", "data/characters/GrowingCharacterAppearanceDefinition.json GrowingCharacterAppearanceDefinition/school_short_brown_school_casual visualPrefab", CharacterPrefab),
            new KnownGap("unity-file", "data/characters/GrowingCharacterAppearanceDefinition.json GrowingCharacterAppearanceDefinition/school_short_brown_school_uniform visualPrefab", CharacterPrefab),

            // 맵 LDtk 프로젝트의 타일셋.
            new KnownGap("tileset-outside-repo", "Assets/LDtk/Town/town.ldtk tileset Snow_ruin_non_rm_a2_square",
                "마을 LDtk 프로젝트에 정의만 있고 그 타일셋을 쓰는 층이 없다. 그림은 만든 사람의 내려받기 폴더를 가리킨다. 새 맵에서 타일셋 정의를 지운다"),

            // 맨 문자열 id 필드가 가리키는 엔티티가 없다. Unity 에셋(Assets/Data)에도 그 id를 가진 정의가 없다(그 에셋 자신에만 적혀 있다).
            new KnownGap("missing-id",
                "data/discoveryclues/ClueInterpretationSourceDefinition.json ClueInterpretationSourceDefinition/interpret.source.workbench targetId object.workbench",
                "단서 해석 출처 kind 2(물건)의 대상은 정의가 아니라 맵 물건 이름인데, 그 물건(작업대)은 맵 데이터에 없다(시험용 단서 묶음)"),
            new KnownGap("missing-id",
                "data/discoveryclues/DiscoveryClueLocationVisitCompletion.json DiscoveryClueLocationVisitCompletion/Completion_LibraryArchiveVisit locationId location.library.archive-room",
                "도서관 기록실 장소 정의가 Unity 판에도 없다. 이 방문 완료는 이루어질 수 없다"),
            new KnownGap("missing-id",
                "data/discoveryclues/DiscoveryClueLocationVisitCompletion.json DiscoveryClueLocationVisitCompletion/Completion_PlayLoopTraceVisit locationId location.discovery.play-trace",
                "시험용 단서 흔적 장소 정의가 Unity 판에도 없다(PlayMode 단서 시험 데이터)"),
            new KnownGap("missing-id",
                "data/studentlife/MilestoneRouteDefinition.json MilestoneRouteDefinition/milestone.route.study-basics recommendedActionId activity.study-basics",
                "추천 행동 id에 맞는 활동 정의가 Unity 판에도 없다. 마일스톤 길 안내에 추천 행동이 비어 보인다"),

            // 등록부
            new KnownGap("missing-registry-id", "data/registry.json textCatalog GameTextCatalog",
                "Unity 현지화 카탈로그(Assets/Localization/GameTextCatalog.asset, 보고서 refsOutsideData)는 data/ 엔티티로 내보내지 않는다. 문구는 data/localization과 game/localization이 갖는다"),
        };
    }
}
