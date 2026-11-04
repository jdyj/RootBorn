using System;
using System.Collections.Generic;
using Rootborn.Game.Placement;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;

namespace Rootborn.Game.Careers
{
    // Domain persistence routes to the one live owner without a Game -> UI assembly dependency.
    public static class AdultCareerPersistenceRouter
    {
        private sealed class Entry
        {
            public object Owner;
            public bool Pinned;
            public Func<StudentLifeProgress, WorldStateProgress, FurnitureUnlockProgress, bool> Save;
            public Func<WorldStateProgress> World;
            public Func<FurnitureUnlockProgress> Furniture;
            public Func<StudentLifeProgress> Student;
        }
        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

        public static void Register(string slot, string player, object owner, Func<StudentLifeProgress, WorldStateProgress, FurnitureUnlockProgress, bool> save, Func<WorldStateProgress> world = null, Func<FurnitureUnlockProgress> furniture = null, Func<StudentLifeProgress> student = null)
        {
            if (string.IsNullOrEmpty(slot) || string.IsNullOrEmpty(player) || owner == null || save == null) return;
            if (Entries.TryGetValue(Key(slot, player), out var current) && current.Pinned && !ReferenceEquals(current.Owner, owner)
                && !(current.Owner is UnityEngine.Object unityOwner && unityOwner == null)) return;
            Entries[Key(slot, player)] = new Entry { Owner = owner, Save = save, World = world, Furniture = furniture, Student = student };
        }
        public static bool IsOwner(string slot, string player, object owner)
        {
            return owner != null && Entries.TryGetValue(Key(slot, player), out var entry) && ReferenceEquals(entry.Owner, owner);
        }
        public static bool TryPinOwner(string slot, string player, object owner)
        {
            if (owner == null || !Entries.TryGetValue(Key(slot, player), out var entry)
                || !ReferenceEquals(entry.Owner, owner) || entry.Pinned) return false;
            entry.Pinned = true;
            return true;
        }
        public static bool ReleaseOwnerPin(string slot, string player, object owner)
        {
            if (owner == null || !Entries.TryGetValue(Key(slot, player), out var entry)
                || !ReferenceEquals(entry.Owner, owner) || !entry.Pinned) return false;
            entry.Pinned = false;
            return true;
        }
        public static void Unregister(object owner)
        {
            if (owner == null) return;
            var remove = new List<string>();
            foreach (var pair in Entries) if (ReferenceEquals(pair.Value.Owner, owner)) remove.Add(pair.Key);
            for (int i = 0; i < remove.Count; i++) Entries.Remove(remove[i]);
        }
        public static bool TrySaveStudent(StudentLifeProgress progress, out bool saved)
        {
            saved = false;
            if (progress == null || !Entries.TryGetValue(Key(progress.SaveSlot, progress.PlayerId), out var entry)) return false;
            saved = entry.Save(progress, null, null); return true;
        }
        public static bool TrySaveWorld(WorldStateProgress progress, out bool saved)
        {
            saved = false;
            if (progress == null || !Entries.TryGetValue(Key(progress.SaveSlot, progress.PlayerId), out var entry)) return false;
            saved = entry.Save(null, progress, null); return true;
        }
        public static bool TrySaveFurniture(FurnitureUnlockProgress progress, out bool saved)
        {
            saved = false;
            if (progress == null || !Entries.TryGetValue(Key(progress.SaveSlot, progress.PlayerId), out var entry)) return false;
            saved = entry.Save(null, null, progress); return true;
        }
        public static bool TryGetWorld(string slot, string player, out WorldStateProgress progress)
        {
            progress = null;
            if (Entries.TryGetValue(Key(slot, player), out var entry)) progress = entry.World?.Invoke();
            return progress != null;
        }
        public static bool TryGetStudent(string slot, string player, out StudentLifeProgress progress)
        {
            progress = null;
            if (Entries.TryGetValue(Key(slot, player), out var entry)) progress = entry.Student?.Invoke();
            return progress != null;
        }
        public static bool TryGetFurniture(string slot, string player, out FurnitureUnlockProgress progress)
        {
            progress = null;
            if (Entries.TryGetValue(Key(slot, player), out var entry)) progress = entry.Furniture?.Invoke();
            return progress != null;
        }
        private static string Key(string slot, string player)
        {
            string normalized = slot ?? string.Empty;
            return normalized.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + normalized + (player ?? string.Empty);
        }
    }
}
