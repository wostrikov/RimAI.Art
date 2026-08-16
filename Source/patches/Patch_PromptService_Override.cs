using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Art.integration;
using Ustas.RimAI.Art.promptoverride;
using Ustas.RimAI.Core.Communication;

namespace Ustas.RimAI.Art.patches
{
    public static class Patch_PromptService_Override
    {
        static bool _registered;

        public static void Register()
        {
            if (_registered)
                return;
            _registered = true;
            TalkLifecycle.PromptDecorated += OnPromptDecorated;
        }

        static void OnPromptDecorated(object talkRequestObj, object _, string __)
        {
            if (talkRequestObj is not TalkRequest talkRequest)
                return;

            var ctx = PromptOverrideService.Consume();
            if (ctx != null && ctx.HasOverride)
            {
                if (!string.IsNullOrWhiteSpace(ctx.OverridePrompt))
                    talkRequest.Prompt = ctx.OverridePrompt;

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
