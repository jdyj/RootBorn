using System;
using System.Collections.Generic;
using Rootborn.Core.Building;
using Rootborn.Core.Inventory;
using Rootborn.Core.Room;
using Rootborn.Core.World;

namespace Rootborn.Game
{
    /// <summary>
    /// 같이 쓰는 집의 꾸밈(집 꾸미기 설계 8절, Core <see cref="RoomHouseBuild"/>). 집을 같이 쓰면 꾸밈도 방의 것이다:
    /// - 보이는 꾸밈·미리 보기는 방이 알린 기록을 읽은 두 번째 <see cref="HouseFinishes"/>(비추기)로 낸다. 판정은 혼자 하는 집과 같은 Core 코드(자기 해금, 자기 돈).
    /// - 적용은 "칸 잡기 → 돈 내기 → 쓰기/풀기"다: 여기서는 잡기 요청만 적고 저장한다(<see cref="WaitingForRoom"/>). 방이 잡아 주면 그때 돈을 내는 바꾸기 안에서
    ///   쓰기를 적고(돈을 같이 쓰면 같은 명령을 <see cref="Shared{T}"/>로 — 방 지갑 잡기 → 허락 → 다시 하기), 못 내면 풀기를 적는다. 개인 쪽을 먼저 바꾸고 되돌리는 흐름은 없다.
    /// - 돈 내기와 쓰기 요청은 한 저장 묶음(스냅샷·보낼 목록)에 든다: 디스크에서 "돈을 냈다" ⇔ "그 잡기의 쓰기가 보낼 목록에 있다(또는 방이 이미 받았다)".
    ///   그래서 다시 켠 판에서 내 잡기 가운데 답을 기다리지도(잡기 요청이 보낼 목록에 있음) 쓰기·풀기를 보내지도 않은 것은 돈을 내지 않은 것이다 — 풀어도 잃는 것이 없다
    ///   (상자 <see cref="GameSession.OnStorageAnswered"/>와 같은 틀: 잡기 요청은 답을 처리한 저장 전까지 보낼 목록에 남아 장부가 같은 답을 다시 준다).
    /// - 자기 house-build.json은 같이 쓰는 집에서 읽은 대로 들고만 있고 쓰지 않는다(같은 슬롯을 혼자 하면 자기 꾸밈 그대로).
    /// 해금은 요청한 사람의 진행이라 방은 확인하지 않는다(가구 놓기와 같다). 꾸밈 id로 분기하지 않는다.
    /// </summary>
    public sealed partial class GameSession
    {
        private static readonly string[] SharedFinishTopics = { GameChangeTopics.HouseBuild, GameChangeTopics.Room };
        private RoomHouseBuildClient _roomBuild;
        private readonly HouseFinishes _sharedFinishes = new HouseFinishes();
        // 답을 기다리는 내 꾸밈 잡기 요청 id(한 번에 하나). 다시 켠 판은 보낼 목록에서 되찾는다.
        private string _finishWaiting;
        // 허락받고 돈을 내는 중인 잡기 id(방 지갑의 허락을 기다림). 메모리만 — 다시 켠 판은 비어 있고 그 잡기는 떠도는 잡기로 푼다.
        private readonly HashSet<string> _finishPaying = new HashSet<string>(StringComparer.Ordinal);
        // 이 판에서 쓰기나 풀기를 보낸 잡기 id(답이 상태보다 먼저 와 낡은 상태에 잡기가 남아 보여도 다시 풀지 않게). 메모리만.
        private readonly HashSet<string> _finishSettled = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _holdScratch = new List<string>(4);

        /// <summary>집과 함께 꾸밈도 같이 쓰는 방이다(보이는 꾸밈과 꾸미기가 방의 것).</summary>
        public bool SharesHouseBuild => _roomBuild != null;

        /// <summary>방이 마지막 꾸밈 잡기를 거절했으면 <see cref="HouseFinishReasons.RoomRefused"/>(화면이 보인다). 다음 꾸밈 요청에서 지운다.</summary>
        public string LastHouseFinishRefusal { get; private set; }

