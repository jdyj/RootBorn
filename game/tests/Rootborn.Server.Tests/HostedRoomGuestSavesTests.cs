using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.Data;
using Rootborn.Data.Builders;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Net.Room;
using Rootborn.Net.Session;

namespace Rootborn.Server.Tests
{
    /// <summary>
    /// 내 PC에서 연 방도 손님의 진행을 방을 가진 쪽에 저장한다(설계 3절·5절): 방을 연 사람(owner)의 슬롯 폴더 안 guests/&lt;플레이어&gt;/ —
    /// 데디케이티드 서버와 같은 자리·같은 검사(GuestSaveFolder). 방을 연 사람의 진행은 그 슬롯 그 자체라 손님 저장으로 받지 않고,
    /// 공유 조각 요청은 따로 보낸다(RoomGuestLink.FlushRequests). RoomServer + 같은 프로세스 안 전송, 실제 data/. 명령은 GameSession 직접 호출.
    /// </summary>
    public sealed class HostedRoomGuestSavesTests
    {
        private const string Work = "work.clinic-helper"; // 보상: 용돈 5
        private const string Slot = "my-slot";
        private const string Owner = "slot-owner";
        private static GameDefinitions _definitions;
        private static GameStartSettings _settings;
        private string _root;

        [OneTimeSetUp]
        public void LoadData()
        {
            var load = GameDataLoader.Load(Path.Combine(ServerProcessTests.RepoRoot(), "data"), RootbornBuilders.Register);
            load.ThrowIfFailed();
            _definitions = load.Definitions;
            _settings = GameStartSettings.FromDefinitions(_definitions);
        }

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "rootborn-hosted-guests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private string Saves => Path.Combine(_root, "saves");

        // 방을 연 사람의 세이브 폴더(Saves)의 그 슬롯이 방이다 — Host RoomHosting과 같다.
        private RoomRig OpenMyRoom(params string[] shared)
        {
            var guests = new GuestSaveFolder(GuestSaveFolder.FolderFor(Saves, Slot), GameSession.SaveCheck(_definitions, _settings));
            return new RoomRig(new FileSaveStore(Saves), slot: Slot, settings: new RoomServerSettings { MaxPlayers = 4, SharedPieces = shared },
                timeRules: Program.RoomTimeRules(_definitions), guests: guests, owner: Owner);
        }

        private static void Shift(GameSession game, string request)
        {
            Assert.That(game.BeginPartTimeWork(Work, request, 1).Applied, Is.True, request);
            Assert.That(game.SettleMinigame(request, true).Applied, Is.True, request);
        }

        private (SessionClient Client, RoomGuestLink Link, GameSession Game) JoinAsOwner(RoomRig rig)
        {
            var client = rig.Join(Owner);
            var link = new RoomGuestLink(client, new SessionClientSettings().MaxMessageBytes);
            rig.Run(0.5);
            Assert.That(link.Ready, Is.True);
            Assert.That(link.TakesGuestSaves, Is.False, "방을 연 사람의 진행은 자기 슬롯이다");
            var game = GameSession.Start(_definitions, _settings, new FileSaveStore(Saves), Slot, SaveFileNames.DefaultPlayerId, shares: link.Shares);
            return (client, link, game);
        }

        private static (SessionClient Client, RoomGuestLink Link, GameSession Game) JoinAsGuest(RoomRig rig, string playerId)
        {
            var client = rig.Join(playerId);
            var link = new RoomGuestLink(client, new SessionClientSettings().MaxMessageBytes);
            rig.Run(0.5);
            Assert.That(link.Ready && link.TakesGuestSaves, Is.True, playerId + ": 손님 저장을 받는 방");
            var store = new MemorySaveSlot(RoomGuestLink.Slot, link.Files.Writes);
            store.Committed += c => link.Upload(SaveFileSet.From(c.Writes, c.Deletes));
            return (client, link, GameSession.Start(_definitions, _settings, store, RoomGuestLink.Slot, playerId, shares: link.Shares));
        }

