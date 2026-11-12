using System;
using System.Collections.Generic;
using System.Globalization;
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
        /// <summary>HUD 시계가 보이는 시각(0~1439분). HUD가 없으면 -1.</summary>
        public int ClockMinutes = -1;
        /// <summary>HUD 시계의 시간 경고(곧 잘 시간, 잘 시간이 지남, 지침) — 사람이 화면에서 보는 것.</summary>
        public Rootborn.Screens.Hud.ClockAlert Alert;
        /// <summary>잘 시간이다: HUD가 잘 시간이 지났거나 지쳤다고 보인다.</summary>
        public bool TimeToSleep => Alert == Rootborn.Screens.Hud.ClockAlert.PastBedtime || Alert == Rootborn.Screens.Hud.ClockAlert.Exhausted;
        /// <summary>추적 중인 목표가 있다.</summary>
        public bool HasTracked;
        public string TrackedStep;
        /// <summary>추적 중인 단계에서 아직 다 채우지 않은 목표가 가리키는 id 묶음(Core 목표의 Target*·Accepted* 값).</summary>
        public readonly HashSet<string> TrackedTargets = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>아직 다 채우지 않은 목표의 종류(Core 목표 클래스 이름). 정책의 목표 종류 힌트(<see cref="ObjectiveHints"/>)가 본다.</summary>
        public readonly HashSet<string> TrackedKinds = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>추적 중인 단계의 목표를 다 채웠다(단계가 넘어가려면 다른 일 — 예: 하루 마감 — 이 필요하다).</summary>
        public bool TrackedObjectivesDone;
        /// <summary>미니게임 화면에서 보이는 것(떠 있지 않으면 null).</summary>
        public MinigameSight Minigame;
        /// <summary>오늘 이 날이 몇 걸음째인가(달리는 쪽이 센다).</summary>
        public int StepsToday;

        public const string WaitPrefix = "wait:", PointPrefix = "point:", IdlePrefix = "idle:", HoldPrefix = "hold:", TypePrefix = "type:";

        /// <summary>상자인 가구(데이터 FurnitureStorageDefinition이 가리키는 가구 id — 정책이 검색 칸에 친다). 엔티티 id로 분기하는 데 쓰지 않는다.</summary>
        public readonly List<string> StorageFurniture = new List<string>(2);

        /// <summary>그 글자 칸에 글자를 친다(기록·재생의 이름에 글자가 든다). 그 칸이 없으면 null.</summary>
        public PlayAction TypeInto(string screen, string element, string text) => Find(TypePrefix + screen + "/" + element + "=" + text);

        /// <summary>떠 있는 화면 그대로 N프레임 기다리기(같이 하는 방에서 자는 중일 때 — 월드에 있을 때만).</summary>
        public PlayAction Hold(int frames) => Find(HoldPrefix + Math.Max(1, frames).ToString(CultureInfo.InvariantCulture));

        /// <summary>
        /// 이름으로 지금 할 수 있는 일을 찾는다. 관찰이 내놓은 것 외에 두 모양을 만들어 준다(기록·재생이 같은 일을 다시 고르게):
        /// "wait:N"(기다리기가 있을 때 N프레임), "point:화면/요소>표시"(그 요소를 누를 수 있을 때, 그 위에 보이는 표시 — 예: 움직이는 과녁 — 의 가운데).
        /// 표시의 자리는 이름에 넣지 않는다: 같은 판을 다시 하면 그때 보이는 자리를 누른다.
        /// </summary>
        public PlayAction Find(string key)
        {
            for (int i = 0; i < Actions.Count; i++) if (Actions[i].Key == key) return Actions[i];
            if (key == null) return null;
            if (key.StartsWith(WaitPrefix, StringComparison.Ordinal))
            {
                PlayAction wait = Find("wait");
                if (wait == null || !int.TryParse(key.Substring(WaitPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int frames) || frames < 1) return null;
                return new PlayAction { Kind = PlayActionKind.Wait, Key = key, Category = "wait", Frames = frames };
            }
            if (key.StartsWith(IdlePrefix, StringComparison.Ordinal))
            {
                // 월드에서 아무것도 누르지 않고 서 있기(시간이 흐르기를 기다린다 — 밤샘하는 플레이어가 지쳐서 할 것이 없을 때).
                if (!InWorld || TopScreen != null || !int.TryParse(key.Substring(IdlePrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int idle) || idle < 1) return null;
                return new PlayAction { Kind = PlayActionKind.Wait, Key = key, Category = "idle", Frames = idle };
            }
            if (key.StartsWith(TypePrefix, StringComparison.Ordinal))
            {
                int eq = key.IndexOf('=');
                if (eq < 0) return null;
                PlayAction field = Find("text:" + key.Substring(TypePrefix.Length, eq - TypePrefix.Length));
                if (field == null) return null;
                return new PlayAction { Kind = PlayActionKind.Type, Key = key, Category = "text", Screen = field.Screen, Element = field.Element, X = field.X, Y = field.Y, Text = key.Substring(eq + 1) };
            }
            if (key.StartsWith(HoldPrefix, StringComparison.Ordinal))
            {
                // 같이 하는 방에서 남을 기다리기(자는 중 — 다른 사람이 아직 깨어 있다): 떠 있는 화면 그대로 시간이 흐르기를 기다린다.
                if (!InWorld || !int.TryParse(key.Substring(HoldPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int hold) || hold < 1) return null;
                return new PlayAction { Kind = PlayActionKind.Wait, Key = key, Category = "hold", Frames = hold };
            }
            if (key.StartsWith(PointPrefix, StringComparison.Ordinal))
            {
                int at = key.LastIndexOf('>');
                if (at < 0 || Minigame == null) return null;
                PlayAction on = Find("button:" + key.Substring(PointPrefix.Length, at - PointPrefix.Length));
                SightElement mark = Minigame.Get(key.Substring(at + 1));
                if (on == null || mark == null || !mark.Visible) return null;
                return new PlayAction { Kind = PlayActionKind.Click, Key = key, Category = "point", Screen = on.Screen, Element = on.Element, X = mark.Rect.CenterX, Y = mark.Rect.CenterY };
            }
            return null;
        }

        /// <summary>월드에서 N프레임 서 있기(맨 위 화면이 없을 때만).</summary>
        public PlayAction Idle(int frames) => Find(IdlePrefix + Math.Max(1, frames).ToString(CultureInfo.InvariantCulture));

        /// <summary>N프레임 기다리기(미니게임처럼 기다리기가 있을 때만).</summary>
        public PlayAction WaitFrames(int frames) => Find(WaitPrefix + Math.Max(1, frames).ToString(CultureInfo.InvariantCulture));

        /// <summary>누를 수 있는 요소 위에 보이는 표시(움직이는 과녁처럼 요소 가운데가 아닌 곳)를 누른다.</summary>
        public PlayAction Point(string screen, string element, string mark) => Find(PointPrefix + screen + "/" + element + ">" + mark);
    }
}