        // 같이 쓰는 집이다(집 또는 꾸밈 조각이 방의 것). 꾸밈 조각은 집을 따라 늘 함께 켜지지만, 혹시 없어도 자기 꾸밈을 보이거나 쓰지 않는다.
        private bool InSharedHouse => _roomBuild != null || _roomHouse != null;

        // 보이는 꾸밈: 같이 쓰는 집이면 방의 것(비추기), 아니면 자기 것.
        private HouseFinishes VisibleFinishes => InSharedHouse ? _sharedFinishes : _houseFinishes;

        private void UseSharedHouseBuild(RoomShares shares)
        {
            _roomBuild = shares?.Find<RoomHouseBuildClient>();
            if (_roomBuild != null) _sharedFinishes.Load(_roomBuild.Data);
        }

        // 시작한 뒤 한 번: 기다리던 꾸밈 요청을 보낼 목록에서 되찾고, 답을 듣고(붙기 전에 온 답은 이때 온다), 떠도는 내 잡기를 푼다. 저장할 것이 생겼으면 true.
        private bool ListenToSharedHouseBuild()
        {
            if (_roomBuild == null) return false;
            foreach (var r in Shares.Outbox.Requests)
                if (IsBuildHoldRequest(r))
                {
                    _finishWaiting = r.RequestId;
                    break;
                }

            _roomBuild.Answered += OnFinishAnswered;
            return ReleaseStrayHolds();
        }

        // 방의 꾸밈 상태가 바뀌었다(남이 바꿨거나 내 쓰기가 적용됐다): 비추기를 다시 읽고, 떠도는 내 잡기를 풀고, 알린다.
        private void FollowRoomHouseBuild()
        {
            _sharedFinishes.Load(_roomBuild.Data);
            if (ReleaseStrayHolds()) Save();
            Notify(null, GameChangeTopics.HouseBuild);
        }

        // 같이 쓰는 집: 미리 보기가 되고 기다리는 것이 없을 때만 잡기를 요청하고 저장한다. 지금 바뀐 것은 없으므로 늘 false(가구 요청과 같다).
        private bool RequestSharedFinish(HouseFinishPreview preview, Func<string> request)
        {
            if (_roomBuild == null || !preview.CanApply || _finishWaiting != null || _finishPaying.Count > 0) return false;
            LastRoomRefusal = false;
            LastRoomCommandApplied = null;
            LastHouseFinishRefusal = null;
            _finishWaiting = request();
            Save();
            Notify(SharedFinishTopics);
            return false;
        }

        // 방이 꾸밈 잡기에 답했다. 거절이면 아무것도 바뀌지 않는다. 허락이면 돈을 내는 명령(쓰기를 같은 바꾸기에)을 개인 지갑 또는 방 지갑으로 돌린다.
        private void OnFinishAnswered(RoomRequestSaveData request, bool applied)
        {
            if (request.RequestId == _finishWaiting) _finishWaiting = null;
            string hold = request.RequestId;
            if (!applied)
            {
                LastHouseFinishRefusal = HouseFinishReasons.RoomRefused;
                LastRoomCommandApplied = false;
                SaveAndNotify(SharedFinishTopics);
                return;
            }

            bool band = request.Kind == RoomHouseBuild.Band;
            string surface = request.Ids != null && request.Ids.Length > 0 ? request.Ids[0] : null;
            var finish = request.Ids != null && request.Ids.Length > 1 ? FindFinish(request.Ids[1]) : null;
            // 돈은 방이 잡은 칸 수로 셈한다(요청의 Amount = 내 미리 보기의 바뀌는 칸 수, 방이 같다고 확인했다). 띠는 한 번.
            var cost = finish == null ? null : HouseFinishes.CostFor(finish, band ? 1 : request.Amount);
            _finishPaying.Add(hold);
            bool done = Shared(() => PayForHold(hold, surface, cost, !band), d => d, () => ReleaseHold(hold));
            if (done) return;                          // 냈다 — PayForHold가 저장하고 알렸다
            if (_finishPaying.Contains(hold)) return;  // 방 지갑의 허락을 기다린다(Shared가 저장하고 알렸다)
            LastRoomCommandApplied = false;            // 낼 수 없어 풀었다(ReleaseHold가 저장하고 알렸다)
        }

