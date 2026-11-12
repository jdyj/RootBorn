using System;
using Rootborn.Core.Save;

namespace Rootborn.Server
{
    /// <summary>
    /// 방의 저장소를 감싸 마지막으로 쓴 시각을 센다(서버의 status 명령이 보인다). 읽기·쓰기·사본은 그대로 넘긴다.
    /// 방의 시계는 세션이 스스로(들어옴·나감·잠·게임 1시간마다) 쓰므로 그 쓰기도 여기서 보인다.
    /// </summary>
    public sealed class ObservedSaveStore : IBatchSaveStore, ISaveSlotDayCopies
    {
        private readonly ISaveStore _inner;
        private readonly Func<DateTime> _now;
        private readonly object _gate = new object();
        private DateTime? _lastWrite;

        public ObservedSaveStore(ISaveStore inner, Func<DateTime> now = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _now = now ?? (() => DateTime.Now);
        }

        /// <summary>마지막으로 쓰거나 지운 시각. 아직 없으면 null.</summary>
        public DateTime? LastWrite
        {
            get { lock (_gate) return _lastWrite; }
        }

        public bool TryRead<T>(string saveSlot, string fileName, out T data) where T : class, new() => _inner.TryRead(saveSlot, fileName, out data);

        public void Write<T>(string saveSlot, string fileName, T data) where T : class
        {
            _inner.Write(saveSlot, fileName, data);
            Mark();
        }

        public void Delete(string saveSlot, string fileName)
        {
            _inner.Delete(saveSlot, fileName);
            Mark();
        }

        public ISaveBatch BeginBatch(string saveSlot) => new ObservedBatch(this, SaveBatches.Begin(_inner, saveSlot));

        public void KeepDayCopy(string saveSlot, int dayNumber, int keepCount)
        {
            if (_inner is ISaveSlotDayCopies copies) copies.KeepDayCopy(saveSlot, dayNumber, keepCount);
        }

        private void Mark()
        {
            lock (_gate) _lastWrite = _now();
        }

        private sealed class ObservedBatch : ISaveBatch
        {
            private readonly ObservedSaveStore _owner;
            private readonly ISaveBatch _inner;

            public ObservedBatch(ObservedSaveStore owner, ISaveBatch inner)
            {
                _owner = owner;
                _inner = inner;
            }

            public bool TryRead<T>(string saveSlot, string fileName, out T data) where T : class, new() => _inner.TryRead(saveSlot, fileName, out data);
            public void Write<T>(string saveSlot, string fileName, T data) where T : class => _inner.Write(saveSlot, fileName, data);
            public void Delete(string saveSlot, string fileName) => _inner.Delete(saveSlot, fileName);

            public void Commit()
            {
                _inner.Commit();
                _owner.Mark();
            }

            public void Dispose() => _inner.Dispose();
        }
    }
}
