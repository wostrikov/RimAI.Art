using HarmonyLib;
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
        }
    }
}
