using System.Text;

namespace Ustas.RimAI.Art.Policy
{
    /// <summary>
    /// Authoritative experience-book authoring: a named pawn author plus a
    /// usable memory summary. Fail-closed when either is missing.
    /// </summary>
    public static class ArtExperienceBookPolicy
    {
        public const string MemorySummaryMarker = "[MemorySummary]";
        public const string KeywordsPrefix = "Keywords: ";
        public const string TonePrefix = "Tone: ";

        public static bool HasUsableSummary(string summary) =>
            !string.IsNullOrWhiteSpace(summary);

        public static bool CanAuthorFromExperience(bool hasAuthor, string summary) =>
            hasAuthor && HasUsableSummary(summary);

        public static bool ShouldDispatchExperienceAuthoring(bool hasAuthor, bool hasSummaryRequest) =>
            hasAuthor && hasSummaryRequest;

        public static string ComposeMemoryContext(
            string baseContext,
            string summary,
            string[] keywords,
            string tone)
        {
            if (!HasUsableSummary(summary))
                return null;

            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(baseContext))
                sb.AppendLine(baseContext.TrimEnd());

            sb.AppendLine(MemorySummaryMarker);
            sb.AppendLine(summary.Trim());

            if (keywords != null && keywords.Length > 0)
                sb.AppendLine(KeywordsPrefix + string.Join(", ", keywords));

            if (!string.IsNullOrWhiteSpace(tone))
                sb.AppendLine(TonePrefix + tone.Trim());

            return sb.ToString().TrimEnd();
        }
    }
}
