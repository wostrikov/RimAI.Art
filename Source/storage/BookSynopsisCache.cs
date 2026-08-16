/*
 * File: BookSynopsisCache.cs
 *
 * Purpose:
 * - Cache generated book synopses to avoid repeated LLM calls.
 *
 * Dependencies:
 * - BookKey
 * - BookSynopsis
 *
 * Responsibilities:
 * - Store and retrieve synopsis by BookKey.
 * - Used by scanner and integration layers.
 *
 * Do NOT:
 * - Do not generate content.
 * - Do not decide scanning logic.
 */
using System;
using System.Collections.Generic;
using Verse;

namespace Ustas.RimAI.Art.storage
{
    public sealed class BookSynopsisCache : IExposable
    {
        private Dictionary<string, BookSynopsisRecord> _records =
            new Dictionary<string, BookSynopsisRecord>(StringComparer.Ordinal);

        public int Count => _records?.Count ?? 0;

        public bool TryGet(BookKey key, out BookSynopsisRecord record)
        {
            record = null;
            if (key == null || !key.IsValid) return false;
            return _records.TryGetValue(key.Id, out record);
        }

        public bool Contains(BookKey key)
        {
            if (key == null || !key.IsValid) return false;
            return _records.ContainsKey(key.Id);
        }

        public void Set(BookKey key, BookSynopsisRecord record)
        {
            if (key == null || !key.IsValid) return;
            if (record == null) return;
            _records[key.Id] = record;
        }

        public bool Remove(BookKey key)
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
            List<BookSynopsisRecord> values = null;
            Scribe_Collections.Look(ref _records, "records", LookMode.Value, LookMode.Deep, ref keys, ref values);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && _records == null)
                _records = new Dictionary<string, BookSynopsisRecord>(StringComparer.Ordinal);
        }
    }
}
