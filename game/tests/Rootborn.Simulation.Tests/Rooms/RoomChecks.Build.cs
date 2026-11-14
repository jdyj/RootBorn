using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rootborn.Core.Building;
using Rootborn.Core.Inventory;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.Core.World;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Net.Room;

namespace Rootborn.Simulation.Rooms
{
    /// <summary>
    /// 같이 쓰는 집의 꾸밈 불변식(집 꾸미기 1단계, 작업 7). 방의 꾸밈 조각(house-build)은 "칸 잡기 → 돈 내기 → 쓰기/풀기"로 움직인다. 여기서는 그 걸음을 바깥에서 센다:
    /// - 장부: 꾸밈 잡기마다 누가 무엇을 몇 칸 잡았나, 돈을 냈나(보낼 목록에 쓰기가 처음 나타났다 = 돈을 빼는 바꾸기와 같은 묶음), 풀었나, 방에서 사라졌나.
    /// - 돈 보존: 나눠 쓰지 않는 지갑이면 그 자리의 지갑이 한 번의 받기 동안 돈 낸 잡기의 비용만큼만 줄어든다. 돈을 낸 잡기는 풀리지 않고, 방이 그 잡기를 지울 때
    ///   (쓰기 적용) 낸 칸 수와 방이 실제로 바꾼 칸 수가 같고 방의 기록에 그 꾸밈이 있다. 방의 꾸밈 기록은 쓰기 없이 바뀌지 않는다(공짜 꾸밈 없음). 조용해지면 자리마다 낸 돈 = 적용된 돈.
    /// - 모두가 같은 것을 본다: 조용하고 가라앉은 뒤 모든 자리의 HouseWallAt·HouseFloorAt(틀의 모든 칸)·HouseBandColors가 방의 기록으로 만든 비추기와 같다.
    /// - 끝난 뒤 남은 잡기 0(영영 막힌 칸 없음), 돈 낸 표시 0, 손님의 자기 house-build.json은 같이 쓰는 집에서 바뀌지 않는다.
    /// 꾸밈 id로 분기하지 않는다. 화면·입력 근거가 아니다(창 없는 몸).
    /// </summary>
    public sealed partial class RoomChecks
    {
        private sealed class BuildHold
        {
            public RoomSeat Seat;
            public string Id, Kind, Surface, FinishId, Tool = string.Empty;
            public int X, Y, Amount;
            public GameSession Created;
            public bool Paid, Released, Gone, Applied, Unknown;
            public string LastUse = string.Empty;
            public int Cost;

            public bool IsBand => Kind == RoomHouseBuild.Band;
            public override string ToString() => Seat?.PlayerId + " " + Kind + " " + FinishId + (IsBand ? string.Empty : " " + Tool + "@" + X + "," + Y + " x" + Amount)
                + (Paid ? " 돈 냄" : string.Empty) + (Released ? " 풀음" : string.Empty) + (Gone ? " 방에서 사라짐" : string.Empty) + " [" + Id + "]";
        }

        private readonly Dictionary<string, BuildHold> _build = new Dictionary<string, BuildHold>(StringComparer.Ordinal);
        private readonly HashSet<string> _serverBuildHolds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<RoomSeat, int> _buildPaid = new Dictionary<RoomSeat, int>();
        private readonly Dictionary<RoomSeat, int> _buildApplied = new Dictionary<RoomSeat, int>();
        private int _buildRevision = -1;
        private string _buildSignature;
        private HouseFinishes _buildPrev;
        private bool _buildConcurrent;

        private RoomHouseBuild BuildPiece => _play.Server.Pieces.TryGet(RoomHouseBuild.PieceName, out var p) ? (RoomHouseBuild)p : null;

        /// <summary>이 방에 꾸밈 조각이 있다(같이 쓰는 집).</summary>
        public bool TracksBuild => BuildPiece != null;

        private RoomFinishDefinition FinishOf(string id) => !string.IsNullOrEmpty(id) && _play.Atlas.Definitions.TryGet<RoomFinishDefinition>(id, out var d) ? d : null;

        private static int Money(IReadOnlyList<ItemAmount<string>> cost)
        {
            int total = 0;
            if (cost != null) for (int i = 0; i < cost.Count; i++) total += cost[i].Count;
            return total;
        }

