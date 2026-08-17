using HarmonyLib;
using Ustas.RimAI.Art.patches;
using Ustas.RimAI.Core.Handshake;
using Verse;

namespace Ustas.RimAI.Art
{
    [StaticConstructorOnStartup]
    public static class Startup
    {
        static Startup()
        {
            if (!RimAiHandshake.IsApproved(RimAiModuleIds.Art))
            {
                return;
            }

            var harmony = new Harmony("Ustas.RimAI.Art");
            harmony.PatchAll();
            Patch_PromptService_Override.Register();
            Patch_ScribanParser_TvContent.Register();
            RimAiHandshakeRegistry.Current.MarkActivated(RimAiModuleIds.Art);
        }
    }
}
