using System.Collections.Generic;
using Verse;

namespace Ustas.RimAI.Art.Storage
{
    public sealed class IdeoDescriptionCache : IExposable
    {
        private Dictionary<int, string> _flavors = new Dictionary<int, string>();
        private HashSet<int> _processed = new HashSet<int>();

        public int Count => _flavors?.Count ?? 0;
        public int ProcessedCount => _processed?.Count ?? 0;

        public bool TryGet(int ideoId, out string flavor)
        {
            flavor = null;
            if (ideoId < 0) return false;
            return _flavors.TryGetValue(ideoId, out flavor);
        }

        public void Set(int ideoId, string flavor)
        {
            if (ideoId < 0) return;
            if (string.IsNullOrWhiteSpace(flavor)) return;
            _flavors[ideoId] = flavor;
        }

        public bool IsProcessed(int ideoId)
        {
            if (ideoId < 0) return false;
            return _processed != null && _processed.Contains(ideoId);
        }

        public void MarkProcessed(int ideoId)
        {
            if (ideoId < 0) return;
            _processed ??= new HashSet<int>();
            _processed.Add(ideoId);
        }

        public void ExposeData()
        {
            List<int> keys = null;
            List<string> values = null;
            Scribe_Collections.Look(ref _flavors, "flavors", LookMode.Value, LookMode.Value, ref keys, ref values);
            Scribe_Collections.Look(ref _processed, "processed", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && _flavors == null)
                _flavors = new Dictionary<int, string>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && _processed == null)
                _processed = new HashSet<int>();
        }
    }
}
