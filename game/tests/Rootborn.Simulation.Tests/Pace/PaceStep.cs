using System.Collections.Generic;

namespace Rootborn.Simulation.Pace
{
    /// <summary>
    /// 걸음 하나의 속도 기록: 게임이 돈 프레임(실제로 돈 값)과 사람 쪽 시간의 재료(새로 나온 글자 수, 고르기 횟수, 시작한 미니게임 도전의 제한 시간).
    /// 사람 쪽 시간은 여기 담지 않는다 — 보고서가 축 값마다 계산한다(<see cref="PaceAxis"/>).
    /// </summary>
    public sealed class PaceStep
    {
        public int Index;
        /// <summary>걸음 앞의 날과 그날 0시부터 센 분.</summary>
        public int Day, Minutes;
        /// <summary>그 걸음 동안 학생 시계가 간 분(날을 넘기면 하루 1440분으로 센다). 혼자 하기의 활동은 걸리는 분만큼 건너뛴다.</summary>
        public int GameMinutes;
        public long Frames;
        public string Category = PaceCategories.ReadChoose;
        /// <summary>행동 이름(PlayAction.Key)과 고른 쪽(정책 이름, start, day-end …).</summary>
        public string Key = string.Empty, By = string.Empty;
        public string Map = string.Empty, Top;
        /// <summary>걷기·상호작용 걸음이면 그 상호작용 종류와 대상 id(가까운 층의 잰 값을 먼 층에 넘길 때 쓴다), 아니면 null.</summary>
        public string InteractionKind, Subject;
        public int NewChars, Choices;
        /// <summary>미니게임 걸음이면 그 판의 도전 번호(한 판에서 0부터), 아니면 -1.</summary>
        public int Challenge = -1;
        /// <summary>이 걸음에서 처음 보인 도전의 제한 시간(초). 보너스가 더한 시간 포함.</summary>
        public double MinigameLimitSeconds;
        /// <summary>이 걸음 뒤에 처음 생긴 새로운 것.</summary>
        public readonly List<PaceNovelty> Novelties = new List<PaceNovelty>();

        public double GameSeconds(double secondsPerFrame) => Frames * secondsPerFrame;
    }

    /// <summary>
    /// 시간을 넣는 분류(설계 3.2절의 일곱). 고정된 표다 — 화면·엔티티 종류가 아니라 보고서의 칸이다.
    /// </summary>
    public static class PaceCategories
    {
        public const string Walk = "walk", Busy = "busy", Minigame = "minigame", ReadChoose = "read-choose", Browse = "browse", NothingToDo = "nothing-to-do", Sleep = "sleep";

        /// <summary>보고서의 칸 순서.</summary>
        public static readonly IReadOnlyList<string> All = new[] { Walk, Busy, Minigame, ReadChoose, Browse, NothingToDo, Sleep };

        public static string Label(string category)
        {
            switch (category)
            {
                case Walk: return "걷기";
                case Busy: return "활동 중";
                case Minigame: return "미니게임";
                case ReadChoose: return "읽기·고르기";
                case Browse: return "살펴보기";
                case NothingToDo: return "할 것 없음";
                case Sleep: return "잠";
                default: return category;
            }
        }
    }

    /// <summary>새로운 것 하나: 종류 이름, 무엇(데이터에서 온 id·이름 그대로 — 뜻은 모른다), 지점인지.</summary>
    public readonly struct PaceNovelty
    {
        public PaceNovelty(string kind, string what, bool milestone) { Kind = kind; What = what; Milestone = milestone; }
        public string Kind { get; }
        public string What { get; }
        public bool Milestone { get; }
        public override string ToString() => Kind + ":" + What;
    }
}
