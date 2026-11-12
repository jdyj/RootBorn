using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Rootborn.Net.Messages;
using Rootborn.Net.Room;

namespace Rootborn.Net.Tests
{
    /// <summary>
    /// 세이브 묶음 나눠 보내기와 크기 한도(Net의 한 곳, RoomTransfer): 메시지 하나의 최대 크기보다 큰 세이브를 조각으로 나누고 차례로 모은다.
    /// 순서가 틀리거나 한도를 넘으면 그 묶음을 버린다. 방 설정·조각 상태·답 메시지의 형식도 본다.
    /// </summary>
    public sealed class RoomTransferTests
    {
        private static SaveFileSet BigSet(int bytes)
        {
            var set = new SaveFileSet();
            set.Writes.Add(new KeyValuePair<string, string>("adult-career-snapshot-a.json", "{\"Text\":\"" + new string('가', bytes / 3) + "\"}"));
            set.Writes.Add(new KeyValuePair<string, string>("room-outbox-a.json", "{}"));
            set.Deletes.Add("old.json");
            return set;
        }

        [Test]
        public void ASaveBiggerThanAMessage_IsSplitAndAssembledInOrder()
        {
            var set = BigSet(100_000);
            Assert.That(RoomTransfer.TryEncode(set, out var bytes), Is.True);
            var chunks = RoomTransfer.Split(7, bytes, 4096);
            Assert.That(chunks.Count, Is.GreaterThan(20));

            var catalog = RootbornMessages.CreateCatalog(4096);
            var assembler = new ChunkAssembler();
            byte[] payload = null;
            foreach (var chunk in chunks)
            {
                Assert.That(catalog.TryEncode(chunk, out var wire), Is.True, "조각 하나는 메시지 하나에 든다");
                Assert.That(catalog.Decode(wire, out var decoded), Is.EqualTo(DecodeOutcome.Ok));
                if (assembler.Add((SaveChunkMessage)decoded, out uint id, out var done)) { payload = done; Assert.That(id, Is.EqualTo(7u)); }
            }

            Assert.That(RoomTransfer.TryDecode(payload, out var back), Is.True);
            Assert.That(back.Writes.Select(w => (w.Key, w.Value)), Is.EqualTo(set.Writes.Select(w => (w.Key, w.Value))));
            Assert.That(back.Deletes, Is.EqualTo(new[] { "old.json" }));
        }

        [Test]
        public void AnEmptySave_IsOneChunk()
        {
            Assert.That(RoomTransfer.TryEncode(new SaveFileSet(), out var bytes), Is.True);
            var chunks = RoomTransfer.Split(1, bytes, 4096);
            Assert.That(chunks.Count, Is.EqualTo(1));
            Assert.That(new ChunkAssembler().Add(chunks[0], out _, out var payload), Is.True);
            Assert.That(RoomTransfer.TryDecode(payload, out var set) && set.IsEmpty, Is.True);
        }

        [Test]
        public void OutOfOrderChunks_DropTheSave()
        {
            RoomTransfer.TryEncode(BigSet(20_000), out var bytes);
            var chunks = RoomTransfer.Split(3, bytes, 4096);
            var assembler = new ChunkAssembler();
            assembler.Add(chunks[0], out _, out _);
            Assert.That(assembler.Add(chunks[2], out _, out _), Is.False);
            Assert.That(assembler.Failed, Is.EqualTo(3u));
            Assert.That(chunks.Skip(3).Any(c => assembler.Add(c, out _, out _)), Is.False, "버린 묶음의 나머지로는 끝나지 않는다");

            var again = RoomTransfer.Split(4, bytes, 4096);
            byte[] payload = null;
            foreach (var c in again) assembler.Add(c, out _, out payload);
            Assert.That(payload, Is.Not.Null, "새 묶음 id로 다시 보내면 받는다");
        }

        [Test]
        public void SizeAndNameLimits_AreChecked()
        {
            Assert.That(RoomTransfer.TryEncode(BigSet(RoomTransfer.MaxSaveBytes + 10), out _), Is.False, "크기 한도");
            var longName = new SaveFileSet();
            longName.Writes.Add(new KeyValuePair<string, string>(new string('a', RoomTransfer.MaxFileNameBytes + 1), "{}"));
            Assert.That(RoomTransfer.TryEncode(longName, out _), Is.False, "이름 한도");
            Assert.That(RoomTransfer.TryDecode(new byte[] { 1, 2, 3 }, out _), Is.False, "깨진 바이트");
        }

