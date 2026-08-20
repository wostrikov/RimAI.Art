using Ustas.RimAI.Communication.Prompt;
using Ustas.RimAI.Art.Settings;
using Ustas.RimAI.Art.TV;
using Ustas.RimAI.Core.Communication;
using Verse;
using Verse.AI;

namespace Ustas.RimAI.Art.Patches
{
    /// <summary>
    /// Talk-path contributor: TV snippet inject via <see cref="TalkLifecycle.ScribanRendered"/>.
    /// Not a Harmony patch. Register/Unregister are idempotent; Stop clears <c>_registered</c>
    /// so Start can re-subscribe.
    /// </summary>
    public static class Patch_ScribanParser_TvContent
    {
        static bool _registered;

        public static bool IsRegistered => _registered;

        public static void Register()
        {
            if (_registered)
                return;
            TalkLifecycle.ScribanRendered += OnScribanRendered;
            _registered = true;
        }

        /// <summary>
        /// Unsubscribes Art-owned handler only. Does not call TalkLifecycle.Clear().
        /// </summary>
        public static void Unregister()
        {
            if (!_registered)
                return;
            TalkLifecycle.ScribanRendered -= OnScribanRendered;
            _registered = false;
        }

        static void OnScribanRendered(ScribanRenderedArgs args)
        {
            if (args == null || string.IsNullOrEmpty(args.Result))
                return;
            if (args.Context is not PromptContext context)
                return;

            var settings = LiteratureMod.Settings;
            if (settings == null || !settings.allowTvContent) return;

            var pawn = context.CurrentPawn;
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

            args.Result = $"{args.Result}\n\n{snippet}";
        }

        private static TvProgramRecord GetRecord(Thing tvThing)
        {
            var cache = Storage.Save.LiteratureSaveData.Current?.TvProgramCache;
            if (cache == null) return null;

            if (!TvProgramKeyProvider.TryGetKey(tvThing, out var key)) return null;
            cache.TryGet(key, out var record);
            return record;
        }
    }
}
