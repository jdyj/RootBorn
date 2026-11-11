using System.Text;

namespace Rootborn.Screens.Start
{
    /// <summary>방(서버)에 들어가는 일의 단계. 화면은 이것만 보고 문구를 고른다.</summary>
    public enum RoomJoinPhase
    {
        /// <summary>아직 들어가려 하지 않았거나, 취소했다.</summary>
        Idle,
        Connecting,
        /// <summary>방이 받아들였고 게임 화면으로 넘어갔다.</summary>
        Joined,
        /// <summary>들어가지 못했다(이유는 <see cref="IRoomJoin.FailureCode"/>).</summary>
        Failed,
    }

    /// <summary>
    /// 서버 주소로 방에 들어가는 쪽(Host가 만든다 — 전송과 접속은 Host와 Rootborn.Net에 있다). 화면은 주소와 슬롯을 넘기고 단계만 읽는다.
    /// 단계가 바뀌면 Host가 화면 쌓기에 <see cref="RoomJoinText.Topic"/>을 알린다(화면은 매 프레임 읽지 않는다).
    /// </summary>
    public interface IRoomJoin
    {
        RoomJoinPhase Phase { get; }

        /// <summary>실패·거절·끊김의 이유 코드(서버의 RoomRefusal 코드, 또는 들어가는 쪽이 정한 <see cref="RoomJoinText"/>의 코드). 없으면 빈 글.</summary>
        string FailureCode { get; }

        /// <summary>주소("호스트:포트")로 접속을 시작한다. 그 슬롯의 인물로 들어간다. 주소가 틀리면 바로 Failed.</summary>
        void Begin(string address, string slotId);

        /// <summary>접속 중이면 그만둔다(Idle).</summary>
        void Cancel();
    }

    /// <summary>
    /// 이유 코드 → 문구 키. 키는 코드에서 만든다: "ui.join.reason." + 코드(소문자, 영문자·숫자 밖은 '-'). 코드마다 분기하지 않으므로
    /// 서버가 새 코드를 보내도 화면 코드는 그대로이고, 그 키의 문구가 어느 언어에도 없으면 <see cref="UnknownReasonKey"/>를 보인다.
    /// 들어가는 쪽(Host)이 스스로 정하는 코드도 여기 둔다.
    /// </summary>
    public static class RoomJoinText
    {
        /// <summary>방에 들어가는 단계가 바뀌었다(ScreenStack.NotifyChanged로 넘긴다).</summary>
        public const string Topic = "room-join";

        public const string ReasonPrefix = "ui.join.reason.";
        public const string UnknownReasonKey = ReasonPrefix + "unknown";

        /// <summary>주소를 "호스트:포트" 꼴로 읽지 못했다.</summary>
        public const string BadAddress = "bad address";
        /// <summary>이 PC에서 소켓을 열지 못했다.</summary>
        public const string CannotConnect = "cannot connect";
        /// <summary>서버에 닿지 못했다(들어가기 전에 연결이 이유 없이 닫혔다).</summary>
        public const string Unreachable = "unreachable";
        /// <summary>방은 받아들였지만 고른 슬롯으로 게임을 시작하지 못했다.</summary>
        public const string EntryFailed = "entry failed";

        public static string ReasonKey(string code)
        {
            if (string.IsNullOrEmpty(code)) return UnknownReasonKey;
            var b = new StringBuilder(ReasonPrefix.Length + code.Length);
            b.Append(ReasonPrefix);
            foreach (char c in code)
            {
                char l = char.ToLowerInvariant(c);
                b.Append((l >= 'a' && l <= 'z') || (l >= '0' && l <= '9') ? l : '-');
            }
            return b.ToString();
        }
    }

    /// <summary>들어가기 창을 여는 값: 시작 화면이 쓰는 것, 미리 적어 둘 주소(--join), 미리 고를 슬롯(--slot).</summary>
    public sealed record JoinServerOpen(StartMenu Menu, string Address = null, string SlotId = null);

    /// <summary>끊김 알림을 여는 값: 이유 코드.</summary>
    public sealed record RoomNoticeOpen(string ReasonCode);
}
