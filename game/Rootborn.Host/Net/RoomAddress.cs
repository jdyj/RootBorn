using System;
using System.Globalization;
using System.Net;

namespace Rootborn.Host.Net
{
    /// <summary>
    /// 서버 주소 "호스트:포트"를 읽는다(마지막 ':'로 나눈다). 호스트는 IPv4 주소나 이름, 포트는 1~65535.
    /// IPv6 주소는 받지 않는다(UDP 전송이 IPv6를 끄고 있다 — Rootborn.Net UdpTransport).
    /// </summary>
    public static class RoomAddress
    {
        public static bool TryParse(string text, out string host, out int port)
        {
            host = null;
            port = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();
            int colon = text.LastIndexOf(':');
            if (colon <= 0 || colon == text.Length - 1) return false;
            string h = text.Substring(0, colon);
            if (h.IndexOf(':') >= 0 || h.IndexOf(' ') >= 0) return false;
            if (!int.TryParse(text.Substring(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int p) || p < 1 || p > 65535) return false;
            host = h;
            port = p;
            return true;
        }

        /// <summary>
        /// 방을 열 때 들을 곳 "IP 주소:포트"를 읽는다(내 PC에서 열기, --host와 방 열기 창). 주소는 IPv4 주소만(이름은 받지 않는다 —
        /// 이 PC의 어느 주소로 들을지 정하는 값이다), 포트는 0~65535이고 0이면 운영체제가 빈 포트를 준다.
        /// </summary>
        public static bool TryParseListen(string text, out IPAddress address, out int port)
        {
            address = null;
            port = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();
            int colon = text.LastIndexOf(':');
            if (colon <= 0 || colon == text.Length - 1) return false;
            if (!IPAddress.TryParse(text.Substring(0, colon), out IPAddress ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
            if (!int.TryParse(text.Substring(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int p) || p > 65535) return false;
            // IPAddress.TryParse는 "1"이나 "1.2"도 받으므로 점 넷인 꼴만 받는다.
            if (text.Substring(0, colon).Split('.').Length != 4) return false;
            address = ip;
            port = p;
            return true;
        }

        /// <summary>
        /// 자기 쪽 소켓을 어디에 묶나: 상대가 이 기계(루프백)면 루프백에만(같은 기계 검증에서 방화벽 창이 뜨지 않게), 아니면 모든 주소.
        /// </summary>
        public static IPAddress LocalBindFor(string host)
        {
            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) return IPAddress.Loopback;
            return IPAddress.TryParse(host, out IPAddress ip) && IPAddress.IsLoopback(ip) ? IPAddress.Loopback : IPAddress.Any;
        }
    }
}
