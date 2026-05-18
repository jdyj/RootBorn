namespace Rootborn.Game.StudentLife
{
    public abstract class CampaignRewardBase : StudentLifeDefinitionBase
    {
        public virtual bool CanApply(StudentLifeProgress progress) => true;
        public abstract void Apply(StudentLifeProgress progress);
    }
}
