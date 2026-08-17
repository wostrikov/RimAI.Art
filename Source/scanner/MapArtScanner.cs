/*
 * File: MapArtScanner.cs
 *
 * Purpose:
 * - Scan the current Map for art Things that have not yet been processed.
 *
 * Dependencies:
 * - Verse.Map
 * - Map.listerThings
 * - ArtClassifier
 * - PendingArtQueue
 *
 * Responsibilities:
 * - Iterate all Things in the map.
 * - Identify art via ArtClassifier.
 * - Enqueue unprocessed art for later handling.
 *
 * Do NOT:
 * - Do not generate art content here.
 * - Do not write to save data directly.
 * - Do not run LLM calls.
 */
using Ustas.RimAI.Art.art;
using Ustas.RimAI.Art.scanner.queue;
using Ustas.RimAI.Art.storage;
using Ustas.RimAI.Art.storage.save;
using Verse;

namespace Ustas.RimAI.Art.scanner
{
    public static class MapArtScanner
    {
        public static void Scan(Map map)
        {
            if (map == null) return;
            if (!Ustas.RimAI.Art.integration.ArtCacheUtil.IsArtEditingEnabled())
            {
                Log.Message($"[RimAI.Art] Art scan skipped: art category edits disabled (map {map.uniqueID}, {Ustas.RimAI.Art.integration.ArtCacheUtil.DescribeArtSettings()}).");
                return;
            }

            var cache = LiteratureSaveData.Current?.ArtCache;
            if (cache == null)
            {
                Log.Message($"[RimAI.Art] Art scan skipped: ArtCache unavailable (map {map.uniqueID}).");
                return;
            }
            var things = map.listerThings?.AllThings;
            if (things == null || things.Count == 0)
            {
                Log.Message($"[RimAI.Art] Art scan: no things on map {map.uniqueID}.");
                return;
            }

            int matched = 0;
            int enqueued = 0;
            int cached = 0;
            int noArtComp = 0;
            int notShowable = 0;
            int notEligible = 0;

            for (int i = 0; i < things.Count; i++)
            {
                var thing = things[i];
                if (thing == null || thing.DestroyedOrNull()) continue;

                var comp = thing.TryGetComp<RimWorld.CompArt>();
                if (comp == null)
                    noArtComp++;
                else if (!comp.CanShowArt)
                    notShowable++;

                var meta = ArtClassifier.Classify(thing);
                if (meta == null)
                {
                    notEligible++;
                    continue;
                }
                matched++;

                if (ArtKeyProvider.TryGetKey(meta.Thing, out var key) &&
                    cache != null &&
                    cache.Contains(key))
                {
                    cached++;
                    continue;
                }

                if (PendingArtQueue.Enqueue(meta))
                    enqueued++;
            }

            if (matched > 0)
            {
                Log.Message($"[RimAI.Art] Scan map {map.uniqueID}: art {matched}, enqueued {enqueued}, cached {cached}, noComp {noArtComp}, notShowable {notShowable}, notEligible {notEligible}.");
            }
            else
            {
                Log.Message($"[RimAI.Art] Scan map {map.uniqueID}: no art matched (noComp {noArtComp}, notShowable {notShowable}, notEligible {notEligible}).");
            }
        }
    }
}
