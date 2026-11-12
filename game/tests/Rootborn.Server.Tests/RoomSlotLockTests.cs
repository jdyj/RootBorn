using System;
using System.IO;
using System.Net;
using NUnit.Framework;
using Rootborn.Data.Save;
using Rootborn.Net.Room;

namespace Rootborn.Server.Tests
{
    /// <summary>
    /// 슬롯 잠금: 같은 세이브 폴더·슬롯을 두 번 열지 못한다(서버 둘, 내 PC의 방과 서버). 죽은 주인이 남긴 잠금은 넘어간다.
    /// 이 프로세스 안에서 두 번 잡는 것으로 본다(운영체제 잠금은 열린 파일마다라 같은 프로세스에서도 막힌다). 두 프로세스는 <see cref="ServerOperationsProcessTests"/>.
    /// </summary>
    [NonParallelizable]
    public sealed class RoomSlotLockTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "rootborn-slot-lock", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void SecondOpen_OfTheSameSlot_IsRefusedWithWhoHasIt()
        {
            using var first = RoomSlotLock.Acquire(_root, "room", "데디케이티드 서버");
            Assert.That(first.PreviousOwnerCrashed, Is.False);
            var e = Assert.Throws<RoomSlotLockedException>(() => RoomSlotLock.Acquire(_root, "room", "데디케이티드 서버"));
            StringAssert.Contains("데디케이티드 서버", e.Message, "누가 잡았는지");
            StringAssert.Contains("프로세스", e.Message);
            Assert.That(e, Is.InstanceOf<InvalidOperationException>(), "Host의 방 열기가 실패로 받는 형");

            using var other = RoomSlotLock.Acquire(_root, "other-room", "데디케이티드 서버");
            Assert.That(other, Is.Not.Null, "다른 슬롯은 된다");
        }

        [Test]
        public void AfterACleanRelease_TheNextOpenIsQuiet()
        {
            RoomSlotLock.Acquire(_root, "room", "a").Dispose();
            using var again = RoomSlotLock.Acquire(_root, "room", "b");
            Assert.That(again.PreviousOwnerCrashed, Is.False);
            Assert.That(File.Exists(Path.Combine(_root, "room", RoomSlotLock.OwnerFileName)), Is.True);
        }

        [Test]
        public void LeftoverFromADeadOwner_IsTakenOver_AndReported()
        {
            // 죽은 주인: 잠금 파일과 주인 파일이 남았지만 아무도 잠금을 쥐고 있지 않다.
            Directory.CreateDirectory(Path.Combine(_root, "room"));
            File.WriteAllText(Path.Combine(_root, "room", RoomSlotLock.LockFileName), "");
            File.WriteAllText(Path.Combine(_root, "room", RoomSlotLock.OwnerFileName), "데디케이티드 서버, 프로세스 4242");
            using var taken = RoomSlotLock.Acquire(_root, "room", "데디케이티드 서버");
            Assert.That(taken.PreviousOwnerCrashed, Is.True);
            Assert.That(taken.PreviousOwner, Does.Contain("4242"));
        }

        [Test]
        public void LockFiles_AreNotSaveFiles()
        {
            using var held = RoomSlotLock.Acquire(_root, "room", "a");
            var store = new FileSaveStore(_root);
            Assert.That(store.ReadSlotFiles("room"), Is.Empty, "세이브 파일 목록(손님 세이브·묶음과 같은 규칙)에 잠금 파일이 끼지 않는다");
            store.KeepDayCopy("room", 1, 3);
            Assert.That(Directory.GetFiles(Path.Combine(_root, "room", FileSaveStore.DayCopiesFolder, "1")), Is.Empty, "날 사본에도");
        }

        [Test]
        public void HostedRoom_OnASlotTheServerHolds_FailsBeforeListening()
        {
            using var server = RoomSlotLock.Acquire(_root, "room", "데디케이티드 서버");
            var e = Assert.Throws<RoomSlotLockedException>(() => HostedRoom.Open(IPAddress.Loopback, 0, new RoomServerSettings(), RoomRig.Versions,
                new FileSaveStore(_root), "room", new ManualRoomClock(), null));
            StringAssert.Contains("데디케이티드 서버", e.Message);
            Assert.That(File.Exists(Path.Combine(_root, "room", Rootborn.Core.Save.SaveFileNames.RoomClock)), Is.False, "방 파일을 쓰지 않았다");
        }

        [Test]
        public void HostedRoom_HoldsTheSlotWhileOpen_AndLetsGoOnShutdown()
        {
            var room = HostedRoom.Open(IPAddress.Loopback, 0, new RoomServerSettings(), RoomRig.Versions, new FileSaveStore(_root), "room", new ManualRoomClock(), null);
            Assert.Throws<RoomSlotLockedException>(() => RoomSlotLock.Acquire(_root, "room", "데디케이티드 서버"));
            room.Shutdown();
            using var after = RoomSlotLock.Acquire(_root, "room", "데디케이티드 서버");
            Assert.That(after.PreviousOwnerCrashed, Is.False, "방을 닫으면 잠금을 깨끗이 푼다");
        }
    }
}
