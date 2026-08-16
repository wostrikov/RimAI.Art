/*
 * Purpose:
 * - Temporarily override RimTalk prompt behavior for specific requests
 *   (e.g. memory summary generation).
 *
 * Uses:
 * - RimTalk PromptService
 * - PromptOverrideService
 *
 * Responsibilities:
 * - Intercept prompt building at request scope.
 * - Inject custom instruction when explicitly enabled.
 *
 * Design notes:
 * - Overrides must be reversible and request-local.
 *
 * Do NOT:
 * - Do not change Constant.Instruction globally.
 * - Do not affect unrelated TalkRequests.
 */
using HarmonyLib;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Service;
using Ustas.RimAI.Art.integration;
using Ustas.RimAI.Art.promptoverride;

namespace Ustas.RimAI.Art.patches
{
    [HarmonyPatch(typeof(PromptService), nameof(PromptService.DecoratePrompt))]
    public static class Patch_PromptService_Override
    {
        public static void Postfix(TalkRequest talkRequest)
        {
            if (talkRequest == null) return;

            var ctx = PromptOverrideService.Consume();
            if (ctx != null && ctx.HasOverride)
            {
                if (!string.IsNullOrWhiteSpace(ctx.OverridePrompt))
                {
                    talkRequest.Prompt = ctx.OverridePrompt;
                }

                if (!string.IsNullOrWhiteSpace(ctx.AppendPrompt))
                {
                    if (string.IsNullOrWhiteSpace(talkRequest.Prompt))
                        talkRequest.Prompt = ctx.AppendPrompt;
                    else
                        talkRequest.Prompt = $"{talkRequest.Prompt}\n{ctx.AppendPrompt}";
                }
            }

            TalkPromptBookInjector.InjectIfAvailable(talkRequest);
        }
    }
}
