/*
 * File: TalkPromptBookInjector.cs
 *
 * Purpose:
 * - Inject book synopsis into a TalkRequest when a pawn is reading
 *   or telling a story.
 *
 * Dependencies:
 * - RimAI.Communication PromptOverrideService
 * - BookSynopsisCache
 * - BookMeta
 *
 * Responsibilities:
 * - Detect current book context (provided by caller).
 * - Temporarily augment the prompt/context for the current TalkRequest.
 *
 * Design notes:
 * - Injection is local to a single request.
 * - Should not affect global Constant.Instruction.
 *
 * Do NOT:
 * - Do not persist prompt changes.
 * - Do not generate synopsis here.
 */
using System;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Art.Books;
using Ustas.RimAI.Art.Policy;
using Ustas.RimAI.Art.Scanner.Queue;
using Ustas.RimAI.Art.Settings;
using Ustas.RimAI.Art.Storage;
using Ustas.RimAI.Art.Storage.Save;
using Ustas.RimAI.Art.Synopsis;
using Ustas.RimAI.Art.Synopsis.Model;
using RimWorld;
using Verse;
using Verse.AI;

namespace Ustas.RimAI.Art.Integration
{
    public static class TalkPromptBookInjector
    {
        public static void InjectIfAvailable(TalkRequest request)
        {
            if (request == null) return;
            var settings = LiteratureMod.Settings;
            bool enabled = settings == null || settings.enabled;

            BookMeta meta = null;
            bool hasBook = TryResolveBookMeta(request.Initiator, out meta) ||
                TryResolveBookMeta(request.Recipient, out meta);
            bool allowed = hasBook && BookFilterPolicy.IsAllowed(meta);

            var cache = LiteratureSaveData.Current?.SynopsisCache;
            bool cached = false;
            BookSynopsis synopsis = null;
            if (hasBook && cache != null && BookKeyProvider.TryGetKey(meta.Thing, out var key) &&
                cache.TryGet(key, out var record))
            {
                synopsis = record.ToSynopsis();
                cached = synopsis != null;
            }

            var action = ArtReadingDialoguePolicy.Decide(enabled, hasBook, allowed, cached);
            if (action == ArtReadingInjectAction.SkipDisabled ||
                action == ArtReadingInjectAction.SkipNoBook ||
                action == ArtReadingInjectAction.SkipFiltered)
                return;

            if (action == ArtReadingInjectAction.EnqueueMissing)
            {
                if (meta != null)
                    PendingBookQueue.Enqueue(meta);
                return;
            }

            var title = string.IsNullOrWhiteSpace(synopsis.Title) ? meta?.Title : synopsis.Title;
            var snippet = ArtReadingDialoguePolicy.BuildSnippet(
                title,
                synopsis.Synopsis,
                SynopsisTokenPolicy.PromptSynopsisMaxChars);
            request.Context = ArtReadingDialoguePolicy.AppendToContext(request.Context, snippet);
        }

        private static bool TryResolveBookMeta(Pawn pawn, out BookMeta meta)
        {
            meta = null;
            if (pawn == null) return false;

            if (TryGetBookFromJob(pawn, out var jobBook))
            {
                meta = BookClassifier.Classify(jobBook);
                if (meta != null) return true;
            }

            var carried = pawn.carryTracker?.CarriedThing;
            if (carried != null)
            {
                meta = BookClassifier.Classify(carried);
                if (meta != null) return true;
            }

            return false;
        }

        private static bool TryGetBookFromJob(Pawn pawn, out Thing book)
        {
            book = null;
            var job = pawn?.CurJob;
            if (job == null) return false;

            foreach (TargetIndex index in Enum.GetValues(typeof(TargetIndex)))
            {
                LocalTargetInfo target;
                try
                {
                    target = job.GetTarget(index);
                }
                catch (ArgumentOutOfRangeException)
                {
                    continue;
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (!target.HasThing) continue;
                var thing = target.Thing;
                if (thing == null) continue;

                if (thing is Book || thing.TryGetComp<CompBook>() != null)
                {
                    book = thing;
                    return true;
                }
            }

            return false;
        }
    }
}
