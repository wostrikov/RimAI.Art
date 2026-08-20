/*
 * File: TalkPromptBookInjector.cs
 *
 * Purpose:
 * - Inject book synopsis into a TalkRequest when a pawn is reading
 *   or telling a story.
 *
 * Dependencies:
 * - RimTalk PromptOverrideService
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
using System.Text;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Art.Books;
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
            if (settings != null && !settings.enabled) return;

            BookMeta meta = null;
            if (!TryResolveBookMeta(request.Initiator, out meta) &&
                !TryResolveBookMeta(request.Recipient, out meta))
            {
                return;
            }

            if (!BookFilterPolicy.IsAllowed(meta))
                return;

            var cache = LiteratureSaveData.Current?.SynopsisCache;
            if (cache == null) return;

            if (!BookKeyProvider.TryGetKey(meta.Thing, out var key)) return;

            if (!cache.TryGet(key, out var record))
            {
                PendingBookQueue.Enqueue(meta);
                return;
            }

            var synopsis = record.ToSynopsis();
            if (synopsis == null) return;

            var snippet = BuildSnippet(meta, synopsis);
            if (string.IsNullOrWhiteSpace(snippet)) return;

            if (string.IsNullOrWhiteSpace(request.Context))
                request.Context = snippet;
            else
                request.Context = $"{request.Context}\n\n{snippet}";
        }

        private static string BuildSnippet(BookMeta meta, BookSynopsis synopsis)
        {
            var title = string.IsNullOrWhiteSpace(synopsis.Title) ? meta?.Title : synopsis.Title;
            var text = synopsis.Synopsis ?? string.Empty;

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(text)) return null;

            if (text.Length > SynopsisTokenPolicy.PromptSynopsisMaxChars)
                text = text.Substring(0, SynopsisTokenPolicy.PromptSynopsisMaxChars).TrimEnd();

            var sb = new StringBuilder();
            sb.AppendLine("[Book]");

            if (!string.IsNullOrWhiteSpace(title))
                sb.AppendLine($"Title: {title}");

            if (!string.IsNullOrWhiteSpace(text))
                sb.AppendLine($"Text: {text}");

            return sb.ToString().TrimEnd();
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
