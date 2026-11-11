using System.Collections.Generic;

namespace Rootborn.Screens.Start
{
    /// <summary>
    /// 내 PC에서 방을 여는 쪽(Host가 만든다 — 방과 전송은 Host와 Rootborn.Net에 있다. 설계 6절 "내 PC에서 열기").
    /// 시작 화면의 방 열기 창은 듣는 주소·정원·슬롯을 넘기고 단계만 읽는다. 단계는 들어가기와 같은 <see cref="RoomJoinPhase"/>다:
    /// Connecting은 방을 열고 내가 들어가는 중, Joined는 방이 열려 있고 내가 그 안에 있다. 설정 화면은 열려 있는지·몇 명인지를 보이고 닫는다.
    /// 무엇이 바뀌면(단계, 들어온 사람 수) Host가 화면 쌓기에 <see cref="RoomHostText.Topic"/>을 알린다(화면은 매 프레임 읽지 않는다).
    /// </summary>
    public interface IRoomHost
    {
        RoomJoinPhase Phase { get; }

        /// <summary>실패 이유 코드(<see cref="RoomHostText"/>·<see cref="RoomJoinText"/>의 코드, 서버의 거절 코드). 없으면 빈 글.</summary>
        string FailureCode { get; }

        /// <summary>정원을 고르지 않았을 때의 값과 고를 수 있는 가장 큰 값(방 설정이 정한다).</summary>
        int DefaultMaxPlayers { get; }
        int MaxPlayersLimit { get; }

        /// <summary>같이 쓸 수 있는 것(공유 조각 이름, 등록 순서). 시간은 항상 같이, 가방·개인 성장은 항상 각자라 여기 없다.</summary>
        IReadOnlyList<string> ShareablePieces { get; }

        /// <summary>창을 열 때 켜 둘 것(--share). 주지 않았으면 빈 목록.</summary>
        IReadOnlyList<string> DefaultSharedPieces { get; }

        /// <summary>그 슬롯에 이미 있는 방의 설정(없으면 null — 새 방). 이미 있는 방은 만들 때 정한 것으로 열린다(바꿀 수 없다, 설계 10절 3번).</summary>
        SavedRoom SavedRoomIn(string slotId);

        /// <summary>
        /// 방을 열고 그 슬롯의 인물로 들어간다. listenAddress는 이 PC에서 들을 "IP 주소:포트"(포트 0이면 운영체제가 빈 포트를 준다).
        /// sharedPieces는 같이 쓸 것(방을 처음 만들 때만 쓴다). 주소가 틀리거나 듣지 못하면 바로 Failed.
        /// </summary>
        void Begin(string listenAddress, int maxPlayers, string slotId, IReadOnlyList<string> sharedPieces);

        /// <summary>열린 방이 같이 쓰는 것(열려 있을 때. 아니면 빈 목록).</summary>
        IReadOnlyList<string> SharedPieces { get; }

        /// <summary>여는 중이면 그만둔다(Idle).</summary>
        void Cancel();

        /// <summary>방이 열려 있고 내가 그 안에 있다.</summary>
        bool IsOpen { get; }

        /// <summary>다른 사람이 들어올 주소 "IP 주소:포트"(열려 있을 때). 포트 0으로 열었으면 운영체제가 준 포트다.</summary>
        string ListenAddress { get; }

        /// <summary>지금 들어와 있는 사람 수(나 포함)와 정원(열려 있을 때).</summary>
        int PlayerCount { get; }
        int MaxPlayers { get; }

        /// <summary>방을 닫는다: 들어와 있던 사람에게 알리고 저장한 뒤 시작 화면으로 돌아간다.</summary>
        void Close();
    }

    /// <summary>방 열기의 알림 주제와, 여는 쪽이 스스로 정하는 이유 코드. 문구 키는 들어가기와 같은 규칙(<see cref="RoomJoinText.ReasonKey"/>).</summary>
    public static class RoomHostText
    {
        /// <summary>방 열기 단계나 들어온 사람 수가 바뀌었다(ScreenStack.NotifyChanged로 넘긴다).</summary>
        public const string Topic = "room-host";

        /// <summary>그 주소·포트로 듣지 못했다(포트를 쓰는 중, 이 PC의 주소가 아님 등).</summary>
        public const string CannotListen = "cannot listen";

        /// <summary>그 슬롯의 방 상태 파일을 읽을 수 없다(덮어쓰지 않는다).</summary>
        public const string RoomSaveUnreadable = "room save unreadable";
    }

    /// <summary>방 열기 창을 여는 값: 시작 화면이 쓰는 것, 미리 적어 둘 듣는 주소(--host), 미리 고를 슬롯(--slot).</summary>
    public sealed record HostRoomOpen(StartMenu Menu, string Address = null, string SlotId = null);
}
