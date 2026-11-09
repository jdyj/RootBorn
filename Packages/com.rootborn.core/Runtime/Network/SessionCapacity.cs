namespace Rootborn.Core.Network
{
    /// <summary>Approval decision for a joining client against the session's player limit (host included in the count).</summary>
    public readonly struct SessionCapacityDecision
    {
        public SessionCapacityDecision(bool approved, string reason)
        {
            Approved = approved;
            Reason = reason ?? string.Empty;
        }

        public bool Approved { get; }
        public string Reason { get; }
    }

    public static class SessionCapacity
    {
        public const string ServerFullReason = "server full";
        public const string InvalidLimitReason = "invalid player limit";

        /// <summary>True when a refused client's disconnect reason says the session was full.</summary>
        public static bool IsServerFull(string disconnectReason) => disconnectReason == ServerFullReason;

        /// <param name="connectedClients">Clients already connected, the host's own client included.</param>
        public static SessionCapacityDecision Decide(int connectedClients, int maxPlayers)
        {
            if (maxPlayers <= 0) return new SessionCapacityDecision(false, InvalidLimitReason);
            return connectedClients < maxPlayers
                ? new SessionCapacityDecision(true, string.Empty)
                : new SessionCapacityDecision(false, ServerFullReason);
        }
    }
}
