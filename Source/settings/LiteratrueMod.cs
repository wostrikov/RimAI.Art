/*
 * Purpose:
 * - RimWorld mod entry for Literature Expansion settings.
 * - Owns the settings object and exposes the settings UI entry point.
 *
 * Uses:
 * - Verse.Mod
 * - LiteratureSettings
 * - LiteratureSettingsWindow (optional helper) OR inline DoSettingsWindowContents
 *
 * Responsibilities:
 * - Initialize settings via GetSettings<LiteratureSettings>().
 * - Provide SettingsCategory() label.
 * - Draw settings UI (enable toggle, API reuse toggle, API fields).
 *
 * Do NOT:
 * - Do not implement LLM logic here.
 * - Do not reference RimTalk services directly (only settings integration).
 */
using UnityEngine;
using Verse;

namespace RimTalk_LiteratureExpansion.settings
{
    public sealed class LiteratureMod : Mod
    {
        public static LiteratureSettings Settings;

        public LiteratureMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<LiteratureSettings>();
        }

        public override string SettingsCategory()
        {
            return Content?.Name ?? "RimAI.Literature";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            LiteratureSettingsWindow.Draw(inRect, Settings);
            Settings?.Write();
        }
    }
}
