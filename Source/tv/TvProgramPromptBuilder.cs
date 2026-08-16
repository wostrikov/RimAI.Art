using System.Text;
using Ustas.RimAI.Art.settings;
using Ustas.RimAI.Art.settings.util;
using RimWorld;
using Verse;
using Ustas.RimAI.Communication.Data;

namespace Ustas.RimAI.Art.tv
{
    public static class TvProgramPromptBuilder
    {
        public static string BuildPrompt(Thing tvBuilding)
        {
            var settings = LiteratureMod.Settings;
            string template = BuildTemplate();
            return PromptTemplateUtil.Resolve(
                settings?.promptTvProgram,
                template,
                ("LANG", Constant.Lang),
                ("TITLE_MAX_CHARS", LiteratureSettingsDef.TvTitleMaxChars.ToString()),
                ("CONTENT_MAX_CHARS", LiteratureSettingsDef.TvContentMaxChars.ToString()));
        }

        public static string BuildDefaultPrompt()
        {
            string template = BuildTemplate();
            return PromptTemplateUtil.ApplyTokens(
                template,
                ("LANG", Constant.Lang),
                ("TITLE_MAX_CHARS", LiteratureSettingsDef.TvTitleMaxChars.ToString()),
                ("CONTENT_MAX_CHARS", LiteratureSettingsDef.TvContentMaxChars.ToString()));
        }

        private static string BuildTemplate()
        {
            return
$@"Ти пишеш внутрішньосвітовий вміст телепрограми, яку персонаж RimWorld дивиться просто зараз.
Пиши мовою {Constant.Lang}. Виведи лише JSON.

Обов'язкові поля JSON:
- ""title"" (назва програми, <= {LiteratureSettingsDef.TvTitleMaxChars} символів)
- ""content"" (що відбувається в програмі зараз, <= {LiteratureSettingsDef.TvContentMaxChars} символів)

Обмеження:
- Вміст має відображати контекст ігрового світу (події колонії, політику фракцій, життя тварин, наслідки рейдів, торгівлю тощо).
- Варіюй тон: випуск новин, документальна програма, драма, комедія, поради з виживання або арена в давньоримському стилі — що доречніше.
- НЕ згадуй реальні земні телешоу, бренди чи знаменитостей.
- Використовуй наданий ігровий контекст (пора року, погода, час доби) як творче натхнення.
- Пиши так, ніби pawn пасивно сприймає цей вміст під час перегляду.";
        }

        public static string BuildContext(Thing tvBuilding)
        {
            if (tvBuilding == null) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("[Television]");
            sb.AppendLine($"DefName: {tvBuilding.def?.defName ?? "unknown"}");
            sb.AppendLine($"Label: {tvBuilding.LabelNoCount ?? "TV"}");

            var map = tvBuilding.Map;
            if (map != null)
            {
                var ticks = Find.TickManager.TicksAbs;
                var longLat = Find.WorldGrid.LongLatOf(map.Tile);

                sb.AppendLine($"Hour: {GenDate.HourOfDay(ticks, longLat.x)}");
                sb.AppendLine($"Season: {GenLocalDate.Season(map).Label()}");
                sb.AppendLine($"Weather: {map.weatherManager?.curWeather?.label ?? "clear"}");
                sb.AppendLine($"Temperature: {UnityEngine.Mathf.RoundToInt(map.mapTemperature.OutdoorTemp)}C");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
