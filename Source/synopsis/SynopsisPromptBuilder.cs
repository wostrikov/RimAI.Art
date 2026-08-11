/*
 * File: SynopsisPromptBuilder.cs
 *
 * Purpose:
 * - Build the LLM prompt for generating a book synopsis.
 *
 * Dependencies:
 * - BookMeta
 * - Pawn context (if provided externally)
 *
 * Responsibilities:
 * - Construct a clear, bounded instruction.
 * - Request structured JSON output (BookSynopsis).
 *
 * Do NOT:
 * - Do not call AIService.
 * - Do not inject RimTalk Constant.Instruction.
 */
using System.Text;
using RimTalk.Data;
using RimTalk_LiteratureExpansion.settings.util;
using RimTalk_LiteratureExpansion.settings;
using RimTalk_LiteratureExpansion.book;

namespace RimTalk_LiteratureExpansion.synopsis
{
    public static class SynopsisPromptBuilder
    {
        public static string BuildPrompt(BookMeta meta)
        {
            int tokenTarget = GetTokenTarget();
            var settings = LiteratureMod.Settings;
            string template = BuildTemplate(tokenTarget);
            return PromptTemplateUtil.Resolve(
                settings?.promptSynopsis,
                template,
                ("LANG", RimTalkConstantShim.Lang),
                ("TITLE_MAX_CHARS", SynopsisTokenPolicy.TitleMaxChars.ToString()),
                ("SYNOPSIS_MAX_CHARS", SynopsisTokenPolicy.SynopsisMaxChars.ToString()),
                ("SYNOPSIS_MAX_SENTENCES", SynopsisTokenPolicy.SynopsisMaxSentences.ToString()),
                ("TOKEN_TARGET", tokenTarget.ToString()));
        }

        public static string BuildDefaultPrompt()
        {
            int tokenTarget = GetTokenTarget();
            string template = BuildTemplate(tokenTarget);
            return PromptTemplateUtil.ApplyTokens(
                template,
                ("LANG", RimTalkConstantShim.Lang),
                ("TITLE_MAX_CHARS", SynopsisTokenPolicy.TitleMaxChars.ToString()),
                ("SYNOPSIS_MAX_CHARS", SynopsisTokenPolicy.SynopsisMaxChars.ToString()),
                ("SYNOPSIS_MAX_SENTENCES", SynopsisTokenPolicy.SynopsisMaxSentences.ToString()),
                ("TOKEN_TARGET", tokenTarget.ToString()));
        }

        private static string BuildTemplate(int tokenTarget)
        {
            return
$@"Ти пишеш внутрішньосвітовий текст книги RimWorld.
Пиши мовою {RimTalkConstantShim.Lang}. Виведи лише JSON.

Обов'язкові поля JSON:
- ""title""
- ""synopsis""

Обмеження:
- Довжина title <= {SynopsisTokenPolicy.TitleMaxChars} символів.
- Довжина synopsis <= {SynopsisTokenPolicy.SynopsisMaxChars} символів і {SynopsisTokenPolicy.SynopsisMaxSentences} речень.
- Вигадай НОВУ назву; не повторюй OriginalTitle або фрагменти ключів перекладу.
- ""synopsis"" — фактичний текст книги (близько {tokenTarget} токенів), а не підсумок.
- Використовуй лише надані підказки (тип, переваги, навичка, оригінальний опис); не додавай стороннього лору.
- Якщо переваги передбачають навчання, пиши практичні покрокові вказівки й приклади.
- Якщо type — CB_ChildrensBook або CB_ColoringBook: лагідна проста історія чи заняття.
- Якщо type — VBE_Newspaper: короткий випуск новин із наданими часовими полями.
- Якщо type — VBE_SkillBook або переваги передбачають навчання: тон практичного посібника.
- Якщо type — Journal: особистий щоденниковий запис від першої особи, де pawn — автор щоденника.
- Для Journal використовуй лише надані факти зі щоденника або контексту pawn; не перетворюй його на посібник, роман, звіт чи сторонню історію.";
        }

        public static string BuildContext(BookMeta meta)
        {
            if (meta == null) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("[Book]");
            sb.AppendLine($"Type: {meta.Type}");

            if (!string.IsNullOrWhiteSpace(meta.Title))
                sb.AppendLine($"OriginalTitle: {meta.Title}");

            if (!string.IsNullOrWhiteSpace(meta.FlavorUI))
                sb.AppendLine($"OriginalBlurb: {meta.FlavorUI}");

            if (!string.IsNullOrWhiteSpace(meta.DescriptionDetailed))
                sb.AppendLine($"OriginalDescription: {meta.DescriptionDetailed}");

            var benefits = ExtractBenefitLines(meta.DescriptionDetailed);
            if (!string.IsNullOrWhiteSpace(benefits))
            {
                sb.AppendLine("[Benefits]");
                sb.AppendLine(benefits);
            }

            if (meta.Type == BookType.VBE_SkillBook && !string.IsNullOrWhiteSpace(meta.SkillDefName))
                sb.AppendLine($"Skill: {meta.SkillDefName}");

            if (meta.Type == BookType.VBE_Newspaper)
            {
                if (meta.VbeExpireTime.HasValue)
                    sb.AppendLine($"NewspaperExpireTime: {meta.VbeExpireTime.Value}");
                if (meta.VbeExpireTimeAbs.HasValue)
                    sb.AppendLine($"NewspaperExpireTimeAbs: {meta.VbeExpireTimeAbs.Value}");
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

        private static string ExtractBenefitLines(string description)
        {
            if (string.IsNullOrWhiteSpace(description)) return string.Empty;

            var sb = new StringBuilder();
            var lines = description.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();
                if (trimmed.StartsWith("-", System.StringComparison.Ordinal))
                    sb.AppendLine(trimmed);
            }

            return sb.ToString().TrimEnd();
        }
    }
}
