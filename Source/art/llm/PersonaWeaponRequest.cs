/*
 * Purpose:
 * - Generate persona weapon name and description from a pawn memory summary.
 *
 * Uses:
 * - IndependentBookLlmClient (independent LLM request)
 */
using System.Text;
using System.Threading.Tasks;
using Ustas.RimAI.Art.authoring;
using Ustas.RimAI.Art.art.model;
using Ustas.RimAI.Art.llm;
using Ustas.RimAI.Art.settings;
using Ustas.RimAI.Art.settings.util;
using Ustas.RimAI.Art.synopsis;
using Ustas.RimAI.Art.synopsis.llm;
using Verse;

namespace Ustas.RimAI.Art.art.llm
{
    public static class PersonaWeaponRequest
    {
        public static Task<ArtDescription> QueryAsync(
            ArtMeta meta,
            MemorySummarySpec summary,
            Pawn pawn,
            string baseContext = null)
        {
            var request = BuildRequest(meta, summary, pawn, baseContext);
            if (request == null) return Task.FromResult<ArtDescription>(null);
            return IndependentBookLlmClient.QueryJsonAsync<ArtDescription>(request);
        }

        private static LiteratureLlmRequest BuildRequest(
            ArtMeta meta,
            MemorySummarySpec summary,
            Pawn pawn,
            string baseContext)
        {
            if (summary == null || pawn == null || meta == null) return null;

            var prompt = BuildPrompt();
            var context = BuildContext(meta, summary, baseContext);

            return new LiteratureLlmRequest(prompt)
            {
                Context = context
            };
        }

        private static string BuildPrompt()
        {
            var settings = LiteratureMod.Settings;
            string template = BuildTemplate();
            return PromptTemplateUtil.Resolve(
                settings?.promptPersonaWeapon,
                template,
                ("LANG", Ustas.RimAI.Art.RimTalkConstantShim.Lang),
                ("TITLE_MAX_CHARS", SynopsisTokenPolicy.TitleMaxChars.ToString()),
                ("SYNOPSIS_MAX_CHARS", SynopsisTokenPolicy.SynopsisMaxChars.ToString()));
        }

        public static string BuildDefaultPrompt()
        {
            string template = BuildTemplate();
            return PromptTemplateUtil.ApplyTokens(
                template,
                ("LANG", Ustas.RimAI.Art.RimTalkConstantShim.Lang),
                ("TITLE_MAX_CHARS", SynopsisTokenPolicy.TitleMaxChars.ToString()),
                ("SYNOPSIS_MAX_CHARS", SynopsisTokenPolicy.SynopsisMaxChars.ToString()));
        }

        private static string BuildTemplate()
        {
            return
$@"Ти даєш ім'я персональній зброї та пишеш її внутрішньосвітовий опис.
Пиши мовою {Ustas.RimAI.Art.RimTalkConstantShim.Lang}. Виведи лише JSON.

Обов'язкові поля JSON:
- ""title""
- ""text""

Обмеження:
- Довжина title <= {SynopsisTokenPolicy.TitleMaxChars} символів.
- Довжина text <= {SynopsisTokenPolicy.SynopsisMaxChars} символів.
- ""text"" — яскравий опис, заснований на спогадах pawn.
- Дотримуйся тону RimWorld; не додавай метакоментарів.";
        }

        private static string BuildContext(ArtMeta meta, MemorySummarySpec summary, string baseContext)
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

            sb.AppendLine("[PersonaWeapon]");
            sb.AppendLine($"ThingLabel: {meta.ThingLabel}");
            sb.AppendLine($"DefName: {meta.DefName}");
            if (meta.Quality.HasValue)
                sb.AppendLine($"Quality: {meta.Quality.Value}");
            if (!string.IsNullOrWhiteSpace(meta.OriginalTitle))
                sb.AppendLine($"OriginalTitle: {meta.OriginalTitle}");
            if (!string.IsNullOrWhiteSpace(meta.OriginalDescription))
                sb.AppendLine($"OriginalDescription: {meta.OriginalDescription}");

            return sb.ToString().TrimEnd();
        }
    }
}