        [Test]
        public void Overlay_KeepsTheLaterWriteAndDelete()
        {
            var first = SaveFileSet.From(new[] { new KeyValuePair<string, string>("a.json", "1"), new KeyValuePair<string, string>("b.json", "1") }, null);
            first.Overlay(SaveFileSet.From(new[] { new KeyValuePair<string, string>("a.json", "2") }, new[] { "b.json" }));
            Assert.That(first.Writes.Single().Value, Is.EqualTo("2"));
            Assert.That(first.Deletes, Is.EqualTo(new[] { "b.json" }));
        }

        [Test]
        public void SettingsStateAndAnswerMessages_RoundTrip()
        {
            var catalog = RootbornMessages.CreateCatalog(4096);
            object Round(object m)
            {
                Assert.That(catalog.TryEncode(m, out var bytes), Is.True);
                Assert.That(catalog.Decode(bytes, out var back), Is.EqualTo(DecodeOutcome.Ok));
                return back;
            }

            var settings = (RoomSettingsMessage)Round(new RoomSettingsMessage { SharedPieces = new[] { "town", "wallet" } });
            Assert.That(settings.SharedPieces, Is.EqualTo(new[] { "town", "wallet" }));
            var state = (RoomPieceStateMessage)Round(new RoomPieceStateMessage { Piece = "wallet", Amount = 42, Ids = new[] { "x" } });
            Assert.That((state.Piece, state.Amount, state.Ids.Single()), Is.EqualTo(("wallet", 42, "x")));
            var answer = (RoomAnswerMessage)Round(new RoomAnswerMessage { Piece = "wallet", RequestId = "r1", Outcome = "applied", Amount = 7 });
            Assert.That((answer.RequestId, answer.Outcome, answer.Amount), Is.EqualTo(("r1", "applied", 7)));
            var transfer = (SaveTransferAnswerMessage)Round(new SaveTransferAnswerMessage { TransferId = 9, Accepted = false, Reason = GuestSaveRefusal.Damaged });
            Assert.That((transfer.TransferId, transfer.Accepted, transfer.Reason), Is.EqualTo((9u, false, GuestSaveRefusal.Damaged)));
        }

        [Test]
        public void APieceStateTooBigForOneMessage_IsSplit_EveryChunkFits_AndComesBackWhole()
        {
            const int max = 4096;
            var catalog = RootbornMessages.CreateCatalog(max);
            uint next = 1;
            var small = new Rootborn.Core.Room.RoomPieceState("wallet", 5, Array.Empty<string>());
            Assert.That(RoomPieceStates.Messages(small, max, ref next).Single(), Is.InstanceOf<RoomPieceStateMessage>(), "작은 상태는 메시지 하나");
            Assert.That(next, Is.EqualTo(1u));

            var ids = Enumerable.Range(0, 400).Select(i => i + "|" + new string('f', 84) + "|house|" + (i % 28) + "|" + (i / 28) + "|0").ToArray();
            var big = new Rootborn.Core.Room.RoomPieceState("house", ids.Length, ids);
            var messages = RoomPieceStates.Messages(big, max, ref next);
            Assert.That(messages.Count, Is.GreaterThan(1));
            Assert.That(next, Is.EqualTo(2u));
            var assembler = new ChunkAssembler();
            byte[] payload = null;
            foreach (var m in messages.Cast<RoomPieceStateChunkMessage>())
            {
                Assert.That(catalog.TryEncode(m, out var bytes), Is.True, "조각마다 메시지 하나에 들어간다");
                Assert.That(catalog.Decode(bytes, out var back), Is.EqualTo(DecodeOutcome.Ok));
                var c = (RoomPieceStateChunkMessage)back;
                assembler.Add(new SaveChunkMessage { TransferId = c.TransferId, Index = c.Index, Count = c.Count, Bytes = c.Bytes }, out _, out payload);
            }

            Assert.That(RoomPieceStates.TryDecode(payload, out var whole), Is.True);
            Assert.That((whole.Piece, whole.Amount), Is.EqualTo(("house", 400)));
            Assert.That(whole.Ids, Is.EqualTo(ids));
            Assert.That(RoomPieceStates.TryDecode(new byte[] { 1, 2, 3 }, out _), Is.False, "깨진 바이트");
        }
    }
}
