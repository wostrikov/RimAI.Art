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
namespace Ustas.RimAI.Art.synopsis
{
    public static class SynopsisTokenPolicy
    {
        public const int TitleMaxChars = 60;
        public const int SynopsisMaxChars = 600;
        public const int SynopsisMaxSentences = 6;
        public const int PromptSynopsisMaxChars = 600;
    }
}
