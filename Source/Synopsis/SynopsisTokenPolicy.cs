/*
 * File: SynopsisTokenPolicy.cs
 *
 * Purpose:
 * - Define token/length limits for book synopsis generation.
 *
 * Dependencies:
 * - None
 *
 * Responsibilities:
 * - Provide max token or word-count guidance.
 *
 * Do NOT:
 * - Do not read user settings.
 * - Do not choose model/provider.
 */
namespace Ustas.RimAI.Art.Synopsis
{
    public static class SynopsisTokenPolicy
    {
        public const int TitleMaxChars = Ustas.RimAI.Core.Art.ArtPromptDefaults.TitleMaxChars;
        public const int SynopsisMaxChars = Ustas.RimAI.Core.Art.ArtPromptDefaults.SynopsisMaxChars;
        public const int SynopsisMaxSentences = Ustas.RimAI.Core.Art.ArtPromptDefaults.SynopsisMaxSentences;
        public const int PromptSynopsisMaxChars = Ustas.RimAI.Core.Art.ArtPromptDefaults.PromptSynopsisMaxChars;
    }
}
