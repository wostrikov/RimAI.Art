/*
 * File: BookSynopsisService.cs
 *
 * Purpose:
 * - Central service for generating or retrieving book synopses.
 *
 * Dependencies:
 * - IndependentBookLlmClient (uses RimTalk API config directly)
 * - SynopsisPromptBuilder
 * - BookSynopsisCache
 *
 * Responsibilities:
 * - Check cache first.
 * - If missing, call LLM to generate BookSynopsis.
 *
 * Design notes:
 * - This is the ONLY class allowed to trigger LLM generation of book content.
 *
 * Do NOT:
 * - Do not apply results to Book objects.
 * - Do not manage scan scheduling.
 */
using System.Threading.Tasks;
using Ustas.RimAI.Art.authoring;
using Ustas.RimAI.Art.authoring.llm;
using Ustas.RimAI.Art.book;
using Ustas.RimAI.Art.llm;
using Ustas.RimAI.Art.storage;
using Ustas.RimAI.Art.storage.save;
using Ustas.RimAI.Art.synopsis.llm;
using Ustas.RimAI.Art.synopsis.model;
using Verse;
using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Art.synopsis
{
    public static class BookSynopsisService
    {
        public static async Task<BookSynopsis> GetOrGenerateAsync(BookMeta meta, Pawn contextPawn = null)
        {
            if (meta == null) return null;

            var cache = LiteratureSaveData.Current?.SynopsisCache;
            if (cache != null && BookKeyProvider.TryGetKey(meta.Thing, out var key))
            {
                if (cache.TryGet(key, out var record))
                    return record?.ToSynopsis();
            }

            var pawn = contextPawn;
            if (pawn == null) return null;

            var request = new LiteratureLlmRequest(SynopsisPromptBuilder.BuildPrompt(meta))
            {
                Context = SynopsisPromptBuilder.BuildContext(meta)
            };

            RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] BookSynopsisService: dispatch LLM request for {meta.DefName}.");
            var synopsis = await SynopsisLLMAdapter.QuerySynopsisAsync(request);
            RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] BookSynopsisService: LLM request completed for {meta.DefName} (null={synopsis == null}).");
            return Normalize(synopsis);
        }

        public static async Task<BookSynopsis> GenerateFromSummaryAsync(
            BookMeta meta,
            Pawn author,
            MemorySummarySpec summary,
            string baseContext = null)
        {
            if (summary == null || author == null) return null;

            var spec = await BookFromSummaryRequest.QueryAsync(meta, summary, author, baseContext);
            if (spec == null) return null;

            return Normalize(new BookSynopsis
            {
                Title = spec.Title,
                Synopsis = spec.Synopsis
            });
        }

        private static BookSynopsis Normalize(BookSynopsis synopsis)
        {
            if (synopsis == null) return null;

            var title = synopsis.Title?.Trim();
            var text = synopsis.Synopsis?.Trim();

            if (title != null && title.Length > SynopsisTokenPolicy.TitleMaxChars)
                title = title.Substring(0, SynopsisTokenPolicy.TitleMaxChars).TrimEnd();

            if (text != null && text.Length > SynopsisTokenPolicy.SynopsisMaxChars)
                text = text.Substring(0, SynopsisTokenPolicy.SynopsisMaxChars).TrimEnd();

            synopsis.Title = title;
            synopsis.Synopsis = text;
            return synopsis;
        }
    }
}
