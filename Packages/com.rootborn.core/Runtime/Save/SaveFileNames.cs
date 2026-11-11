using System.Globalization;
using System.Text;

namespace Rootborn.Core.Save
{
    /// <summary>
    /// 세이브 파일 이름을 한 곳에서 만든다. 결과는 Unity 판이 쓴 파일 이름과 같다.
    /// (Rootborn.Core.StudentLife.SaveFileNames.Sanitize는 캠페인·장소 상태·마일스톤이 아직 쓰는 정리 규칙이다.)
    /// </summary>
    public static class SaveFileNames
    {
        /// <summary>기본 플레이어 id(PlayerIdentity.DefaultPlayerId). 이것과 "player"는 옛 공용 파일을 같이 쓴다.</summary>
        public const string DefaultPlayerId = "local-player";
        private const string LegacyPlayerId = "player";

        public const string QuestLog = "quest-log.json";

        public static bool IsDefaultPlayer(string playerId)
            => string.IsNullOrEmpty(playerId) || playerId == DefaultPlayerId || playerId == LegacyPlayerId;

        /// <summary>기본 플레이어는 "quest-chain-progress.json"을 같이 쓰고, 다른 플레이어는 id의 UTF-8 바이트를 모두 16진으로 적어 겹치지 않는다.</summary>
        public static string QuestChain(string playerId)
        {
            if (IsDefaultPlayer(playerId)) return "quest-chain-progress.json";
            var bytes = Encoding.UTF8.GetBytes(playerId);
            var name = new StringBuilder("quest-chain-progress-", 21 + bytes.Length * 2 + 5);
            for (int i = 0; i < bytes.Length; i++) name.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
            return name.Append(".json").ToString();
        }

        /// <summary>Unity AdultCareerSnapshotPersistence.FileNameFor와 같다: 빈 id는 "player", UTF-16 글자마다 4자리 16진.</summary>
        public static string AdultCareerSnapshot(string playerId)
        {
            var value = string.IsNullOrEmpty(playerId) ? LegacyPlayerId : playerId;
            var encoded = new StringBuilder("adult-career-snapshot-", 22 + value.Length * 4 + 5);
            for (int i = 0; i < value.Length; i++) encoded.Append(((int)value[i]).ToString("x4", CultureInfo.InvariantCulture));
            return encoded.Append(".json").ToString();
        }
    }
}
