/*
 * File: ArtProductionTracker.cs
 *
 * Purpose:
 * - Track newly generated art and enqueue for LLM description.
 */
using Ustas.RimAI.Art.Art;
using Ustas.RimAI.Art.Scanner.Queue;
using Ustas.RimAI.Art.Storage;
using Ustas.RimAI.Art.Storage.Save;
using Verse;
using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Art.Scanner.Production
{
    public static class ArtProductionTracker
    {
        public static void NotifyGenerated(Thing thing)
        {
            if (thing == null || thing.DestroyedOrNull()) return;
            if (!Ustas.RimAI.Art.Integration.ArtCacheUtil.IsArtEditingEnabled())
            {
                RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Art generation skipped: art category edits disabled ({Ustas.RimAI.Art.Integration.ArtCacheUtil.DescribeArtSettings()}).");
                return;
            }

            var meta = ArtClassifier.Classify(thing);
            if (meta == null)
            {
                var comp = thing.TryGetComp<RimWorld.CompArt>();
                if (comp != null)
                {
                    var settings = Ustas.RimAI.Art.Settings.LiteratureMod.Settings;
                    bool allowLabelEdits = settings != null && settings.allowArtLabelEdits;
                    if (!Ustas.RimAI.Art.Art.ArtEditPolicy.ShouldGenerate(thing, allowLabelEdits))
                        RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Art generation skipped: not eligible ({thing.LabelCap}, {thing.def?.defName}).");
                }
                return;
            }

            var cache = LiteratureSaveData.Current?.ArtCache;
            if (ArtKeyProvider.TryGetKey(thing, out var key) &&
                cache != null &&
                cache.Contains(key))
            {
                return;
            }

            if (PendingArtQueue.Enqueue(meta))
                RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Enqueued art {meta.ThingLabel} ({meta.DefName}) from generation.");
        }
    }
}
