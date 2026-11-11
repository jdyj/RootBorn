using Rootborn.Core.Save;
using Rootborn.Data.Save;

namespace Rootborn.Host.Net
{
    /// <summary>
    /// 임시(설계 7절 5단계 "손님 저장" 전): 방에 들어간 인물의 진행을 어디에 저장하고, 방에서 그 인물을 무엇으로 알아보나.
    /// 설계는 "개인 진행은 방을 가진 쪽의 세이브에 남는다"이고 손님 저장 받기가 아직 없으므로, 지금은 자기 PC의 슬롯에서 인물을 고르고
    /// 진행은 지금처럼 자기 PC의 그 슬롯에 저장한다. 5단계에서 이 클래스의 저장소가 "서버로 보내는 저장소"로 바뀐다 — 바뀔 자리는 여기 하나다.
    /// </summary>
    public static class GuestProgress
    {
        /// <summary>방에 들어간 동안 게임 한 판(GameSession)이 쓸 저장소. 임시: 자기 PC의 세이브 폴더.</summary>
        public static ISaveStore StoreFor(string saveRoot) => new FileSaveStore(saveRoot);

        /// <summary>게임 한 판의 플레이어 id(세이브 파일 이름). 지금 슬롯의 플레이어 id와 같다.</summary>
        public const string SavePlayerId = SaveFileNames.DefaultPlayerId;

        /// <summary>
        /// 방에서의 신원. 슬롯의 플레이어 id는 모든 슬롯이 같은 값(local-player)이라 두 PC가 들어가면 서로를 밀어낸다(서버: 같은 id면 앞의 연결을 끊는다).
        /// 그래서 슬롯 정보(만든 시각, 시드 둘)에서 그 인물에 고정된 id를 만든다. 같은 인물은 늘 같은 id다. 설계 3절은 Steam 계정이 신원이다 —
        /// Steam 전송(7절 7단계)이 들어오면 여기를 바꾼다. 슬롯 정보가 없으면(새 슬롯) 슬롯 id를 붙인다.
        /// </summary>
        public static string RoomIdentity(string slotId, SaveSlotMetadata metadata)
        {
            if (metadata == null) return "slot-" + slotId;
            int a = SaveSlotMetadata.StableHash(metadata.CreatedAtUtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture), metadata.WorldSeed);
            int b = SaveSlotMetadata.StableHash(slotId ?? string.Empty, metadata.TileSeed);
            return "slot-" + ((uint)a).ToString("x8") + ((uint)b).ToString("x8");
        }
    }
}
