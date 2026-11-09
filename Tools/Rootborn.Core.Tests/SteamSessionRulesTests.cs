using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Network;

namespace Rootborn.Core.Tests
{
    public sealed class PeerTableTests
    {
        [Test]
        public void Add_GivesEachConnectionItsOwnClientIdStartingAfterTheServer()
        {
            var table = new PeerTable();
            Assert.That(table.Add(70u), Is.EqualTo(1UL));
            Assert.That(table.Add(71u), Is.EqualTo(2UL));
            Assert.That(table.TryGetHandle(2UL, out uint handle), Is.True);
            Assert.That(handle, Is.EqualTo(71u));
            Assert.That(table.TryGetClientId(70u, out ulong id), Is.True);
            Assert.That(id, Is.EqualTo(1UL));
            Assert.That(table.Count, Is.EqualTo(2));
        }

        [Test]
        public void Add_SameConnectionTwiceKeepsOneId()
        {
            var table = new PeerTable();
            ulong first = table.Add(9u);
            Assert.That(table.Add(9u), Is.EqualTo(first));
            Assert.That(table.Count, Is.EqualTo(1));
        }

        [Test]
        public void Remove_ForgetsBothDirectionsAndNeverReusesTheId()
        {
            var table = new PeerTable();
            table.Add(5u);
            Assert.That(table.Remove(5u, out ulong removed), Is.True);
            Assert.That(removed, Is.EqualTo(1UL));
            Assert.That(table.TryGetHandle(1UL, out _), Is.False);
            Assert.That(table.TryGetClientId(5u, out _), Is.False);
            Assert.That(table.Remove(5u, out _), Is.False);
            // A reused Steam handle is a new player: Netcode must not mistake it for the one that left.
            Assert.That(table.Add(5u), Is.EqualTo(2UL));
        }

        [Test]
        public void Clear_EmptiesTheTable()
        {
            var table = new PeerTable();
            table.Add(1u);
            table.Add(2u);
            var handles = new List<uint>(table.Handles);
            Assert.That(handles, Is.EquivalentTo(new[] { 1u, 2u }));
            table.Clear();
            Assert.That(table.Count, Is.Zero);
        }
    }

    public sealed class SteamLaunchArgsTests
    {
        [Test]
        public void TryGetLobbyId_ReadsTheIdSteamPassesWhenAFriendClicksJoin()
        {
            var args = new[] { "rootborn.exe", "+connect_lobby", "109775241021923456" };
            Assert.That(SteamLaunchArgs.TryGetLobbyId(args, out ulong lobby), Is.True);
            Assert.That(lobby, Is.EqualTo(109775241021923456UL));
        }

        [TestCase("rootborn.exe")]
        [TestCase("rootborn.exe +connect_lobby")]
        [TestCase("rootborn.exe +connect_lobby abc")]
        [TestCase("rootborn.exe +connect_lobby 0")]
        [TestCase("rootborn.exe -connect_lobby 123")]
        public void TryGetLobbyId_IgnoresMissingOrBrokenValues(string commandLine)
        {
            Assert.That(SteamLaunchArgs.TryGetLobbyId(commandLine.Split(' '), out _), Is.False);
        }

        [Test]
        public void TryGetLobbyId_HandlesNull()
        {
            Assert.That(SteamLaunchArgs.TryGetLobbyId(null, out _), Is.False);
        }

        [Test]
        public void ConnectString_IsWhatTryGetLobbyIdReadsBack()
        {
            string connect = SteamLaunchArgs.ConnectString(42UL);
            Assert.That(connect, Is.EqualTo("+connect_lobby 42"));
            Assert.That(SteamLaunchArgs.TryGetLobbyId(connect.Split(' '), out ulong lobby), Is.True);
            Assert.That(lobby, Is.EqualTo(42UL));
        }
    }

    public sealed class FriendRoomsTests
    {
        private const uint OurApp = 480;

        [Test]
        public void Joinable_KeepsFriendsInOurGameWithALobbySortedByName()
        {
            var candidates = new[]
            {
                new FriendRoomCandidate(1, "minji", OurApp, 900),
                new FriendRoomCandidate(2, "Alex", OurApp, 901),
                new FriendRoomCandidate(3, "other game", 570, 902),
                new FriendRoomCandidate(4, "menu only", OurApp, 0),
                new FriendRoomCandidate(5, "not playing", 0, 0),
            };

            var rooms = FriendRooms.Joinable(candidates, OurApp);

            Assert.That(rooms.Count, Is.EqualTo(2));
            Assert.That(rooms[0].Name, Is.EqualTo("Alex"));
            Assert.That(rooms[0].LobbyId, Is.EqualTo(901UL));
            Assert.That(rooms[1].Name, Is.EqualTo("minji"));
        }

        [Test]
        public void Joinable_ListsASharedLobbyOnce()
        {
            // Two friends already in the same room show it once, under the first name.
            var candidates = new[]
            {
                new FriendRoomCandidate(1, "Bo", OurApp, 900),
                new FriendRoomCandidate(2, "Ari", OurApp, 900),
            };

            var rooms = FriendRooms.Joinable(candidates, OurApp);

            Assert.That(rooms.Count, Is.EqualTo(1));
            Assert.That(rooms[0].Name, Is.EqualTo("Ari"));
        }

        [Test]
        public void Joinable_EmptyOrNullGivesNoRooms()
        {
            Assert.That(FriendRooms.Joinable(null, OurApp), Is.Empty);
            Assert.That(FriendRooms.Joinable(new FriendRoomCandidate[0], OurApp), Is.Empty);
        }

        [Test]
        public void Joinable_BlankNameStillShowsTheRoom()
        {
            var rooms = FriendRooms.Joinable(new[] { new FriendRoomCandidate(7, null, OurApp, 3) }, OurApp);
            Assert.That(rooms.Count, Is.EqualTo(1));
            Assert.That(rooms[0].Name, Is.EqualTo(string.Empty));
        }
    }
}
