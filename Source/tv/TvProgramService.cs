using System.Text;
using System.Threading.Tasks;
using Ustas.RimAI.Art.llm;
using Ustas.RimAI.Art.settings;
using Ustas.RimAI.Art.storage.save;
using RimWorld;
using Verse;
using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Art.tv
{
    public static class TvProgramService
    {
        public static TvProgramContent GetContent(Thing tvBuilding)
        {
            if (tvBuilding == null || tvBuilding.DestroyedOrNull()) return null;

            var cache = LiteratureSaveData.Current?.TvProgramCache;
            if (cache == null) return null;

            if (!TvProgramKeyProvider.TryGetKey(tvBuilding, out var key)) return null;
            if (!cache.TryGet(key, out var record)) return null;

            return record.ToContent();
        }

        public static string BuildTvSnippet(TvProgramRecord record)
        {
            if (record == null) return null;

            var title = record.Title ?? string.Empty;
            var content = record.Content ?? string.Empty;

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(content)) return null;

            if (content.Length > LiteratureSettingsDef.TvContentMaxChars)
                content = content.Substring(0, LiteratureSettingsDef.TvContentMaxChars).TrimEnd();

            var sb = new StringBuilder();
            sb.AppendLine("[Television Program]");

            if (!string.IsNullOrWhiteSpace(title))
                sb.AppendLine($"Program: {title}");

            if (!string.IsNullOrWhiteSpace(content))
                sb.AppendLine($"Content: {content}");

            return sb.ToString().TrimEnd();
        }

        public static async Task<TvProgramContent> GetOrGenerateAsync(Thing tvBuilding, Pawn contextPawn = null)
        {
            if (tvBuilding == null || tvBuilding.DestroyedOrNull()) return null;

            var cache = LiteratureSaveData.Current?.TvProgramCache;
            if (cache == null) return null;

            if (!TvProgramKeyProvider.TryGetKey(tvBuilding, out var key)) return null;

            if (cache.TryGet(key, out var existing))
                return existing.ToContent();

            var pawn = contextPawn;
            if (pawn == null) return null;

            var request = new LiteratureLlmRequest(TvProgramPromptBuilder.BuildPrompt(tvBuilding))
            {
                Context = TvProgramPromptBuilder.BuildContext(tvBuilding)
            };

            RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] TvProgramService: dispatch LLM request for {tvBuilding.def?.defName}.");
            var program = await TvProgramLlmAdapter.QueryAsync(request);
            RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] TvProgramService: LLM request completed for {tvBuilding.def?.defName} (null={program == null}).");

            program = Normalize(program);
            if (program != null)
            {
                var record = TvProgramRecord.FromGenerated(program);
                cache.Set(key, record);
            }

            return program;
        }

        private static TvProgramContent Normalize(TvProgramContent program)
        {
            if (program == null) return null;

            var title = program.Title?.Trim();
            var content = program.Content?.Trim();

            if (title != null && title.Length > LiteratureSettingsDef.TvTitleMaxChars)
                title = title.Substring(0, LiteratureSettingsDef.TvTitleMaxChars).TrimEnd();

            if (content != null && content.Length > LiteratureSettingsDef.TvContentMaxChars)
                content = content.Substring(0, LiteratureSettingsDef.TvContentMaxChars).TrimEnd();

            program.Title = title;
            program.Content = content;
            return program;
        }
    }
}
