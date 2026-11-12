using System;
using System.Collections.Generic;
using Rootborn.Core.Interaction;
using Rootborn.Core.Placement;
using Rootborn.Core.World;
using Rootborn.Game;
using Rootborn.Screens;
using Rootborn.Screens.Furniture;
using Rootborn.World;

namespace Rootborn.Simulation
{
    /// <summary>
    /// 자동 플레이어의 몸: 게임을 한 판 띄우고 입력을 넣는 쪽. 창 없는 몸(HeadlessBody)과 실제 창 몸(Host 테스트의 DriverBody)이 있고,
    /// 판단·관찰·불변식·기록은 둘이 같은 코드를 쓴다. 몸은 입력만 넣는다 — GameSession 명령을 직접 부르지 않는다.
    /// 읽기(화면 쌓기, 월드, 게임 한 판)는 관찰과 불변식 검사에만 쓴다.
    /// </summary>
    public interface IPlayBody : IDisposable
    {
        /// <summary>지금 화면 쌓기(슬롯 선택 화면 → 슬롯을 고르면 월드 위의 것으로 바뀐다).</summary>
        ScreenStack Screens { get; }
        GameText Text { get; }
        /// <summary>게임 한 판(슬롯을 고르기 전에는 null).</summary>
        GameSession Session { get; }
        /// <summary>월드가 떠 있다(슬롯을 골랐다).</summary>
        bool InWorld { get; }
        WorldSession Walk { get; }
        /// <summary>지금 맵의 상호작용 후보(물건, 그 시각에 선 NPC, 문).</summary>
        IReadOnlyList<InteractionCandidate> Candidates { get; }
        /// <summary>지금 E를 누르면 반응할 대상(없으면 null).</summary>
        InteractionCandidate Target { get; }
        /// <summary>지금 맵이 가구 방이면 그 방.</summary>
        FurnitureRoom Room { get; }
        /// <summary>배치 화면이 쓰는 화면 좌표 ↔ 칸(가구 방일 때).</summary>
        IPlacementView PlacementView { get; }
        /// <summary>논리 화면 크기(1920x1080).</summary>
        float Width { get; }
        float Height { get; }
        /// <summary>한 프레임에 학생 시계로 넘기는 실제 초(고정 프레임 간격 × 시간 배속).</summary>
        float ClockSecondsPerFrame { get; }
        /// <summary>지금까지 돈 프레임 수.</summary>
        long Frames { get; }
        /// <summary>벽을 뚫고 걸은 프레임 수(몸 상자가 막힌 칸에 들어갔다).</summary>
        int CollisionViolations { get; }
        /// <summary>세이브 폴더와 슬롯(다시 읽기 검사용).</summary>
        string SaveRoot { get; }
        string SlotId { get; }
        /// <summary>마지막으로 들어간 가구 방의 칸과 면(EnterHouse에 넘긴 것). 없으면 null.</summary>
        (IPlacementGrid Grid, string Surface)? HouseEntered { get; }

        /// <summary>화면이 열렸다(어느 화면 쌓기든). 범위 보고용.</summary>
        event Action<string> ScreenOpened;
        /// <summary>월드 상호작용이 일어났다.</summary>
        event Action<InteractionEvent> Interacted;

        void Click(float x, float y);
        void PressKey(ScreenKey key);
        void TypeInto(float x, float y, string text);
        void Wait(int frames);
        /// <summary>그 지점(맵 픽셀)에서 within 픽셀 안의 칸까지 걷는다. 길이 없거나 도중에 맵이 바뀌면 false.</summary>
        bool WalkNear(float x, float y, float within);
        /// <summary>그 문을 지나 맵이 바뀌면 true.</summary>
        bool WalkThroughDoor(DoorArea door);
        /// <summary>그 자리에서 상호작용 키를 누른다.</summary>
        void Interact();
        /// <summary>그려진 화면을 저장한다(창 없는 몸은 null).</summary>
        string Screenshot(string name);
    }

    /// <summary>
    /// 저장된 진행을 새 게임 한 판으로 다시 읽을 수 있는 몸(같이 하는 방의 자리 — 진행이 방에 있다). 날마다 다시 읽기 검사가 이것을 쓴다.
    /// 없으면(혼자 하기) 몸의 세이브 폴더와 슬롯으로 다시 읽는다. 다시 읽을 수 없으면(방에 없다) null.
    /// </summary>
    public interface ISavedGameSource
    {
        GameSession ReloadSaved();
    }
}
