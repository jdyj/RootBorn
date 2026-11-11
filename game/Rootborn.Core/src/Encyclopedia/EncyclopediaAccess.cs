using System;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Encyclopedia
{
    /// <summary>
    /// 탐색 결과가 id로 도감을 여는 창구(main의 IExplorationEncyclopedia). 처음 발견한 시각은 받은 시계에서 읽는다
    /// (원본은 DateTime.UtcNow.Ticks).
    /// </summary>
    public sealed class EncyclopediaAccess : IExplorationEncyclopedia
    {
        private readonly EncyclopediaProgress _progress;
        private readonly Func<long> _utcNowTicks;

        public EncyclopediaAccess(EncyclopediaProgress progress, Func<long> utcNowTicks)
        {
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _utcNowTicks = utcNowTicks ?? throw new ArgumentNullException(nameof(utcNowTicks));
        }

        public bool TryUnlock(string entryId, int revealStage) => _progress.TryUnlock(entryId, revealStage, _utcNowTicks());
    }
}
