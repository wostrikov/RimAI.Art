using System.Text;

namespace Ustas.RimAI.Art.Policy
{
    public enum ArtReadingInjectAction
    {
        SkipDisabled,
        SkipNoBook,
        SkipFiltered,
        EnqueueMissing,
        Inject
    }

    /// <summary>
    /// Authoritative reading-context injection for TalkRequest.
    /// Injects only when a pawn is reading an allowed book with a cached synopsis.
    /// </summary>
    public static class ArtReadingDialoguePolicy
    {
        public const string BookMarker = "[Book]";
        public const string TitlePrefix = "Title: ";
        public const string TextPrefix = "Text: ";

        public static ArtReadingInjectAction Decide(
            bool featureEnabled,
            bool hasBook,
            bool bookAllowed,
            bool hasCachedSynopsis)
        {
            if (!featureEnabled)
                return ArtReadingInjectAction.SkipDisabled;
            if (!hasBook)
                return ArtReadingInjectAction.SkipNoBook;
            if (!bookAllowed)
                return ArtReadingInjectAction.SkipFiltered;
            if (!hasCachedSynopsis)
                return ArtReadingInjectAction.EnqueueMissing;
            return ArtReadingInjectAction.Inject;
        }

        public static string BuildSnippet(string title, string text, int maxChars)
        {
            string body = text ?? string.Empty;
            if (maxChars > 0 && body.Length > maxChars)
                body = body.Substring(0, maxChars).TrimEnd();

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
                return null;

            var sb = new StringBuilder();
            sb.AppendLine(BookMarker);
            if (!string.IsNullOrWhiteSpace(title))
                sb.AppendLine(TitlePrefix + title.Trim());
            if (!string.IsNullOrWhiteSpace(body))
                sb.AppendLine(TextPrefix + body);
            return sb.ToString().TrimEnd();
        }

        public static string AppendToContext(string existing, string snippet)
        {
            if (string.IsNullOrWhiteSpace(snippet))
                return existing;
            if (string.IsNullOrWhiteSpace(existing))
                return snippet;
            return existing + "\n\n" + snippet;
        }
    }
}
