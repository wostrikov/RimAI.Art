using Ustas.RimAI.Art.art.model;
using Ustas.RimAI.Art.synopsis;

namespace Ustas.RimAI.Art.art
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
