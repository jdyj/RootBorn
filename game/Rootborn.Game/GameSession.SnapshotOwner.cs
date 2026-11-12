using Rootborn.Core.Careers;

namespace Rootborn.Game
{
    public sealed partial class GameSession
    {
        // 세이브의 장비 선택. MonoGame 판은 장비를 바꾸지 않지만 다음 저장이 지우지 않게 그대로 쓴다(Unity 판은 가방과 같은 스냅샷에 두었다).
        private string _equippedToolItemId = string.Empty;

        /// <summary>
        /// 이 플레이어의 스냅샷으로 이어 갈 수 없는 이유. 없으면 null.
        /// 다른 플레이어의 id가 든 기록(학생·일과·진로 후보·성인·월드·체인·가구 해금)을 거절한다
        /// (Unity AdultCareerSnapshotPersistence.IsValid·Restore). 그대로 읽으면 다른 플레이어의 진행을 가져온다.
        /// 빈 id는 엔진이 채운 빈 껍데기라 이 플레이어로 읽는다.
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

            // 데이터에 없는 아이템·장비는 거절하지 않는다. 가방(Core ItemInventory)이 보이지 않게 들고 있다가 그대로 다시 쓰고,
            // 장비 선택은 id 그대로 다시 쓴다. 전에는 슬롯 전체를 거절해, 데이터에서 아이템 하나를 빼면 그 세이브가 열리지 않았다.
            return null;
        }

        private bool IsForeign(string playerId) => !string.IsNullOrEmpty(playerId) && playerId != PlayerId;
    }
}
