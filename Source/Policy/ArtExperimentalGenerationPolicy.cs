using System.Collections.Generic;

namespace Ustas.RimAI.Art.Policy
{
    /// <summary>
    /// Authoritative toggle gates for ideology flavor, letter rewrite,
    /// easter letters, quest rewrite, and experimental quest generation.
    /// Empty allow-lists fail closed. Auto-scheduled experimental quests
    /// stay off; debug/manual dispatch remains separately gated.
    /// </summary>
    public static class ArtExperimentalGenerationPolicy
    {
        public const bool AutoScheduleExperimentalQuests = false;

        public static bool AllowIdeology(bool allowIdeoDescriptionRewrite) =>
            allowIdeoDescriptionRewrite;

        public static bool AllowLetterRewrite(bool allowLetterTextRewrite) =>
            allowLetterTextRewrite;

        public static bool AllowEasterLetters(bool featureEnabled, bool allowEasterLetters) =>
            featureEnabled && allowEasterLetters;

        public static bool AllowListed(string defName, IReadOnlyList<string> allowList)
        {
            if (string.IsNullOrWhiteSpace(defName))
                return false;
            if (allowList == null || allowList.Count == 0)
                return false;
            for (int i = 0; i < allowList.Count; i++)
            {
                if (string.Equals(allowList[i], defName, System.StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public static bool AllowQuestRewrite(bool featureEnabled, string questDefName, IReadOnlyList<string> allowList) =>
            featureEnabled && AllowListed(questDefName, allowList);

        public static bool AllowManualExperimentalQuest(bool featureEnabled) =>
            featureEnabled;

        public static bool AllowAutoExperimentalQuest(bool featureEnabled) =>
            featureEnabled && AutoScheduleExperimentalQuests;
    }
}
