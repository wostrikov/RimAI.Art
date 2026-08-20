using System;
using System.Text;
using RimWorld;
using Ustas.RimAI.Art.Art;
using Ustas.RimAI.Art.Settings;
using Ustas.RimAI.Art.Storage;
using Ustas.RimAI.Art.Storage.Save;
using Verse;

namespace Ustas.RimAI.Art.Integration
{
    public static class ArtCacheUtil
    {
        [ThreadStatic]
        private static int _artTabSuppressDepth;

        public static bool IsArtEditingEnabled()
        {
            var settings = LiteratureMod.Settings;
            settings?.EnsureContentFiltersInitialized();
            return settings != null &&
                   settings.artRewriteAllowList != null &&
                   settings.artRewriteAllowList.Count > 0 &&
                   (settings.allowArtBuildingEdits || settings.allowArtWeaponEdits || settings.allowArtApparelEdits);
        }

        public static bool AllowsArtTabEdit(Thing thing)
        {
            if (!IsArtEditingEnabled()) return false;
            return ArtEditPolicy.Allows(thing, ArtEditTarget.ArtTab);
        }

        public static bool AllowsLabelEdit(Thing thing)
        {
            var settings = LiteratureMod.Settings;
            if (settings == null || !settings.allowArtLabelEdits)
                return false;
            if (!IsArtEditingEnabled()) return false;

            return ArtEditPolicy.Allows(thing, ArtEditTarget.Label);
        }

        public static bool AllowsDescriptionEdit(Thing thing)
        {
            var settings = LiteratureMod.Settings;
            if (settings == null || !settings.allowArtLabelEdits)
                return false;
            if (!IsArtEditingEnabled()) return false;

            return ArtEditPolicy.Allows(thing, ArtEditTarget.Description);
        }

        public static bool IsArtTabOverrideSuppressed => _artTabSuppressDepth > 0;

        internal static void PushArtTabOverrideSuppression()
        {
            _artTabSuppressDepth++;
        }

        internal static void PopArtTabOverrideSuppression()
        {
            if (_artTabSuppressDepth > 0)
                _artTabSuppressDepth--;
        }

        public static bool TryGetRecord(Thing thing, out ArtDescriptionRecord record)
        {
            record = null;
            if (thing == null) return false;

            var cache = LiteratureSaveData.Current?.ArtCache;
            if (cache == null) return false;

            if (!ArtKeyProvider.TryGetKey(thing, out var key)) return false;
            return cache.TryGet(key, out record);
        }

        public static bool TryBuildDescription(ArtDescriptionRecord record, string author, out string description)
        {
            description = null;
            if (record == null) return false;

            var title = record.Title;
            var text = record.Text;
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(text))
                return false;

            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(title))
                sb.Append(title.Trim());

            if (!string.IsNullOrWhiteSpace(text))
            {
                if (sb.Length > 0)
                    sb.Append("\n\n");
                sb.Append(text.Trim());
            }

            if (!string.IsNullOrWhiteSpace(author))
            {
                sb.Append("\n\n");
                sb.Append("Author".Translate());
                sb.Append(": ");
                sb.Append(author);
            }

            description = sb.ToString();
            return true;
        }

        public static bool TryBuildLabel(Thing thing, string title, out string label)
        {
            label = null;
            if (thing == null) return false;
            if (string.IsNullOrWhiteSpace(title)) return false;

            label = title.Trim();
            label += GenLabel.LabelExtras(thing, includeHp: true, includeQuality: true);

            if (thing is ThingWithComps thingWithComps)
            {
                var comps = thingWithComps.AllComps;
                for (int i = 0; i < comps.Count; i++)
                {
                    var comp = comps[i];
                    if (comp == null) continue;
                    if (comp is CompArt) continue;
                    if (comp is CompGeneratedNames) continue;
                    if (comp is CompUniqueWeapon) continue;
                    label = comp.TransformLabel(label);
                }
            }

            return true;
        }

        public static string DescribeArtSettings()
        {
            var settings = LiteratureMod.Settings;
            if (settings == null) return "settings=null";
            return $"buildings={settings.allowArtBuildingEdits}, weapons={settings.allowArtWeaponEdits}, apparel={settings.allowArtApparelEdits}, labels={settings.allowArtLabelEdits}, allowList={settings.artRewriteAllowList?.Count ?? 0}";
        }
    }
}
