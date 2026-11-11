using System;
using System.Collections.Generic;
using Rootborn.Core.Room;

namespace Rootborn.Net.Room
{
    /// <summary>
    /// 같이 쓸 것을 고르는 실행 인자 값(--share, 환경 변수 <see cref="Variable"/>) 읽기 — 데디케이티드 서버와 내 PC에서 여는 방(Host --host)이 같은 이름·같은 규칙을 쓴다.
    /// 쉼표로 나눈 공유 조각 이름(Core <see cref="RoomPieceCatalog"/>에 등록된 것). <see cref="Nothing"/>이나 빈 값은 시간만 같이 쓴다.
    /// 방을 처음 만들 때만 쓰고 방 설정 파일에 남는다(이미 있는 방은 저장된 설정을 따른다).
    /// </summary>
    public static class RoomShareArgument
    {
        public const string Variable = "ROOTBORN_ROOM_SHARE";

        /// <summary>시간만 같이 쓴다.</summary>
        public const string Nothing = "none";

        /// <summary>값을 읽는다. 모르는 이름이 있으면 false와 그 이름(아는 이름은 <see cref="Known"/>).</summary>
        public static bool TryParse(string value, RoomPieceCatalog catalog, out string[] names, out string unknown)
        {
            names = Array.Empty<string>();
            unknown = null;
            catalog ??= RoomPieceCatalog.CreateDefault();
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), Nothing, StringComparison.Ordinal)) return true;
            var list = new List<string>(4);
            foreach (var part in value.Split(','))
            {
                var name = part.Trim();
                if (name.Length == 0) continue;
                if (!catalog.Knows(name))
                {
                    unknown = name;
                    return false;
                }

                if (!list.Contains(name)) list.Add(name);
            }

            names = list.ToArray();
            return true;
        }

        /// <summary>오류 문구에 붙일 아는 값들("wallet, town 또는 none").</summary>
        public static string Known(RoomPieceCatalog catalog) => string.Join(", ", (catalog ?? RoomPieceCatalog.CreateDefault()).Names) + " 또는 " + Nothing;
    }
}
