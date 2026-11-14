using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using NUnit.Framework;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 상위호환: 더 새 프로그램이 쓴 슬롯(모든 파일의 형식 버전이 더 높고 모르는 필드가 들어 있다)으로 이 프로그램이 게임을 이어 하고 저장한다.
    /// 저장한 뒤에도 모르는 필드와 높은 버전이 모든 파일에 그대로 남는다. "더 새 프로그램이 쓴 슬롯"은 지금 슬롯에 필드를 더해 만든 것이다. 자동 테스트다.
    /// </summary>
    public sealed class NewerSlotIsPlayableTests
    {
        private const int VersionBump = 100;

        private static string Folder(TempSaves saves) => Path.Combine(saves.Path, SlotCheckpoints.Slot);

        // 슬롯의 모든 파일에 모르는 필드를 넣고 형식 버전을 올린다. 파일 이름 → 올린 버전(버전이 없는 파일은 null).
        private static Dictionary<string, int?> MakeNewer(TempSaves saves)
        {
            var bumped = new Dictionary<string, int?>();
            foreach (var path in Directory.GetFiles(Folder(saves)))
            {
                if (!(JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8)) is JsonObject json)) continue;
                json["FutureField"] = new JsonObject { ["from"] = Path.GetFileName(path) };
                int? version = null;
                if (json["Version"] is JsonValue value && value.TryGetValue(out int now))
                {
                    version = now + VersionBump;
                    json["Version"] = version.Value;
                }

                File.WriteAllText(path, json.ToJsonString(), new UTF8Encoding(false));
                bumped[Path.GetFileName(path)] = version;
            }

            return bumped;
        }

        [TestCase("day-3")]
        [TestCase("furniture-placed")]
        [TestCase("house-finished")]
        [TestCase("adult-chosen")]
        [TestCase("workspace-entered")]
        public void ASlotFromANewerProgram_Starts_Plays_AndKeepsWhatThisProgramDoesNotKnow(string checkpoint)
        {
            using var saves = SlotCheckpoints.Restore(checkpoint);
            using var plain = SlotCheckpoints.Restore(checkpoint);
            var bumped = MakeNewer(saves);
            Assert.That(bumped, Is.Not.Empty);

            var game = TestWorld.Start(saves.Store, SlotCheckpoints.Slot);
            var same = TestWorld.Start(plain.Store, SlotCheckpoints.Slot);
            Assert.That(game.Student.CurrentDay, Is.EqualTo(same.Student.CurrentDay), "아는 필드는 그대로 읽힌다");
            Assert.That(game.Wallet.Balance, Is.EqualTo(same.Wallet.Balance));

            game.VisitLocation("location.town-square"); // 저장하는 명령

            // 다음 실행(새 게임 한 판)으로도 열리고, 한 번 더 저장해도 남는다.
            var again = TestWorld.Start(saves.Store, SlotCheckpoints.Slot);
            again.VisitLocation("location.town-square");

            foreach (var pair in bumped)
            {
                var json = JsonNode.Parse(File.ReadAllText(Path.Combine(Folder(saves), pair.Key), Encoding.UTF8));
                Assert.That((string)json["FutureField"]?["from"], Is.EqualTo(pair.Key), pair.Key + ": 모르는 필드");
                if (pair.Value.HasValue) Assert.That((int)json["Version"], Is.EqualTo(pair.Value.Value), pair.Key + ": 형식 버전");
            }
        }
    }
}
