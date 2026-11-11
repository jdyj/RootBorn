using Rootborn.Core.Careers;
using Rootborn.Core.DiscoveryClues;
using Rootborn.Core.Encyclopedia;
using Rootborn.Core.Gathering;
using Rootborn.Core.StudentLife;
using Rootborn.Core.WorldState;

namespace Rootborn.Game
{
    /// <summary>
    /// 복사한 슬롯: 세이브 안의 슬롯 이름이 지금 슬롯과 달라도(슬롯 폴더를 다른 이름으로 복사했거나, 날마다 남긴 사본을 다른 슬롯 폴더에 넣었을 때)
    /// 지금 슬롯·플레이어로 읽는다. 진행 객체는 자기 슬롯 이름으로 저장하므로 옛 이름을 지니면 첫 저장이 옛 슬롯으로 간다.
    /// 읽은 세이브 데이터의 이름을 고치고, Core가 읽어 만든 진행 객체는 이름이 다를 때만 세이브 데이터로 돌려 다시 만든다.
    /// </summary>
    public sealed partial class GameSession
    {
        private bool IsCurrent(string saveSlot, string playerId) => saveSlot == SaveSlot && playerId == PlayerId;

        private AdultCareerProgressSaveData Rebind(AdultCareerProgressSaveData data)
        {
            data.SaveSlot = SaveSlot;
            data.PlayerId = PlayerId;
            return data;
        }

        private WorldStateProgressSaveData Rebind(WorldStateProgressSaveData data)
        {
            data.SaveSlot = SaveSlot;
            data.PlayerId = PlayerId;
            return data;
        }

        private CampaignProgressSaveData Rebind(CampaignProgressSaveData data)
        {
            data.SaveSlot = SaveSlot;
            data.PlayerId = PlayerId;
            return data;
        }

        private MilestoneProgressSaveData Rebind(MilestoneProgressSaveData data)
        {
            data.SaveSlot = SaveSlot;
            data.PlayerId = PlayerId;
            return data;
        }

        private CareerInterestProgressSaveData Rebind(CareerInterestProgressSaveData data)
        {
            data.SaveSlot = SaveSlot;
            data.PlayerId = PlayerId;
            return data;
        }

        private LocationStateProgressSaveData Rebind(LocationStateProgressSaveData data)
        {
            data.SaveSlot = SaveSlot;
            data.PlayerId = PlayerId;
            return data;
        }

        private WorldStateProgress Rebind(WorldStateProgress progress)
            => IsCurrent(progress.SaveSlot, progress.PlayerId) ? progress : WorldStateProgress.FromSaveData(Rebind(progress.ToSaveData()), SaveSlot, PlayerId);

        private ExplorationProgress Rebind(ExplorationProgress progress)
        {
            if (IsCurrent(progress.SaveSlot, progress.PlayerId)) return progress;
            var data = progress.ToSaveData();
            data.SaveSlot = SaveSlot;
            data.PlayerId = PlayerId;
            return ExplorationProgress.FromSaveData(data);
        }

        private DiscoveryClueProgress Rebind(DiscoveryClueProgress progress)
        {
            if (IsCurrent(progress.SaveSlot, progress.PlayerId)) return progress;
            var data = progress.ToSaveData();
            data.SaveSlot = SaveSlot;
            data.PlayerId = PlayerId;
            return DiscoveryClueProgress.FromSaveData(data);
        }

        private ClueInterpretationProgress Rebind(ClueInterpretationProgress progress)
        {
            if (IsCurrent(progress.SaveSlot, progress.PlayerId)) return progress;
            var data = progress.ToSaveData();
            data.SaveSlot = SaveSlot;
            data.PlayerId = PlayerId;
            return ClueInterpretationProgress.FromSaveData(data);
        }

        private EncyclopediaProgress Rebind(EncyclopediaProgress progress)
        {
            if (IsCurrent(progress.SaveSlot, progress.PlayerId)) return progress;
            var data = progress.ToSaveData();
            data.SaveSlot = SaveSlot;
            data.PlayerId = PlayerId;
            return EncyclopediaProgress.FromSaveData(data);
        }

        private ResourceRecipeProgress Rebind(ResourceRecipeProgress progress)
        {
            if (IsCurrent(progress.SaveSlot, progress.PlayerId)) return progress;
            var data = progress.ToSaveData();
            data.SaveSlot = SaveSlot;
            data.PlayerId = PlayerId;
            return ResourceRecipeProgress.FromSaveData(data, SaveSlot, PlayerId);
        }

        private Rootborn.Core.Placement.FurnitureUnlockProgress Rebind(Rootborn.Core.Placement.FurnitureUnlockProgress progress)
        {
            if (IsCurrent(progress.SaveSlot, progress.PlayerId)) return progress;
            var data = progress.ToSaveData();
            data.SaveSlot = SaveSlot;
            data.PlayerId = PlayerId;
            return Rootborn.Core.Placement.FurnitureUnlockProgress.FromSaveData(data, SaveSlot, PlayerId);
        }
    }
}
