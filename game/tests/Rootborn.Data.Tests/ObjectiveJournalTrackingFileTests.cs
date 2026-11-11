using System.IO;
using NUnit.Framework;
using Rootborn.Core.Objectives;
using Rootborn.Core.Save;
using Rootborn.Data.Save;

namespace Rootborn.Data.Tests
{
    /// <summary>
    /// Unity 판이 쓴 objective-journal-tracking.json(JsonUtility 모양, 버전 없음)을 읽고 다시 쓴다.
    /// 고정 세이브(Fixtures/saves)에는 이 파일이 없어서 Unity 판 모양의 글을 직접 둔다. 자동 테스트다.
    /// </summary>
    public sealed class ObjectiveJournalTrackingFileTests
    {
        // Unity JsonUtility.ToJson(data, true)이 쓰는 모양.
        private const string UnityJson = "{\n    \"CategoryId\": \"quest-chains\",\n    \"StableKey\": \"quest-chain:chain.main\"\n}";

        [Test]
        public void AUnityFile_IsRead_ThenWrittenWithAVersion_AndReadAgain()
        {
            using var dir = new TempDir();
            dir.Write(Path.Combine("slot-0", SaveFileNames.ObjectiveJournalTracking), UnityJson);
            var store = new FileSaveStore(dir.Path);

            Assert.That(store.TryRead("slot-0", SaveFileNames.ObjectiveJournalTracking, out ObjectiveJournalTrackingSaveData unity), Is.True);
            Assert.That(unity.Version, Is.EqualTo(SaveVersion.Unity));
            Assert.That(ObjectiveJournalTrackingSaveFiles.TryLoad(store, "slot-0", out var tracking), Is.True);
            Assert.That(tracking.CategoryId, Is.EqualTo("quest-chains"));
            Assert.That(tracking.StableKey, Is.EqualTo("quest-chain:chain.main"));

            store.Write("slot-0", SaveFileNames.ObjectiveJournalTracking, unity);
            Assert.That(store.TryRead("slot-0", SaveFileNames.ObjectiveJournalTracking, out ObjectiveJournalTrackingSaveData again), Is.True);
            Assert.That(again.Version, Is.EqualTo(ObjectiveJournalTrackingSaveData.KnownVersion));
            Assert.That(again.CategoryId, Is.EqualTo("quest-chains"));
            Assert.That(again.StableKey, Is.EqualTo("quest-chain:chain.main"));
        }
    }
}