        // 돈을 낸다: 비용을 빼는 바꾸기 커밋 안에서 쓰기를 적는다(메모리만, 던지지 않는다). 칸 꾸밈이면 요청한 사람의 인테리어가 하루 한도 안에서 자란다.
        private bool PayForHold(string hold, string surface, IReadOnlyList<ItemAmount<string>> cost, bool grows)
        {
            if (cost == null || !TryGrants(cost, out var grants)) return false;
            if (!Inventory.TryExchange(grants, null, () => _roomBuild.AddUse(hold))) return false;
            _finishPaying.Remove(hold);
            _finishSettled.Add(hold);
            LastRoomCommandApplied = true;
            bool grown = grows && GrowByPlacing(surface);
            return Saved(true, true, grown ? new[] { GameChangeTopics.Student } : null, GameChangeTopics.HouseBuild, GameChangeTopics.Inventory,
                GameChangeTopics.Room, grown ? GameChangeTopics.Workspace : null);
        }

        // 잡은 칸을 쓰지 않는다(돈을 내지 못했다): 풀기를 적고 저장·알린다. 꾸밈·돈은 그대로다.
        private void ReleaseHold(string hold)
        {
            _finishPaying.Remove(hold);
            if (_finishSettled.Add(hold)) _roomBuild.AddRelease(hold);
            SaveAndNotify(SharedFinishTopics);
        }

        // 떠도는 내 잡기: 방의 상태에 내 잡기로 있는데, 답을 기다리지도(잡기 요청이 보낼 목록에 있음) 돈을 내는 중도 아니고 쓰기·풀기도 보내지 않은 것.
        // 시간이 지나도 풀리지 않아 그 칸이 모두에게 막히므로 푼다. 돈은 내지 않았다(낸 돈과 그 쓰기는 한 저장 묶음 — 위 요약). 풀기를 적었으면 true.
        private bool ReleaseStrayHolds()
        {
            if (_roomBuild == null || !_roomBuild.Known) return false;
            _roomBuild.CopyHoldsOf(PlayerId, _holdScratch);
            bool any = false;
            foreach (var hold in _holdScratch)
            {
                if (hold == _finishWaiting || _finishPaying.Contains(hold) || _finishSettled.Contains(hold) || HasOutgoingFor(hold)) continue;
                _finishSettled.Add(hold);
                _roomBuild.AddRelease(hold);
                any = true;
            }

            return any;
        }

        // 그 잡기에 대한 요청(잡기 요청 자신, 그 잡기의 쓰기·풀기)이 보낼 목록에 있다.
        private bool HasOutgoingFor(string hold)
        {
            foreach (var r in Shares.Outbox.Requests)
            {
                if (r.Piece != RoomHouseBuild.PieceName) continue;
                if (r.RequestId == hold) return true;
                if ((r.Kind == RoomHouseBuild.Use || r.Kind == RoomHouseBuild.Release) && r.Ids != null && r.Ids.Length == 1 && r.Ids[0] == hold) return true;
            }

            return false;
        }

        private static bool IsBuildHoldRequest(RoomRequestSaveData r)
            => r.Piece == RoomHouseBuild.PieceName && (r.Kind == RoomHouseBuild.Finish || r.Kind == RoomHouseBuild.Band);

        private bool RequestSharedFinish(string finishId, string tool, CellPos pointer)
        {
            var preview = PreviewHouseFinish(finishId, tool, pointer);
            return RequestSharedFinish(preview, () => _roomBuild.RequestFinish(House.SurfaceId, finishId, tool, pointer, preview.ChangedCells.Count));
        }

        private bool RequestSharedBand(string finishId)
            => RequestSharedFinish(PreviewHouseBand(finishId), () => _roomBuild.RequestBand(House.SurfaceId, finishId));
    }
}
