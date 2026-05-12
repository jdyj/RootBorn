using System;
using System.Collections.Generic;
using System.IO;
using Rootborn.Game.Bootstrap;
using Unity.Netcode;
using UnityEngine;

namespace Rootborn.Network.Time
{
    public enum WorldTimeOfDay
    {
        Morning = 0,
        Afternoon = 1,
        Evening = 2,
        Night = 3
    }

    public enum WorldSchedulePhase
    {
        DayStart = 0,
        Activity = 1,
        DayEndReady = 2
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkWorldTimeState : NetworkBehaviour
    {
        private readonly NetworkVariable<int> _currentDay = new NetworkVariable<int>(1);
        private readonly NetworkVariable<int> _timeOfDay = new NetworkVariable<int>((int)WorldTimeOfDay.Morning);
        private readonly NetworkVariable<int> _schedulePhase = new NetworkVariable<int>((int)WorldSchedulePhase.DayStart);
        private readonly HashSet<ulong> _dayEndReadyClients = new HashSet<ulong>();

        public static NetworkWorldTimeState Active { get; private set; }

        public int CurrentDay => Mathf.Max(1, _currentDay.Value);
        public int WeekdayIndex => (CurrentDay - 1) % 7;
        public string WeekdayName => WeekdayIndex switch
        {
            0 => "Mon",
            1 => "Tue",
            2 => "Wed",
            3 => "Thu",
            4 => "Fri",
            5 => "Sat",
            _ => "Sun"
        };
        public WorldTimeOfDay TimeOfDay => (WorldTimeOfDay)Mathf.Clamp(_timeOfDay.Value, 0, 3);
        public WorldSchedulePhase SchedulePhase => (WorldSchedulePhase)Mathf.Clamp(_schedulePhase.Value, 0, 2);

        public string SnapshotText => $"day={CurrentDay} weekday={WeekdayName} timeOfDay={TimeOfDay} schedulePhase={SchedulePhase}";

        public static bool TryRequestDayEndReady()
        {
            if (Active == null || !Active.IsSpawned)
            {
                Debug.LogWarning("[ROOTBORN] World time day-end ready request ignored because no active network world time exists.");
                return false;
            }

            Active.RequestDayEndReadyServerRpc();
            return true;
        }

        public override void OnNetworkSpawn()
        {
            if (Active == null || OwnerClientId == 0UL)
            {
                Active = this;
            }

            _currentDay.OnValueChanged += HandleDayChanged;
            _timeOfDay.OnValueChanged += HandleTimeChanged;
            _schedulePhase.OnValueChanged += HandleSchedulePhaseChanged;

            if (IsServer && Active == this)
            {
                LoadServerSnapshot();
            }

            Debug.Log($"[ROOTBORN] World time spawned owner={OwnerClientId} local={NetworkManager.Singleton?.LocalClientId} isOwner={IsOwner} isServer={IsServer} active={Active == this} {SnapshotText}");
        }

        public override void OnNetworkDespawn()
        {
            _currentDay.OnValueChanged -= HandleDayChanged;
            _timeOfDay.OnValueChanged -= HandleTimeChanged;
            _schedulePhase.OnValueChanged -= HandleSchedulePhaseChanged;
            if (Active == this)
            {
                Active = null;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestDayEndReadyServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!IsAuthoritativeServerInstance())
            {
                return;
            }

            ulong clientId = rpcParams.Receive.SenderClientId;
            _dayEndReadyClients.Add(clientId);
            _schedulePhase.Value = (int)WorldSchedulePhase.DayEndReady;

            int connectedClients = NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClientsIds.Count : 1;
            Debug.Log($"[ROOTBORN] World time day-end ready client={clientId} ready={_dayEndReadyClients.Count}/{connectedClients} {SnapshotText}");

            if (_dayEndReadyClients.Count >= connectedClients)
            {
                AdvanceToNextDay();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void AdvanceTimeOfDayServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!IsAuthoritativeServerInstance())
            {
                return;
            }

            int next = _timeOfDay.Value + 1;
            if (next > (int)WorldTimeOfDay.Night)
            {
                AdvanceToNextDay();
                return;
            }

            _timeOfDay.Value = next;
            _schedulePhase.Value = (int)WorldSchedulePhase.Activity;
            SaveServerSnapshot();
            Debug.Log($"[ROOTBORN] World time advanced by server requester={rpcParams.Receive.SenderClientId} {SnapshotText}");
        }

        private bool IsAuthoritativeServerInstance()
        {
            return IsServer && Active == this;
        }

        private void AdvanceToNextDay()
        {
            _currentDay.Value = Mathf.Max(1, _currentDay.Value + 1);
            _timeOfDay.Value = (int)WorldTimeOfDay.Morning;
            _schedulePhase.Value = (int)WorldSchedulePhase.DayStart;
            _dayEndReadyClients.Clear();
            SaveServerSnapshot();
            Debug.Log($"[ROOTBORN] World time next day confirmed by server {SnapshotText}");
        }

        private void HandleDayChanged(int previous, int current)
        {
            Debug.Log($"[ROOTBORN] World time day sync previous={previous} current={current} {SnapshotText}");
        }

        private void HandleTimeChanged(int previous, int current)
        {
            Debug.Log($"[ROOTBORN] World time timeOfDay sync previous={(WorldTimeOfDay)previous} current={(WorldTimeOfDay)current} {SnapshotText}");
        }

        private void HandleSchedulePhaseChanged(int previous, int current)
        {
            Debug.Log($"[ROOTBORN] World time schedule sync previous={(WorldSchedulePhase)previous} current={(WorldSchedulePhase)current} {SnapshotText}");
        }

        private void LoadServerSnapshot()
        {
            string path = SavePath();
            if (!File.Exists(path))
            {
                SaveServerSnapshot();
                return;
            }

            try
            {
                var data = JsonUtility.FromJson<WorldTimeSaveData>(File.ReadAllText(path));
                if (data != null)
                {
                    _currentDay.Value = Mathf.Max(1, data.CurrentDay);
                    _timeOfDay.Value = Mathf.Clamp(data.TimeOfDay, 0, 3);
                    _schedulePhase.Value = Mathf.Clamp(data.SchedulePhase, 0, 2);
                    Debug.Log($"[ROOTBORN] World time restored from saveSlot={SaveSlot()} {SnapshotText}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ROOTBORN] Failed to restore world time saveSlot={SaveSlot()}: {ex.Message}");
            }
        }

        private void SaveServerSnapshot()
        {
            try
            {
                Directory.CreateDirectory(SaveDirectory());
                var data = new WorldTimeSaveData
                {
                    CurrentDay = CurrentDay,
                    TimeOfDay = (int)TimeOfDay,
                    SchedulePhase = (int)SchedulePhase
                };
                File.WriteAllText(SavePath(), JsonUtility.ToJson(data));
                Debug.Log($"[ROOTBORN] World time saved saveSlot={SaveSlot()} {SnapshotText}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ROOTBORN] Failed to save world time saveSlot={SaveSlot()}: {ex.Message}");
            }
        }

        private static string SaveSlot()
        {
            var config = GameBootstrap.Config;
            return config != null && !string.IsNullOrEmpty(config.SaveSlot) ? config.SaveSlot : "default";
        }

        private static string SaveDirectory()
        {
            return Path.Combine(Application.persistentDataPath, "world-time");
        }

        private static string SavePath()
        {
            string safeSlot = SaveSlot().Replace(Path.DirectorySeparatorChar, '_').Replace(Path.AltDirectorySeparatorChar, '_');
            return Path.Combine(SaveDirectory(), safeSlot + ".json");
        }

        [Serializable]
        private sealed class WorldTimeSaveData
        {
            public int CurrentDay = 1;
            public int TimeOfDay;
            public int SchedulePhase;
        }
    }
}
