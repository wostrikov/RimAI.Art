/*
 * Purpose:
 * - Generate a diary-style title and entry from a memory summary.
 *
 * Uses:
 * - IndependentBookLlmClient (independent LLM request)
 *
 * Do NOT:
 * - Do not access pawn context directly.
 */
using System.Text;
using System.Threading.Tasks;
using Ustas.RimAI.Art.authoring;
using Ustas.RimAI.Art.book;
using Ustas.RimAI.Art.llm;
using Ustas.RimAI.Art.settings;
using Ustas.RimAI.Art.settings.util;
using Ustas.RimAI.Art.synopsis;
using Ustas.RimAI.Art.synopsis.llm;
using Verse;

namespace Ustas.RimAI.Art.journal.llm
{
    public static class JournalFromSummaryRequest
    {
        public static LiteratureLlmRequest BuildRequest(BookMeta meta, MemorySummarySpec summary, Pawn author, string baseContext = null)
        {
            if (summary == null || author == null) return null;

            var prompt = BuildPrompt();
            var context = BuildContext(meta, summary, baseContext);

            return new LiteratureLlmRequest(prompt)
            {
                Context = context
            };
        }

        public static Task<BookTitleSpec> QueryAsync(
            BookMeta meta,
            MemorySummarySpec summary,
            Pawn author,
            string baseContext = null)
        {
            var request = BuildRequest(meta, summary, author, baseContext);
            if (request == null) return Task.FromResult<BookTitleSpec>(null);
            return IndependentBookLlmClient.QueryJsonAsync<BookTitleSpec>(request);
        }

        private static string BuildPrompt()
        {
            int tokenTarget = GetTokenTarget();
            var settings = LiteratureMod.Settings;
            string template = BuildTemplate(tokenTarget);
            var prompt = PromptTemplateUtil.Resolve(
                settings?.promptJournal,
                template,
                ("LANG", RimTalkConstantShim.Lang),
                ("TITLE_MAX_CHARS", SynopsisTokenPolicy.TitleMaxChars.ToString()),
                ("SYNOPSIS_MAX_CHARS", SynopsisTokenPolicy.SynopsisMaxChars.ToString()),
                ("SYNOPSIS_MAX_SENTENCES", SynopsisTokenPolicy.SynopsisMaxSentences.ToString()),
                ("TOKEN_TARGET", tokenTarget.ToString()));
            return ApplyJournalBoundary(prompt);
        }

        public static string BuildDefaultPrompt()
        {
            int tokenTarget = GetTokenTarget();
            string template = BuildTemplate(tokenTarget);
            var prompt = PromptTemplateUtil.ApplyTokens(
                template,
                ("LANG", RimTalkConstantShim.Lang),
                ("TITLE_MAX_CHARS", SynopsisTokenPolicy.TitleMaxChars.ToString()),
                ("SYNOPSIS_MAX_CHARS", SynopsisTokenPolicy.SynopsisMaxChars.ToString()),
                ("SYNOPSIS_MAX_SENTENCES", SynopsisTokenPolicy.SynopsisMaxSentences.ToString()),
                ("TOKEN_TARGET", tokenTarget.ToString()));
            return ApplyJournalBoundary(prompt);
        }

        private static string ApplyJournalBoundary(string prompt)
        {
            const string marker = "[TaskBoundary:Journal]";
            if (string.IsNullOrWhiteSpace(prompt)) return marker;
            if (prompt.Contains(marker)) return prompt;
            return
$@"{prompt.TrimEnd()}

{marker}
- This request creates a personal journal entry, never generic book content.
- The author is the first-person diarist.
- Use only the supplied pawn context and MemorySummary; do not invent an unrelated story.";
        }

        private static string BuildTemplate(int tokenTarget)
        {
            return
$@"Напиши особистий щоденниковий запис від імені pawn.
Пиши мовою {RimTalkConstantShim.Lang}. Виведи лише JSON.

Обов'язкові поля JSON:
- ""title""
- ""synopsis""

Обмеження:
- Довжина title <= {SynopsisTokenPolicy.TitleMaxChars} символів.
- Довжина synopsis <= {SynopsisTokenPolicy.SynopsisMaxChars} символів і {SynopsisTokenPolicy.SynopsisMaxSentences} речень.
- ""synopsis"" — текст щоденникового запису (близько {tokenTarget} токенів), а не підсумок.
- Пиши від першої особи й використовуй конкретні подробиці з підсумку спогадів.
- Автор є оповідачем щоденника. Не пиши роман, посібник, звіт або сторонню вигадану історію.
- Кожна подія має спиратися на наданий контекст pawn або MemorySummary.
- Дотримуйся приземленого світу RimWorld; без метакоментарів.";
        }

        private static string BuildContext(BookMeta meta, MemorySummarySpec summary, string baseContext)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(baseContext))
                sb.AppendLine(baseContext.TrimEnd());

            sb.AppendLine("[MemorySummary]");
            sb.AppendLine(summary.Summary ?? string.Empty);

            if (summary.Keywords != null && summary.Keywords.Length > 0)
                sb.AppendLine("Keywords: " + string.Join(", ", summary.Keywords));

            if (!string.IsNullOrWhiteSpace(summary.Tone))
                sb.AppendLine("Tone: " + summary.Tone);

            if (meta != null)
            {
                sb.AppendLine("[Journal]");
                sb.AppendLine($"EntryType: {meta.Type}");
                if (!string.IsNullOrWhiteSpace(meta.Title))
                    sb.AppendLine($"OriginalTitle: {meta.Title}");
                if (!string.IsNullOrWhiteSpace(meta.DescriptionDetailed))
                    sb.AppendLine($"OriginalDescription: {meta.DescriptionDetailed}");
            }

            return sb.ToString().TrimEnd();
        }

        private static int GetTokenTarget()
        {
            var settings = LiteratureMod.Settings;
            int target = settings?.synopsisTokenTarget ?? LiteratureSettingsDef.DefaultSynopsisTokenTarget;
            if (target < LiteratureSettingsDef.MinSynopsisTokenTarget)
                target = LiteratureSettingsDef.MinSynopsisTokenTarget;
            if (target > LiteratureSettingsDef.MaxSynopsisTokenTarget)
                target = LiteratureSettingsDef.MaxSynopsisTokenTarget;
            return target;
        }
    }
}
