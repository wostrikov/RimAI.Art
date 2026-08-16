/*
 * Purpose:
 * - Orchestrate the book authoring pipeline when a pawn finishes writing a book.
 *
 * Uses:
 * - IndependentBookLlmClient via MemorySummaryRequest / BookFromSummaryRequest
 * - BookSynopsisService (optional finalization)
 *
 * Flow:
 * 1) Build pawn context through RimTalk's context service.
 * 2) Use a standalone request to obtain a structured MemorySummarySpec (JSON).
 * 3) Feed the summary into a second request to generate book title/synopsis.
 *
 * Design notes:
 * - This class coordinates steps only; no LLM prompt strings are hardcoded here.
 *
 * Do NOT:
 * - Do not directly read RimTalkMemoryExpansion.
 * - Do not call Chat/ChatStreaming.
 * - Do not write book UI fields here.
 */
using System.Threading.Tasks;
using Ustas.RimAI.Art.authoring.llm;
using Ustas.RimAI.Art.book;
using Ustas.RimAI.Art.llm;
using Ustas.RimAI.Art.synopsis;
using Ustas.RimAI.Art.synopsis.model;
using Verse;

namespace Ustas.RimAI.Art.authoring
{
    public static class BookAuthoringPipeline
    {
        public static async Task<BookSynopsis> GenerateAsync(BookMeta meta, Pawn author)
        {
            if (meta == null || author == null) return null;

            var summaryRequest = MemorySummaryRequest.BuildRequest(author);
            if (summaryRequest == null) return null;

            return await GenerateFromSummaryRequestAsync(meta, author, summaryRequest);
        }

        public static async Task<BookSynopsis> GenerateFromSummaryRequestAsync(
            BookMeta meta,
            Pawn author,
            LiteratureLlmRequest summaryRequest)
        {
            if (meta == null || author == null || summaryRequest == null) return null;

            var summary = await MemorySummaryRequest.QueryAsync(summaryRequest);
            if (summary == null) return null;

            return await BookSynopsisService.GenerateFromSummaryAsync(meta, author, summary, summaryRequest.Context);
        }
    }
}
