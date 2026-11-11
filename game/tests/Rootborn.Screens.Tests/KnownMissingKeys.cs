using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Rootborn.Screens.Tests
{
    /// <summary>
    /// 알려진 빠진 현지화 키: Unity 판이 코드에 적은 한글·영어로 대신하던 문구(또는 Unity 판에서도 키 그대로 보이던 데이터 키).
    /// data/localization은 이 묶음에서 고치지 않는다 — 보고서에 키·ko·en 목록으로 내고, 들어오면 여기서 지운다.
    /// 이 목록 밖의 키가 빠지면 화면 테스트가 실패한다.
    /// </summary>
    internal static class KnownMissingKeys
    {
        public static readonly string[] All =
        {
            "ui.hud.energy",
            "ui.hud.focus",
            "ui.objectives.tracked",
            "ui.career-candidate.open",
            "ui.career-interest.open",
            "dialogue.firstNpc.greeting",
        };

        /// <summary>화면이 쓴 키가 두 언어에 다 있는지(알려진 빠진 키는 빼고), 그리고 빠진 키 기록이 알려진 것뿐인지.</summary>
        public static void AssertOnlyKnownMissing(GameText text, IEnumerable<string> usedKeys)
        {
            foreach (string key in usedKeys)
            {
                if (All.Contains(key)) continue;
                Assert.That(text.Has(key, "ko"), Is.True, "ko에 없는 키: " + key);
                Assert.That(text.Has(key, "en"), Is.True, "en에 없는 키: " + key);
            }
            string[] unknown = text.MissingKeys.Select(k => k.Substring(k.IndexOf(':') + 1)).Where(k => !All.Contains(k)).ToArray();
            Assert.That(unknown, Is.Empty, "알려진 목록 밖의 빠진 키");
        }
    }
}