        private HouseFinishes BuildView(HouseBuildSaveData data, string surface)
        {
            HouseBuildFrame frame = string.IsNullOrEmpty(surface) ? null : _play.HouseRules.BuildFrameFor(surface);
            if (frame == null) return null;
            var view = new HouseFinishes();
            view.Attach(frame, _play.HouseRules.FindFinish);
            view.Load(data);
            return view;
        }

        // ---------- 판이 시작될 때 ----------

        private void BuildStarted(RoomSeat seat, GameSession session)
        {
            var client = session.Shares?.Find<RoomHouseBuildClient>();
            if (client == null) return;
            client.Answered += (request, applied) => BuildAnswered(seat, session, request, applied);
            foreach (BuildHold h in _build.Values)
                if (h.Seat == seat && !h.Gone) { Result.Count("다시 들어옴: 풀리지 않은 꾸밈 잡기가 있었다" + (h.Paid ? "(돈 낸 것)" : "(돈 내기 전)")); break; }
        }

        private void BuildAnswered(RoomSeat seat, GameSession session, RoomRequestSaveData request, bool applied)
        {
            if (request.Kind != RoomHouseBuild.Finish && request.Kind != RoomHouseBuild.Band) return;
            Result.Count("꾸밈 잡기 답");
            if (applied)
            {
                Result.Count("꾸밈 잡힘");
                RegisterHold(seat, session, request);
                return;
            }
            // 거절: 방에 잡기가 생기지 않았다. 남이 잡은 칸과 겹쳤는지(먼저 온 것만 됨) 센다.
            _build.Remove(request.RequestId);
            Result.Count("먼저 온 것만 됨: 꾸밈");
            foreach (BuildHold h in _build.Values)
                if (h.Seat != seat && !h.Gone && _serverBuildHolds.Contains(h.Id)) { Result.Count("꾸밈 거절: 남이 잡아 둔 것이 있을 때"); break; }
        }

        private BuildHold RegisterHold(RoomSeat seat, GameSession session, RoomRequestSaveData r)
        {
            if (_build.TryGetValue(r.RequestId, out BuildHold known)) return known;
            var h = new BuildHold
            {
                Seat = seat, Id = r.RequestId, Kind = r.Kind, Created = session, Amount = r.Amount,
                Surface = r.Ids != null && r.Ids.Length > 0 ? r.Ids[0] : string.Empty,
                FinishId = r.Ids != null && r.Ids.Length > 1 ? r.Ids[1] : string.Empty,
            };
            if (r.Kind == RoomHouseBuild.Finish && r.Ids != null && r.Ids.Length == 5)
            {
                h.Tool = r.Ids[2];
                int.TryParse(r.Ids[3], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out h.X);
                int.TryParse(r.Ids[4], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out h.Y);
            }
            var def = FinishOf(h.FinishId);
            h.Cost = def == null ? 0 : Money(HouseFinishes.CostFor(def, h.IsBand ? 1 : h.Amount));
            _build[h.Id] = h;
            Result.Count("꾸밈 잡기 요청");
            Result.Count(h.IsBand ? "꾸밈 잡기 요청: 띠" : "꾸밈 잡기 요청: 칸");
            return h;
        }

        // ---------- 받기 한 번 ----------

        /// <summary>보낼 목록의 꾸밈 요청을 장부에 옮긴다(요청이 생기는 때와 쓰기·풀기가 적히는 때를 놓치지 않게 받기 앞뒤로 부른다).</summary>
        private void ScanBuild(RoomSeat seat, GameSession s)
        {
            var requests = s.Shares.Outbox.Requests;
            for (int i = 0; i < requests.Count; i++)
            {
                RoomRequestSaveData r = requests[i];
                if (r.Piece != RoomHouseBuild.PieceName) continue;
                if (r.Kind == RoomHouseBuild.Finish || r.Kind == RoomHouseBuild.Band) RegisterHold(seat, s, r);
                else if (r.Kind == RoomHouseBuild.Use && r.Ids != null && r.Ids.Length == 1) NoteUse(seat, s, r);
                else if (r.Kind == RoomHouseBuild.Release && r.Ids != null && r.Ids.Length == 1) NoteRelease(seat, s, r);
            }
        }

