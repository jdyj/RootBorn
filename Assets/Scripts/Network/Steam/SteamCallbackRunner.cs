using Steamworks;
using UnityEngine;

namespace Rootborn.Network.Steam
{
    /// <summary>Pumps Steam callbacks every frame and closes Steam when the game quits.</summary>
    [DisallowMultipleComponent]
    public sealed class SteamCallbackRunner : MonoBehaviour
    {
        private void Update()
        {
            if (SteamPlatform.IsReady) SteamAPI.RunCallbacks();
        }

        private void OnApplicationQuit() => SteamPlatform.Shutdown();
    }
}
