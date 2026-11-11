using System;
using Rootborn.Ui;

namespace Rootborn.Screens.Start
{
    /// <summary>캐릭터 그림 한 칸: 그림 id(Host가 푼다), 그림 안의 칸, 기준점(발, 칸의 왼쪽 위에서 폭·높이 비율).</summary>
    public readonly record struct PreviewFrame(string Image, PixelRect Source, float PivotX, float PivotY);

    /// <summary>
    /// 캐릭터 그림을 찾는 곳(Host가 월드가 쓰는 캐릭터 그림 시트에서 준다). 화면은 시트 id만 안다. 아래를 보고 선 칸을 준다.
    /// </summary>
    public interface ICharacterPreviewArt
    {
        bool TryStandingFrame(string sheetId, out PreviewFrame frame);
    }

    /// <summary>슬롯으로 게임에 들어가자는 요청. NewGame이면 방금 새 이야기를 쓴 슬롯이다.</summary>
    public readonly record struct StartEntry(string SlotId, bool NewGame);

    /// <summary>
    /// 시작 화면(슬롯 선택, 캐릭터 만들기)이 쓰는 것. Host가 만들어 슬롯 선택 화면을 열 때 넘긴다(ScreenOpenContext.Argument).
    /// 게임 한 판(GameSession)은 슬롯을 고른 뒤에 Host가 시작하므로 시작 화면에는 없다.
    /// </summary>
    public sealed class StartMenu
    {
        /// <param name="enter">고른 슬롯으로 게임에 들어간다. 들어가지 못하면 false(화면이 실패 문구를 보인다).</param>
        /// <param name="quit">돌아가기(시작 화면 뒤에 아무것도 없어 프로그램을 끝낸다). 없으면 돌아가기 버튼이 보이지 않는다.</param>
        /// <param name="utcNow">새 이야기의 만든 시각. 없으면 지금 UTC.</param>
        /// <param name="tickCount">시드와 무작위 고르기에 쓰는 값(Unity Environment.TickCount). 없으면 Environment.TickCount.</param>
        public StartMenu(SaveSlots slots, CreatorLooks looks, ICharacterPreviewArt art, Func<StartEntry, bool> enter,
            Action quit = null, Func<long> utcNow = null, Func<int> tickCount = null)
        {
            Slots = slots ?? throw new ArgumentNullException(nameof(slots));
            Looks = looks ?? throw new ArgumentNullException(nameof(looks));
            Art = art ?? throw new ArgumentNullException(nameof(art));
            Enter = enter ?? throw new ArgumentNullException(nameof(enter));
            Quit = quit;
            UtcNow = utcNow ?? (() => DateTime.UtcNow.Ticks);
            TickCount = tickCount ?? (() => Environment.TickCount);
        }

        public SaveSlots Slots { get; }
        public CreatorLooks Looks { get; }
        public ICharacterPreviewArt Art { get; }
        public Func<StartEntry, bool> Enter { get; }
        public Action Quit { get; }
        public Func<long> UtcNow { get; }
        public Func<int> TickCount { get; }

        /// <summary>서버 주소로 방에 들어가는 쪽. 없으면 슬롯 선택에 "서버 주소로 들어가기"가 보이지 않는다.</summary>
        public IRoomJoin Join { get; set; }

        /// <summary>내 PC에서 방을 여는 쪽. 없으면 슬롯 선택에 "방 열기"가 보이지 않는다.</summary>
        public IRoomHost Host { get; set; }
    }

    /// <summary>만들기 화면을 여는 값: 시작 화면이 쓰는 것과 새 이야기를 쓸 슬롯.</summary>
    public sealed record CreatorOpen(StartMenu Menu, string SlotId);

    /// <summary>지우기 확인을 여는 값. Deleted는 지운 뒤 불린다(슬롯 칸을 다시 읽는다).</summary>
    public sealed record DeleteSlotOpen(StartMenu Menu, SaveSlotSummary Slot, Action Deleted);
}
