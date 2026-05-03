using System.Collections.Generic;

namespace Rootborn.Game.Lineage
{
    public sealed class LineageBook
    {
        private readonly List<AncestorRecord> _ancestors = new List<AncestorRecord>();
        public IReadOnlyList<AncestorRecord> Ancestors => _ancestors;

        public void Record(AncestorRecord record)
        {
            if (record != null) _ancestors.Add(record);
        }

        public int GenerationCount => _ancestors.Count;
    }
}
