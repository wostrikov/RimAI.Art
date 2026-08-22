using System.Reflection;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Art.Integration
{
    /// <summary>
    /// Fail-closed inspect safety for CompArt. Vanilla GenerateImageDescription
    /// NREs when taleRef is missing; a player click must not reach that.
    /// </summary>
    public static class ArtClickSafety
    {
        static readonly FieldInfo TaleRefField = typeof(CompArt).GetField(
            "taleRef",
            BindingFlags.Instance | BindingFlags.NonPublic);
        public static bool TryGetSafeImageDescription(CompArt art, out TaggedString result)
        {
            result = TaggedString.Empty;
            if (art?.parent == null)
                return true;

            if (ArtCacheUtil.TryGetRecord(art.parent, out var record) &&
                !string.IsNullOrWhiteSpace(record.Text))
            {
                result = record.Text;
                return true;
            }

            if (!IsVanillaDescriptionSafe(art))
            {
                result = Fallback(art);
                return true;
            }

            return false;
        }

        public static bool IsVanillaDescriptionSafe(CompArt art)
        {
            if (art?.parent == null)
                return false;
            if (!art.CanShowArt)
                return false;
            if (TaleRefField == null || TaleRefField.GetValue(art) == null)
                return false;
            if (string.IsNullOrWhiteSpace(art.AuthorName) && string.IsNullOrWhiteSpace(art.Title))
                return false;
            return true;
        }

        static TaggedString Fallback(CompArt art)
        {
            if (ArtCacheUtil.TryBuildDescription(
                    ArtCacheUtil.TryGetRecord(art.parent, out var record) ? record : null,
                    art.AuthorName,
                    out var composed)
                && !string.IsNullOrWhiteSpace(composed))
            {
                return composed;
            }

            string flavor = art.parent.def?.description;
            return string.IsNullOrWhiteSpace(flavor) ? TaggedString.Empty : new TaggedString(flavor);
        }
    }
}
