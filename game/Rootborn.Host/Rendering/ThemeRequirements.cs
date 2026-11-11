using System;
using System.Collections.Generic;
using System.Reflection;
using Rootborn.Ui;

namespace Rootborn.Host.Rendering
{
    /// <summary>
    /// 모든 UI 테마가 가져야 하는 스킨과 아이콘.
    /// - 스킨: Rootborn.Ui.UiSkin의 이름 전부(코드에서 읽는다).
    /// - 아이콘: 화면 코드가 "icon:&lt;키&gt;"로 말하는 키(2026-11-12 Rootborn.Screens에서 모음). 화면이 새 아이콘 키를 쓰면 여기에 한 줄 더한다.
    ///   빠뜨리면 ThemeRequirementTests(화면 코드 훑기, 모든 화면 그려 보기)가 잡는다.
    /// 정의별 아이콘(definitionIcons)이 가리키는 키는 필수가 아니다 — 테마에 그 키가 있어야 할 뿐이다.
    /// </summary>
    public static class ThemeRequirements
    {
        public static readonly IReadOnlyList<string> Skins = ReadSkinNames();

        public static readonly IReadOnlyList<string> Icons = new[]
        {
            // HUD: 시계, 시계의 시간 경고(취침 시각 무렵·지남, 지침), 체력·집중, 추적 목표
            "time", "bedtime", "exhausted", "energy", "focus", "journal",
            // 상태 화면
            "stress",
            // 도감
            "encyclopedia",
            // 가방: 탭, 검색·지우기·거르기, 빈 가방, 닫기
            "inventory-all", "inventory-tools", "inventory-consumables", "inventory-misc",
            "inventory-search", "inventory-delete", "inventory-filter", "inventory-empty", "close",
            // 일과 계획판, 일과 카드 대체 아이콘
            "daily-planner", "plan",
            // 목표 수첩 분류(목표 journal, 퀘스트 plan, 캠페인, 힌트)
            "town_map", "discovery",
        };

        private static IReadOnlyList<string> ReadSkinNames()
        {
            var names = new List<string>();
            foreach (FieldInfo field in typeof(UiSkin).GetFields(BindingFlags.Public | BindingFlags.Static))
                if (field.IsLiteral && field.FieldType == typeof(string)) names.Add((string)field.GetRawConstantValue());
            if (names.Count == 0) throw new InvalidOperationException("UiSkin에 스킨 이름이 없다");
            return names;
        }
    }
}
