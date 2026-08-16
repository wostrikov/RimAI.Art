using System;
using System.Collections.Generic;
using Verse;

namespace Ustas.RimAI.Art.tv
{
    public sealed class TvProgramCache : IExposable
    {
        private Dictionary<string, TvProgramRecord> _records =
            new Dictionary<string, TvProgramRecord>(StringComparer.Ordinal);

        public int Count => _records?.Count ?? 0;

        public bool TryGet(string key, out TvProgramRecord record)
        {
            record = null;
            if (string.IsNullOrEmpty(key)) return false;
            return _records.TryGetValue(key, out record);
        }

        public void Set(string key, TvProgramRecord record)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (record == null) return;
            _records[key] = record;
        }

        public bool Remove(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return _records.Remove(key);
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
            List<TvProgramRecord> values = null;
            Scribe_Collections.Look(ref _records, "records", LookMode.Value, LookMode.Deep, ref keys, ref values);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && _records == null)
                _records = new Dictionary<string, TvProgramRecord>(StringComparer.Ordinal);
        }
    }
}
