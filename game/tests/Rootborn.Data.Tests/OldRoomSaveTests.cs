using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Rootborn.Core.Building;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.Data.Save;

namespace Rootborn.Data.Tests
{
    /// <summary>
    /// 방 슬롯 고정 폴더(Fixtures/saves/room-*)를 지금 프로그램의 방 조각(Core RoomPieceSet, 기본 등록 표)으로 읽고 다시 쓴다 — 서버를 다시 켠 것과 같다.
    /// 조각마다 다시 쓴 파일이 고정 파일과 같아야 한다(모양이 버리는 필드도, 바꿔 쓰는 값도 없다). 자동 테스트다.
    /// </summary>
    public sealed class OldRoomSaveTests
    {
        private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "saves");
        private const string RoomSlot = "room-1";

        // 고정 파일은 git이 체크아웃하며 줄 끝을 바꿀 수 있다(core.autocrlf). 저장소가 쓰는 줄 끝(LF)으로 맞춰 견준다.
        private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n");

        private static Dictionary<string, byte[]> CopyTopFiles(string from, string to)
        {
            Directory.CreateDirectory(to);
            var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var path in Directory.GetFiles(from))
            {
                var bytes = File.ReadAllBytes(path);
                files[Path.GetFileName(path)] = bytes;
                File.WriteAllBytes(Path.Combine(to, Path.GetFileName(path)), bytes);
            }

            return files;
        }

        [Test]
        public void Room20261114_Reads_AndRewritesTheSame()
        {
            string fixture = Path.Combine(FixtureRoot, "room-20261114", RoomSlot);
            using var dir = new TempDir();
            var original = CopyTopFiles(fixture, Path.Combine(dir.Path, RoomSlot));
            var store = new FileSaveStore(dir.Path);
            string finishFile = SaveFileNames.RoomPiece(RoomHouseBuild.PieceName);
            Assert.That(original.Keys, Is.SupersetOf(new[]
            {
                SaveFileNames.RoomSettings, SaveFileNames.RoomClock, SaveFileNames.RoomRequestLedger,
                SaveFileNames.RoomPiece(RoomWallet.PieceName), SaveFileNames.RoomPiece(RoomHouse.PieceName), SaveFileNames.RoomPiece(RoomChests.PieceName),
                finishFile, SaveFileNames.RoomHouseBuildHolds,
            }), "전제: 방 설정·시계·장부와 조각 다섯 파일");

            Assert.That(RoomSettingsSaveFiles.TryReadSaved(store, RoomSlot, out var settings, out _), Is.True);
            Assert.That(settings.SharedPieces, Is.EquivalentTo(new[] { RoomWallet.PieceName, RoomHouse.PieceName }));
            var pieces = new RoomPieceSet(settings, RoomPieceCatalog.CreateDefault());
            pieces.Load(store, RoomSlot);

            Assert.That(pieces.TryGet(RoomHouseBuild.PieceName, out var piece), Is.True, "집을 따라 꾸밈 조각이 켜진다");
            var build = (RoomHouseBuild)piece;
            var finishes = build.ToSaveData();
            Assert.That(finishes.Finishes.Length, Is.EqualTo(87), "왼쪽 위 방 바닥 꾸밈 87칸");
            Assert.That(finishes.Finishes.Select(f => f.Floor).Distinct().Count(), Is.EqualTo(1));
            Assert.That(build.HeldCount, Is.EqualTo(1), "걸려 있는 잡기 하나");
            Assert.That(pieces.Ledger.Count, Is.GreaterThan(0));

            pieces.Save(store, RoomSlot);

            var rewritten = Directory.GetFiles(Path.Combine(dir.Path, RoomSlot)).ToDictionary(Path.GetFileName, File.ReadAllBytes, StringComparer.Ordinal);
            Assert.That(rewritten.Keys, Is.EquivalentTo(original.Keys), "새 파일을 만들지도 지우지도 않는다");
            foreach (var pair in original)
                Assert.That(Text(rewritten[pair.Key]), Is.EqualTo(Text(pair.Value)), pair.Key + ": 읽고 다시 쓴 것이 고정 파일과 같다");
        }

        [Test]
        public void Room20261114_TheGuestsOutbox_HoldsTheHouseBuildLine()
        {
            // 손님 폴더(<방 슬롯>/guests/<플레이어 id>/)의 보낼 목록에 꾸밈 잡기 요청 줄 하나가 있고, 그 id가 방의 잡기와 같다(답을 받기 전에 끊긴 때).
            var guests = new FileSaveStore(Path.Combine(FixtureRoot, "room-20261114", RoomSlot, "guests"));
            string player = SaveFileNames.DefaultPlayerId;
            Assert.That(guests.TryRead(player, SaveFileNames.RoomOutbox(player), out RoomOutboxSaveData outbox), Is.True);
            var line = outbox.Requests.Single();
            Assert.That((line.Piece, line.Kind, line.Amount), Is.EqualTo((RoomHouseBuild.PieceName, RoomHouseBuild.Finish, 1)));

            var room = new FileSaveStore(Path.Combine(FixtureRoot, "room-20261114"));
            Assert.That(room.TryRead(RoomSlot, SaveFileNames.RoomHouseBuildHolds, out RoomHouseBuildHoldsSaveData holds), Is.True);
            Assert.That(holds.Holds.Single().RequestId, Is.EqualTo(line.RequestId));
            Assert.That(holds.Holds.Single().PlayerId, Is.EqualTo(player));
        }
    }
}