        private BuildHold HoldOf(RoomSeat seat, RoomRequestSaveData r)
        {
            string id = r.Ids[0];
            if (_build.TryGetValue(id, out BuildHold h)) return h;
            // 요청을 보지 못한 잡기(관찰 틈): 비용을 모른다 — 비용 검사에서 뺀다.
            h = new BuildHold { Seat = seat, Id = id, Kind = RoomHouseBuild.Finish, Unknown = true, Created = null };
            _build[id] = h;
            Result.Count("꾸밈: 요청을 보지 못한 잡기(관찰 틈)");
            return h;
        }

        private void NoteUse(RoomSeat seat, GameSession s, RoomRequestSaveData r)
        {
            BuildHold h = HoldOf(seat, r);
            if (h.Released) Fail("풀어 버린 꾸밈 잡기에 쓰기가 적혔다: " + h);
            if (!h.Paid)
            {
                h.Paid = true;
                h.LastUse = r.RequestId;
                Add(_buildPaid, seat, h.Cost);
                Result.Count("꾸밈 돈 냄(쓰기를 적음)");
                if (h.Created != null && h.Created != s) Result.Count("떠도는 잡기 정리: 다시 켠 판이 쓰기를 적음");
            }
            else if (h.LastUse != r.RequestId)
            {
                h.LastUse = r.RequestId;
                Result.Count("꾸밈 쓰기 다시 보냄(돈 낸 표시로 또는 거절 뒤)");
            }
        }

        private void NoteRelease(RoomSeat seat, GameSession s, RoomRequestSaveData r)
        {
            BuildHold h = HoldOf(seat, r);
            if (h.Paid) Fail("돈을 낸 꾸밈 잡기를 풀었다 — 낸 돈이 사라진다: " + h);
            if (h.Released) return;
            h.Released = true;
            Result.Count("꾸밈 풀기를 적음(돈 내기 전)");
            if (h.Created != null && h.Created != s) Result.Count("떠도는 잡기 정리: 다시 켠 판이 풀기를 적음");
        }

        private static void Add(Dictionary<RoomSeat, int> into, RoomSeat key, int by) => into[key] = (into.TryGetValue(key, out int n) ? n : 0) + by;

        private int PaidOf(RoomSeat seat) => _buildPaid.TryGetValue(seat, out int n) ? n : 0;

        /// <summary>
        /// 그 자리의 접속 일 한 번을 돈 낸 잡기의 비용과 함께 본다: 지갑을 나눠 쓰지 않으면 이 받기 동안 지갑이 줄어든 만큼이 새로 돈을 낸 잡기들의 비용과 같다
        /// (돈을 빼는 바꾸기와 쓰기를 적는 일이 한 묶음이다). 아니면 앞뒤 장부만 갱신한다.
        /// </summary>
        private void PumpBuild(RoomSeat seat, double now, GameSession s)
        {
            ScanBuild(seat, s);
            int paidBefore = PaidOf(seat), balanceBefore = s.Wallet.Balance, day = s.StudentDay;
            PumpChests(seat, now);
            if (seat.Session != s) return;
            ScanBuild(seat, s);
            if (s.SharesWallet || !seat.Connected || s.StudentDay != day) return;
            int paid = PaidOf(seat) - paidBefore, spent = balanceBefore - s.Wallet.Balance;
            if (spent != paid) Fail("꾸밈으로 나간 돈이 돈 낸 잡기의 비용과 다르다: " + seat + " 지갑 " + balanceBefore + " → " + s.Wallet.Balance + "(줄어든 " + spent + "), 새로 돈 낸 잡기 비용 " + paid);
            if (paid > 0) Result.Count("꾸밈 돈 보존: 지갑 줄어듦 = 비용");
        }

        // ---------- 방 쪽 ----------

