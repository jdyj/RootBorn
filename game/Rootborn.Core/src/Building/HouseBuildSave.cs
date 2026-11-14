using System;
using Rootborn.Core.Save;

namespace Rootborn.Core.Building
{
    /// <summary>
    /// 짓는 집의 꾸밈 기록 세이브 모양. 필드 이름과 형은 바꾸지 않는다(바뀌면 <see cref="KnownVersion"/>을 올린다).
    /// 기록은 바닥 칸마다 벽지·바닥 id이고, 빈 글은 프레임 기본이다. 데이터에 없는 id나 지금 바닥이 아닌 칸의 기록도 그대로 들고 다시 쓴다.
    /// </summary>
    [Serializable]
    public sealed class HouseBuildSaveData : IVersionedSave
    {
        public const int KnownVersion = 1;

        public int Version;
        /// <summary>이 기록을 쓴 집 틀(맵 build.frameId). 틀이 바뀌어도 칸 좌표가 같으면 기록이 그대로 든다.</summary>
        public string FrameId = "";
        /// <summary>띠 색 줄 이름. 빈 글이면 프레임 기본.</summary>
        public string Band = "";
        public HouseFinishCellSaveData[] Finishes = Array.Empty<HouseFinishCellSaveData>();

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }

    /// <summary>바닥 칸 하나의 꾸밈 기록. 빈 글은 프레임 기본.</summary>
    [Serializable]
    public sealed class HouseFinishCellSaveData
    {
        public int X;
        public int Y;
        public string Wall = "";
        public string Floor = "";
    }
}
