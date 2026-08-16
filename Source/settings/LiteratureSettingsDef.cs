/*
 * Purpose:
 * - Centralize default values and constants for settings UI.
 *
 * Examples:
 * - defaultBaseUrl = "https://api.openai.com/v1"
 * - defaultModel is blank until the user explicitly configures an independent provider
 * - max input lengths
 *
 * Do NOT:
 * - Do not import RimTalk Constant.Instruction or override it.
 */
namespace Ustas.RimAI.Art.settings
{
    public static class LiteratureSettingsDef
    {
        public const string DefaultBaseUrl = "";
        public const string DefaultModel = "";

        public const int MaxBaseUrlLength = 200;
        public const int MaxApiKeyLength = 200;
        public const int MaxModelLength = 120;
        public const int MaxPromptLength = 8000;

        public const int DefaultSynopsisTokenTarget = 340;
        public const int MinSynopsisTokenTarget = 120;
        public const int MaxSynopsisTokenTarget = 800;
        public const int StoryTokenBonus = 220;

        public const float RowHeight = 24f;
        public const float FieldGap = 6f;
        public const float PromptTextHeight = 140f;

        public const int TvTitleMaxChars = 80;
        public const int TvContentMaxChars = 800;
    }
}
