using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Art.integration;
using Ustas.RimAI.Art.promptoverride;
using Ustas.RimAI.Core.Communication;

namespace Ustas.RimAI.Art.patches
{
    /// <summary>
    /// Talk-path contributor: prompt override + book inject via
    /// <see cref="TalkLifecycle.PromptDecorated"/>. Not a Harmony patch.
    /// Register/Unregister are idempotent; Stop clears <c>_registered</c> so Start can re-subscribe.
    /// </summary>
    public static class Patch_PromptService_Override
    {
        static bool _registered;

        public static bool IsRegistered => _registered;

        public static void Register()
        {
            if (_registered)
                return;
            TalkLifecycle.PromptDecorated += OnPromptDecorated;
            _registered = true;
        }

        /// <summary>
        /// Unsubscribes Art-owned handler only. Does not call TalkLifecycle.Clear().
        /// </summary>
        public static void Unregister()
        {
            if (!_registered)
                return;
            TalkLifecycle.PromptDecorated -= OnPromptDecorated;
            _registered = false;
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
