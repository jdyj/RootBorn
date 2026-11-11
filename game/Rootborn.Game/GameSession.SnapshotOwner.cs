using Rootborn.Core.Careers;

namespace Rootborn.Game
{
    public sealed partial class GameSession
    {
        // 세이브의 장비 선택. MonoGame 판은 장비를 바꾸지 않지만 다음 저장이 지우지 않게 그대로 쓴다(Unity 판은 가방과 같은 스냅샷에 두었다).
        private string _equippedToolItemId = string.Empty;

        /// <summary>
        /// 이 플레이어의 스냅샷으로 이어 갈 수 없는 이유. 없으면 null.
        /// 다른 플레이어의 id가 든 기록(학생·일과·진로 후보·성인·월드·체인·가구 해금)과 데이터에 없는 아이템(가방·장비)을 거절한다
        /// (Unity AdultCareerSnapshotPersistence.IsValid·Restore). 그대로 읽으면 다른 플레이어의 진행을 가져오거나, 아이템을 빼고 읽은 뒤
        /// 다음 저장이 그 아이템을 지운다. 빈 id는 엔진이 채운 빈 껍데기라 이 플레이어로 읽는다.
        /// 슬롯 이름은 보지 않는다: 복사한 슬롯은 지금 슬롯으로 읽는다(Rebind).
        /// </summary>
        private string SnapshotRefusal(AdultCareerSnapshotSaveData snapshot)
        {
            if (snapshot == null) return null;
            if (IsForeign(snapshot.Adult?.PlayerId)) return "성인 진행이 다른 플레이어 것이다";
            if (IsForeign(snapshot.Student?.PlayerId)) return "학생 진행이 다른 플레이어 것이다";
            if (IsForeign(snapshot.Student?.DailyPlan?.PlayerId)) return "일과가 다른 플레이어 것이다";
            if (IsForeign(snapshot.Student?.CareerCandidates?.PlayerId)) return "진로 후보가 다른 플레이어 것이다";
            if (IsForeign(snapshot.World?.PlayerId)) return "월드 상태가 다른 플레이어 것이다";
            if (IsForeign(snapshot.FurnitureUnlocks?.PlayerId)) return "가구 해금이 다른 플레이어 것이다";
            var chains = snapshot.QuestChains?.Chains;
            if (chains != null)
                foreach (var chain in chains)
                    if (chain != null && IsForeign(chain.PlayerId)) return "퀘스트 체인 기록이 다른 플레이어 것이다: " + chain.ChainId;

            var items = snapshot.Inventory?.Items;
            if (items != null)
                foreach (var item in items)
                    if (!string.IsNullOrEmpty(item.ItemId) && FindItem(item.ItemId) == null) return "데이터에 없는 아이템: " + item.ItemId;
            string equipped = snapshot.Inventory?.EquippedToolItemId;
            if (!string.IsNullOrEmpty(equipped) && FindItem(equipped) == null) return "데이터에 없는 장비: " + equipped;
            return null;
        }

        private bool IsForeign(string playerId) => !string.IsNullOrEmpty(playerId) && playerId != PlayerId;
    }
}
