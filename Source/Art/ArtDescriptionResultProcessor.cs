using Ustas.RimAI.Art.Art.Model;
using Ustas.RimAI.Art.Policy;

namespace Ustas.RimAI.Art.Art
{
    /// <summary>
    /// Separates provider JSON result shaping from host application / cache write.
    /// Prompt construction and transport stay outside this type.
    /// </summary>
    public static class ArtDescriptionResultProcessor
    {
        public static ArtDescription Normalize(ArtDescription description)
        {
            if (description == null) return null;

            var normalized = ArtDescriptionPipeline.Normalize(description.Title, description.Text);
            description.Title = normalized.Title;
            description.Text = normalized.Body;
            return description;
        }
    }
}
