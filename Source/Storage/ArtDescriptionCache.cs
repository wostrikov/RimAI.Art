using System;
using System.Collections.Generic;
using Verse;

namespace Ustas.RimAI.Art.Storage
{
    public sealed class ArtDescriptionCache : IExposable
    {
        private Dictionary<string, ArtDescriptionRecord> _records =
            new Dictionary<string, ArtDescriptionRecord>(StringComparer.Ordinal);

        public int Count => _records?.Count ?? 0;

        public bool TryGet(ArtKey key, out ArtDescriptionRecord record)
        {
            record = null;
            if (key == null || !key.IsValid) return false;
            return _records.TryGetValue(key.Id, out record);
        }

        public bool Contains(ArtKey key)
        {
            if (key == null || !key.IsValid) return false;
            return _records.ContainsKey(key.Id);
        }

        public void Set(ArtKey key, ArtDescriptionRecord record)
        {
            if (key == null || !key.IsValid) return;
            if (record == null) return;
            _records[key.Id] = record;
        }

        public bool Remove(ArtKey key)
        {
            if (key == null || !key.IsValid) return false;
            return _records.Remove(key.Id);
        }

        public int Clear()
        {
            int count = _records?.Count ?? 0;
            _records?.Clear();
            return count;
        }

        public void ExposeData()
        {
            List<string> keys = null;
            List<ArtDescriptionRecord> values = null;
            Scribe_Collections.Look(ref _records, "records", LookMode.Value, LookMode.Deep, ref keys, ref values);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && _records == null)
                _records = new Dictionary<string, ArtDescriptionRecord>(StringComparer.Ordinal);
        }
    }
}