        /// <summary>프레임마다(가볍게): 꾸밈 조각이 바뀌었을 때만 읽는다. 사라진 잡기는 쓰기·풀기 중 하나여야 하고, 기록은 쓰기 없이 바뀌지 않는다.</summary>
        private void BuildServerStep()
        {
            RoomHouseBuild piece = BuildPiece;
            if (piece == null) return;
            CountConcurrentBuilds();
            if (piece.Revision == _buildRevision) return;
            _buildRevision = piece.Revision;
            RoomPieceState state = piece.State();
            var holds = new List<KeyValuePair<string, string>>();
            if (!RoomHouseBuildLines.TryDecode(state.Ids, out HouseBuildSaveData data, holds)) { Fail("꾸밈 조각의 상태 줄을 읽을 수 없다"); return; }
            var now = new HashSet<string>(StringComparer.Ordinal);
            foreach (var h in holds) now.Add(h.Key);

            string surface = null;
            foreach (BuildHold h in _build.Values) if (!string.IsNullOrEmpty(h.Surface)) { surface = h.Surface; break; }
            HouseFinishes view = BuildView(data, surface);
            HouseFinishes prev = _buildPrev ?? (view == null ? null : BuildView(new HouseBuildSaveData { Version = HouseBuildSaveData.KnownVersion, FrameId = data.FrameId }, surface));

            foreach (string id in now) if (!_serverBuildHolds.Contains(id)) Result.Count("방이 꾸밈 칸을 잡아 줌");
            bool anyUsed = false;
            foreach (string id in _serverBuildHolds)
            {
                if (now.Contains(id)) continue;
                if (!_build.TryGetValue(id, out BuildHold h)) { Fail("방의 꾸밈 잡기를 아무도 요청한 적이 없다: " + id); continue; }
                h.Gone = true;
                if (h.Paid) { anyUsed = true; Landed(h, prev, view); }
                else if (h.Released) Result.Count("꾸밈 풀기가 방에 적용됨");
                else Fail("꾸밈 잡기가 쓰기도 풀기도 없이 방에서 사라졌다: " + h);
            }

            string signature = data.Band + ";" + string.Join(",", data.Finishes.Select(c => c.X + "," + c.Y + "," + c.Wall + "," + c.Floor));
            if (_buildSignature != null && signature != _buildSignature && !anyUsed)
                Fail("방의 꾸밈 기록이 돈 낸 쓰기 없이 바뀌었다(공짜 꾸밈이거나 풀기가 기록을 바꿨다): 전 " + _buildSignature + " 후 " + signature);
            _buildSignature = signature;
            _buildPrev = view;
            _serverBuildHolds.Clear();
            foreach (string id in now) _serverBuildHolds.Add(id);
        }

        // 돈을 낸 잡기가 방에서 지워졌다(쓰기 적용): 낸 칸 수와 방이 실제로 바꾼 칸 수가 같고, 방의 기록에 그 꾸밈이 있다.
        private void Landed(BuildHold h, HouseFinishes before, HouseFinishes after)
        {
            h.Applied = true;
            Add(_buildApplied, h.Seat, h.Cost);
            Result.Count("꾸밈 쓰기가 방에 적용됨(돈 낸 것)");
            if (h.Unknown || before == null || after == null) return;
            var def = FinishOf(h.FinishId);
            if (def == null) return;
            if (h.IsBand)
            {
                if (after.BandColors != def.Art) Fail("돈 낸 띠 꾸밈이 방의 기록에 없다: " + h + " — 보이는 띠 " + after.BandColors + ", 꾸밈의 그림 " + def.Art);
                return;
            }
            var preview = before.Preview(h.FinishId, h.Tool, new CellPos(h.X, h.Y), _ => true, _ => true);
            if (preview.ChangedCells.Count != h.Amount)
                Fail("돈 낸 칸 수와 방이 바꾼 칸 수가 다르다: " + h + " — 낸 칸 " + h.Amount + ", 방이 바꾼 칸 " + preview.ChangedCells.Count);
            bool wall = string.Equals(def.Slot, RoomFinishSlots.Wall, StringComparison.Ordinal);
            foreach (CellPos c in preview.ChangedCells)
            {
                string seen = wall ? after.WallAt(c) : after.FloorAt(c);
                if (seen != h.FinishId) Fail("돈 낸 꾸밈이 방의 기록에 없다: " + h + " 칸 " + c.X + "," + c.Y + " 보이는 것 " + seen);
            }
        }

