using Rootborn.Core.Save;
using Rootborn.Data.Save;
using Rootborn.Net.Room;

namespace Rootborn.Host.Net
{
    /// <summary>
    /// 방에 들어간 인물의 진행을 어디에 저장하고, 방에서 그 인물을 무엇으로 알아보나(설계 3절·3.1절: 개인 진행은 각자 계산하고 저장은 방을 가진 쪽에 남는다).
    /// - 손님 저장을 받는 방(데디케이티드 서버 Rootborn.Server, 남이 내 PC에서 연 방 — 방을 연 사람의 슬롯 안 guests/): 방이 내려 준 그 사람의 세이브로
    ///   메모리 저장소를 만들고(<see cref="RoomStoreFor"/>), 저장할 때마다 바뀐 파일을 방으로 보낸다. 이 PC의 슬롯에는 진행을 남기지 않는다.
    ///   처음 온 사람은 빈 세이브(새 게임)다. 예전에 이 PC 슬롯으로 들어갔던 진행(임시 방식)은 옮기지 않는다.
    /// - 손님 저장을 받지 않는 사람(내 PC에서 방을 연 나 — 그 슬롯이 방이다, 테스트의 방 등): 이 PC의 그 슬롯에 저장한다(<see cref="LocalStoreFor"/>).
    /// 이 PC의 슬롯은 들어갈 인물(이름·겉모습·신원)을 고르는 데 쓴다 — 캐릭터 만들기는 이 PC의 시작 화면에서 한다.
    /// 아직 이 PC에 남는 것: 맵 자리(world-progress.json)와 옷 선택(wearables-*.json)은 월드 화면·옷장이 이 PC의 슬롯에 쓴다(보고서 "필요한 변경").
    /// </summary>
    public static class GuestProgress
    {
        /// <summary>손님 저장을 받지 않는 방에 들어갔을 때 게임 한 판이 쓸 저장소: 자기 PC의 세이브 폴더.</summary>
        public static ISaveStore LocalStoreFor(string saveRoot) => new FileSaveStore(saveRoot);

        /// <summary>
        /// 손님 저장을 받는 방에 들어갔을 때 게임 한 판이 쓸 저장소: 방이 내려 준 세이브(아직 방이 받았다고 답하지 않은 저장을 겹친 것)로 시작하고,
        /// 저장할 때마다 바뀐 파일을 방으로 보낸다. 슬롯 이름은 <see cref="RoomGuestLink.Slot"/>이다.
        /// </summary>
        public static MemorySaveSlot RoomStoreFor(RoomGuestLink link)
        {
            var store = new MemorySaveSlot(RoomGuestLink.Slot, link.Files.Writes);
            store.Committed += changes => link.Upload(SaveFileSet.From(changes.Writes, changes.Deletes));
            return store;
        }

        /// <summary>손님 저장을 받지 않는 방에서 게임 한 판의 플레이어 id(세이브 파일 이름). 지금 슬롯의 플레이어 id와 같다.</summary>
        public const string SavePlayerId = SaveFileNames.DefaultPlayerId;

        /// <summary>
        /// 방에서의 신원. 슬롯의 플레이어 id는 모든 슬롯이 같은 값(local-player)이라 두 PC가 들어가면 서로를 밀어낸다(서버: 같은 id면 앞의 연결을 끊는다).
        /// 그래서 슬롯 정보(만든 시각, 시드 둘)에서 그 인물에 고정된 id를 만든다. 같은 인물은 늘 같은 id다. 손님 저장을 받는 방에서는 이것이
        /// 게임 한 판의 플레이어 id이기도 하다(방의 세이브 폴더 이름). 설계 3절은 Steam 계정이 신원이다 — Steam 전송(7절 7단계)이 들어오면 여기를 바꾼다.
        /// 슬롯 정보가 없으면(새 슬롯) 슬롯 id를 붙인다.
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
