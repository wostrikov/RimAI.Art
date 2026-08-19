using System.Threading.Tasks;
using Ustas.RimAI.Art.art.model;
using Ustas.RimAI.Art.llm;
using Ustas.RimAI.Art.storage;
using Ustas.RimAI.Art.storage.save;
using Ustas.RimAI.Art.synopsis;
using Ustas.RimAI.Art.synopsis.llm;
using Verse;
using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Art.art
{
    public static class ArtDescriptionService
    {
        public static async Task<ArtDescription> GetOrGenerateAsync(ArtMeta meta, Pawn contextPawn = null)
        {
            if (meta == null) return null;

            var cache = LiteratureSaveData.Current?.ArtCache;
            if (cache != null && ArtKeyProvider.TryGetKey(meta.Thing, out var key))
            {
                if (cache.TryGet(key, out var record))
                    return record?.ToDescription();
            }

            var pawn = contextPawn;
            if (pawn == null) return null;

            var request = new LiteratureLlmRequest(ArtPromptBuilder.BuildPrompt(meta))
            {
                Context = ArtPromptBuilder.BuildContext(meta)
            };

            RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] ArtDescriptionService: dispatch LLM request for {meta.DefName}.");
            var result = await IndependentBookLlmClient.QueryJsonAsync<ArtDescription>(request);
            RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] ArtDescriptionService: LLM request completed for {meta.DefName} (null={result == null}).");
            return ArtDescriptionResultProcessor.Normalize(result);
        }
    }
}
