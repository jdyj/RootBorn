using System;
using System.Collections.Generic;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.Core.WorldState;

namespace Rootborn.Game
{
    /// <summary>
    /// 같이 하는 방의 공유 조각(멀티플레이 설계 3.2절·4절). GameSession은 돈·마을 상태를 직접 들지 않고 "주는 쪽"(<see cref="RoomShares"/>)을 받는다.
    /// 받지 않거나(혼자 하기) 그 조각을 같이 쓰지 않는 방이면 지금처럼 자기 것이다. 규칙 코드와 화면은 차이를 모른다.
    /// - 돈: 방 지갑이 가방 뒤에 끼워진다. 돈을 쓰는 명령은 방이 허락한 뒤에만 바뀐다 — 처음에는 잡기를 보내고(<see cref="WaitingForRoom"/>),
    ///   허락이 오면 같은 명령을 다시 해서 끝낸다. 거절되면 아무것도 바뀌지 않는다. 다시 할 명령이 없으면(꺼졌다 켜짐) 잡은 돈을 푼다.
    /// - 마을 상태: 저장할 때 켜진 마을 상태 깃발(범위 Shared·SaveSlot)을 방에 보내고, 방이 알린 깃발을 자기 월드 상태에 켠다.
    /// - 보낼 요청 목록은 이 플레이어의 세이브(<see cref="SaveFileNames.RoomOutbox"/>)에 같이 쓴다. 방은 저장을 받을 때 그 요청을 반영한다.
    /// 방이 보낸 상태·답은 방 접속(Net)이 RoomShares에 넘기고, 여기서는 그 알림을 받아 명령을 잇는다.
    /// </summary>
    public sealed partial class GameSession
    {
        private static readonly string[] SharedMoneyTopics = { GameChangeTopics.Inventory, GameChangeTopics.Room };
        private RoomWalletClient _roomWallet;
        private RoomWorldFlagsClient _roomTown;
        private readonly Dictionary<string, Func<bool>> _waiting = new Dictionary<string, Func<bool>>(StringComparer.Ordinal);
        private readonly List<string> _townScratch = new List<string>(8);

        /// <summary>같이 하는 방의 조각 묶음. 혼자 하기는 null.</summary>
        public RoomShares Shares { get; private set; }

        /// <summary>돈을 같이 쓰는 방이다(지갑이 방 지갑 — 화면이 "방 지갑"이라고 보인다).</summary>
        public bool SharesWallet => _roomWallet != null;

        /// <summary>돈을 쓰는 명령이나 같이 쓰는 집의 가구 요청이 방의 허락을 기다린다(화면은 그 명령의 버튼을 잠깐 막는다).</summary>
        public bool WaitingForRoom => _waiting.Count > 0 || _houseWaiting != null || _storageWaiting != null;

        /// <summary>마지막으로 방이 거절한 명령이 있었다(돈이 모자람). 다음 명령에서 지운다.</summary>
        public bool LastRoomRefusal { get; private set; }

        /// <summary>
        /// 방의 허락을 기다리던 마지막 명령이 끝났는지: true면 허락받아 그 명령이 됐다, false면 거절됐거나(<see cref="LastRoomRefusal"/>) 허락받고도
        /// 다시 한 명령이 되지 않았다(그 사이 다른 확인이 바뀜 — 잡은 돈은 풀었다). 기다리는 중이거나 아직 기다린 명령이 없으면 null.
        /// 화면이 기다림 표시 뒤에 결과 문구를 고르는 데 쓴다. 다음 돈 쓰는 명령에서 지운다.
        /// </summary>
        public bool? LastRoomCommandApplied { get; private set; }

        private void UseShares(RoomShares shares)
        {
            Shares = shares;
            if (shares == null) return;
            _roomWallet = shares.Find<RoomWalletClient>();
            _roomTown = shares.Find<RoomWorldFlagsClient>();
            UseSharedHouse(shares);
            UseSharedChests(shares);
        }

        private void LoadRoomOutbox(ISaveStore store)
        {
            if (Shares == null) return;
            store.TryRead<RoomOutboxSaveData>(SaveSlot, SaveFileNames.RoomOutbox(PlayerId), out var outbox);
            Shares.LoadOutbox(outbox);
        }

        // 시작한 뒤 한 번: 알림을 잇고, 다시 할 명령이 없는 허락(꺼졌다 켜지기 전에 보낸 잡기)을 풀고, 방이 이미 켠 깃발을 따른다.
        private void AfterStartInRoom()
        {
            bool changed = FollowRoom(out _) | ResendToRoom();
            if (Shares != null)
            {
                Shares.Changed += OnSharesChanged;
                if (_roomWallet != null)
                {
                    _roomWallet.Granted += OnGranted;
                    _roomWallet.Refused += OnRefused;
                    foreach (var scope in new List<string>(_roomWallet.Grants)) changed |= _roomWallet.ReleaseGrant(scope);
                }

                changed |= FollowRoomTownFlags();
                ListenToSharedHouse();
                ListenToSharedChests();
            }

            if (changed) Save();
        }

