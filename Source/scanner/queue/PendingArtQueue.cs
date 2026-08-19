using System;
using System.Collections.Generic;
using Ustas.RimAI.Art.art;
using Ustas.RimAI.Art.storage;
using Verse;

namespace Ustas.RimAI.Art.scanner.queue
{
    public static class PendingArtQueue
    {
        private static readonly Queue<PendingArtRecord> Queue = new Queue<PendingArtRecord>();
        private static readonly HashSet<string> Keys = new HashSet<string>(StringComparer.Ordinal);

        public static int Count => Queue.Count;

        public static bool Enqueue(ArtMeta meta)
        {
            if (meta == null || meta.Thing == null || meta.Thing.DestroyedOrNull()) return false;
            if (!ArtKeyProvider.TryGetKey(meta.Thing, out var key)) return false;
            if (Keys.Contains(key.Id)) return false;

            Queue.Enqueue(new PendingArtRecord(key, meta));
            Keys.Add(key.Id);
            return true;
        }

        public static bool TryDequeue(out PendingArtRecord record)
        {
            record = null;
            if (Queue.Count == 0) return false;

            record = Queue.Dequeue();
            if (record?.Key != null)
                Keys.Remove(record.Key.Id);

            return record != null;
        }

        public static void Requeue(PendingArtRecord record)
        {
            if (record == null || record.Key == null || !record.Key.IsValid) return;
            if (Keys.Contains(record.Key.Id)) return;
            Queue.Enqueue(record);
            Keys.Add(record.Key.Id);
        }

        public static bool Contains(ArtKey key)
        {
            return key != null && key.IsValid && Keys.Contains(key.Id);
        }

        /// <summary>
        /// Drops all pending art generation work. Owned lifecycle call site:
        /// <c>ArtComposition.Stop</c>.
        /// </summary>
        public static int Clear()
        {
            int count = Queue.Count;
            Queue.Clear();
            Keys.Clear();
            return count;
        }
    }
}