        private void CountConcurrentBuilds()
        {
            int mask = 0;
            foreach (BuildHold h in _build.Values) if (!h.Gone) mask |= 1 << h.Seat.Index;
            bool two = (mask & (mask - 1)) != 0;
            if (two && !_buildConcurrent) Result.Count("꾸밈 동시 요청: 두 사람 이상이 한꺼번에 잡기를 가짐");
            _buildConcurrent = two;
        }

        private void BuildSevered(RoomSeat seat, string kind)
        {
            bool paid = false, held = false, requested = false;
            foreach (BuildHold h in _build.Values)
            {
                if (h.Seat != seat || h.Gone) continue;
                if (h.Paid) paid = true;
                else if (_serverBuildHolds.Contains(h.Id)) held = true;
                else requested = true;
            }
            if (paid) Result.Count("끊김: 꾸밈 돈 낸 뒤 쓰기가 방에 닿기 전");
            else if (held) Result.Count("끊김: 꾸밈 잡기 허락 뒤(돈 내기 전)");
            else if (requested) Result.Count("끊김: 꾸밈 잡기 요청 직후");
        }

        private bool OwnBuildHold(RoomSeat seat)
        {
            foreach (BuildHold h in _build.Values) if (h.Seat == seat && !h.Gone && _serverBuildHolds.Contains(h.Id)) return true;
            return false;
        }

        private int BuildHeld => BuildPiece?.HeldCount ?? 0;

        private void BuildAfterRestart() => _buildRevision = -1;

        // ---------- 조용할 때 ----------

        /// <summary>
        /// 조용하고 가라앉았을 때: 걸린 것이 없는데 방에 없는 요청은 끊김으로 사라진 것이라 장부에서 지운다. 모든 자리가 보는 꾸밈이 방의 기록과 같다.
        /// 낸 돈 = 적용된 돈(자리마다).
        /// </summary>
        private void CheckBuild()
        {
            RoomHouseBuild piece = BuildPiece;
            if (piece == null) return;
            foreach (string id in _build.Where(p => !p.Value.Gone && !p.Value.Paid && !_serverBuildHolds.Contains(p.Key)).Select(p => p.Key).ToList())
            {
                _build.Remove(id);
                Result.Count("꾸밈 요청이 방에 닿기 전에 사라짐(끊김 — 돈은 내지 않았다)");
            }
            foreach (RoomSeat seat in _play.Seats)
                if (PaidOf(seat) != (_buildApplied.TryGetValue(seat, out int a) ? a : 0))
                    Fail("자리가 꾸밈으로 낸 돈과 방에 적용된 돈이 다르다: " + seat + " 낸 " + PaidOf(seat) + ", 적용된 " + a);
            HouseBuildSaveData data = piece.ToSaveData();
            foreach (RoomSeat seat in _play.Seats)
            {
                if (!seat.Playing) continue;
                GameSession s = seat.Session;
                if (!s.SharesHouseBuild || s.House == null || seat.Body.HouseFrame == null) continue;
                HouseFinishes reference = BuildView(data, s.House.SurfaceId);
                if (reference == null) continue;
                if (s.HouseBandColors() != reference.BandColors)
                    Fail("모두가 보는 띠 색이 다르다: " + seat + " " + s.HouseBandColors() + ", 방의 기록 " + reference.BandColors);
                foreach (var cell in reference.Frame.Tiles.Cells)
                {
                    CellPos c = cell.Cell;
                    if (s.HouseWallAt(c) != reference.WallAt(c)) Fail("모두가 보는 벽지가 다르다: " + seat + " 칸 " + c.X + "," + c.Y + " — 본 것 '" + s.HouseWallAt(c) + "', 방의 기록 '" + reference.WallAt(c) + "'");
                    if (s.HouseFloorAt(c) != reference.FloorAt(c)) Fail("모두가 보는 바닥이 다르다: " + seat + " 칸 " + c.X + "," + c.Y + " — 본 것 '" + s.HouseFloorAt(c) + "', 방의 기록 '" + reference.FloorAt(c) + "'");
                }
                Result.Count("꾸밈 비추기 비교");
            }
        }

