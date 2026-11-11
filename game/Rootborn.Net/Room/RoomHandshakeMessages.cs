using Rootborn.Net.Messages;

namespace Rootborn.Net.Room
{
    /// <summary>클라이언트 → 방: 이 버전으로 들어가고 싶다. 연결되면 세션 메시지보다 먼저 보낸다.</summary>
    public sealed class JoinCheckMessage
    {
        public int Protocol;
        public string Game;
        public string Data;
    }

    /// <summary>방 → 클라이언트: 버전이 맞다. 이 뒤로 세션 메시지가 오간다.</summary>
    public sealed class JoinCheckAcceptedMessage
    {
    }

    /// <summary>
    /// 접속 확인 메시지 등록 표. 세션 메시지(<see cref="RootbornMessages"/>)와 따로 둔다.
    /// 이 두 메시지는 프로토콜 버전이 서로 다른 프로그램끼리도 읽혀야 버전 차이를 이유와 함께 알릴 수 있다.
    /// 그래서 종류 번호(1000번대, 세션 메시지와 겹치지 않게 비워 둔 구역)와 몸통 모양을 바꾸지 않는다.
    /// </summary>
    public static class RoomHandshakeMessages
    {
        public const ushort JoinCheckKind = 1000;
        public const ushort JoinCheckAcceptedKind = 1001;
        public const int MaxVersionBytes = 128;

        public static MessageCatalog CreateCatalog(int maxMessageBytes)
        {
            var c = new MessageCatalog(maxMessageBytes);
            c.Add<JoinCheckMessage>(JoinCheckKind, 1, WriteJoinCheck, ReadJoinCheck);
            c.Add<JoinCheckAcceptedMessage>(JoinCheckAcceptedKind, 1, (w, m) => { }, ReadAccepted);
            return c;
        }

        private static void WriteJoinCheck(NetWriter w, JoinCheckMessage m)
        {
            w.WriteInt32(m.Protocol);
            w.WriteString(m.Game);
            w.WriteString(m.Data);
        }

        private static bool ReadJoinCheck(NetReader r, int version, out JoinCheckMessage m)
        {
            m = new JoinCheckMessage();
            return r.TryReadInt32(out m.Protocol) && r.TryReadString(MaxVersionBytes, out m.Game) && r.TryReadString(MaxVersionBytes, out m.Data);
        }

        private static bool ReadAccepted(NetReader r, int version, out JoinCheckAcceptedMessage m)
        {
            m = new JoinCheckAcceptedMessage();
            return true;
        }
    }
}
