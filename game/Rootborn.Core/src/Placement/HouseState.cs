using System;
using Rootborn.Core.Save;

namespace Rootborn.Core.Placement
{
    /// <summary>
    /// 집 상태 파일(house-state.json)의 맨 위 모양. 필드 이름은 Unity 판 HouseStateSaveData와 같다.
    /// 지금 집 단계 번호만 쓴다. 옛 파일에 있는 공사·방 구성·돈 필드는 읽지 않는다(Unity 판도 버렸다).
    /// Unity 판은 이 파일을 쓰지 않고 읽기만 했다. 여기서도 읽기만 한다.
    /// </summary>
    [Serializable]
    public sealed class HouseStateSaveData : IVersionedSave
    {
        public const int KnownVersion = 1;

        public int Version;
        public int CurrentStageIndex;

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }
}
