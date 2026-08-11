using System.Text;
using RimTalk.Data;
using RimTalk_LiteratureExpansion.settings.util;
using RimTalk_LiteratureExpansion.settings;
using RimTalk_LiteratureExpansion.synopsis;
using Verse;

namespace RimTalk_LiteratureExpansion.art
{
    public static class ArtPromptBuilder
    {
        public static string BuildPrompt(ArtMeta meta)
        {
            int tokenTarget = GetTokenTarget();
            var settings = LiteratureMod.Settings;
            string template = BuildTemplate(tokenTarget);
            return PromptTemplateUtil.Resolve(
                settings?.promptArt,
                template,
                ("LANG", RimTalk_LiteratureExpansion.RimTalkConstantShim.Lang),
                ("TITLE_MAX_CHARS", SynopsisTokenPolicy.TitleMaxChars.ToString()),
                ("SYNOPSIS_MAX_CHARS", SynopsisTokenPolicy.SynopsisMaxChars.ToString()),
                ("TOKEN_TARGET", tokenTarget.ToString()));
        }

        public static string BuildDefaultPrompt()
        {
            int tokenTarget = GetTokenTarget();
            string template = BuildTemplate(tokenTarget);
            return PromptTemplateUtil.ApplyTokens(
                template,
                ("LANG", RimTalk_LiteratureExpansion.RimTalkConstantShim.Lang),
                ("TITLE_MAX_CHARS", SynopsisTokenPolicy.TitleMaxChars.ToString()),
                ("SYNOPSIS_MAX_CHARS", SynopsisTokenPolicy.SynopsisMaxChars.ToString()),
                ("TOKEN_TARGET", tokenTarget.ToString()));
        }

        private static string BuildTemplate(int tokenTarget)
        {
            return
$@"Ти пишеш внутрішньосвітові описи мистецьких об'єктів RimWorld.
Пиши мовою {RimTalk_LiteratureExpansion.RimTalkConstantShim.Lang}. Виведи лише JSON.

Обов'язкові поля JSON:
- ""title""
- ""text""

Обмеження:
- Довжина title <= {SynopsisTokenPolicy.TitleMaxChars} символів.
- Довжина text <= {SynopsisTokenPolicy.SynopsisMaxChars} символів.
- ""text"" — повний опис твору (близько {tokenTarget} токенів), а не підсумок.
- Використовуй лише надані підказки (оригінальна назва, автор, оригінальний опис, якість).
- Не вигадуй стороннього лору. Пиши яскраво й конкретно.";
        }

        public static string BuildContext(ArtMeta meta)
        {
            if (meta == null) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("[Artwork]");
            sb.AppendLine($"ThingLabel: {meta.ThingLabel}");
            sb.AppendLine($"DefName: {meta.DefName}");

            var def = meta.Thing?.def;
            if (def != null)
            {
                sb.AppendLine($"Category: {def.category}");
                sb.AppendLine($"IsArtBuilding: {def.IsArt}");
                sb.AppendLine($"IsWeapon: {def.IsWeapon}");
                sb.AppendLine($"IsApparel: {def.IsApparel}");
            }

            if (meta.CompArt != null)
                sb.AppendLine($"HasArtTag: {meta.CompArt.CanShowArt}");

            if (meta.Thing?.TryGetComp<RimWorld.CompBladelinkWeapon>() != null)
                sb.AppendLine("PersonaWeapon: True");

            if (meta.Quality.HasValue)
                sb.AppendLine($"Quality: {meta.Quality.Value}");

            if (!string.IsNullOrWhiteSpace(meta.OriginalTitle))
                sb.AppendLine($"OriginalTitle: {meta.OriginalTitle}");

            if (!string.IsNullOrWhiteSpace(meta.AuthorName))
                sb.AppendLine($"Author: {meta.AuthorName}");

            if (!string.IsNullOrWhiteSpace(meta.OriginalDescription))
                sb.AppendLine($"OriginalDescription: {meta.OriginalDescription}");

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
