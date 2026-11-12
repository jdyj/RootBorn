using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Rootborn.Server
{
    /// <summary>
    /// 듣지 못했을 때 무엇을 고치면 되는지 한 줄로 알린다. UDP 전송(LiteNetLib)은 실패 이유를 주지 않으므로 같은 주소·포트에 UDP 소켓을 한 번 묶어 보고
    /// 운영체제의 오류 코드로 나눈다(포트를 쓰는 중, 이 기계의 주소가 아님, 권한).
    /// </summary>
    public static class ListenProblem
    {
        public static string Explain(IPAddress bind, int port)
        {
            string endpoint = bind + ":" + port.ToString(CultureInfo.InvariantCulture);
            SocketError? error = TryBind(bind, port);
            switch (error)
            {
                case SocketError.AddressAlreadyInUse:
                    return "포트 " + port.ToString(CultureInfo.InvariantCulture) + "을(를) 다른 프로그램(다른 서버 포함)이 이미 쓰고 있다 — 그 프로그램을 끄거나 --port 에 다른 포트를 준다 (" + endpoint + ")";
                case SocketError.AddressNotAvailable:
                    return "--bind 주소 " + bind + "은(는) 이 기계의 주소가 아니다 — 이 기계의 LAN 주소를 확인해(Windows: ipconfig, Linux: ip addr) --bind 에 준다";
                case SocketError.AccessDenied:
                    return "이 포트에 묶을 권한이 없다(" + endpoint + ") — 1024 이상의 포트를 쓰거나 그 포트를 막은 프로그램·정책을 확인한다";
                case null:
                    return "UDP 소켓을 열 수 없었다(" + endpoint + ") — 다시 시도해도 같으면 --port 에 다른 포트를 준다";
                default:
                    return "UDP 소켓을 열 수 없다(" + endpoint + ", " + error + ") — --bind 와 --port 를 확인한다";
            }
        }

        // 같은 곳에 UDP로 묶어 본다. 되면 null(지금은 비었다 — 잠깐 쓰던 것), 안 되면 운영체제의 오류.
        private static SocketError? TryBind(IPAddress bind, int port)
        {
            try
            {
                using var socket = new Socket(bind.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                socket.Bind(new IPEndPoint(bind, port));
                return null;
            }
            catch (SocketException e)
            {
                return e.SocketErrorCode;
            }
        }
    }
}
