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
using Ustas.RimAI.Art;
using Ustas.RimAI.Core.Handshake;
using Ustas.RimAI.Core.Modules;
using Verse;

namespace Ustas.RimAI.Art.Settings
{
    public sealed class LiteratureMod : Mod
    {
        public const string HandshakeModuleVersion = "1.0.0";
        public static LiteratureSettings Settings;

        public LiteratureMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<LiteratureSettings>();
            if (!RimAiHandshake.TryActivate(
                    RimAiHandshakeDescriptor.Current(
                        RimAiModuleIds.Art,
                        HandshakeModuleVersion,
                        isOptional: true),
                    ArtComposition.Current.Start))
            {
                return;
            }

            RimAISettingsContributionRegistry.Current.Register(new DelegateSettingsContributor(
                "art-literature",
                "Literature",
                RimAISettingsSection.Module,
                30,
                listing =>
                {
                    var list = (Listing_Standard)listing;
                    list.Label("RimAI.Settings.OpenModuleEntry".Translate("Art", "Literature"));
                },
                "art",
                "literature"));
        }

        public override string SettingsCategory()
        {
            return Content?.Name ?? "RimAI.Art";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            RimAISettingsNavigation.Open("art", "literature");
            LiteratureSettingsWindow.Draw(inRect, Settings);
            Settings?.Write();
        }
    }
}
