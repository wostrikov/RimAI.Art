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
 * - Checkbox: Use same API as RimAI.Communication
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
 * - Do not modify RimAI.Communication settings.
 */
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Art.Books;
using Ustas.RimAI.Art.Settings.Util;
using Ustas.RimAI.Art.Synopsis;
using Ustas.RimAI.Art.Art;
using Ustas.RimAI.Art.Art.LLM;
using Ustas.RimAI.Art.Authoring.LLM;
using Ustas.RimAI.Art.Journal.LLM;
using Ustas.RimAI.Art.Events;
using Ustas.RimAI.Art.Events.Quests;
using Ustas.RimAI.Art.Storage.Save;
using Ustas.RimAI.Art.TV;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Art.Settings
{
    public static class LiteratureSettingsWindow
    {
        private const int PageMain = 0;
        private const int PageContent = 1;
        private const int PageFilters = 2;
        private const int PagePrompts = 3;
        internal static int _settingsPageIndex;
        internal static Vector2 _settingsScrollMain;
        internal static Vector2 _settingsScrollPrompts;
        internal static float _settingsViewHeightMain;
        internal static float _settingsViewHeightPrompts;
        internal static Vector2 _bookFilterScroll;
        internal static Vector2 _artFilterScroll;
        internal static Vector2 _questFilterScroll;
        internal static Vector2 _letterFilterScroll;

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
                    RimAiLog.Warning(RimAiLogCategory.Art, "[RimAI.Art] No active world data; cannot clear book cache.");
                }
                else
                {
                    int cleared = cache.Clear();
                    RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Cleared {cleared} cached book synopses.");
                }
            }

            Rect artCacheRect = listing.GetRect(LiteratureSettingsDef.RowHeight);
            if (Widgets.ButtonText(artCacheRect, "RimTalkLE_Settings_ClearArtCache".Translate()))
            {
                var cache = LiteratureSaveData.Current?.ArtCache;
                if (cache == null)
                {
                    RimAiLog.Warning(RimAiLogCategory.Art, "[RimAI.Art] No active world data; cannot clear art cache.");
                }
                else
                {
                    int cleared = cache.Clear();
                    RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Cleared {cleared} cached art descriptions.");
                }
            }

            Rect rescanRect = listing.GetRect(LiteratureSettingsDef.RowHeight);
            if (Widgets.ButtonText(rescanRect, "RimTalkLE_Settings_RescanArtBooks".Translate()))
            {
                var maps = Find.Maps;
                if (maps == null || maps.Count == 0)
                {
                    RimAiLog.Warning(RimAiLogCategory.Art, "[RimAI.Art] Manual rescan skipped: no active maps.");
                }
                else
                {
                    RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Manual rescan requested for {maps.Count} maps.");
                    bool bookEnabled = settings.enabled;
                    for (int i = 0; i < maps.Count; i++)
                    {
                        var map = maps[i];
                        Ustas.RimAI.Art.Scanner.MapArtScanner.Scan(map);
                        if (bookEnabled)
                            Ustas.RimAI.Art.Scanner.MapBookScanner.Scan(map, detailedLog: true);
                        else
                            RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Book scan skipped: books disabled (map {map?.uniqueID ?? -1}).");
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
            LiteratureSettingsFilterPage.DrawPromptsPage(inRect, settings);
        }


        private static void DrawPromptField(Listing_Standard listing, string label, ref string value, string defaultText)
        {
            LiteratureSettingsFilterPage.DrawPromptField(listing, label, ref value, defaultText);
        }


        internal static string ClampPrompt(string value)
        {
            if (value == null) return string.Empty;
            int maxLength = LiteratureSettingsDef.MaxPromptLength;
            if (maxLength > 0 && value.Length > maxLength)
                return value.Substring(0, maxLength);
            return value;
        }

        internal static float GetPromptPageHeight()
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
            LiteratureSettingsFilterPage.DrawBookFilterColumn(rect, settings);
        }


        private static void DrawArtFilterColumn(Rect rect, LiteratureSettings settings)
        {
            LiteratureSettingsFilterPage.DrawArtFilterColumn(rect, settings);
        }


        private static void DrawThingDefFilter(
            Rect rect,
            List<ThingDef> defs,
            ref Vector2 scrollPosition,
            List<string> currentAllowList,
            System.Action<List<string>> applyAllowList,
            string emptyKey)
        {
            LiteratureSettingsFilterPage.DrawThingDefFilter(rect, defs, ref scrollPosition, currentAllowList, applyAllowList, emptyKey);
        }


        private static void DrawQuestFilter(Rect rect, LiteratureSettings settings)
        {
            LiteratureSettingsFilterPage.DrawQuestFilter(rect, settings);
        }


        private static void DrawFilterColumns(Rect rect, LiteratureSettings settings)
        {
            LiteratureSettingsFilterPage.DrawFilterColumns(rect, settings);
        }


        private static void DrawLetterFilterColumn(Rect rect, LiteratureSettings settings)
        {
            LiteratureSettingsFilterPage.DrawLetterFilterColumn(rect, settings);
        }


        private static void DrawQuestFilterColumn(Rect rect, LiteratureSettings settings)
        {
            LiteratureSettingsFilterPage.DrawQuestFilterColumn(rect, settings);
        }


        internal static void DrawQuestFilterNotesRect(Rect rect)
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
            LiteratureSettingsFilterPage.DrawLetterFilter(rect, settings);
        }


        private static List<LetterDef> GetLetterDefs()
        {
            return LiteratureSettingsFilterPage.GetLetterDefs();
        }


    }
}
