/*
 * Purpose:
 * - Orchestrate diary authoring when a pawn writes a journal entry.
 */
using System.Threading.Tasks;
using Ustas.RimAI.Art.authoring;
using Ustas.RimAI.Art.authoring.llm;
using Ustas.RimAI.Art.book;
using Ustas.RimAI.Art.journal.llm;
using Ustas.RimAI.Art.llm;
using Ustas.RimAI.Art.synopsis;
using Ustas.RimAI.Art.synopsis.model;
using Verse;

namespace Ustas.RimAI.Art.journal
{
    public static class JournalAuthoringPipeline
    {
        public static async Task<BookSynopsis> GenerateFromSummaryRequestAsync(
            BookMeta meta,
            Pawn author,
            LiteratureLlmRequest summaryRequest)
        {
            if (meta == null || author == null || summaryRequest == null) return null;

            var summary = await MemorySummaryRequest.QueryAsync(summaryRequest);
            if (summary == null) return null;

            var spec = await JournalFromSummaryRequest.QueryAsync(meta, summary, author, summaryRequest.Context);
            if (spec == null) return null;

            return Normalize(new BookSynopsis
            {
                Title = spec.Title,
                Synopsis = spec.Synopsis
            }, author);
        }

        private static BookSynopsis Normalize(BookSynopsis synopsis, Pawn author)
        {
            if (synopsis == null) return null;

            var title = synopsis.Title?.Trim();
            var text = synopsis.Synopsis?.Trim();

            if (author != null)
            {
                var authorName = author.LabelShortCap ?? author.Name?.ToStringShort ?? "Unknown";
                var authorLine = "RimTalkLE_JournalAuthorLine".Translate(authorName).ToString();
                if (!string.IsNullOrWhiteSpace(authorLine))
                {
                    text = string.IsNullOrWhiteSpace(text)
                        ? authorLine
                        : $"{authorLine}\n\n{text}";
                }
            }

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
