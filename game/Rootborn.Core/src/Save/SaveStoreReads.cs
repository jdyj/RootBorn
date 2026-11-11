namespace Rootborn.Core.Save
{
    /// <summary>저장소 읽기 도우미.</summary>
    public static class SaveStoreReads
    {
        /// <summary>
        /// 새 이름의 파일을 먼저 읽고, 없으면 옛 이름의 파일을 읽는다. 두 이름이 같거나 옛 이름이 비었으면 한 번만 읽는다.
        /// 쓰기는 부르는 쪽이 늘 새 이름으로 한다(옛 파일은 지우지 않는다).
        /// 저장소가 다룰 수 없는 옛 이름(파일 저장소가 거절하는 ':'·경로 구분자 등, ArgumentException)은 옛 파일이 없는 것으로 본다.
        /// Unity 판은 이런 id도 그대로 붙였지만 그 이름으로는 읽을 파일이 있을 수 없다. 새 이름의 오류는 그대로 낸다.
        /// </summary>
        public static bool TryReadWithLegacyName<T>(this ISaveStore store, string saveSlot, string fileName, string legacyFileName, out T data)
            where T : class, new()
        {
            data = null;
            if (store == null) return false;
            if (store.TryRead(saveSlot, fileName, out data)) return true;
            if (string.IsNullOrEmpty(legacyFileName) || legacyFileName == fileName) return false;
            try
            {
                return store.TryRead(saveSlot, legacyFileName, out data);
            }
            catch (System.ArgumentException)
            {
                data = null;
                return false;
            }
        }
    }
}
