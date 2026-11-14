using Rootborn.Core.Building;
using Rootborn.Core.Save;

namespace Rootborn.Game
{
    /// <summary>
    /// 짓는 집의 꾸밈 기록(방마다 벽지·바닥). 기록은 슬롯 공용 파일 house-build.json(<see cref="SaveFileNames.HouseBuild"/>)이고,
    /// 시작할 때 읽고 꾸밈이 바뀐 저장 묶음에서만 쓴다(읽기만으로 쓰지 않는다).
    /// </summary>
    public sealed partial class GameSession
    {
        private readonly HouseFinishes _houseFinishes = new HouseFinishes();
        private bool _houseBuildDirty;

        private void LoadHouseBuild(ISaveStore store)
        {
            store.TryRead<HouseBuildSaveData>(SaveSlot, SaveFileNames.HouseBuild, out var data);
            _houseFinishes.Load(data);
        }

        // 저장 묶음 안에서: 꾸밈이 바뀌었으면(붙었든 아니든) 기록 전체를 같은 묶음으로 쓴다. 읽은 기록(모르는 id, 지금 바닥이 아닌 칸)도 그대로 든다.
        private void SaveHouseBuildIfDirty(ISaveStore batch)
        {
            if (!_houseBuildDirty) return;
            batch.Write(SaveSlot, SaveFileNames.HouseBuild, _houseFinishes.ToSaveData());
        }
    }
}
