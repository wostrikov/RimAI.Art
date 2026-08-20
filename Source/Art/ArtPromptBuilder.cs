using System.Text;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Art.Settings.Util;
using Ustas.RimAI.Art.Settings;
using Ustas.RimAI.Art.Synopsis;
using Ustas.RimAI.Core.Art;
using Verse;

namespace Ustas.RimAI.Art.Art
{
    public static class ArtPromptBuilder
    {
        public static string BuildPrompt(ArtMeta meta)
        {
            int tokenTarget = GetTokenTarget();
            var settings = LiteratureMod.Settings;
            string template = ArtPromptDefaults.BuildDefaultInstructionTemplate(tokenTarget, Constant.Lang);
            return PromptTemplateUtil.Resolve(
                settings?.promptArt,
                template,
                ("LANG", Constant.Lang),
                ("TITLE_MAX_CHARS", ArtPromptDefaults.TitleMaxChars.ToString()),
                ("SYNOPSIS_MAX_CHARS", ArtPromptDefaults.SynopsisMaxChars.ToString()),
                ("TOKEN_TARGET", tokenTarget.ToString()));
        }

        public static string BuildDefaultPrompt()
        {
            int tokenTarget = GetTokenTarget();
            string template = ArtPromptDefaults.BuildDefaultInstructionTemplate(tokenTarget, Constant.Lang);
            return PromptTemplateUtil.ApplyTokens(
                template,
                ("LANG", Constant.Lang),
                ("TITLE_MAX_CHARS", ArtPromptDefaults.TitleMaxChars.ToString()),
                ("SYNOPSIS_MAX_CHARS", ArtPromptDefaults.SynopsisMaxChars.ToString()),
                ("TOKEN_TARGET", tokenTarget.ToString()));
        }

        public static string BuildContext(ArtMeta meta)
        {
            if (meta == null) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine(ArtPromptDefaults.ContextArtworkHeader);
            sb.AppendLine(ArtPromptDefaults.ContextThingLabelPrefix + meta.ThingLabel);
            sb.AppendLine(ArtPromptDefaults.ContextDefNamePrefix + meta.DefName);

            var def = meta.Thing?.def;
            if (def != null)
            {
                sb.AppendLine(ArtPromptDefaults.ContextCategoryPrefix + def.category);
                sb.AppendLine(ArtPromptDefaults.ContextIsArtBuildingPrefix + def.IsArt);
                sb.AppendLine(ArtPromptDefaults.ContextIsWeaponPrefix + def.IsWeapon);
                sb.AppendLine(ArtPromptDefaults.ContextIsApparelPrefix + def.IsApparel);
            }

            if (meta.CompArt != null)
                sb.AppendLine(ArtPromptDefaults.ContextHasArtTagPrefix + meta.CompArt.CanShowArt);

            if (meta.Thing?.TryGetComp<RimWorld.CompBladelinkWeapon>() != null)
                sb.AppendLine(ArtPromptDefaults.ContextPersonaWeaponLine);

            if (meta.Quality.HasValue)
                sb.AppendLine(ArtPromptDefaults.ContextQualityPrefix + meta.Quality.Value);

            if (!string.IsNullOrWhiteSpace(meta.OriginalTitle))
                sb.AppendLine(ArtPromptDefaults.ContextOriginalTitlePrefix + meta.OriginalTitle);

            if (!string.IsNullOrWhiteSpace(meta.AuthorName))
                sb.AppendLine(ArtPromptDefaults.ContextAuthorPrefix + meta.AuthorName);

            if (!string.IsNullOrWhiteSpace(meta.OriginalDescription))
                sb.AppendLine(ArtPromptDefaults.ContextOriginalDescriptionPrefix + meta.OriginalDescription);

            return sb.ToString().TrimEnd();
        }

        private static int GetTokenTarget()
        {
            var settings = LiteratureMod.Settings;
            int target = settings?.synopsisTokenTarget ?? ArtPromptDefaults.DefaultSynopsisTokenTarget;
            return ArtPromptDefaults.ClampTokenTarget(target);
        }
    }
}
