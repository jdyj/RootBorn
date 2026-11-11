using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Rootborn.Core.Careers;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;

namespace Rootborn.Data.Tests
{
    /// <summary>
    /// 세이브 파일 이름 무늬 → Core 세이브 모양. 고정 폴더의 모든 파일은 모양이 있거나 "아직 모양 없음"에 이유와 함께 있어야 한다.
    /// 모양이 버리는 필드는 "허용 목록"에 이유와 함께 있어야 한다. 영역을 옮길 때마다 아래 두 목록이 줄어든다.
    /// </summary>
    internal static class SaveShapeCoverage
    {
        public static readonly IReadOnlyList<(string Pattern, Type Shape)> Shapes = new[]
        {
            ("quest-log.json", typeof(QuestLogSaveData)),
            ("adult-career-snapshot-*.json", typeof(AdultCareerSnapshotSaveData)),
            ("campaign-progress.json", typeof(CampaignProgressSaveData)),
            ("location-state-progress.json", typeof(LocationStateProgressSaveData)),
            ("milestone-progress.json", typeof(MilestoneProgressSaveData)),
            ("career-interest-progress.json", typeof(CareerInterestProgressSaveData)),
        };

        public static readonly IReadOnlyList<(string Pattern, string Reason)> NotShapedYet = new[]
        {
            ("metadata.json", "슬롯 정보(이름, 시드, 캐릭터 겉모습). Host 몫"),
            ("world-progress.json", "현재 장면과 입구. 월드 프로젝트 몫"),
            ("student-day-result-quest-inventory-summary.json", "하루 결과 화면 요약. 화면 몫"),
            ("house-furniture-layout-stage-0.json", "집 가구 배치. 집 꾸미기(house) 영역을 옮길 때"),
            ("objective-journal-tracking.json", "목표 저널 추적. 화면 몫"),
        };

        /// <summary>(파일 무늬, JSON 경로, 이유). 경로 아래 전체를 허용한다. 배열은 "[]"로 원소 형으로 내려간다.</summary>
        public static readonly IReadOnlyList<(string Pattern, string Path, string Reason)> DroppedFields = new[]
        {
            ("adult-career-snapshot-*.json", "World", "Core에 월드 상태 모양 없음. 월드 영역(core-world)을 옮길 때 뺀다"),
            ("adult-career-snapshot-*.json", "FurnitureUnlocks", "Core에 가구 해금 모양 없음. 집 꾸미기 영역을 옮길 때 뺀다"),
        };

        public static Type ShapeFor(string fileName)
        {
            foreach (var (pattern, shape) in Shapes)
                if (Matches(pattern, fileName)) return shape;
            return null;
        }

        public static bool IsListedAsNotShaped(string fileName)
        {
            foreach (var (pattern, _) in NotShapedYet)
                if (Matches(pattern, fileName)) return true;
            return false;
        }

        public static bool IsDropAllowed(string fileName, string path)
        {
            foreach (var (pattern, allowed, _) in DroppedFields)
                if (Matches(pattern, fileName) && (path == allowed || path.StartsWith(allowed + ".", StringComparison.Ordinal)
                    || path.StartsWith(allowed + "[]", StringComparison.Ordinal))) return true;
            return false;
        }

        public static bool Matches(string pattern, string fileName)
        {
            var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(fileName, regex);
        }
    }
}
