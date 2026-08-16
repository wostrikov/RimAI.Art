using HarmonyLib;
using Ustas.RimAI.Art.patches;
using Verse;

namespace Ustas.RimAI.Art
{
    [StaticConstructorOnStartup]
    public static class Startup
    {
        static Startup()
        {
            var harmony = new Harmony("Ustas.RimAI.Art");
            harmony.PatchAll();
            Patch_PromptService_Override.Register();
            Patch_ScribanParser_TvContent.Register();
        }
    }
}
