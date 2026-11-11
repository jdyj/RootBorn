using System;

namespace Rootborn.Core.Save
{
    /// <summary>
    /// 한 슬롯의 여러 파일을 한 번에 쓰는 묶음. 묶음 안의 쓰기·지우기는 <see cref="Commit"/> 때 함께 적용된다.
    /// 묶음을 지원하는 저장소에서는 Commit 도중 꺼져도 다음에 읽을 때 슬롯이 "전부 옛것" 또는 "전부 새것"이다.
    /// 묶음 안의 읽기는 아직 적용하지 않은 쓰기를 본다. Commit하지 않고 Dispose하면 묶음을 버린다.
    /// </summary>
    public interface ISaveBatch : ISaveStore, IDisposable
    {
        void Commit();
    }

    /// <summary>묶음 쓰기를 지원하는 저장소. 지원하지 않는 저장소는 <see cref="SaveBatches.Begin"/>이 바로 쓰는 묶음으로 감싼다.</summary>
    public interface IBatchSaveStore : ISaveStore
    {
        /// <summary>saveSlot 하나에만 쓰는 묶음을 연다. 다른 슬롯에 쓰면 ArgumentException.</summary>
        ISaveBatch BeginBatch(string saveSlot);
    }

    public static class SaveBatches
    {
        /// <summary>
        /// 저장소가 묶음을 지원하면 그 묶음을, 아니면 쓰기를 바로 넘기는 묶음을 연다(메모리 저장소 등: 지금처럼 파일마다 쓴다).
        /// 바로 넘기는 묶음은 Commit이 아무것도 하지 않고, Commit 없이 Dispose해도 이미 쓴 것은 남는다.
        /// </summary>
        public static ISaveBatch Begin(ISaveStore store, string saveSlot)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            return store is IBatchSaveStore batches ? batches.BeginBatch(saveSlot) : new DirectBatch(store);
        }

        private sealed class DirectBatch : ISaveBatch
        {
            private readonly ISaveStore _store;

            public DirectBatch(ISaveStore store) => _store = store;

            public bool TryRead<T>(string saveSlot, string fileName, out T data) where T : class, new() => _store.TryRead(saveSlot, fileName, out data);
            public void Write<T>(string saveSlot, string fileName, T data) where T : class => _store.Write(saveSlot, fileName, data);
            public void Delete(string saveSlot, string fileName) => _store.Delete(saveSlot, fileName);
            public void Commit() { }
            public void Dispose() { }
        }
    }
}
