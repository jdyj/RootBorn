namespace Rootborn.Screens
{
    /// <summary>
    /// 데이터에 이름 키가 없거나 키가 아닌 글이 든 정의를 위한 규칙 키: id에서 만든다(화면이 id마다 분기하지 않는다).
    /// id를 소문자로 바꾸고 밑줄·빈칸을 하이픈으로: "Quest_Discovery_X" → "data.quest-discovery-x.name".
    /// 문구는 game/localization에 이 키로 넣는다. 키가 없으면 화면은 데이터의 글이나 id를 그대로 보인다(예전과 같다).
    /// </summary>
    public static class DataTextKeys
    {
        public const string Prefix = "data.";

        public static string Name(string id) => Key(id, ".name");
        public static string Description(string id) => Key(id, ".description");

        private static string Key(string id, string suffix)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var chars = id.Trim().ToLowerInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++) if (chars[i] == '_' || chars[i] == ' ') chars[i] = '-';
            return Prefix + new string(chars) + suffix;
        }
    }
}
