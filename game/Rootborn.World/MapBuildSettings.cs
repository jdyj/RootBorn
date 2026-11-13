namespace Rootborn.World
{
    /// <summary>
    /// 짓는 집(maps.json 맵 항목의 "build")의 값. 이 값이 있는 맵이 "짓는 집"이다 — 맵 id가 아니라 이 성질로 고른다.
    /// FrameLayer: 지을 수 있는 칸을 칠한 LDtk IntGrid 층 이름(0 고정, 1 지을 수 있음). FrameId: 테두리의 판 이름(맵을 새로 그리면 올린다).
    /// 둘 다 비면 맵 목록을 읽지 않는다(WorldCatalog). 맵 검사(MapChecks)가 그 층과 고정 지점을 본다.
    /// </summary>
    public sealed class MapBuildSettings
    {
        public string FrameLayer;
        public string FrameId;
    }
}
