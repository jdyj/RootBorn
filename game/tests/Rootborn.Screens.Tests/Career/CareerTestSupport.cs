using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Career
{
    internal static class CareerTestSupport
    {
        /// <summary>준비 단계: 진로 후보 하나에 단서를 더해 그 후보를 장래희망 후보로 고를 수 있게 한다(게임 안의 단서 얻기를 대신하는 fixture).</summary>
        public static void RevealCandidate(TestSession t, string candidateId, string hintId)
        {
            var hint = t.Session.Definitions.Get<CareerHint>(hintId);
            // 소개 단서의 출처는 시설 소개 결과 기록이다(data/careers/CareerResultLogHintSource).
            string[] log = { "opening.introduce." + FacilityOf(candidateId) };
            var result = t.Session.CareerCandidates.ApplyHint(hint, t.Session.Student, "fixture:" + hintId, log);
            Assert.That(result.Applied, Is.True, "fixture 단서가 적용되어야 한다: " + hintId);
            Assert.That(result.CandidateId, Is.EqualTo(candidateId));
        }

        private static string FacilityOf(string candidateId) => candidateId switch
        {
            "candidate.chef" => "restaurant",
            "candidate.emergency-care" => "medical-clinic",
            "candidate.interior" => "design-studio",
            "candidate.soldier" => "training-ground",
            _ => throw new System.ArgumentException("시설을 모르는 후보: " + candidateId),
        };

        /// <summary>카드나 목록 칸을 포인터로 누른다(UiTree 포인터 판정을 거친다).</summary>
        public static void ClickRect(TestSession t, UiNode owner, UiRect rect)
        {
            Assert.That(t.Stack.Tree.HitTest(rect.CenterX, rect.CenterY), Is.SameAs(owner), "그 자리 맨 위 요소가 대상이어야 한다");
            t.Stack.Pointer(rect.CenterX, rect.CenterY, false, 0);
            t.Stack.Pointer(rect.CenterX, rect.CenterY, true, 0);
            t.Stack.Pointer(rect.CenterX, rect.CenterY, false, 0);
        }
    }
}
