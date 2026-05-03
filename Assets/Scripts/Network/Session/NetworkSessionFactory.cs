using Rootborn.Game.Bootstrap;

namespace Rootborn.Network.Session
{
    public static class NetworkSessionFactory
    {
        public static INetworkSession Create(SessionMode mode)
        {
            return mode switch
            {
                SessionMode.Host => new HostSession(),
                SessionMode.Client => new ClientSession(),
                SessionMode.Server => new DedicatedServerSession(),
                _ => new SinglePlayerSession()
            };
        }
    }
}