        /// <summary>
        /// 돈을 쓸 수 있는 명령을 감싼다. 방 지갑이 아니면 그대로 한다. 방 지갑이면 범위 하나로 돌리고, 지갑이 잡기를 적었으면
        /// (다른 확인은 다 지났고 돈만 남았다) 그 명령을 기다리는 목록에 두고 저장한다(잡기 요청이 세이브와 함께 방으로 간다).
        /// </summary>
        private T Shared<T>(Func<T> run, Func<T, bool> applied)
        {
            if (_roomWallet == null) return run();
            LastRoomRefusal = false;
            LastRoomCommandApplied = null;
            string scope = Shares.Outbox.NewId();
            T result = InScope(scope, run);
            if (applied(result) || !_roomWallet.WasAsked(scope)) return result;
            _waiting[scope] = () => applied(InScope(scope, run));
            Save();
            Notify(SharedMoneyTopics);
            return result;
        }

        private T InScope<T>(string scope, Func<T> run)
        {
            _roomWallet.BeginScope(scope);
            _walletScopeOpen = true;
            try { return run(); }
            finally
            {
                _walletScopeOpen = false;
                _roomWallet.EndScope();
            }
        }

        /// <summary>지갑 범위 안에서 명령이 도는 중(그 명령의 알림을 받은 화면이 확인을 부를 때 범위를 겹쳐 열지 않게).</summary>
        private bool _walletScopeOpen;

        private void OnGranted(string scope)
        {
            // 다시 한 명령이 되면 그 명령이 저장하고 알린다(쓰기 요청이 같은 저장에 든다). 안 되면 잡은 돈을 푼다.
            // 다시 하는 명령이 알리는 동안 이미 기다림이 끝나 있게 먼저 뺀다. 결과는 명령 뒤에 정해지므로 한 번 더 알린다(화면이 결과 문구를 고른다).
            _waiting.TryGetValue(scope, out var again);
            _waiting.Remove(scope);
            bool done = again != null && again();
            LastRoomCommandApplied = done;
            if (done)
            {
                Notify(SharedMoneyTopics);
                return;
            }
            _roomWallet.ReleaseGrant(scope);
            SaveAndNotify(SharedMoneyTopics);
        }

        private void OnRefused(string scope)
        {
            _waiting.Remove(scope);
            LastRoomRefusal = true;
            LastRoomCommandApplied = false;
            SaveAndNotify(SharedMoneyTopics);
        }

        private void OnSharesChanged(string piece)
        {
            if (_roomTown != null && piece == _roomTown.Name)
            {
                if (FollowRoomTownFlags()) SaveAndNotify(null, GameChangeTopics.World);
                return;
            }

            if (_roomChests != null && piece == _roomChests.Name)
            {
                Notify(null, GameChangeTopics.Storage);
                return;
            }

            if (_roomHouse != null && piece == _roomHouse.Name)
            {
                FollowRoomHouse();
                return;
            }

            if (_roomWallet != null && piece == _roomWallet.Name) Notify(SharedMoneyTopics);
        }

        // 방이 켠 마을 상태 깃발을 자기 월드 상태에 켠다(켜기만 있어 되돌릴 일이 없다). 정의가 마을 상태(Shared·SaveSlot)인 것만.
        private bool FollowRoomTownFlags()
        {
            if (_roomTown == null) return false;
            bool any = false;
            foreach (var id in _roomTown.RoomFlags)
            {
                if (World.IsActive(id) || !_definitions.TryGet<WorldStateFlag>(id, out var flag) || !IsTownFlag(flag.Scope)) continue;
                any |= World.TryActivate(flag, new WorldStateActivationSource(string.Empty, string.Empty, "room", Student.CurrentDay));
            }

            return any;
        }

        // 저장 직전: 켜진 마을 상태 깃발 중 방에 없는 것을 보낼 목록에 적는다.
        private void PublishTownFlags()
        {
            if (_roomTown == null) return;
            _townScratch.Clear();
            foreach (var record in World.ToSaveData().Records)
            {
                if (record == null || !Enum.TryParse(record.Scope, out WorldStateScopeKind scope) || !IsTownFlag(scope)) continue;
                _townScratch.Add(record.FlagId);
            }

            _roomTown.Publish(_townScratch);
        }

        private static bool IsTownFlag(WorldStateScopeKind scope) => scope == WorldStateScopeKind.Shared || scope == WorldStateScopeKind.SaveSlot;
    }
}
