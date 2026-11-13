using System.Collections.Generic;

namespace Rootborn.World
{
    /// <summary>
    /// 짓는 집(maps.json 맵 항목의 "build")의 값. 이 값이 있는 맵이 "짓는 집"이다 — 맵 id가 아니라 이 성질로 고른다.
    /// 값은 모두 필수다(빠지면 WorldCatalog가 맵 목록을 읽지 않는다 — 기본값으로 메우지 않는다). 키트가 쓰는 방 폴더 maps.json의 build와 같은 값이다
    /// (키트 room_ldtk.build_entry, 키트 시험 HouseRoomTests가 두 쪽을 견준다).
    /// - FrameLayer: 지을 수 있는 칸을 칠한 LDtk IntGrid 층 이름(0 고정, 1 지을 수 있음). FrameId: 테두리의 판 이름(맵을 새로 그리면 올린다).
    /// - FaceHeight: 벽 정면 높이(칸). Band: 벽 윗면 띠 두께(px). 둘 다 Core RoomTilePlan.Build에 그대로 간다(출처표를 정한다).
    /// - BandColors: 띠 칠 네 색의 줄 이름(조각 시트 좌표표의 bands). DefaultWall·DefaultFloor: 칸마다의 꾸밈이 없을 때의 벽·바닥 꾸밈 id
    ///   (data/building의 RoomFinishDefinition). ReplacesLayers: 게임이 조합해 대신 그리는 LDtk 타일 층(그 층은 MapPicture에서 건너뛴다).
    /// 맵 검사(MapChecks)가 Build 층, 고정 지점, 바꿔 그릴 층, 기본 꾸밈, 띠 색 줄·띠 두께를 본다.
    /// </summary>
    public sealed class MapBuildSettings
    {
        public string FrameLayer;
        public string FrameId;
        public int FaceHeight;
        public int Band;
        public string BandColors;
        public string DefaultWall;
        public string DefaultFloor;
        public IReadOnlyList<string> ReplacesLayers;
    }
}
