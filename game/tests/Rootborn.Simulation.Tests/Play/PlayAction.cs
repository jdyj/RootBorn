using System;
using System.Collections.Generic;
using Rootborn.Screens;

namespace Rootborn.Simulation
{
    /// <summary>자동 플레이어가 할 수 있는 일의 종류. 모두 입력(포인터·키·글자·방향 키)으로 바뀐다.</summary>
    public enum PlayActionKind
    {
        /// <summary>논리 좌표 한 점을 포인터로 누르고 뗀다(UI 포인터 판정을 거친다).</summary>
        Click,
        /// <summary>키 하나를 눌렀다 뗀다(키 배정 표와 화면 쌓기를 거친다).</summary>
        Key,
        /// <summary>글자 칸을 눌러 글자를 치고 Esc로 칸에서 나온다.</summary>
        Type,
        /// <summary>아무것도 누르지 않고 프레임만 보낸다(미니게임 미리 보기처럼 시간이 흘러야 하는 화면).</summary>
        Wait,
        /// <summary>맵의 상호작용 지점 가까이까지 방향 키로 걸어간 뒤 상호작용 키를 누른다.</summary>
        WalkInteract,
        /// <summary>문까지 방향 키로 걸어가 지나간다.</summary>
        Door,
    }

    /// <summary>
    /// 지금 할 수 있는 일 하나. Key는 한 관찰 안에서 겹치지 않는 이름이고, 행동 기록과 재생이 이것으로 같은 일을 다시 고른다.
    /// 정책은 Category·InteractionKind·Subject 같은 일반 정보만 본다. Subject는 지점·줄·카드가 가리키는 것의 id(뜻은 모른다 — 추적 목표의
    /// id 묶음과 같은지만 비교한다).
    /// </summary>
    public sealed class PlayAction
    {
        public PlayActionKind Kind;
        public string Key = string.Empty;
        /// <summary>button, row, card, tab, cell, text, key, wait, interaction, door.</summary>
        public string Category = string.Empty;
        /// <summary>요소가 든 화면 id(월드 행동이면 null).</summary>
        public string Screen;
        /// <summary>UI 요소 id(있으면).</summary>
        public string Element;
        /// <summary>월드 상호작용 종류(npc, location-visit …) 또는 문.</summary>
        public string InteractionKind;
        /// <summary>그 행동이 가리키는 것의 id(지점 id, 줄의 데이터 id, 문의 목적지 맵). 없으면 null.</summary>
        public string Subject;
        /// <summary>문 행동이면 목적지 맵 id.</summary>
        public string DestinationMap;
        /// <summary>Click은 논리 좌표, WalkInteract는 맵 픽셀 좌표.</summary>
        public float X, Y;
        public ScreenKey ScreenKey;
        public int Frames;
        public string Text;

        public override string ToString() => Key;
    }

    /// <summary>한 걸음 앞의 관찰: 지금 할 수 있는 일과, 정책이 볼 수 있는 일반 정보.</summary>
    public sealed class PlayView
    {
        public readonly List<PlayAction> Actions = new List<PlayAction>(64);
        public bool InWorld;
        public string MapId = string.Empty;
        public string TopScreen;
        public readonly List<string> OpenScreens = new List<string>(8);
        public bool MovementBlocked;
        public int Day;
        public int Minutes;
        public (int Remaining, int Budget) TimePoints;
        /// <summary>추적 중인 목표가 있다.</summary>
        public bool HasTracked;
        public string TrackedStep;
        /// <summary>추적 중인 단계에서 아직 다 채우지 않은 목표가 가리키는 id 묶음(Core 목표의 Target*·Accepted* 값).</summary>
        public readonly HashSet<string> TrackedTargets = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>아직 다 채우지 않은 목표의 종류(Core 목표 클래스 이름). 정책의 목표 종류 힌트(<see cref="ObjectiveHints"/>)가 본다.</summary>
        public readonly HashSet<string> TrackedKinds = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>추적 중인 단계의 목표를 다 채웠다(단계가 넘어가려면 다른 일 — 예: 하루 마감 — 이 필요하다).</summary>
        public bool TrackedObjectivesDone;
        /// <summary>미니게임이 미리 보여 준 순서(사람이 보고 외우는 것). 없으면 null.</summary>
        public int[] RememberedSequence;
        /// <summary>오늘 이 날이 몇 걸음째인가(달리는 쪽이 센다).</summary>
        public int StepsToday;

        public PlayAction Find(string key)
        {
            for (int i = 0; i < Actions.Count; i++) if (Actions[i].Key == key) return Actions[i];
            return null;
        }
    }
}
