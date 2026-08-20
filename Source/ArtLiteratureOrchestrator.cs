using System.Threading.Tasks;
using Ustas.RimAI.Art.Art;
using Ustas.RimAI.Art.Art.Model;
using Verse;

namespace Ustas.RimAI.Art
{
    /// <summary>
    /// Thin literature generation entry owned by <see cref="ArtComposition"/>.
    /// Does not own HTTP, prompt templates, or cache persistence — delegates to existing services.
    /// </summary>
    public sealed class ArtLiteratureOrchestrator
    {
        public bool IsAcceptingWork => ArtComposition.Current.IsStarted;

        public Task<ArtDescription> GetOrGenerateArtDescriptionAsync(ArtMeta meta, Pawn contextPawn = null)
        {
            if (!IsAcceptingWork)
                return Task.FromResult<ArtDescription>(null);
            return ArtDescriptionService.GetOrGenerateAsync(meta, contextPawn);
        }
    }
}
