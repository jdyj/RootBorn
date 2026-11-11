using System;
using System.Collections.Generic;

namespace Rootborn.Net.Transport
{
    /// <summary>
    /// 서버 쪽 전송 여럿을 하나로 묶는다(호스트 모드: 방을 연 사람은 같은 프로세스 안 전송으로, 다른 사람은 UDP로 들어온다).
    /// 위(<see cref="Room.RoomServer"/>)에는 전송 하나로 보인다. 연결 번호는 묶음 안에서 겹치지 않게 바꿔 붙인다:
    /// 바깥 번호 = 안쪽 번호 × <see cref="MaxTransports"/> + 전송 순번. 그래서 바꿔 붙이는 데 표가 필요 없고 할당도 없다.
    /// 시간은 <see cref="Update"/>에 넘긴 값을 모든 안쪽 전송에 그대로 넘긴다. 닫으면 안쪽을 모두 닫는다.
    /// </summary>
    public sealed class MergedServerTransport : ITransport
    {
        /// <summary>묶을 수 있는 전송 수의 상한(번호를 바꿔 붙이는 셈의 몫).</summary>
        public const int MaxTransports = 8;

        private readonly ITransport[] _inner;
        private int _receiveFrom;

        public MergedServerTransport(params ITransport[] inner)
        {
            if (inner == null || inner.Length == 0) throw new ArgumentException("묶을 전송이 없다", nameof(inner));
            if (inner.Length > MaxTransports) throw new ArgumentException("전송은 " + MaxTransports + "개까지 묶는다", nameof(inner));
            foreach (ITransport t in inner)
                if (t == null) throw new ArgumentException("빈 전송이 있다", nameof(inner));
            _inner = (ITransport[])inner.Clone();
        }

        /// <summary>그 바깥 번호가 몇 번째 전송의 연결인가(시험·진단용).</summary>
        public static int TransportIndexOf(PeerId peer) => (int)(peer.Value % MaxTransports);

        public void Update(double now)
        {
            foreach (ITransport t in _inner) t.Update(now);
            _receiveFrom = 0;
        }

        public bool TryReceive(out TransportEvent transportEvent)
        {
            // 앞의 전송에 쌓인 것부터 차례로 비운다. 한 전송 안의 순서는 그대로 지킨다.
            while (_receiveFrom < _inner.Length)
            {
                if (_inner[_receiveFrom].TryReceive(out TransportEvent e))
                {
                    transportEvent = new TransportEvent(e.Kind, Outer(e.Peer, _receiveFrom), e.Data);
                    return true;
                }

                _receiveFrom++;
            }

            transportEvent = default;
            return false;
        }

        public void Send(PeerId peer, byte[] data, Delivery delivery)
        {
            if (TryInner(peer, out ITransport t, out PeerId inner)) t.Send(inner, data, delivery);
        }

        public void Disconnect(PeerId peer, byte[] reason)
        {
            if (TryInner(peer, out ITransport t, out PeerId inner)) t.Disconnect(inner, reason);
        }

        public void Dispose()
        {
            foreach (ITransport t in _inner) t.Dispose();
        }

        private static PeerId Outer(PeerId inner, int index) => new PeerId(inner.Value * MaxTransports + index);

        private bool TryInner(PeerId outer, out ITransport transport, out PeerId inner)
        {
            long index = outer.Value % MaxTransports;
            if (outer.Value < 0 || index >= _inner.Length)
            {
                transport = null;
                inner = default;
                return false;
            }

            transport = _inner[index];
            inner = new PeerId(outer.Value / MaxTransports);
            return true;
        }
    }
}
