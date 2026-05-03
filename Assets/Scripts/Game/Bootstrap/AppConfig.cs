namespace Rootborn.Game.Bootstrap
{
    public enum SessionMode
    {
        None,
        Single,
        Host,
        Client,
        Server
    }

    public sealed class AppConfig
    {
        public SessionMode Mode { get; set; } = SessionMode.None;
        public ushort Port { get; set; } = 7777;
        public int MaxPlayers { get; set; } = 4;
        public string SaveSlot { get; set; } = "default";
        public string JoinIp { get; set; } = "127.0.0.1";
    }
}
