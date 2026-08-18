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

namespace Ustas.RimAI.Art.settings;

internal static class LiteratureSettingsFilterPage
{


        internal static void DrawThingDefFilter(
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

        internal static void DrawLetterFilter(Rect rect, LiteratureSettings settings)
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
            if (LiteratureSettingsWindow._letterFilterScroll.y > maxScroll)
                LiteratureSettingsWindow._letterFilterScroll.y = maxScroll;

            Widgets.BeginScrollView(scrollOut, ref LiteratureSettingsWindow._letterFilterScroll, viewRect);
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

        internal static void DrawQuestFilter(Rect rect, LiteratureSettings settings)
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

            Widgets.BeginScrollView(scrollOut, ref LiteratureSettingsWindow._questFilterScroll, viewRect);
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

        internal static List<LetterDef> GetLetterDefs()
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

        internal static void DrawPromptsPage(Rect inRect, LiteratureSettings settings)
        {
            if (settings == null) return;

            if (LiteratureSettingsWindow._settingsViewHeightPrompts < 1f)
                LiteratureSettingsWindow._settingsViewHeightPrompts = LiteratureSettingsWindow.GetPromptPageHeight();

            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, Mathf.Max(LiteratureSettingsWindow._settingsViewHeightPrompts, inRect.height));
            Widgets.BeginScrollView(inRect, ref LiteratureSettingsWindow._settingsScrollPrompts, viewRect);

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
            LiteratureSettingsWindow._settingsViewHeightPrompts = listing.CurHeight + 10f;
            Widgets.EndScrollView();
        }

        internal static void DrawPromptField(Listing_Standard listing, string label, ref string value, string defaultText)
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
            edited = LiteratureSettingsWindow.ClampPrompt(edited);

            if (string.IsNullOrWhiteSpace(edited) || edited == defaultText)
                value = string.Empty;
            else
                value = edited;

            listing.Gap(8f);
        }

        internal static void DrawBookFilterColumn(Rect rect, LiteratureSettings settings)
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
                ref LiteratureSettingsWindow._bookFilterScroll,
                settings.bookRewriteAllowList,
                value => settings.bookRewriteAllowList = value,
                "RimTalkLE_Settings_BookFilterNone");
        }

        internal static void DrawArtFilterColumn(Rect rect, LiteratureSettings settings)
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
                ref LiteratureSettingsWindow._artFilterScroll,
                settings.artRewriteAllowList,
                value => settings.artRewriteAllowList = value,
                "RimTalkLE_Settings_ArtFilterNone");
        }

        internal static void DrawQuestFilterColumn(Rect rect, LiteratureSettings settings)
        {
            float lineHeight = Text.LineHeight;
            Rect titleRect = new Rect(rect.x, rect.y, rect.width, lineHeight);
            Rect notesRect = new Rect(rect.x, rect.y + lineHeight, rect.width, lineHeight * 2f);
            Widgets.Label(titleRect, "RimTalkLE_Settings_QuestFilter".Translate());
            LiteratureSettingsWindow.DrawQuestFilterNotesRect(notesRect);

            float listTop = rect.y + lineHeight * 3f + 6f;
            Rect listRect = new Rect(rect.x, listTop, rect.width, rect.yMax - listTop);
            DrawQuestFilter(listRect, settings);
        }

        internal static void DrawLetterFilterColumn(Rect rect, LiteratureSettings settings)
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

        internal static void DrawFilterColumns(Rect rect, LiteratureSettings settings)
        {
            if (settings == null) return;
            float gap = LiteratureSettingsDef.FieldGap;
            float halfWidth = (rect.width - gap) / 2f;
            Rect leftRect = new Rect(rect.x, rect.y, halfWidth, rect.height);
            Rect rightRect = new Rect(leftRect.xMax + gap, rect.y, halfWidth, rect.height);

            DrawLetterFilterColumn(leftRect, settings);
            DrawQuestFilterColumn(rightRect, settings);
        }
}
