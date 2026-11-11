using System.Collections.Generic;
using Rootborn.Ui;

namespace Rootborn.Screens.Career
{
    /// <summary>
    /// 진로 묶음 화면의 공통 바탕: 데이터가 넘긴 문구 키가 아직 문구표에 없을 수 있을 때(Unity 판은 코드에 적은 대체 문구를 썼다) 쓰는 도움.
    /// 키가 지금 언어에 있으면 그 문구, 없으면 대체 키의 문구, 그것도 없으면 중립 기호(Unity ExplorationTextPresentation.Neutral).
    /// 쓴 키는 TextKeys에 모여 테스트가 두 언어에 다 있는지 본다.
    /// </summary>
    public abstract class CareerScreenBase : GameScreen
    {
        /// <summary>문구를 하나도 찾지 못했을 때 보이는 기호(언어와 무관).</summary>
        public const string Neutral = "—";

        protected CareerScreenBase(ScreenServices services) : base(services) { }

        protected bool HasText(string key) =>
            !string.IsNullOrWhiteSpace(key) && Services.Text.Has(key, Services.Text.Language);

        /// <summary>key가 있으면 그 문구, 없으면 fallbackKey의 문구, 그것도 없으면 Neutral.</summary>
        protected string Known(string key, string fallbackKey = null)
        {
            if (HasText(key)) return T(key);
            if (HasText(fallbackKey)) return T(fallbackKey);
            return Neutral;
        }

        /// <summary>값을 끼우는 문구. 키가 없으면 Neutral.</summary>
        protected string FormatKnown(string key, params object[] args) => HasText(key) ? F(key, args) : Neutral;

        /// <summary>줄 목록을 '\n'으로 이은 글(줄 구조일 뿐 문장을 이어 붙이는 것이 아니다).</summary>
        protected static string JoinLines(List<string> lines) => string.Join("\n", lines);
    }
}
