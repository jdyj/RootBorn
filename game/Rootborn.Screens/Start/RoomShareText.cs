using System.Collections.Generic;
using System.Text;

namespace Rootborn.Screens.Start
{
    /// <summary>
    /// 방이 같이 쓰는 것(공유 조각 이름 목록)을 사람이 읽는 문구로(멀티플레이 설계 2절). 조각 이름의 문구 키는 이름에서 만든다:
    /// "ui.room.share." + 이름 — 조각마다 분기하지 않으므로 조각을 더하면 그 키의 문구만 더하면 된다(모든 언어 — Screens 테스트가 본다).
    /// 방 열기 창, 들어간 직후 알림, 설정 화면의 방 줄이 같은 문구를 쓴다.
    /// </summary>
    public static class RoomShareText
    {
        public const string PiecePrefix = "ui.room.share.";

        /// <summary>같이 쓰는 것이 있을 때: "같이 쓰는 것: {0}".</summary>
        public const string SharesKey = "ui.room.shares";

        /// <summary>같이 쓰는 것이 없을 때(시간만).</summary>
        public const string NothingKey = "ui.room.shares-nothing";

        /// <summary>조각 이름 사이(", ").</summary>
        public const string SeparatorKey = "ui.room.share-separator";

        public static string PieceKey(string pieceName) => PiecePrefix + (pieceName ?? string.Empty);

        /// <summary>"같이 쓰는 것: 돈, 마을 상태" 또는 "같이 쓰는 것: 시간만".</summary>
        public static string Describe(GameText text, IReadOnlyList<string> pieces)
        {
            if (text == null) return string.Empty;
            if (pieces == null || pieces.Count == 0) return text.Get(NothingKey);
            var b = new StringBuilder(32);
            string separator = text.Get(SeparatorKey);
            for (int i = 0; i < pieces.Count; i++)
            {
                if (i > 0) b.Append(separator);
                b.Append(text.Get(PieceKey(pieces[i])));
            }

            return text.Format(SharesKey, b.ToString());
        }

        /// <summary>Describe가 쓰는 문구 키(화면의 TextKeys에 더한다).</summary>
        public static void AddKeys(ICollection<string> into, IReadOnlyList<string> pieces)
        {
            into.Add(SharesKey);
            into.Add(NothingKey);
            into.Add(SeparatorKey);
            if (pieces == null) return;
            foreach (string piece in pieces) into.Add(PieceKey(piece));
        }
    }

    /// <summary>
    /// 그 슬롯에 이미 있는 방의 설정(방 열기 창이 잠가서 보인다): 같이 쓰는 조각 이름, 정원(적지 않은 옛 방이면 0 — 그때는 정원을 고른다).
    /// </summary>
    public sealed record SavedRoom(IReadOnlyList<string> SharedPieces, int MaxPlayers);
}
