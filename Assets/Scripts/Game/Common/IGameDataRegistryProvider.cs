namespace Rootborn.Game.Common
{
    public interface IGameDataRegistryProvider
    {
        bool TryGet(out GameDataRegistry registry);
        GameDataRegistry Require();
    }
}
