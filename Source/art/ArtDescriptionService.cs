using System.Threading.Tasks;
using Ustas.RimAI.Art.art.model;
using Ustas.RimAI.Art.llm;
using Ustas.RimAI.Art.storage;
using Ustas.RimAI.Art.storage.save;
using Ustas.RimAI.Art.synopsis;
using Ustas.RimAI.Art.synopsis.llm;
using Verse;

namespace Ustas.RimAI.Art.art
{
    public static class ArtDescriptionService
    {
        public static async Task<ArtDescription> GetOrGenerateAsync(ArtMeta meta, Pawn contextPawn = null)
        {
            if (meta == null) return null;

            var cache = LiteratueSaveData.Current?.ArtCache;
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

            Log.Message($"[RimAI.Art] ArtDescriptionService: dispatch LLM request for {meta.DefName}.");
            var result = await IndependentBookLlmClient.QueryJsonAsync<ArtDescription>(request);
            Log.Message($"[RimAI.Art] ArtDescriptionService: LLM request completed for {meta.DefName} (null={result == null}).");
            return Normalize(result);
        }

        private static ArtDescription Normalize(ArtDescription description)
        {
            if (description == null) return null;

            var title = description.Title?.Trim();
            var text = description.Text?.Trim();

            if (title != null && title.Length > SynopsisTokenPolicy.TitleMaxChars)
                title = title.Substring(0, SynopsisTokenPolicy.TitleMaxChars).TrimEnd();

            if (text != null && text.Length > SynopsisTokenPolicy.SynopsisMaxChars)
                text = text.Substring(0, SynopsisTokenPolicy.SynopsisMaxChars).TrimEnd();

            description.Title = title;
            description.Text = text;
            return description;
        }
    }
}
