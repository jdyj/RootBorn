using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Placement;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.Game.Careers
{
    public static class AdultCareerSnapshotPersistence
    {
        public static AdultCareerSnapshot Capture(AdultCareerProgress adult, StudentLifeProgress student, Inventory inventory, WorldStateProgress world)
        {
            if (adult == null || student == null || inventory == null || world == null) return null;
            var items = new List<PlayerInventoryItemSaveData>(inventory.Slots.Count);
            for (int i = 0; i < inventory.Slots.Count; i++)
            {
                var slot = inventory.Slots[i];
                if (slot.Item == null || slot.Count <= 0) continue;
                if (string.IsNullOrEmpty(slot.Item.Id)) return null;
                items.Add(new PlayerInventoryItemSaveData { ItemId = slot.Item.Id, Count = slot.Count });
            }
            var snapshot = new AdultCareerSnapshot { Adult = adult.ToSaveData(), Student = student.ToSaveData(), Inventory = new PlayerInventorySaveData { Items = items.ToArray() }, World = world.ToSaveData(), QuestChains = new QuestChainLogSaveData(), FurnitureUnlocks = new FurnitureUnlockProgress(adult.SaveSlot, adult.PlayerId).ToSaveData() };
            return IsValid(snapshot, adult.SaveSlot, adult.PlayerId, false) ? snapshot : null;
        }

        public static AdultCareerSnapshot CaptureSession(AdultCareerSession session)
        {
            if (session == null || session.FurnitureUnlocks == null) return null;
            var snapshot = Capture(session.Progress, session.Student, session.Inventory, session.World);
            if (snapshot == null) return null;
            snapshot.Inventory.EquippedToolItemId = session.EquippedToolItemId;
            snapshot.QuestChains = JsonUtility.FromJson<QuestChainLogSaveData>(JsonUtility.ToJson(session.QuestChains));
            snapshot.FurnitureUnlocks = session.FurnitureUnlocks.ToSaveData();
            return IsValid(snapshot, session.Progress.SaveSlot, session.Progress.PlayerId, false) ? snapshot : null;
        }

        public static AdultCareerSession Restore(AdultCareerSnapshot snapshot, string expectedSlot, string expectedPlayer, ItemDefinition[] registeredItems, LifeCalendarDefinition calendar)
        {
            if (!IsValid(snapshot, expectedSlot, expectedPlayer, false) || registeredItems == null || calendar == null) return null;
            var items = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            for (int i = 0; i < registeredItems.Length; i++)
            {
                var item = registeredItems[i];
                if (item == null || string.IsNullOrEmpty(item.Id)) continue;
                if (items.ContainsKey(item.Id)) return null;
                items.Add(item.Id, item);
            }
            var grants = new List<InventoryGrant>(snapshot.Inventory.Items.Length);
            for (int i = 0; i < snapshot.Inventory.Items.Length; i++)
            {
                var saved = snapshot.Inventory.Items[i];
                if (!items.TryGetValue(saved.ItemId, out var item)) return null;
                grants.Add(new InventoryGrant(item, saved.Count));
            }
            if (!string.IsNullOrEmpty(snapshot.Inventory.EquippedToolItemId) && !items.ContainsKey(snapshot.Inventory.EquippedToolItemId)) return null;
            var inventory = new Inventory();
            if (!inventory.TryAddAll(grants)) return null;
            var adult = AdultCareerProgress.FromSaveData(snapshot.Adult);
            var student = StudentLifeProgress.FromSaveData(snapshot.Student, null, null, null);
            student.ConfigureCalendar(calendar);
            var world = WorldStateProgress.FromSaveData(snapshot.World, expectedSlot, expectedPlayer);
            var furniture = FurnitureUnlockProgress.FromSaveData(snapshot.FurnitureUnlocks, expectedSlot, expectedPlayer);
            return new AdultCareerSession(adult, student, inventory, world, snapshot.Inventory.EquippedToolItemId, snapshot.QuestChains, furniture);
        }

        public static bool Save(SaveService storage, AdultCareerSnapshot snapshot)
        {
            return SaveCore(storage, snapshot, true);
        }

        public static bool SaveSession(SaveService storage, AdultCareerSnapshot snapshot)
        {
            return SaveCore(storage, snapshot, false);
        }

        private static bool SaveCore(SaveService storage, AdultCareerSnapshot snapshot, bool requireAdult)
        {
            if (storage == null || snapshot?.Adult == null || !IsValid(snapshot, storage.Slot, snapshot.Adult.PlayerId, requireAdult)) return false;
            string target = Path.Combine(storage.DirectoryPath, FileNameFor(snapshot.Adult.PlayerId));
            string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                string json = JsonUtility.ToJson(snapshot, true);
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true))
                    { writer.Write(json); writer.Flush(); }
                    stream.Flush(true);
                }
                if (File.Exists(target)) ReplaceWithTransientLockRetry(temporary, target);
                else File.Move(temporary, target);
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static void ReplaceWithTransientLockRetry(string temporary, string target)
        {
            // Keep atomic replacement: never delete the committed file to work around a reader.
            // Three retries wait at most 70 ms, only after Windows sharing/removal failures.
            for (int attempt = 0; ; attempt++)
            {
                try { File.Replace(temporary, target, null); return; }
                catch (IOException exception) when (attempt < 3 && IsTransientReplacementLock(exception))
                {
                    System.Threading.Thread.Sleep(10 << attempt);
                }
            }
        }

        private static bool IsTransientReplacementLock(IOException exception)
        {
            int error = exception.HResult & 0xffff;
            // 1175 retains both original names. Do not retry the partial-move errors 1176/1177.
            return error == 32 || error == 33 || error == 1175;
        }

        public static AdultCareerSnapshot Load(SaveService storage, string playerId)
        {
            if (storage == null || string.IsNullOrEmpty(playerId)) return null;
            string target = Path.Combine(storage.DirectoryPath, FileNameFor(playerId));
            try
            {
                if (!File.Exists(target)) return null;
                var snapshot = JsonUtility.FromJson<AdultCareerSnapshot>(File.ReadAllText(target, Encoding.UTF8));
                return IsValid(snapshot, storage.Slot, playerId, false) ? snapshot : null;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (ArgumentException) { return null; }
        }

        public static bool HasCommittedFile(SaveService storage, string playerId)
        {
            return storage != null && !string.IsNullOrEmpty(playerId) && File.Exists(Path.Combine(storage.DirectoryPath, FileNameFor(playerId)));
        }

        public static string FileNameFor(string playerId)
        {
            string value = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            var encoded = new StringBuilder(value.Length * 4);
            for (int i = 0; i < value.Length; i++) encoded.Append(((int)value[i]).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
            return "adult-career-snapshot-" + encoded + ".json";
        }

        private static bool IsValid(AdultCareerSnapshot snapshot, string slot, string player, bool requireAdult)
        {
            if (snapshot == null || (snapshot.Version != 1 && snapshot.Version != 2) || snapshot.Adult == null || snapshot.Student == null || snapshot.Inventory?.Items == null || snapshot.World == null || string.IsNullOrEmpty(slot) || string.IsNullOrEmpty(player)) return false;
            if (snapshot.Version == 1)
            {
                snapshot.QuestChains ??= new QuestChainLogSaveData();
                // Unity's inline-object serializer materializes absent version-1 domains
                // as empty objects rather than null. Only that empty legacy shell migrates.
                if (snapshot.FurnitureUnlocks == null ||
                    (string.IsNullOrEmpty(snapshot.FurnitureUnlocks.SaveSlot) &&
                     string.IsNullOrEmpty(snapshot.FurnitureUnlocks.PlayerId) &&
                     (snapshot.FurnitureUnlocks.UnlockTokens == null || snapshot.FurnitureUnlocks.UnlockTokens.Length == 0)))
                    snapshot.FurnitureUnlocks = new FurnitureUnlockProgressSaveData { SaveSlot = slot, PlayerId = player };
                snapshot.Version = 2;
            }
            if (snapshot.QuestChains?.Chains == null || snapshot.FurnitureUnlocks?.UnlockTokens == null) return false;
            if (snapshot.FurnitureUnlocks.SaveSlot != slot || snapshot.FurnitureUnlocks.PlayerId != player) return false;
            var chainIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < snapshot.QuestChains.Chains.Length; i++)
            {
                var chain = snapshot.QuestChains.Chains[i];
                if (chain == null || string.IsNullOrEmpty(chain.ChainId) || !chainIds.Add(chain.ChainId) || chain.SaveSlot != slot || chain.PlayerId != player || !Enum.TryParse(chain.ChainState, out QuestChainState _)) return false;
            }
            if (snapshot.Adult.Version != 1 || snapshot.Adult.SaveSlot != slot || snapshot.Student.SaveSlot != slot || snapshot.World.SaveSlot != slot || snapshot.Adult.PlayerId != player || snapshot.Student.PlayerId != player || snapshot.World.PlayerId != player) return false;
            bool adult = !string.IsNullOrEmpty(snapshot.Adult.FacilityId);
            if (requireAdult && !adult) return false;
            if ((adult && (snapshot.Adult.FacilityLevel < 1 || snapshot.Adult.FacilityLevel > 3)) || (!adult && snapshot.Adult.FacilityLevel != 0)) return false;
            if (snapshot.Student.CurrentDay < 1 || snapshot.Adult.Reputation < 0) return false;
            var diary = snapshot.Student.DailyPlan;
            if (!DailyPlanProgressSaveData.IsEmpty(diary)
                && (diary.SaveSlot != slot || diary.PlayerId != player || diary.CurrentDay != snapshot.Student.CurrentDay)) return false;
            var candidates = snapshot.Student.CareerCandidates;
            if (candidates != null && (!string.IsNullOrEmpty(candidates.SaveSlot) || !string.IsNullOrEmpty(candidates.PlayerId) || (candidates.Candidates != null && candidates.Candidates.Length > 0)))
                if (candidates.SaveSlot != slot || candidates.PlayerId != player) return false;
            var studentPending = snapshot.Student.PendingMinigame;
            if (!PendingStudentMinigameSaveData.IsEmpty(studentPending))
            {
                if (adult || !PendingFacilityWorkSaveData.IsEmpty(snapshot.Adult.PendingWork) || !Enum.IsDefined(typeof(StudentMinigameKind), studentPending.Kind)
                    || string.IsNullOrEmpty(studentPending.RequestId) || string.IsNullOrEmpty(studentPending.DefinitionId) || string.IsNullOrEmpty(studentPending.MinigameId)
                    || studentPending.Day != snapshot.Student.CurrentDay || studentPending.ReservedTimePoints <= 0 || snapshot.Student.TimePointsSpentToday < studentPending.ReservedTimePoints) return false;
                if (snapshot.Student.AppliedRequestIds != null && Array.IndexOf(snapshot.Student.AppliedRequestIds, studentPending.RequestId) >= 0) return false;
            }
            var pending = snapshot.Adult.PendingWork;
            if (!PendingFacilityWorkSaveData.IsEmpty(pending))
            {
                if (!adult || string.IsNullOrEmpty(pending.RequestId) || string.IsNullOrEmpty(pending.WorkId) || string.IsNullOrEmpty(pending.MinigameId)
                    || pending.Day != snapshot.Student.CurrentDay || pending.ReservedTimePoints <= 0 || snapshot.Student.TimePointsSpentToday < pending.ReservedTimePoints
                    || pending.FacilityLevel != snapshot.Adult.FacilityLevel || pending.RewardPercent < 0 || pending.RewardPercent > 10000) return false;
                if (snapshot.Adult.AppliedRequestIds != null && Array.IndexOf(snapshot.Adult.AppliedRequestIds, pending.RequestId) >= 0) return false;
            }
            for (int i = 0; i < snapshot.Inventory.Items.Length; i++) if (string.IsNullOrEmpty(snapshot.Inventory.Items[i].ItemId) || snapshot.Inventory.Items[i].Count <= 0) return false;
            return true;
        }
    }
}
