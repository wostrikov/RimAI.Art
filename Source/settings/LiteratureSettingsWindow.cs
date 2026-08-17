/*
 * Purpose:
 * - Draw the settings UI for Literature Expansion.
 *
 * Uses:
 * - Verse.Widgets / Listing_Standard
 * - LiteratureSettings + LiteratureSettingsApi
 *
 * UI requirements:
 * - Checkbox: Enable Literature Expansion
 * - Checkbox: Use same API as RimTalk
 * - If not using Ustas.RimAI.Communication API:
 *   - Text field: Base URL
 *   - Text field (masked if feasible): API Key
 *   - Text field: Model
 *   - Optional: "Test" button (LOCAL validation only)
 *
 * Design notes:
 * - Keep UI minimal and stable; avoid complex layout.
 * - Put all strings behind translation keys if you already have a translation workflow.
 *
 * Do NOT:
 * - Do not call LLM here.
 * - Do not modify RimTalk settings.
 */
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Art.book;
using Ustas.RimAI.Art.settings.util;
using Ustas.RimAI.Art.synopsis;
using Ustas.RimAI.Art.art;
using Ustas.RimAI.Art.art.llm;
using Ustas.RimAI.Art.authoring.llm;
using Ustas.RimAI.Art.journal.llm;
using Ustas.RimAI.Art.events;
using Ustas.RimAI.Art.events.quests;
using Ustas.RimAI.Art.storage.save;
using Ustas.RimAI.Art.tv;
using RimWorld;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Art.settings
{
    public static class LiteratureSettingsWindow
    {
        private const int PageMain = 0;
        private const int PageContent = 1;
        private const int PageFilters = 2;
        private const int PagePrompts = 3;
        private static int _settingsPageIndex;
        private static Vector2 _settingsScrollMain;
        private static Vector2 _settingsScrollPrompts;
        private static float _settingsViewHeightMain;
        private static float _settingsViewHeightPrompts;
        private static Vector2 _bookFilterScroll;
        private static Vector2 _artFilterScroll;
        private static Vector2 _questFilterScroll;
        private static Vector2 _letterFilterScroll;

        public static void Draw(Rect inRect, LiteratureSettings settings)
        {
            if (settings == null) return;
            settings.api ??= new LiteratureSettingsApi();
            settings.EnsureContentFiltersInitialized();

            float tabHeight = LiteratureSettingsDef.RowHeight;
            float tabGap = 6f;
            Rect tabRect = new Rect(inRect.x, inRect.y, inRect.width, tabHeight);
            DrawPageTabs(tabRect);

            Rect contentRect = new Rect(inRect.x, inRect.y + tabHeight + tabGap, inRect.width,
                inRect.height - tabHeight - tabGap);

            if (_settingsPageIndex == PageContent)
                DrawContentPage(contentRect, settings);
            else if (_settingsPageIndex == PageFilters)
                DrawFiltersPage(contentRect, settings);
            else if (_settingsPageIndex == PagePrompts)
                DrawPromptsPage(contentRect, settings);
            else
                DrawMainPage(contentRect, settings);
        }

        private static void DrawPageTabs(Rect rect)
        {
            float gap = LiteratureSettingsDef.FieldGap;
            float tabWidth = (rect.width - gap * 3f) / 4f;
            Rect mainRect = new Rect(rect.x, rect.y, tabWidth, rect.height);
            Rect contentRect = new Rect(mainRect.xMax + gap, rect.y, tabWidth, rect.height);
            Rect filterRect = new Rect(contentRect.xMax + gap, rect.y, tabWidth, rect.height);
            Rect promptRect = new Rect(filterRect.xMax + gap, rect.y, tabWidth, rect.height);

            if (Widgets.ButtonText(mainRect, "RimTalkLE_Settings_PageMain".Translate()))
                _settingsPageIndex = PageMain;
            if (Widgets.ButtonText(contentRect, "RimTalkLE_Settings_PageContent".Translate()))
                _settingsPageIndex = PageContent;
            if (Widgets.ButtonText(filterRect, "RimTalkLE_Settings_PageFilters".Translate()))
                _settingsPageIndex = PageFilters;
            if (Widgets.ButtonText(promptRect, "RimTalkLE_Settings_PagePrompts".Translate()))
                _settingsPageIndex = PagePrompts;
        }

        private static void DrawMainPage(Rect inRect, LiteratureSettings settings)
        {
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, Mathf.Max(_settingsViewHeightMain, inRect.height));
            Widgets.BeginScrollView(inRect, ref _settingsScrollMain, viewRect);

            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            listing.CheckboxLabeled("RimTalkLE_Settings_AllowBooks".Translate(), ref settings.enabled);
            listing.Gap(6f);
            listing.Label("RimTalkLE_Settings_AllowArt".Translate());
            listing.CheckboxLabeled("RimTalkLE_Settings_AllowArtBuildings".Translate(), ref settings.allowArtBuildingEdits);
            listing.CheckboxLabeled("RimTalkLE_Settings_AllowArtWeapons".Translate(), ref settings.allowArtWeaponEdits);
            listing.CheckboxLabeled("RimTalkLE_Settings_AllowArtApparel".Translate(), ref settings.allowArtApparelEdits);
            listing.CheckboxLabeled("RimTalkLE_Settings_AllowArtLabels".Translate(), ref settings.allowArtLabelEdits);
            listing.Gap(6f);
            listing.CheckboxLabeled("RimTalkLE_Settings_UseRimTalkApi".Translate(), ref settings.useRimTalkApi);
            listing.Gap(12f);

            listing.Label("RimTalkLE_Settings_TextOverrides".Translate());
            listing.Gap(4f);
            listing.CheckboxLabeled("RimTalkLE_Settings_AllowIdeoDescriptionRewrite".Translate(), ref settings.allowIdeoDescriptionRewrite);
            listing.CheckboxLabeled("RimTalkLE_Settings_AllowLetterTextRewrite".Translate(), ref settings.allowLetterTextRewrite);
            listing.CheckboxLabeled("RimTalkLE_Settings_AllowEasterLetters".Translate(), ref settings.allowEasterLetters);
            listing.Gap(12f);

            listing.Label("RimTalkLE_Settings_ManualEdits".Translate());
            listing.Gap(4f);
            listing.CheckboxLabeled("RimTalkLE_Settings_AllowManualBookEdits".Translate(), ref settings.allowManualBookEdits);
            listing.CheckboxLabeled("RimTalkLE_Settings_AllowManualArtEdits".Translate(), ref settings.allowManualArtEdits);
            listing.Gap(6f);
            listing.Label("RimTalkLE_Settings_TvContent".Translate());
            listing.Gap(4f);
            listing.CheckboxLabeled("RimTalkLE_Settings_AllowTvContent".Translate(), ref settings.allowTvContent);
            listing.CheckboxLabeled("RimTalkLE_Settings_AllowManualTvEdits".Translate(), ref settings.allowManualTvEdits);
            listing.Label("RimTalkLE_Settings_ManualEditNote".Translate());
            listing.Gap(12f);

            if (!settings.useRimTalkApi)
            {
                listing.Label("RimTalkLE_Settings_StandaloneApi".Translate());
                listing.Gap(4f);

                settings.api.baseUrl = SettingsUIHelpers.TextFieldLabeled(
                    listing,
                    "RimTalkLE_Settings_BaseUrl".Translate(),
                    settings.api.baseUrl,
                    LiteratureSettingsDef.MaxBaseUrlLength);

                settings.api.apiKey = SettingsUIHelpers.PasswordFieldLabeled(
                    listing,
                    "RimTalkLE_Settings_ApiKey".Translate(),
                    settings.api.apiKey,
                    LiteratureSettingsDef.MaxApiKeyLength);

                settings.api.model = SettingsUIHelpers.TextFieldLabeled(
                    listing,
                    "RimTalkLE_Settings_Model".Translate(),
                    settings.api.model,
                    LiteratureSettingsDef.MaxModelLength);

                var errors = settings.api.GetValidationErrors();
                if (errors != null && errors.Count > 0)
                {
                    SettingsUIHelpers.DrawValidationMessages(
                        listing,
                        "RimTalkLE_Settings_Validation".Translate(),
                        errors.ToArray());
                }
            }

            listing.Gap(12f);
            listing.Label("RimTalkLE_Settings_Debug".Translate());
            listing.Gap(4f);
            settings.synopsisTokenTarget = SettingsUIHelpers.IntFieldLabeled(
                listing,
                "RimTalkLE_Settings_TokenTarget".Translate(),
                settings.synopsisTokenTarget,
                LiteratureSettingsDef.MinSynopsisTokenTarget,
                LiteratureSettingsDef.MaxSynopsisTokenTarget);

            Rect buttonRect = listing.GetRect(LiteratureSettingsDef.RowHeight);
            if (Widgets.ButtonText(buttonRect, "RimTalkLE_Settings_ClearBookCache".Translate()))
            {
                var cache = LiteratureSaveData.Current?.SynopsisCache;
                if (cache == null)
                {
                    Log.Warning("[RimAI.Art] No active world data; cannot clear book cache.");
                }
                else
                {
                    int cleared = cache.Clear();
                    Log.Message($"[RimAI.Art] Cleared {cleared} cached book synopses.");
                }
            }

            Rect artCacheRect = listing.GetRect(LiteratureSettingsDef.RowHeight);
            if (Widgets.ButtonText(artCacheRect, "RimTalkLE_Settings_ClearArtCache".Translate()))
            {
                var cache = LiteratureSaveData.Current?.ArtCache;
                if (cache == null)
                {
                    Log.Warning("[RimAI.Art] No active world data; cannot clear art cache.");
                }
                else
                {
                    int cleared = cache.Clear();
                    Log.Message($"[RimAI.Art] Cleared {cleared} cached art descriptions.");
                }
            }

            Rect rescanRect = listing.GetRect(LiteratureSettingsDef.RowHeight);
            if (Widgets.ButtonText(rescanRect, "RimTalkLE_Settings_RescanArtBooks".Translate()))
            {
                var maps = Find.Maps;
                if (maps == null || maps.Count == 0)
                {
                    Log.Warning("[RimAI.Art] Manual rescan skipped: no active maps.");
                }
                else
                {
                    Log.Message($"[RimAI.Art] Manual rescan requested for {maps.Count} maps.");
                    bool bookEnabled = settings.enabled;
                    for (int i = 0; i < maps.Count; i++)
                    {
                        var map = maps[i];
                        Ustas.RimAI.Art.scanner.MapArtScanner.Scan(map);
                        if (bookEnabled)
                            Ustas.RimAI.Art.scanner.MapBookScanner.Scan(map, detailedLog: true);
                        else
                            Log.Message($"[RimAI.Art] Book scan skipped: books disabled (map {map?.uniqueID ?? -1}).");
                    }
                }
            }

            listing.End();
            _settingsViewHeightMain = listing.CurHeight + 10f;
            Widgets.EndScrollView();
        }

        private static void DrawFiltersPage(Rect inRect, LiteratureSettings settings)
        {
            DrawFilterColumns(inRect, settings);
        }

        private static void DrawContentPage(Rect inRect, LiteratureSettings settings)
        {
            if (settings == null) return;
            DrawContentFilterColumns(inRect, settings);
        }

        private static void DrawPromptsPage(Rect inRect, LiteratureSettings settings)
        {
            if (settings == null) return;

            if (_settingsViewHeightPrompts < 1f)
                _settingsViewHeightPrompts = GetPromptPageHeight();

            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, Mathf.Max(_settingsViewHeightPrompts, inRect.height));
            Widgets.BeginScrollView(inRect, ref _settingsScrollPrompts, viewRect);

            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            listing.Label("RimTalkLE_Settings_PromptPageTitle".Translate());
            listing.Label("RimTalkLE_Settings_PromptPageNote".Translate());
            listing.Gap(8f);

            DrawPromptField(listing, "RimTalkLE_Settings_Prompt_Synopsis".Translate(),
                ref settings.promptSynopsis, SynopsisPromptBuilder.BuildDefaultPrompt());
            DrawPromptField(listing, "RimTalkLE_Settings_Prompt_Art".Translate(),
                ref settings.promptArt, ArtPromptBuilder.BuildDefaultPrompt());
            DrawPromptField(listing, "RimTalkLE_Settings_Prompt_PersonaWeapon".Translate(),
                ref settings.promptPersonaWeapon, PersonaWeaponRequest.BuildDefaultPrompt());
            DrawPromptField(listing, "RimTalkLE_Settings_Prompt_Journal".Translate(),
                ref settings.promptJournal, JournalFromSummaryRequest.BuildDefaultPrompt());
            DrawPromptField(listing, "RimTalkLE_Settings_Prompt_MemorySummary".Translate(),
                ref settings.promptMemorySummary, MemorySummaryRequest.BuildDefaultPrompt());
            DrawPromptField(listing, "RimTalkLE_Settings_Prompt_BookFromSummary".Translate(),
                ref settings.promptBookFromSummary, BookFromSummaryRequest.BuildDefaultPrompt());
            DrawPromptField(listing, "RimTalkLE_Settings_Prompt_LetterRewrite".Translate(),
                ref settings.promptLetterRewrite, LetterTextRewriter.BuildDefaultPrompt());
            DrawPromptField(listing, "RimTalkLE_Settings_Prompt_IdeoRewrite".Translate(),
                ref settings.promptIdeoRewrite, IdeoDescriptionRewriter.BuildDefaultPrompt());
            DrawPromptField(listing, "RimTalkLE_Settings_Prompt_QuestAdvert".Translate(),
                ref settings.promptQuestAdvert, AdvertisementQuestRequest.BuildDefaultPrompt());
            DrawPromptField(listing, "RimTalkLE_Settings_Prompt_QuestWarning".Translate(),
                ref settings.promptQuestWarning, WarningQuestRequest.BuildDefaultPrompt());
            DrawPromptField(listing, "RimTalkLE_Settings_Prompt_TvProgram".Translate(),
                ref settings.promptTvProgram, TvProgramPromptBuilder.BuildDefaultPrompt());

            listing.End();
            _settingsViewHeightPrompts = listing.CurHeight + 10f;
            Widgets.EndScrollView();
        }

        private static void DrawPromptField(Listing_Standard listing, string label, ref string value, string defaultText)
        {
            if (listing == null) return;

            Rect headerRect = listing.GetRect(LiteratureSettingsDef.RowHeight);
            float buttonWidth = 140f;
            Rect labelRect = new Rect(headerRect.x, headerRect.y, headerRect.width - buttonWidth - LiteratureSettingsDef.FieldGap, headerRect.height);
            Rect buttonRect = new Rect(labelRect.xMax + LiteratureSettingsDef.FieldGap, headerRect.y, buttonWidth, headerRect.height);

            var anchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, label);
            Text.Anchor = anchor;

            if (Widgets.ButtonText(buttonRect, "RimTalkLE_Settings_PromptReset".Translate()))
                value = string.Empty;

            Rect areaRect = listing.GetRect(LiteratureSettingsDef.PromptTextHeight);
            defaultText ??= string.Empty;
            value ??= string.Empty;

            string displayText = string.IsNullOrWhiteSpace(value) ? defaultText : value;
            string edited = Widgets.TextArea(areaRect, displayText);
            edited = ClampPrompt(edited);

            if (string.IsNullOrWhiteSpace(edited) || edited == defaultText)
                value = string.Empty;
            else
                value = edited;

            listing.Gap(8f);
        }

        private static string ClampPrompt(string value)
        {
            if (value == null) return string.Empty;
            int maxLength = LiteratureSettingsDef.MaxPromptLength;
            if (maxLength > 0 && value.Length > maxLength)
                return value.Substring(0, maxLength);
            return value;
        }

        private static float GetPromptPageHeight()
        {
            float header = Text.LineHeight * 2f + 8f;
            float row = LiteratureSettingsDef.RowHeight + LiteratureSettingsDef.PromptTextHeight + 8f;
            const int fieldCount = 11;
            return header + fieldCount * row + 10f;
        }

        private static void DrawContentFilterColumns(Rect rect, LiteratureSettings settings)
        {
            float gap = LiteratureSettingsDef.FieldGap;
            float halfWidth = (rect.width - gap) / 2f;
            Rect leftRect = new Rect(rect.x, rect.y, halfWidth, rect.height);
            Rect rightRect = new Rect(leftRect.xMax + gap, rect.y, halfWidth, rect.height);

            DrawBookFilterColumn(leftRect, settings);
            DrawArtFilterColumn(rightRect, settings);
        }

        private static void DrawBookFilterColumn(Rect rect, LiteratureSettings settings)
        {
            float lineHeight = Text.LineHeight;
            Rect titleRect = new Rect(rect.x, rect.y, rect.width, lineHeight);
            Rect helpRect = new Rect(rect.x, rect.y + lineHeight, rect.width, lineHeight);
            Widgets.Label(titleRect, "RimTalkLE_Settings_BookFilter".Translate());
            Widgets.Label(helpRect, "RimTalkLE_Settings_BookFilterHelp".Translate());

            float listTop = rect.y + lineHeight * 2f + 6f;
            Rect listRect = new Rect(rect.x, listTop, rect.width, rect.yMax - listTop);
            DrawThingDefFilter(
                listRect,
                BookFilterPolicy.GetEligibleDefs(),
                ref _bookFilterScroll,
                settings.bookRewriteAllowList,
                value => settings.bookRewriteAllowList = value,
                "RimTalkLE_Settings_BookFilterNone");
        }

        private static void DrawArtFilterColumn(Rect rect, LiteratureSettings settings)
        {
            float lineHeight = Text.LineHeight;
            Rect titleRect = new Rect(rect.x, rect.y, rect.width, lineHeight);
            Rect helpRect = new Rect(rect.x, rect.y + lineHeight, rect.width, lineHeight);
            Widgets.Label(titleRect, "RimTalkLE_Settings_ArtFilter".Translate());
            Widgets.Label(helpRect, "RimTalkLE_Settings_ArtFilterHelp".Translate());

            float listTop = rect.y + lineHeight * 2f + 6f;
            Rect listRect = new Rect(rect.x, listTop, rect.width, rect.yMax - listTop);
            DrawThingDefFilter(
                listRect,
                ArtDefFilterPolicy.GetEligibleDefs(),
                ref _artFilterScroll,
                settings.artRewriteAllowList,
                value => settings.artRewriteAllowList = value,
                "RimTalkLE_Settings_ArtFilterNone");
        }

        private static void DrawThingDefFilter(
            Rect rect,
            List<ThingDef> defs,
            ref Vector2 scrollPosition,
            List<string> currentAllowList,
            System.Action<List<string>> applyAllowList,
            string emptyKey)
        {
            if (defs == null || defs.Count == 0)
            {
                Widgets.Label(rect, emptyKey.Translate());
                return;
            }

            float rowHeight = LiteratureSettingsDef.RowHeight;
            Rect buttonRow = new Rect(rect.x, rect.y, rect.width, rowHeight);
            float halfWidth = (rect.width - LiteratureSettingsDef.FieldGap) / 2f;
            Rect allRect = new Rect(rect.x, rect.y, halfWidth, rowHeight);
            Rect noneRect = new Rect(allRect.xMax + LiteratureSettingsDef.FieldGap, rect.y, halfWidth, rowHeight);

            var allowSet = new HashSet<string>(currentAllowList ?? new List<string>());
            bool changed = false;

            if (Widgets.ButtonText(allRect, "RimTalkLE_Settings_SelectAll".Translate()))
            {
                allowSet.Clear();
                for (int i = 0; i < defs.Count; i++)
                {
                    var def = defs[i];
                    if (def == null || string.IsNullOrWhiteSpace(def.defName)) continue;
                    allowSet.Add(def.defName);
                }
                changed = true;
            }

            if (Widgets.ButtonText(noneRect, "RimTalkLE_Settings_ClearAll".Translate()))
            {
                allowSet.Clear();
                changed = true;
            }

            float gap = 4f;
            Rect scrollOut = new Rect(rect.x, rect.y + rowHeight + gap, rect.width, rect.height - rowHeight - gap);
            float viewHeight = Mathf.Max(defs.Count * (rowHeight + 2f), scrollOut.height);
            Rect viewRect = new Rect(0f, 0f, scrollOut.width - 16f, viewHeight);

            float maxScroll = Mathf.Max(0f, viewHeight - scrollOut.height);
            if (scrollPosition.y > maxScroll)
                scrollPosition.y = maxScroll;

            Widgets.BeginScrollView(scrollOut, ref scrollPosition, viewRect);
            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                if (def == null || string.IsNullOrWhiteSpace(def.defName)) continue;

                string display = string.IsNullOrWhiteSpace(def.label)
                    ? def.defName
                    : $"{def.label} ({def.defName})";
                bool enabled = allowSet.Contains(def.defName);
                listing.CheckboxLabeled(display, ref enabled);
                if (enabled)
                    allowSet.Add(def.defName);
                else
                    allowSet.Remove(def.defName);
            }

            listing.End();
            Widgets.EndScrollView();

            if (!changed)
            {
                var existing = currentAllowList ?? new List<string>();
                if (existing.Count != allowSet.Count || existing.Any(x => !allowSet.Contains(x)))
                    changed = true;
            }

            if (changed)
                applyAllowList(allowSet.OrderBy(x => x).ToList());
        }

        private static void DrawQuestFilter(Rect rect, LiteratureSettings settings)
        {
            if (settings == null) return;
            var defs = DefDatabase<QuestScriptDef>.AllDefsListForReading;
            if (defs == null || defs.Count == 0)
            {
                Widgets.Label(rect, "RimTalkLE_Settings_QuestFilterNone".Translate());
                return;
            }

            float rowHeight = LiteratureSettingsDef.RowHeight;
            Rect buttonRow = new Rect(rect.x, rect.y, rect.width, rowHeight);
            float halfWidth = (rect.width - LiteratureSettingsDef.FieldGap) / 2f;
            Rect allRect = new Rect(rect.x, rect.y, halfWidth, rowHeight);
            Rect noneRect = new Rect(allRect.xMax + LiteratureSettingsDef.FieldGap, rect.y, halfWidth, rowHeight);

            var allowSet = new HashSet<string>(settings.questRewriteAllowList ?? new List<string>());
            bool changed = false;

            if (Widgets.ButtonText(allRect, "RimTalkLE_Settings_SelectAll".Translate()))
            {
                allowSet.Clear();
                for (int i = 0; i < defs.Count; i++)
                {
                    var def = defs[i];
                    if (def == null || string.IsNullOrWhiteSpace(def.defName)) continue;
                    allowSet.Add(def.defName);
                }
                changed = true;
            }

            if (Widgets.ButtonText(noneRect, "RimTalkLE_Settings_ClearAll".Translate()))
            {
                allowSet.Clear();
                changed = true;
            }

            float gap = 4f;
            Rect scrollOut = new Rect(rect.x, rect.y + rowHeight + gap, rect.width, rect.height - rowHeight - gap);
            float viewHeight = defs.Count * (rowHeight + 2f);
            Rect viewRect = new Rect(0f, 0f, scrollOut.width - 16f, viewHeight);

            Widgets.BeginScrollView(scrollOut, ref _questFilterScroll, viewRect);
            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            foreach (var def in defs.OrderBy(d => d.defName))
            {
                if (def == null || string.IsNullOrWhiteSpace(def.defName)) continue;
                string label = def.label;
                string display = string.IsNullOrWhiteSpace(label) ? def.defName : $"{label} ({def.defName})";
                bool enabled = allowSet.Contains(def.defName);
                listing.CheckboxLabeled(display, ref enabled);
                if (enabled)
                    allowSet.Add(def.defName);
                else
                    allowSet.Remove(def.defName);
            }

            listing.End();
            Widgets.EndScrollView();

            if (!changed)
            {
                var existing = settings.questRewriteAllowList ?? new List<string>();
                if (existing.Count != allowSet.Count || existing.Any(x => !allowSet.Contains(x)))
                    changed = true;
            }

            if (changed)
                settings.questRewriteAllowList = allowSet.OrderBy(x => x).ToList();
        }

        private static void DrawFilterColumns(Rect rect, LiteratureSettings settings)
        {
            if (settings == null) return;
            float gap = LiteratureSettingsDef.FieldGap;
            float halfWidth = (rect.width - gap) / 2f;
            Rect leftRect = new Rect(rect.x, rect.y, halfWidth, rect.height);
            Rect rightRect = new Rect(leftRect.xMax + gap, rect.y, halfWidth, rect.height);

            DrawLetterFilterColumn(leftRect, settings);
            DrawQuestFilterColumn(rightRect, settings);
        }

        private static void DrawLetterFilterColumn(Rect rect, LiteratureSettings settings)
        {
            float lineHeight = Text.LineHeight;
            Rect titleRect = new Rect(rect.x, rect.y, rect.width, lineHeight);
            Rect helpRect = new Rect(rect.x, rect.y + lineHeight, rect.width, lineHeight);
            Widgets.Label(titleRect, "RimTalkLE_Settings_LetterFilter".Translate());
            Widgets.Label(helpRect, "RimTalkLE_Settings_LetterFilterHelp".Translate());

            float listTop = rect.y + lineHeight * 2f + 6f;
            Rect listRect = new Rect(rect.x, listTop, rect.width, rect.yMax - listTop);
            DrawLetterFilter(listRect, settings);
        }

        private static void DrawQuestFilterColumn(Rect rect, LiteratureSettings settings)
        {
            float lineHeight = Text.LineHeight;
            Rect titleRect = new Rect(rect.x, rect.y, rect.width, lineHeight);
            Rect notesRect = new Rect(rect.x, rect.y + lineHeight, rect.width, lineHeight * 2f);
            Widgets.Label(titleRect, "RimTalkLE_Settings_QuestFilter".Translate());
            DrawQuestFilterNotesRect(notesRect);

            float listTop = rect.y + lineHeight * 3f + 6f;
            Rect listRect = new Rect(rect.x, listTop, rect.width, rect.yMax - listTop);
            DrawQuestFilter(listRect, settings);
        }

        private static void DrawQuestFilterNotesRect(Rect rect)
        {
            string firstLine = "RimTalkLE_Settings_QuestFilterHelp".Translate();
            string secondLine = "RimTalkLE_Settings_QuestFilterExistingNote".Translate();
            Rect line1 = new Rect(rect.x, rect.y, rect.width, Text.LineHeight);
            Rect line2 = new Rect(rect.x, rect.y + Text.LineHeight, rect.width, Text.LineHeight);
            Widgets.Label(line1, firstLine);
            Widgets.Label(line2, secondLine);
        }

        private static void DrawLetterFilter(Rect rect, LiteratureSettings settings)
        {
            if (settings == null) return;
            var defs = GetLetterDefs();
            if (defs == null || defs.Count == 0)
            {
                Widgets.Label(rect, "RimTalkLE_Settings_LetterFilterNone".Translate());
                return;
            }

            float rowHeight = LiteratureSettingsDef.RowHeight;
            Rect buttonRow = new Rect(rect.x, rect.y, rect.width, rowHeight);
            float halfWidth = (rect.width - LiteratureSettingsDef.FieldGap) / 2f;
            Rect allRect = new Rect(rect.x, rect.y, halfWidth, rowHeight);
            Rect noneRect = new Rect(allRect.xMax + LiteratureSettingsDef.FieldGap, rect.y, halfWidth, rowHeight);

            var allowSet = new HashSet<string>(settings.letterRewriteAllowList ?? new List<string>());
            bool changed = false;

            if (Widgets.ButtonText(allRect, "RimTalkLE_Settings_SelectAll".Translate()))
            {
                allowSet.Clear();
                for (int i = 0; i < defs.Count; i++)
                {
                    var def = defs[i];
                    if (def == null || string.IsNullOrWhiteSpace(def.defName)) continue;
                    allowSet.Add(def.defName);
                }
                changed = true;
            }

            if (Widgets.ButtonText(noneRect, "RimTalkLE_Settings_ClearAll".Translate()))
            {
                allowSet.Clear();
                changed = true;
            }

            float gap = 4f;
            Rect scrollOut = new Rect(rect.x, rect.y + rowHeight + gap, rect.width, rect.height - rowHeight - gap);
            float viewHeight = Mathf.Max(defs.Count * (rowHeight + 2f), scrollOut.height);
            Rect viewRect = new Rect(0f, 0f, scrollOut.width - 16f, viewHeight);

            float maxScroll = Mathf.Max(0f, viewHeight - scrollOut.height);
            if (_letterFilterScroll.y > maxScroll)
                _letterFilterScroll.y = maxScroll;

            Widgets.BeginScrollView(scrollOut, ref _letterFilterScroll, viewRect);
            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            foreach (var def in defs.OrderBy(d => d.defName))
            {
                if (def == null || string.IsNullOrWhiteSpace(def.defName)) continue;
                string label = def.label;
                string display = string.IsNullOrWhiteSpace(label) ? def.defName : $"{label} ({def.defName})";
                bool enabled = allowSet.Contains(def.defName);
                listing.CheckboxLabeled(display, ref enabled);
                if (enabled)
                    allowSet.Add(def.defName);
                else
                    allowSet.Remove(def.defName);
            }

            listing.End();
            Widgets.EndScrollView();

            if (!changed)
            {
                var existing = settings.letterRewriteAllowList ?? new List<string>();
                if (existing.Count != allowSet.Count || existing.Any(x => !allowSet.Contains(x)))
                    changed = true;
            }

            if (changed)
                settings.letterRewriteAllowList = allowSet.OrderBy(x => x).ToList();
        }

        private static List<LetterDef> GetLetterDefs()
        {
            var defs = new List<LetterDef>();
            var seen = new HashSet<string>();

            void AddDef(LetterDef def)
            {
                if (def == null || string.IsNullOrWhiteSpace(def.defName)) return;
                if (seen.Add(def.defName))
                    defs.Add(def);
            }

            void AddDefByName(string defName)
            {
                if (string.IsNullOrWhiteSpace(defName)) return;
                var def = DefDatabase<LetterDef>.GetNamedSilentFail(defName);
                if (def == null)
                    def = new LetterDef { defName = defName, label = defName };
                AddDef(def);
            }

            var database = DefDatabase<LetterDef>.AllDefsListForReading;
            if (database != null)
            {
                for (int i = 0; i < database.Count; i++)
                    AddDef(database[i]);
            }

            AddDefByName("PositiveEvent");
            AddDefByName("NewQuest");
            AddDef(LetterDefOf.ThreatBig);
            AddDef(LetterDefOf.ThreatSmall);
            AddDef(LetterDefOf.NegativeEvent);
            AddDef(LetterDefOf.NeutralEvent);
            AddDef(LetterDefOf.PositiveEvent);
            AddDef(LetterDefOf.Death);
            AddDef(LetterDefOf.AcceptVisitors);
            AddDef(LetterDefOf.AcceptJoiner);
            AddDef(LetterDefOf.GameEnded);
            AddDef(LetterDefOf.ChoosePawn);
            AddDef(LetterDefOf.RitualOutcomeNegative);
            AddDef(LetterDefOf.RitualOutcomePositive);
            AddDef(LetterDefOf.RelicHuntInstallationFound);
            AddDef(LetterDefOf.BabyBirth);
            AddDef(LetterDefOf.BabyToChild);
            AddDef(LetterDefOf.ChildToAdult);
            AddDef(LetterDefOf.ChildBirthday);
            AddDef(LetterDefOf.Bossgroup);
            AddDef(LetterDefOf.AcceptCreepJoiner);
            AddDef(LetterDefOf.EntityDiscovered);
            AddDef(LetterDefOf.BundleLetter);

            return defs;
        }

    }
}
