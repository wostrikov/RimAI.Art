/*
 * Purpose:
 * - Helper object to generate book title/synopsis from a memory summary.
 *
 * Uses:
 * - IndependentBookLlmClient (independent LLM request)
 *
 * Responsibilities:
 * - Build instruction text based on MemorySummarySpec.
 *
 * Design notes:
 * - This represents the SECOND stage of the authoring pipeline.
 *
 * Do NOT:
 * - Do not access pawn context directly.
 * - Do not perform book classification or UI updates.
 */
using System.Text;
using System.Threading.Tasks;
using Ustas.RimAI.Art.LLM;
using Ustas.RimAI.Art.Books;
using Ustas.RimAI.Art.Settings;
using Ustas.RimAI.Art.Settings.Util;
using Ustas.RimAI.Art.Synopsis;
using Ustas.RimAI.Art.Synopsis.LLM;
using Verse;
using Ustas.RimAI.Communication.Data;

namespace Ustas.RimAI.Art.Authoring.LLM
{
    public static class BookFromSummaryRequest
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
            int storyTarget = tokenTarget + LiteratureSettingsDef.StoryTokenBonus;
            var settings = LiteratureMod.Settings;
            string template = BuildTemplate(tokenTarget, storyTarget);
            return PromptTemplateUtil.Resolve(
                settings?.promptBookFromSummary,
                template,
                ("LANG", Constant.Lang),
                ("TITLE_MAX_CHARS", SynopsisTokenPolicy.TitleMaxChars.ToString()),
                ("SYNOPSIS_MAX_CHARS", SynopsisTokenPolicy.SynopsisMaxChars.ToString()),
                ("SYNOPSIS_MAX_SENTENCES", SynopsisTokenPolicy.SynopsisMaxSentences.ToString()),
                ("TOKEN_TARGET", tokenTarget.ToString()),
                ("STORY_TOKEN_TARGET", storyTarget.ToString()));
        }

        public static string BuildDefaultPrompt()
        {
            int tokenTarget = GetTokenTarget();
            int storyTarget = tokenTarget + LiteratureSettingsDef.StoryTokenBonus;
            string template = BuildTemplate(tokenTarget, storyTarget);
            return PromptTemplateUtil.ApplyTokens(
                template,
                ("LANG", Constant.Lang),
                ("TITLE_MAX_CHARS", SynopsisTokenPolicy.TitleMaxChars.ToString()),
                ("SYNOPSIS_MAX_CHARS", SynopsisTokenPolicy.SynopsisMaxChars.ToString()),
                ("SYNOPSIS_MAX_SENTENCES", SynopsisTokenPolicy.SynopsisMaxSentences.ToString()),
                ("TOKEN_TARGET", tokenTarget.ToString()),
                ("STORY_TOKEN_TARGET", storyTarget.ToString()));
        }

        private static string BuildTemplate(int tokenTarget, int storyTarget)
        {
            return
$@"Пиши мовою {Constant.Lang}. Виведи лише JSON.

Обов'язкові поля JSON:
- ""title""
- ""synopsis""

Обмеження:
- Довжина title <= {SynopsisTokenPolicy.TitleMaxChars} символів.
- Довжина synopsis <= {SynopsisTokenPolicy.SynopsisMaxChars} символів і {SynopsisTokenPolicy.SynopsisMaxSentences} речень.
- Вигадай НОВУ назву; не повторюй текст OriginalTitle.
- ""synopsis"" — фактичний текст книги (близько {tokenTarget} токенів), а не підсумок.
- Якщо підсумок спогадів читається як історія, можна розширити текст приблизно до {storyTarget} токенів.
- Зберігай власні назви. Не згадуй, що це підсумок.";
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
                sb.AppendLine("[Book]");
                sb.AppendLine($"Type: {meta.Type}");
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