        /// <summary>정책의 꾸미기 누름 하나 뒤(진단): 화면이 적용·기다림·거절 중 무엇이라 보였고 거절이면 미리 보기의 이유. 불변식이 아니라 넓이를 보는 숫자다.</summary>
        public void AfterAction(RoomSeat seat, PlayAction action)
        {
            if (action == null || (action.Category != "finish-cell" && action.Category != "finish-cell-blocked" && !(action.Category == "row" && action.Element == Rootborn.Screens.Furniture.FurniturePlacementScreen.FinishBandListId))) return;
            if (!(seat.Body.Screens.Find(Rootborn.Screens.ScreenCatalog.FurniturePlacement) is Rootborn.Screens.Furniture.FurniturePlacementScreen p) || !p.IsOpen) return;
            string what = p.FinishLastAction;
            string reason = what == "refused" && action.Category.StartsWith("finish-cell", StringComparison.Ordinal) ? p.FinishPreview?.ReasonKey : null;
            Result.Count("꾸밈 누름(" + (action.Category.StartsWith("finish-cell", StringComparison.Ordinal) ? "칸" : "띠") + "): " + what + (reason == null ? string.Empty : " — " + reason));
        }

        // ---------- 파일 ----------

        private SaveFileSet SavedFilesOf(RoomSeat seat)
        {
            SaveFileSet files = _play.Guests.Read(seat.PlayerId);
            files.Overlay(seat.Uploads.Merged());
            return files;
        }

        /// <summary>손님의 자기 house-build.json은 같이 쓰는 집에서 바뀌지 않는다(쓰이지 않는다).</summary>
        private void CheckOwnBuildFile(RoomSeat seat)
        {
            foreach (var pair in SavedFilesOf(seat).Writes)
                if (pair.Key == SaveFileNames.HouseBuild || pair.Key.EndsWith("/" + SaveFileNames.HouseBuild, StringComparison.Ordinal)
                    || pair.Key.EndsWith("\\" + SaveFileNames.HouseBuild, StringComparison.Ordinal))
                    Fail("손님의 자기 house-build.json이 같이 쓰는 집에서 쓰였다: " + seat + " (" + pair.Key + ")");
        }

        private int PaidMarks(RoomSeat seat)
        {
            var slot = new MemorySaveSlot(RoomGuestLink.Slot, SavedFilesOf(seat).Writes);
            return slot.TryRead(RoomGuestLink.Slot, SaveFileNames.RoomHouseBuildPaid(seat.PlayerId), out RoomHouseBuildPaidSaveData data) && data.Holds != null ? data.Holds.Length : 0;
        }

        /// <summary>서버를 끄기 전: 자기 파일 검사.</summary>
        private void BuildBeforeRestart()
        {
            if (BuildPiece == null) return;
            foreach (RoomSeat seat in _play.Seats) CheckOwnBuildFile(seat);
        }

        /// <summary>
        /// 끝난 뒤(모두 다시 들어와 가만히 있었다): 방에 꾸밈 잡기가 남지 않았고, 돈 낸 표시가 0이고, 모두가 같은 것을 보고, 낸 돈 = 적용된 돈이다.
        /// </summary>
        public void BuildFinal()
        {
            RoomHouseBuild piece = BuildPiece;
            if (piece == null) return;
            if (piece.HeldCount > 0) Fail("끝난 뒤 모두 다시 들어와 가만히 있었는데 방에 꾸밈 잡기가 남았다(영영 막힌 칸): " + string.Join("; ", _build.Values.Where(h => !h.Gone)));
            foreach (BuildHold h in _build.Values.Where(h => !h.Gone && (h.Paid || _serverBuildHolds.Contains(h.Id)))) Fail("끝난 뒤에도 장부의 꾸밈 잡기가 끝나지 않았다: " + h);
            CheckBuild();
            foreach (RoomSeat seat in _play.Seats)
            {
                CheckOwnBuildFile(seat);
                int marks = PaidMarks(seat);
                if (marks > 0) Fail("끝난 뒤에도 돈 낸 꾸밈 표시가 남았다: " + seat + " " + marks + "개 — 방에는 잡기가 없는데 저장에 표시가 남는다");
            }
            Result.Count("끝난 뒤 가만히: 꾸밈 잡기 0, 돈 낸 표시 0");
        }
    }
}
