using HarmonyLib;
using Ustas.RimAI.Communication.Prompt;
using Ustas.RimAI.Art.settings;
using Ustas.RimAI.Art.tv;
using Verse;
using Verse.AI;

namespace Ustas.RimAI.Art.patches
{
    [HarmonyPatch(typeof(ScribanParser), nameof(ScribanParser.Render),
        new[] { typeof(string), typeof(PromptContext), typeof(bool) })]
    public static class Patch_ScribanParser_TvContent
    {
        public static void Postfix(ref string __result, PromptContext context)
        {
            if (string.IsNullOrEmpty(__result)) return;

            var settings = LiteratureMod.Settings;
            if (settings == null || !settings.allowTvContent) return;

            var pawn = context?.CurrentPawn;
            if (pawn == null) return;

            var job = pawn.CurJob;
            if (job == null || job.def == null) return;

            if (job.def.defName != "WatchTelevision") return;

            var tvThing = job.targetA.Thing;
            if (tvThing == null || tvThing.DestroyedOrNull()) return;

            if (!TvFilterPolicy.IsTelevision(tvThing)) return;

            var record = GetRecord(tvThing);
            if (record == null) return;

            var snippet = TvProgramService.BuildTvSnippet(record);
            if (string.IsNullOrWhiteSpace(snippet)) return;

            __result = $"{__result}\n\n{snippet}";
        }

        private static TvProgramRecord GetRecord(Thing tvThing)
        {
            var cache = storage.save.LiteratueSaveData.Current?.TvProgramCache;
            if (cache == null) return null;

            if (!TvProgramKeyProvider.TryGetKey(tvThing, out var key)) return null;
            cache.TryGet(key, out var record);
            return record;
        }
    }
}
