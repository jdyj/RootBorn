using Rootborn.Core.Network;
using Unity.Netcode;

namespace Rootborn.Network.Session
{
    /// <summary>
    /// Netcode puts ConnectionApproval into the config hash, so server, host and client must all turn it on together;
    /// a mismatch makes the server reject every client. The limit decision lives in Rootborn.Core (SessionCapacity).
    /// </summary>
    public static class SessionApproval
    {
        /// <summary>Server or host: enforce the player limit (the host's own client counts toward it).</summary>
        public static void EnableLimit(NetworkManager nm, int maxPlayers)
        {
            if (nm == null) return;
            nm.NetworkConfig.ConnectionApproval = true;
            nm.ConnectionApprovalCallback = (request, response) =>
            {
                var decision = SessionCapacity.Decide(nm.ConnectedClientsIds.Count, maxPlayers);
                response.Approved = decision.Approved;
                // Same default Netcode uses without approval: spawn a player only when a prefab is configured.
                response.CreatePlayerObject = decision.Approved && nm.NetworkConfig.PlayerPrefab != null;
                response.Reason = decision.Reason;
            };
        }

        /// <summary>Client: match the server's config hash.</summary>
        public static void EnableForClient(NetworkManager nm)
        {
            if (nm != null) nm.NetworkConfig.ConnectionApproval = true;
        }

        public static void Disable(NetworkManager nm)
        {
            if (nm != null) nm.ConnectionApprovalCallback = null;
        }
    }
}
