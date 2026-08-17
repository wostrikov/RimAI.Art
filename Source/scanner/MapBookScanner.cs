/*
 * File: MapBookScanner.cs
 *
 * Purpose:
 * - Scan the current Map for book Things that have not yet been processed.
 *
 * Dependencies:
 * - Verse.Map
 * - Map.listerThings
 * - BookClassifier
 * - PendingBookQueue
 *
 * Responsibilities:
 * - Iterate all Things in the map.
 * - Identify books via BookClassifier.
 * - Enqueue unprocessed books for later handling.
 *
 * Do NOT:
 * - Do not generate book content.
 * - Do not write to save data directly.
 * - Do not run LLM calls.
 */
using Ustas.RimAI.Art.book;
using Ustas.RimAI.Art.scanner.queue;
using Ustas.RimAI.Art.settings;
using Ustas.RimAI.Art.storage;
using Ustas.RimAI.Art.storage.save;
using RimWorld;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace Ustas.RimAI.Art.scanner
{
    public static class MapBookScanner
    {
        public static void Scan(Map map, bool detailedLog = false)
        {
            if (map == null) return;
            var settings = LiteratureMod.Settings;
            if (settings != null && !settings.enabled) return;

            var cache = LiteratureSaveData.Current?.SynopsisCache;
            var things = map.listerThings?.AllThings;
            if (things == null || things.Count == 0) return;

            var candidates = new List<BookScanCandidate>(things.Count + 16);
            var seenIds = new HashSet<string>(System.StringComparer.Ordinal);
            int shelfCount = 0;
            int shelfHeldBooks = 0;
            int duplicateCandidates = 0;

            CollectMapCandidates(things, candidates, seenIds, ref duplicateCandidates);
            CollectBookcaseCandidates(map, candidates, seenIds, ref shelfCount, ref shelfHeldBooks, ref duplicateCandidates);

            int matched = 0;
            int enqueued = 0;
            int cached = 0;
            int classifierMiss = 0;
            int filtered = 0;
            int queueDuplicate = 0;
            int invalidKey = 0;

            var classMissSamples = detailedLog ? new List<string>() : null;
            var filteredSamples = detailedLog ? new List<string>() : null;
            var cachedSamples = detailedLog ? new List<string>() : null;
            var enqueuedSamples = detailedLog ? new List<string>() : null;
            var queueDuplicateSamples = detailedLog ? new List<string>() : null;
            var invalidKeySamples = detailedLog ? new List<string>() : null;

            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                var thing = candidate.Thing;
                if (thing == null || thing.DestroyedOrNull()) continue;

                if (thing.def != null &&
                    thing.def.category != ThingCategory.Item &&
                    !(thing is Book))
                {
                    continue;
                }

                var meta = BookClassifier.Classify(thing);
                if (meta == null)
                {
                    if (BookFilterPolicy.IsBookDef(thing.def))
                    {
                        classifierMiss++;
                        AddSample(classMissSamples, DescribeThing(thing, candidate.Source));
                    }
                    continue;
                }

                if (!BookFilterPolicy.IsAllowed(meta))
                {
                    filtered++;
                    AddSample(filteredSamples, DescribeMeta(meta, candidate.Source));
                    continue;
                }
                matched++;

                if (BookKeyProvider.TryGetKey(meta.Thing, out var key) &&
                    cache != null &&
                    cache.Contains(key))
                {
                    cached++;
                    AddSample(cachedSamples, DescribeMeta(meta, candidate.Source));
                    continue;
                }

                if (!BookKeyProvider.TryGetKey(meta.Thing, out var pendingKey))
                {
                    invalidKey++;
                    AddSample(invalidKeySamples, DescribeMeta(meta, candidate.Source));
                    continue;
                }

                if (PendingBookQueue.Contains(pendingKey))
                {
                    queueDuplicate++;
                    AddSample(queueDuplicateSamples, DescribeMeta(meta, candidate.Source));
                    continue;
                }

                if (PendingBookQueue.Enqueue(meta))
                {
                    enqueued++;
                    AddSample(enqueuedSamples, DescribeMeta(meta, candidate.Source));
                }
                else
                {
                    queueDuplicate++;
                    AddSample(queueDuplicateSamples, DescribeMeta(meta, candidate.Source));
                }
            }

            if (matched > 0)
            {
                Log.Message($"[RimAI.Art] Scan map {map.uniqueID}: books {matched}, enqueued {enqueued}, cached {cached}.");
            }

            if (detailedLog)
            {
                LogDetailedSummary(
                    map,
                    things.Count,
                    candidates.Count,
                    shelfCount,
                    shelfHeldBooks,
                    duplicateCandidates,
                    matched,
                    enqueued,
                    cached,
                    classifierMiss,
                    filtered,
                    queueDuplicate,
                    invalidKey,
                    classMissSamples,
                    filteredSamples,
                    cachedSamples,
                    enqueuedSamples,
                    queueDuplicateSamples,
                    invalidKeySamples);
            }
        }

        private static void CollectMapCandidates(
            List<Thing> things,
            List<BookScanCandidate> candidates,
            HashSet<string> seenIds,
            ref int duplicateCandidates)
        {
            for (int i = 0; i < things.Count; i++)
            {
                AddCandidate(candidates, seenIds, things[i], "map", ref duplicateCandidates);
            }
        }

        private static void CollectBookcaseCandidates(
            Map map,
            List<BookScanCandidate> candidates,
            HashSet<string> seenIds,
            ref int shelfCount,
            ref int shelfHeldBooks,
            ref int duplicateCandidates)
        {
            var bookcases = map.listerThings?.GetThingsOfType<Building_Bookcase>();
            if (bookcases == null) return;

            foreach (var bookcase in bookcases)
            {
                if (bookcase == null || bookcase.DestroyedOrNull()) continue;
                shelfCount++;

                var heldBooks = bookcase.HeldBooks;
                if (heldBooks == null) continue;

                for (int i = 0; i < heldBooks.Count; i++)
                {
                    var heldBook = heldBooks[i];
                    if (heldBook == null || heldBook.DestroyedOrNull()) continue;
                    shelfHeldBooks++;
                    AddCandidate(candidates, seenIds, heldBook, $"bookcase:{bookcase.def?.defName ?? "Unknown"}", ref duplicateCandidates);
                }
            }
        }

        private static void AddCandidate(
            List<BookScanCandidate> candidates,
            HashSet<string> seenIds,
            Thing thing,
            string source,
            ref int duplicateCandidates)
        {
            if (thing == null || thing.DestroyedOrNull()) return;

            string loadId = thing.GetUniqueLoadID();
            if (!string.IsNullOrEmpty(loadId) && !seenIds.Add(loadId))
            {
                duplicateCandidates++;
                return;
            }

            candidates.Add(new BookScanCandidate(thing, source));
        }

        private static void LogDetailedSummary(
            Map map,
            int mapThingCount,
            int candidateCount,
            int shelfCount,
            int shelfHeldBooks,
            int duplicateCandidates,
            int matched,
            int enqueued,
            int cached,
            int classifierMiss,
            int filtered,
            int queueDuplicate,
            int invalidKey,
            List<string> classMissSamples,
            List<string> filteredSamples,
            List<string> cachedSamples,
            List<string> enqueuedSamples,
            List<string> queueDuplicateSamples,
            List<string> invalidKeySamples)
        {
            Log.Message(
                $"[RimAI.Art] Detailed book scan map {map.uniqueID}: " +
                $"allThings={mapThingCount}, candidateBooks={candidateCount}, " +
                $"bookcases={shelfCount}, heldBooks={shelfHeldBooks}, duplicateCandidates={duplicateCandidates}, " +
                $"matched={matched}, filtered={filtered}, classifierMiss={classifierMiss}, " +
                $"cached={cached}, queuedAlready={queueDuplicate}, invalidKey={invalidKey}, enqueued={enqueued}.");

            LogSample("Book scan classifier miss", classMissSamples);
            LogSample("Book scan filtered", filteredSamples);
            LogSample("Book scan cached", cachedSamples);
            LogSample("Book scan already queued", queueDuplicateSamples);
            LogSample("Book scan invalid key", invalidKeySamples);
            LogSample("Book scan enqueued", enqueuedSamples);
        }

        private static void LogSample(string title, List<string> samples)
        {
            if (samples == null || samples.Count == 0) return;

            var sb = new StringBuilder();
            sb.Append("[RimAI.Art] ");
            sb.Append(title);
            sb.Append(": ");
            for (int i = 0; i < samples.Count; i++)
            {
                if (i > 0)
                    sb.Append(" | ");
                sb.Append(samples[i]);
            }

            Log.Message(sb.ToString());
        }

        private static void AddSample(List<string> samples, string value)
        {
            if (samples == null || samples.Count >= 8 || string.IsNullOrWhiteSpace(value)) return;
            samples.Add(value);
        }

        private static string DescribeMeta(BookMeta meta, string source)
        {
            if (meta == null)
                return $"[{source}] null-meta";

            return $"[{source}] {meta.DefName}/{meta.Type}/{meta.Title}";
        }

        private static string DescribeThing(Thing thing, string source)
        {
            if (thing == null)
                return $"[{source}] null-thing";

            string label = thing.LabelNoCount;
            if (string.IsNullOrWhiteSpace(label))
                label = thing.def?.label ?? thing.def?.defName ?? "Unknown";

            return $"[{source}] {thing.def?.defName ?? "UnknownDef"}/{label}";
        }

        private readonly struct BookScanCandidate
        {
            public BookScanCandidate(Thing thing, string source)
            {
                Thing = thing;
                Source = source ?? "unknown";
            }

            public Thing Thing { get; }
            public string Source { get; }
        }
    }
}
