using System.Collections.Generic;
using Rootborn.Core.World;

namespace Rootborn.Core.Network
{
    /// <summary>
    /// 주인이 보낸 한 순간의 몸: 보낸 순서 번호, 보낸 쪽 시계의 시각(초), 맵 id, 위치, 방향, 걷는 중인지.
    /// 위치 단위는 보낸 쪽과 받는 쪽이 같은 맵 좌표를 쓰기만 하면 된다(MonoGame 판은 맵 픽셀).
    /// </summary>
    public readonly struct PoseSample
    {
        public PoseSample(uint sequence, double senderTime, string mapId, float x, float y, Facing facing, bool walking)
        {
            Sequence = sequence;
            SenderTime = senderTime;
            MapId = mapId ?? string.Empty;
            X = x;
            Y = y;
            Facing = facing;
            Walking = walking;
        }

        public uint Sequence { get; }
        public double SenderTime { get; }
        public string MapId { get; }
        public float X { get; }
        public float Y { get; }
        public Facing Facing { get; }
        public bool Walking { get; }
    }

    /// <summary>지금 그릴 남의 몸. HasPose가 false면 아직 아무것도 받지 않았다.</summary>
    public readonly struct PoseView
    {
        public PoseView(string mapId, float x, float y, Facing facing, bool walking)
        {
            HasPose = true;
            MapId = mapId;
            X = x;
            Y = y;
            Facing = facing;
            Walking = walking;
        }

        public bool HasPose { get; }
        public string MapId { get; }
        public float X { get; }
        public float Y { get; }
        public Facing Facing { get; }
        public bool Walking { get; }
    }

    /// <summary>
    /// 남의 몸 사이 메우기. 받은 위치들을 보낸 시각 순으로 들고, 지연(delaySeconds)만큼 늦은 시각의 위치를 두 점 사이에서 메운다.
    /// - 번호가 이미 받은 가장 새 번호 이하인 것(순서가 바뀌어 늦게 온 것, 중복)은 버리고 Dropped에 센다.
    /// - 맵이 바뀐 것은 앞의 점을 모두 버리고 새 맵의 점에서 다시 시작한다(맵 사이를 메우지 않는다).
    /// - 마지막 점 뒤로 앞질러 짐작하지 않는다(마지막 점에 선다).
    /// 보낸 쪽 시계와 받는 쪽 시계의 차이는 "도착 시각 - 보낸 시각" 중 가장 작은 값으로 본다(가장 빨리 온 것이 실제 차이에 가깝다).
    /// 시간은 부르는 쪽이 넘긴 값만 쓴다.
    /// </summary>
    public sealed class PoseBuffer
    {
        private readonly List<PoseSample> _samples;
        private readonly double _delay;
        private readonly int _capacity;
        private double _clockOffset;

        public PoseBuffer(double delaySeconds, int capacity = 32)
        {
            _delay = delaySeconds < 0 ? 0 : delaySeconds;
            _capacity = capacity < 2 ? 2 : capacity;
            _samples = new List<PoseSample>(_capacity);
        }

        public int Count => _samples.Count;
        public int Dropped { get; private set; }

        /// <summary>가장 새 점의 맵. 받은 것이 없으면 null.</summary>
        public string MapId => _samples.Count == 0 ? null : _samples[_samples.Count - 1].MapId;

        /// <returns>점을 받아들였으면 true, 늦게 왔거나 중복이라 버렸으면 false.</returns>
        public bool Add(PoseSample sample, double localArrival)
        {
            if (_samples.Count > 0)
            {
                var newest = _samples[_samples.Count - 1];
                if (sample.Sequence <= newest.Sequence)
                {
                    Dropped++;
                    return false;
                }

                if (sample.MapId != newest.MapId) _samples.Clear();
            }

            double offset = localArrival - sample.SenderTime;
            if (_samples.Count == 0 || offset < _clockOffset) _clockOffset = offset;
            _samples.Add(sample);
            if (_samples.Count > _capacity) _samples.RemoveAt(0);
            return true;
        }

        public PoseView Sample(double localNow)
        {
            if (_samples.Count == 0) return default;
            double target = localNow - _clockOffset - _delay;
            var first = _samples[0];
            if (target <= first.SenderTime) return View(first);
            var last = _samples[_samples.Count - 1];
            if (target >= last.SenderTime) return View(last);

            for (int i = 0; i < _samples.Count - 1; i++)
            {
                var from = _samples[i];
                var to = _samples[i + 1];
                if (target >= to.SenderTime) continue;
                double span = to.SenderTime - from.SenderTime;
                float t = span <= 0 ? 1f : (float)((target - from.SenderTime) / span);
                return new PoseView(to.MapId, from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t, to.Facing, to.Walking);
            }

            return View(last);
        }

        /// <summary>그 사람이 나갔을 때. 다시 들어오면 번호가 처음부터 시작하므로 앞의 번호를 잊는다.</summary>
        public void Clear() => _samples.Clear();

        private static PoseView View(PoseSample s) => new PoseView(s.MapId, s.X, s.Y, s.Facing, s.Walking);
    }
}
