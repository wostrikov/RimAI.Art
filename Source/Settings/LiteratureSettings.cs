/*
 * Purpose:
 * - Persist core feature toggles for Literature Expansion.
 *
 * Uses:
 * - Verse.ModSettings / IExposable
 *
 * Fields:
 * - enabled: allow book edits
 * - useRimTalkApi: if true, reuse RimTalk Settings_Api / ApiConfig at runtime
 * - allowArtBuildingEdits: allow art edits for buildings
 * - allowArtWeaponEdits: allow art edits for weapons
 * - allowArtApparelEdits: allow art edits for apparel/gear
 *
 * Responsibilities:
 * - ExposeData() for save/load of settings.
 *
 * Do NOT:
 * - Do not store secrets here if you intend to share settings files publicly.
 *   (If you must store API key, keep it in LiteratureSettingsApi with clear UI warnings.)
 */
using System.Collections.Generic;
using Ustas.RimAI.Art.Art;
using Ustas.RimAI.Art.Books;
using Verse;

namespace Ustas.RimAI.Art.Settings
{
    public sealed class LiteratureSettings : ModSettings
    {
        public bool enabled = true;
        public bool useRimTalkApi = true;
        public LiteratureSettingsApi api = new LiteratureSettingsApi();
        public int synopsisTokenTarget = LiteratureSettingsDef.DefaultSynopsisTokenTarget;
        // Legacy: kept for migration from older settings saves.
        public bool allowArtEdits = false;
        public bool allowArtLabelEdits = false;
        public bool allowArtBuildingEdits = false;
        public bool allowArtWeaponEdits = false;
        public bool allowArtApparelEdits = false;
        public List<string> bookRewriteAllowList = new List<string>();
        public bool bookRewriteAllowListInitialized;
        public List<string> artRewriteAllowList = new List<string>();
        public bool artRewriteAllowListInitialized;
        public List<string> questRewriteAllowList = new List<string>();
        public bool allowIdeoDescriptionRewrite = false;
        public bool allowLetterTextRewrite = false;
        public bool allowManualBookEdits = true;
        public bool allowManualArtEdits = true;
        public bool allowTvContent = true;
        public bool allowManualTvEdits = true;
        public List<string> letterRewriteAllowList = new List<string>();
        public bool allowEasterLetters = true;
        public string promptSynopsis = string.Empty;
        public string promptArt = string.Empty;
        public string promptPersonaWeapon = string.Empty;
        public string promptJournal = string.Empty;
        public string promptMemorySummary = string.Empty;
        public string promptBookFromSummary = string.Empty;
        public string promptLetterRewrite = string.Empty;
        public string promptIdeoRewrite = string.Empty;
        public string promptQuestAdvert = string.Empty;
        public string promptQuestWarning = string.Empty;
        public string promptTvProgram = string.Empty;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref useRimTalkApi, "useRimTalkApi", true);
            Scribe_Deep.Look(ref api, "api");
            Scribe_Values.Look(ref synopsisTokenTarget, "synopsisTokenTarget", LiteratureSettingsDef.DefaultSynopsisTokenTarget);
            Scribe_Values.Look(ref allowArtEdits, "allowArtEdits", false);
            Scribe_Values.Look(ref allowArtLabelEdits, "allowArtLabelEdits", false);
            Scribe_Values.Look(ref allowArtBuildingEdits, "allowArtBuildingEdits", false);
            Scribe_Values.Look(ref allowArtWeaponEdits, "allowArtWeaponEdits", false);
            Scribe_Values.Look(ref allowArtApparelEdits, "allowArtApparelEdits", false);
            Scribe_Collections.Look(ref bookRewriteAllowList, "bookRewriteAllowList", LookMode.Value);
            Scribe_Values.Look(ref bookRewriteAllowListInitialized, "bookRewriteAllowListInitialized", false);
            Scribe_Collections.Look(ref artRewriteAllowList, "artRewriteAllowList", LookMode.Value);
            Scribe_Values.Look(ref artRewriteAllowListInitialized, "artRewriteAllowListInitialized", false);
            Scribe_Collections.Look(ref questRewriteAllowList, "questRewriteAllowList", LookMode.Value);
            Scribe_Values.Look(ref allowIdeoDescriptionRewrite, "allowIdeoDescriptionRewrite", false);
            Scribe_Values.Look(ref allowLetterTextRewrite, "allowLetterTextRewrite", false);
            Scribe_Values.Look(ref allowManualBookEdits, "allowManualBookEdits", true);
            Scribe_Values.Look(ref allowManualArtEdits, "allowManualArtEdits", true);
            Scribe_Values.Look(ref allowTvContent, "allowTvContent", true);
            Scribe_Values.Look(ref allowManualTvEdits, "allowManualTvEdits", true);
            Scribe_Collections.Look(ref letterRewriteAllowList, "letterRewriteAllowList", LookMode.Value);
            Scribe_Values.Look(ref allowEasterLetters, "allowEasterLetters", true);
            Scribe_Values.Look(ref promptSynopsis, "promptSynopsis", string.Empty);
            Scribe_Values.Look(ref promptArt, "promptArt", string.Empty);
            Scribe_Values.Look(ref promptPersonaWeapon, "promptPersonaWeapon", string.Empty);
            Scribe_Values.Look(ref promptJournal, "promptJournal", string.Empty);
            Scribe_Values.Look(ref promptMemorySummary, "promptMemorySummary", string.Empty);
            Scribe_Values.Look(ref promptBookFromSummary, "promptBookFromSummary", string.Empty);
            Scribe_Values.Look(ref promptLetterRewrite, "promptLetterRewrite", string.Empty);
            Scribe_Values.Look(ref promptIdeoRewrite, "promptIdeoRewrite", string.Empty);
            Scribe_Values.Look(ref promptQuestAdvert, "promptQuestAdvert", string.Empty);
            Scribe_Values.Look(ref promptQuestWarning, "promptQuestWarning", string.Empty);
            Scribe_Values.Look(ref promptTvProgram, "promptTvProgram", string.Empty);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && api == null)
                api = new LiteratureSettingsApi();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && synopsisTokenTarget <= 0)
                synopsisTokenTarget = LiteratureSettingsDef.DefaultSynopsisTokenTarget;
            if (Scribe.mode == LoadSaveMode.PostLoadInit && bookRewriteAllowList == null)
                bookRewriteAllowList = new List<string>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && artRewriteAllowList == null)
                artRewriteAllowList = new List<string>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && questRewriteAllowList == null)
                questRewriteAllowList = new List<string>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && letterRewriteAllowList == null)
                letterRewriteAllowList = new List<string>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // In inherited mode RimTalk owns the OpenAI credential. Do not retain
                // a duplicate legacy key in this add-on's mutable settings.
                if (useRimTalkApi && api != null)
                    api.apiKey = string.Empty;

                bool anyCategory = allowArtBuildingEdits || allowArtWeaponEdits || allowArtApparelEdits;
                if (allowArtEdits && !anyCategory)
                {
                    allowArtBuildingEdits = true;
                    allowArtWeaponEdits = true;
                    allowArtApparelEdits = true;
                }

                EnsureContentFiltersInitialized();
            }
        }

        public void EnsureContentFiltersInitialized()
        {
            bookRewriteAllowList ??= new List<string>();
            artRewriteAllowList ??= new List<string>();

            if (!bookRewriteAllowListInitialized)
            {
                var allBooks = BookFilterPolicy.GetAllEligibleDefNames();
                if (allBooks.Count > 0)
                {
                    bookRewriteAllowList = allBooks;
                    bookRewriteAllowListInitialized = true;
                }
            }

            if (!artRewriteAllowListInitialized)
            {
                var allArt = ArtDefFilterPolicy.GetAllEligibleDefNames();
                if (allArt.Count > 0)
                {
                    artRewriteAllowList = allArt;
                    artRewriteAllowListInitialized = true;
                }
            }
        }
    }
}
