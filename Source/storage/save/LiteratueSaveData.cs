/*
 * File: LiteratueSaveData.cs
 *
 * Purpose:
 * - Persist Literature Expansion data across save/load.
 *
 * Dependencies:
 * - Verse.IExposable
 * - BookSynopsisCache (or underlying data)
 *
 * Responsibilities:
 * - Expose cached synopsis and processed-book markers.
 *
 * Do NOT:
 * - Do not perform logic during ExposeData beyond serialization.
 */
using Ustas.RimAI.Art.events.quests;
using Ustas.RimAI.Art.storage;
using Ustas.RimAI.Art.tv;
using RimWorld.Planet;
using Verse;

namespace Ustas.RimAI.Art.storage.save
{
    public sealed class LiteratueSaveData : WorldComponent
    {
        public BookSynopsisCache SynopsisCache = new BookSynopsisCache();
        public ArtDescriptionCache ArtCache = new ArtDescriptionCache();
        public IdeoDescriptionCache IdeoCache = new IdeoDescriptionCache();
        public TvProgramCache TvProgramCache = new TvProgramCache();
        public int NextAllyDiplomacyTick = -1;
        public int NextFamilyLetterTick = -1;
        public int NextAdvertQuestTick = -1;
        public int NextWarningQuestTick = -1;
        public System.Collections.Generic.List<WarningRaidRecord> WarningRaidQueue = new System.Collections.Generic.List<WarningRaidRecord>();

        public LiteratueSaveData(World world) : base(world)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref SynopsisCache, "synopsisCache");
            Scribe_Deep.Look(ref ArtCache, "artCache");
            Scribe_Deep.Look(ref IdeoCache, "ideoCache");
            Scribe_Deep.Look(ref TvProgramCache, "tvProgramCache");
            Scribe_Values.Look(ref NextAllyDiplomacyTick, "nextAllyDiplomacyTick", -1);
            Scribe_Values.Look(ref NextFamilyLetterTick, "nextFamilyLetterTick", -1);
            Scribe_Values.Look(ref NextAdvertQuestTick, "nextAdvertQuestTick", -1);
            Scribe_Values.Look(ref NextWarningQuestTick, "nextWarningQuestTick", -1);
            Scribe_Collections.Look(ref WarningRaidQueue, "warningRaidQueue", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && SynopsisCache == null)
                SynopsisCache = new BookSynopsisCache();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && ArtCache == null)
                ArtCache = new ArtDescriptionCache();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && IdeoCache == null)
                IdeoCache = new IdeoDescriptionCache();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && TvProgramCache == null)
                TvProgramCache = new TvProgramCache();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && WarningRaidQueue == null)
                WarningRaidQueue = new System.Collections.Generic.List<WarningRaidRecord>();

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                Log.Message($"[RimAI.Art] Literature data loaded. Cached synopses: {SynopsisCache?.Count ?? 0}, art: {ArtCache?.Count ?? 0}, ideos: {IdeoCache?.Count ?? 0}, ideoProcessed: {IdeoCache?.ProcessedCount ?? 0}, tv: {TvProgramCache?.Count ?? 0}, warningRaids: {WarningRaidQueue?.Count ?? 0}.");
        }

        public static LiteratueSaveData Current => Find.World?.GetComponent<LiteratueSaveData>();
    }
}
