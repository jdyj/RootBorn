using System;
using Rootborn.Game.Common;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;

namespace Rootborn.Game.Careers
{
    public sealed class AdultCareerCommandCoordinator
    {
        private readonly AdultCareerService _service;
        private readonly ItemDefinition[] _items;
        private readonly LifeCalendarDefinition _calendar;
        private readonly SaveService _storage;
        private bool _executing;

        public AdultCareerCommandCoordinator(FacilityDefinition[] facilities, ItemDefinition[] items, LifeCalendarDefinition calendar, SaveService storage)
        {
            _service = new AdultCareerService(facilities);
            _items = items ?? Array.Empty<ItemDefinition>();
            _calendar = calendar;
            _storage = storage;
        }

        // The publish callback contains prevalidated, nonthrowing live-component restoration.
        // A rejected or unpersisted detached command is never exposed to the player.
        public AdultCareerResult Execute(AdultCareerSnapshot current, Func<AdultCareerSession, AdultCareerService, AdultCareerResult> command, Action<AdultCareerSession> publish)
        {
            if (_executing || current?.Adult == null || command == null || publish == null || _storage == null) return new AdultCareerResult(AdultCareerResultKind.InvalidRequest);
            _executing = true;
            try
            {
                var session = AdultCareerSnapshotPersistence.Restore(current, _storage.Slot, current.Adult.PlayerId, _items, _calendar);
                if (session == null) return new AdultCareerResult(AdultCareerResultKind.InvalidRequest);
                var result = command(session, _service);
                if (!result.Applied) return result;
                var next = AdultCareerSnapshotPersistence.CaptureSession(session);
                if (next == null) return new AdultCareerResult(AdultCareerResultKind.InvalidRequest);
                if (!AdultCareerSnapshotPersistence.SaveSession(_storage, next)) return new AdultCareerResult(AdultCareerResultKind.PersistenceFailed);
                publish(session);
                return result;
            }
            finally { _executing = false; }
        }
    }
}