        [Test]
        public void TheGuestsProgress_IsKeptInTheOwnersSlot_AndComesBackWhenTheRoomIsOpenedAgain()
        {
            string guestFolder = Path.Combine(GuestSaveFolder.FolderFor(Saves, Slot), "friend");
            using (var rig = OpenMyRoom())
            {
                var owner = JoinAsOwner(rig);
                Shift(owner.Game, "o:w1");
                var friend = JoinAsGuest(rig, "friend");
                Assert.That(friend.Link.Files.Writes, Is.Empty, "처음 온 사람은 새 게임");
                Shift(friend.Game, "f:w1");
                Shift(friend.Game, "f:w2");
                rig.Run(0.5);
                Assert.That(File.Exists(Path.Combine(guestFolder, SaveFileNames.AdultCareerSnapshot("friend"))), Is.True, "손님 진행은 방을 연 사람의 슬롯 안 guests/");
                Assert.That(Directory.GetFiles(Path.Combine(Saves, Slot)).Any(f => Path.GetFileName(f).Contains("friend")), Is.False, "방의 슬롯 맨 위에는 섞이지 않는다");
                rig.Room.Shutdown();
            }

            // 방장이 혼자 이어 하기: guests/ 폴더가 있어도 방장의 진행은 그대로 읽힌다.
            var alone = GameSession.Start(_definitions, _settings, new FileSaveStore(Saves), Slot, SaveFileNames.DefaultPlayerId);
            Assert.That(alone.Wallet.Balance, Is.EqualTo(5), "방장의 진행");
            Shift(alone, "o:w2");

            using var again = OpenMyRoom();
            var back = JoinAsGuest(again, "friend");
            Assert.That(back.Game.Wallet.Balance, Is.EqualTo(10), "같은 슬롯으로 다시 열면 손님이 자기 진행으로 돌아온다");
            Assert.That(GameSession.Start(_definitions, _settings, new FileSaveStore(Saves), Slot, SaveFileNames.DefaultPlayerId).Wallet.Balance, Is.EqualTo(10),
                "방장의 진행도 그대로(손님 진행과 섞이지 않는다)");
        }

        [Test]
        public void TheOwnersRequests_GoSeparately_AndTheGuestsGoWithTheirSaves()
        {
            using var rig = OpenMyRoom(RoomWallet.PieceName);
            var owner = JoinAsOwner(rig);
            var friend = JoinAsGuest(rig, "friend");
            Shift(owner.Game, "o:w1");
            owner.Link.FlushRequests(); // Host가 프레임마다
            rig.Run(0.5);
            Shift(friend.Game, "f:w1");
            Assert.That(friend.Link.FlushRequests(), Is.Zero, "손님의 요청은 저장과 함께 간다");
            rig.Run(0.5);
            Assert.That(rig.Room.Pieces.TryGet(RoomWallet.PieceName, out var wallet) && ((RoomWallet)wallet).Balance == 10, Is.True, "둘 다 방 지갑에");
            Assert.That(owner.Game.Wallet.Balance, Is.EqualTo(10));
            Assert.That(friend.Game.Wallet.Balance, Is.EqualTo(10));
        }

        [Test]
        public void AnUploadFromTheOwner_IsNotKeptAsAGuestSave()
        {
            using var rig = OpenMyRoom();
            var client = rig.Join(Owner);
            rig.Run(0.5);
            foreach (var chunk in RoomTransfer.Split(1, Encode(new SaveFileSet { Writes = { new System.Collections.Generic.KeyValuePair<string, string>("x.json", "{}") } }), 4096))
                client.Send(chunk);
            rig.Run(0.5);
            Assert.That(Directory.Exists(Path.Combine(GuestSaveFolder.FolderFor(Saves, Slot), Owner)), Is.False);
            Assert.That(rig.Log.Any(l => l.Contains(GuestSaveRefusal.NotAccepted)), Is.True, string.Join("\n", rig.Log));
        }

        private static byte[] Encode(SaveFileSet set)
        {
            Assert.That(RoomTransfer.TryEncode(set, out var bytes), Is.True);
            return bytes;
        }
    }
}
