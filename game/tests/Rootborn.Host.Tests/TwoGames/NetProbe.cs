using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Rootborn.Host.Tests.TwoGames
{
    /// <summary>
    /// 두 기계 확인의 잴 거리: 이 프로세스 게임의 UDP 연결(LiteNetLib NetManager)에서 왕복 지연과 보낸·받은 바이트를 읽는다.
    /// 게임 코드에는 재는 입구가 없어 테스트가 리플렉션으로 접속 객체 안의 NetManager를 찾는다(읽기와 통계 켜기만 — 게임 동작은 바꾸지 않는다).
    /// LiteNetLib 판이 바뀌어 이름을 못 찾으면 <see cref="Found"/>가 false이고 값은 -1이다(테스트는 표에 "못 잼"으로 적는다).
    /// </summary>
    public sealed class NetProbe
    {
        private readonly object _manager;
        private readonly object _statistics;

        private NetProbe(object manager)
        {
            _manager = manager;
            if (manager == null) return;
            SetProperty(manager, "EnableStatistics", true);
            _statistics = GetProperty(manager, "Statistics");
            Reset();
        }

        /// <summary>접속 객체(예: HostGame.Room)에서 시작해 안의 필드를 따라가며 NetManager를 찾는다.</summary>
        public static NetProbe Find(object root) => new NetProbe(Search(root, 8));

        public bool Found => _manager != null && _statistics != null;

        /// <summary>첫 상대와의 왕복 지연(ms). 못 읽으면 -1.</summary>
        public int RoundTripMs
        {
            get
            {
                object peer = _manager == null ? null : GetProperty(_manager, "FirstPeer");
                if (peer == null) return -1;
                object rtt = GetProperty(peer, "RoundTripTime");
                if (rtt != null) return Convert.ToInt32(rtt);
                object ping = GetProperty(peer, "Ping");
                return ping == null ? -1 : Convert.ToInt32(ping) * 2;
            }
        }

        public long BytesSent => Number(_statistics, "BytesSent");
        public long BytesReceived => Number(_statistics, "BytesReceived");
        public long PacketsSent => Number(_statistics, "PacketsSent");
        public long PacketsReceived => Number(_statistics, "PacketsReceived");
        public long PacketLoss => Number(_statistics, "PacketLoss");

        public void Reset()
        {
            if (_statistics == null) return;
            _statistics.GetType().GetMethod("Reset", BindingFlags.Public | BindingFlags.Instance)?.Invoke(_statistics, null);
        }

        private static long Number(object o, string name)
        {
            object v = o == null ? null : GetProperty(o, name);
            return v == null ? -1 : Convert.ToInt64(v);
        }

        private static object GetProperty(object o, string name)
        {
            var t = o.GetType();
            PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null) return p.GetValue(o);
            FieldInfo f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            return f?.GetValue(o);
        }

        private static void SetProperty(object o, string name, object value)
        {
            var t = o.GetType();
            PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanWrite) { p.SetValue(o, value); return; }
            t.GetField(name, BindingFlags.Public | BindingFlags.Instance)?.SetValue(o, value);
        }

        private static object Search(object root, int depth)
        {
            var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var queue = new Queue<(object, int)>();
            queue.Enqueue((root, 0));
            while (queue.Count > 0)
            {
                var (o, d) = queue.Dequeue();
                if (o == null || !seen.Add(o)) continue;
                Type t = o.GetType();
                if (t.FullName == "LiteNetLib.NetManager") return o;
                if (d >= depth || t.IsPrimitive || o is string || o is Delegate || o is IEnumerable) continue;
                for (Type c = t; c != null && c != typeof(object); c = c.BaseType)
                    foreach (FieldInfo f in c.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                    {
                        if (f.FieldType.IsPrimitive || f.FieldType == typeof(string) || f.FieldType.IsEnum) continue;
                        object v;
                        try { v = f.GetValue(o); } catch (Exception) { continue; }
                        if (v != null && !v.GetType().IsValueType) queue.Enqueue((v, d + 1));
                    }
            }
            return null;
        }
    }
}
