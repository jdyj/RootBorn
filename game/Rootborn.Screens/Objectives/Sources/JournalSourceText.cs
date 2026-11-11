using System.Collections.Generic;

namespace Rootborn.Screens.Objectives.Sources
{
    /// <summary>모으는 쪽들이 함께 쓰는 글자 만들기(Unity ObjectiveJournalSourceText). 키와 값만 만든다.</summary>
    internal static class JournalSourceText
    {
        public const string GenericObjectiveKey = "ui.journal.objective.generic";

        /// <summary>상태 이름(Unity ShownState: "ui.journal.state.{이름}", 없으면 이름).</summary>
        public static JournalText State(string stateName) => JournalText.KeyOr("ui.journal.state." + stateName, stateName);

        /// <summary>키가 비었으면 대신 쓸 키(Unity SafeText).</summary>
        public static JournalText Safe(string primary, string fallback) => JournalText.Data(string.IsNullOrEmpty(primary) ? fallback : primary);

        /// <summary>
        /// 정의의 이름: 데이터의 이름 키 → id로 만든 규칙 키(DataTextKeys.Name) → 데이터의 글(키가 아닌 이름) → id 순서.
        /// 이름 키가 비었거나 문구가 없는 퀘스트가 id나 영어 원문으로 보이던 것을 game/localization의 규칙 키로 고칠 수 있게 한다.
        /// </summary>
        public static JournalText Name(string displayNameKey, string id) =>
            JournalText.FirstKey(string.IsNullOrEmpty(displayNameKey) ? id : displayNameKey, displayNameKey, DataTextKeys.Name(id));

        /// <summary>정의의 설명: 데이터의 설명 키 → 규칙 키(DataTextKeys.Description) → 데이터의 글.</summary>
        public static JournalText Description(string descriptionKey, string id) =>
            JournalText.FirstKey(descriptionKey, descriptionKey, DataTextKeys.Description(id));

        /// <summary>목표 이름(비었으면 일반 목표).</summary>
        public static JournalText ObjectiveLabel(string displayKey) =>
            string.IsNullOrEmpty(displayKey) ? JournalText.Key(GenericObjectiveKey) : JournalText.Data(displayKey);

        /// <summary>목표 한 줄 "{이름} {진행} / {필요}"(자리 표시 키 하나).</summary>
        public static JournalText ObjectiveLine(JournalText label, int current, int required) =>
            JournalText.Key("ui.journal.objective-line", label, current, required);

        /// <summary>목표 줄들을 " | "로 잇는다. 없으면 상태 이름.</summary>
        public static JournalText Lines(List<JournalText> lines, string stateName) =>
            lines.Count == 0 ? State(stateName) : JournalText.List(lines);
    }
}
