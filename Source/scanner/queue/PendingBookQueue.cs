/*
 * File: PendingBookQueue.cs
 *
 * Purpose:
 * - Queue of books waiting for synopsis/title generation.
 *
 * Dependencies:
 * - PendingBookRecord
 *
 * Responsibilities:
 * - Enqueue / dequeue pending book tasks.
 * - Avoid duplicates.
 *
 * Design notes:
 * - This is a lightweight in-memory structure.
 *
 * Do NOT:
 * - Do not persist data here.
 * - Do not call LLM services.
 */
using System;
using System.Collections.Generic;
using Ustas.RimAI.Art.book;
using Ustas.RimAI.Art.storage;
using Verse;

namespace Ustas.RimAI.Art.scanner.queue
{
    public static class PendingBookQueue
    {
        private static readonly Queue<PendingBookRecord> Queue = new Queue<PendingBookRecord>();
        private static readonly HashSet<string> Keys = new HashSet<string>(StringComparer.Ordinal);

        public static int Count => Queue.Count;

        public static bool Enqueue(BookMeta meta, Pawn author = null)
        {
            if (meta == null || meta.Thing == null || meta.Thing.DestroyedOrNull()) return false;
            if (!BookKeyProvider.TryGetKey(meta.Thing, out var key)) return false;
            if (Keys.Contains(key.Id)) return false;

            Queue.Enqueue(new PendingBookRecord(key, meta, author));
            Keys.Add(key.Id);
            return true;
        }

        public static bool Enqueue(BookMeta meta, Pawn author, Map mapOverride)
        {
            if (meta == null || meta.Thing == null || meta.Thing.DestroyedOrNull()) return false;
            if (!BookKeyProvider.TryGetKey(meta.Thing, mapOverride, out var key)) return false;
            if (Keys.Contains(key.Id)) return false;

            Queue.Enqueue(new PendingBookRecord(key, meta, author));
            Keys.Add(key.Id);
            return true;
        }

        public static bool TryDequeue(out PendingBookRecord record)
        {
            record = null;
            if (Queue.Count == 0) return false;

            record = Queue.Dequeue();
            if (record?.Key != null)
                Keys.Remove(record.Key.Id);

            return record != null;
        }

        public static void Requeue(PendingBookRecord record)
        {
            if (record == null || record.Key == null || !record.Key.IsValid) return;
            if (Keys.Contains(record.Key.Id)) return;
            Queue.Enqueue(record);
            Keys.Add(record.Key.Id);
        }

        public static bool Contains(BookKey key)
        {
            return key != null && key.IsValid && Keys.Contains(key.Id);
        }

        /// <summary>
        /// Drops all pending book synopsis/title work. Owned lifecycle call site:
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
