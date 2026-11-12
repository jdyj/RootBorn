using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Rootborn.Host.Measure
{
    /// <summary>끝날 때 한 번 계산한 프레임 통계(<see cref="FrameMeter.Summary"/>).</summary>
    public readonly struct FrameSummary
    {
        public FrameSummary(int frames, double medianMs, double p99Ms, double maxMs, double allocatedBytesPerFrame, long peakPrivateBytes)
        {
            Frames = frames; MedianMs = medianMs; P99Ms = p99Ms; MaxMs = maxMs;
            AllocatedBytesPerFrame = allocatedBytesPerFrame; PeakPrivateBytes = peakPrivateBytes;
        }

        public int Frames { get; }
        public double MedianMs { get; }
        public double P99Ms { get; }
        public double MaxMs { get; }
        public double AllocatedBytesPerFrame { get; }
        public long PeakPrivateBytes { get; }

        /// <summary>쉼표 뒤 공백 없는 한 줄 JSON. 숫자는 문화권과 무관하게 점을 쓴다.</summary>
        public string ToJson()
        {
            var c = CultureInfo.InvariantCulture;
            var b = new StringBuilder(160);
            b.Append("{\"frames\":").Append(Frames.ToString(c));
            b.Append(",\"frameMsMedian\":").Append(MedianMs.ToString("0.###", c));
            b.Append(",\"frameMsP99\":").Append(P99Ms.ToString("0.###", c));
            b.Append(",\"frameMsMax\":").Append(MaxMs.ToString("0.###", c));
            b.Append(",\"allocatedBytesPerFrame\":").Append(AllocatedBytesPerFrame.ToString("0.#", c));
            b.Append(",\"peakPrivateBytes\":").Append(PeakPrivateBytes.ToString(c));
            return b.Append('}').ToString();
        }
    }

    /// <summary>
    /// 프레임 시간·프레임당 할당·전용 메모리 최고값을 잰다(--measure). 엔진과 무관하다.
    /// 프레임마다 객체를 만들지 않는다: 시작할 때 잡은 고정 크기 배열에 적고(넘치면 오래된 것을 덮는다), 통계는 <see cref="Summary"/>에서 한 번 계산한다.
    /// 중앙값·99%는 마지막 <c>capacity</c>개 프레임으로, 최댓값·프레임 수·할당 평균은 전체로 센다.
    /// </summary>
    public sealed class FrameMeter
    {
        private readonly double[] _ms;
        private readonly double _msPerTick;
        private int _next;        // 다음에 쓸 자리
        private int _filled;      // 배열에 든 개수(최대 capacity)
        private int _frames;      // 전체 프레임 수
        private double _max;
        private bool _hasBaseline;
        private long _lastTicks;
        private long _baselineAllocated;
        private long _lastAllocated;
        private long _peakPrivate;

        /// <param name="capacity">남겨 둘 프레임 수(통계 배열 크기).</param>
        /// <param name="ticksPerSecond">틱 주파수. 기본은 <see cref="Stopwatch.Frequency"/>. 시험이 고정 값을 넣는다.</param>
        public FrameMeter(int capacity, long ticksPerSecond = 0)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _ms = new double[capacity];
            _msPerTick = 1000.0 / (ticksPerSecond > 0 ? ticksPerSecond : Stopwatch.Frequency);
        }

        /// <summary>프레임 하나가 끝났다. 첫 호출은 기준만 잡는다(앞 프레임이 없다).</summary>
        /// <param name="timestampTicks">이 프레임이 끝난 때의 틱(<see cref="Stopwatch.GetTimestamp"/>).</param>
        /// <param name="allocatedBytes">지금까지 이 스레드가 할당한 바이트(<see cref="GC.GetAllocatedBytesForCurrentThread"/>).</param>
        public void Frame(long timestampTicks, long allocatedBytes)
        {
            if (!_hasBaseline)
            {
                _hasBaseline = true;
                _lastTicks = timestampTicks;
                _baselineAllocated = _lastAllocated = allocatedBytes;
                return;
            }
            double ms = (timestampTicks - _lastTicks) * _msPerTick;
            _lastTicks = timestampTicks;
            _lastAllocated = allocatedBytes;
            _ms[_next] = ms;
            if (++_next == _ms.Length) _next = 0;
            if (_filled < _ms.Length) _filled++;
            _frames++;
            if (ms > _max) _max = ms;
        }

        /// <summary>전용 메모리 표본(바이트). 최고값만 남긴다.</summary>
        public void NoteMemory(long privateBytes)
        {
            if (privateBytes > _peakPrivate) _peakPrivate = privateBytes;
        }

        /// <summary>통계를 계산한다(끝날 때 한 번 — 배열을 복사해 정렬하므로 프레임 도중에 부르지 않는다).</summary>
        public FrameSummary Summary()
        {
            if (_frames == 0) return new FrameSummary(0, 0, 0, 0, 0, _peakPrivate);
            var sorted = new double[_filled];
            Array.Copy(_ms, sorted, _filled); // 가득 차기 전에는 앞 _filled칸이 전부다; 가득 찬 뒤에는 순서가 필요 없다
            Array.Sort(sorted);
            int n = sorted.Length;
            double median = n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
            int rank = (int)Math.Ceiling(0.99 * n); // 가까운 순위
            double p99 = sorted[Math.Max(rank, 1) - 1];
            double allocated = (_lastAllocated - _baselineAllocated) / (double)_frames;
            return new FrameSummary(_frames, median, p99, _max, allocated, _peakPrivate);
        }
    }
}
