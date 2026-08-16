/*
 * Purpose:
 * - Carry temporary prompt override state for a single TalkRequest.
 *
 * Uses:
 * - RimTalk TalkRequest
 *
 * Responsibilities:
 * - Store flags / override text used during prompt construction.
 *
 * Design notes:
 * - Lifetime is strictly per-request.
 *
 * Do NOT:
 * - Do not persist this object.
 * - Do not apply overrides globally.
 */
namespace Ustas.RimAI.Art.promptoverride
{
    public sealed class PromptOverrideContext
    {
        public string OverridePrompt { get; }
        public string AppendPrompt { get; }

        public PromptOverrideContext(string overridePrompt = null, string appendPrompt = null)
        {
            OverridePrompt = overridePrompt;
            AppendPrompt = appendPrompt;
        }

        public bool HasOverride =>
            !string.IsNullOrWhiteSpace(OverridePrompt) ||
            !string.IsNullOrWhiteSpace(AppendPrompt);
    }
}
