using System.Collections.Generic;
using HarmonyLib;
using Ustas.RimAI.Art.art;
using Ustas.RimAI.Art.art.model;
using Ustas.RimAI.Art.book;
using Ustas.RimAI.Art.integration;
using Ustas.RimAI.Art.scanner.queue;
using Ustas.RimAI.Art.settings;
using Ustas.RimAI.Art.storage;
using Ustas.RimAI.Art.storage.save;
using Ustas.RimAI.Art.synopsis.model;
using Ustas.RimAI.Art.tv;
using RimWorld;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Art.manual
{
    public enum ManualTextEditKind
    {
        None = 0,
        Book = 1,
        Art = 2,
        Tv = 3
    }

    public sealed class ManualTextEditContext
    {
        public Thing Thing;
        public ManualTextEditKind Kind;
        public string TargetLabel;
        public string TargetDefName;
        public string Title;
        public string Body;
        public List<TextHistoryEntry> History = new List<TextHistoryEntry>();
        public bool CanRestoreAutomatic;

        public string KindLabelKey =>
            Kind == ManualTextEditKind.Book
                ? "RimTalkLE_ManualEditor_KindBook"
                : Kind == ManualTextEditKind.Tv
                    ? "RimTalkLE_ManualEditor_KindTv"
                    : "RimTalkLE_ManualEditor_KindArt";
    }

    public static class ManualTextEditService
    {
        public static Gizmo CreateGizmo(ThingWithComps thing)
        {
            if (thing == null) return null;
            if (!TryCreateContext(thing, out _, includeBody: false)) return null;

            return new Command_Action
            {
                defaultLabel = "RimTalkLE_ManualEditor_OpenLabel".Translate(),
                defaultDesc = "RimTalkLE_ManualEditor_OpenDesc".Translate(),
                icon = TexButton.Rename,
                action = () => OpenEditor(thing)
            };
        }

        public static void OpenEditor(Thing thing)
        {
            if (!TryCreateContext(thing, out var context))
            {
                Messages.Message("RimTalkLE_ManualEditor_TargetUnavailable".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            Find.WindowStack.Add(new Dialog_ManualTextEditor(context));
        }

        public static bool Save(ManualTextEditContext context, string title, string body)
        {
            if (context?.Thing == null || context.Thing.DestroyedOrNull()) return false;

            title = Clean(title);
            body = Clean(body);

            return context.Kind switch
            {
                ManualTextEditKind.Book => SaveBook(context.Thing, title, body),
                ManualTextEditKind.Art => SaveArt(context.Thing, title, body),
                ManualTextEditKind.Tv => SaveTv(context.Thing, title, body),
                _ => false
            };
        }

        public static bool RestoreAutomatic(ManualTextEditContext context)
        {
            if (context?.Thing == null || context.Thing.DestroyedOrNull()) return false;

            return context.Kind switch
            {
                ManualTextEditKind.Book => RestoreBook(context.Thing),
                ManualTextEditKind.Art => RestoreArt(context.Thing),
                ManualTextEditKind.Tv => RestoreTv(context.Thing),
                _ => false
            };
        }

        public static bool TryCreateContext(Thing thing, out ManualTextEditContext context)
        {
            return TryCreateContext(thing, out context, includeBody: true);
        }

        private static bool TryCreateContext(Thing thing, out ManualTextEditContext context, bool includeBody)
        {
            context = null;
            if (thing == null || thing.DestroyedOrNull()) return false;

            if (TryCreateBookContext(thing, out context))
                return true;

            if (TryCreateArtContext(thing, out context, includeBody))
                return true;

            if (TryCreateTvContext(thing, out context))
                return true;

            return false;
        }

        private static bool TryCreateBookContext(Thing thing, out ManualTextEditContext context)
        {
            context = null;
            var settings = LiteratureMod.Settings;
            if (settings == null || !settings.allowManualBookEdits) return false;
            if (!BookFilterPolicy.IsAllowed(thing)) return false;

            var meta = BookClassifier.Classify(thing);
            if (meta == null) return false;

            BookSynopsisRecord record = null;
            var cache = LiteratureSaveData.Current?.SynopsisCache;
            if (cache != null && BookKeyProvider.TryGetKey(thing, out var key))
                cache.TryGet(key, out record);

            string title = record?.Title;
            if (string.IsNullOrWhiteSpace(title))
                title = meta.Title ?? thing.LabelNoCount;

            string body = record?.Synopsis;
            if (string.IsNullOrWhiteSpace(body))
                body = meta.DescriptionDetailed ?? thing.DescriptionDetailed ?? string.Empty;

            context = new ManualTextEditContext
            {
                Thing = thing,
                Kind = ManualTextEditKind.Book,
                TargetLabel = meta.Title ?? thing.LabelCap,
                TargetDefName = thing.def?.defName ?? string.Empty,
                Title = title ?? string.Empty,
                Body = body ?? string.Empty,
                History = record?.History != null ? new List<TextHistoryEntry>(record.History) : new List<TextHistoryEntry>(),
                CanRestoreAutomatic = record != null && record.TryCreateAutomaticFallback(out _, out _)
            };
            return true;
        }

        private static bool TryCreateArtContext(Thing thing, out ManualTextEditContext context, bool includeBody)
        {
            context = null;
            var settings = LiteratureMod.Settings;
            if (settings == null || !settings.allowManualArtEdits) return false;
            if (!settings.allowArtLabelEdits) return false;
            if (!ArtDefFilterPolicy.IsAllowed(thing)) return false;
            if (ArtEditPolicy.GetTargets(thing) == ArtEditTarget.None) return false;

            var meta = new ArtMeta(thing);
            if (meta == null) return false;

            ArtDescriptionRecord record = null;
            var cache = LiteratureSaveData.Current?.ArtCache;
            if (cache != null && ArtKeyProvider.TryGetKey(thing, out var key))
                cache.TryGet(key, out record);

            string title = record?.Title;
            if (string.IsNullOrWhiteSpace(title))
                title = meta.OriginalTitle ?? thing.LabelNoCount;

            string body = record?.Text;
            if (includeBody && string.IsNullOrWhiteSpace(body))
                body = ResolveCurrentArtBody(thing, meta);

            context = new ManualTextEditContext
            {
                Thing = thing,
                Kind = ManualTextEditKind.Art,
                TargetLabel = meta.ThingLabel ?? thing.LabelCap,
                TargetDefName = thing.def?.defName ?? string.Empty,
                Title = title ?? string.Empty,
                Body = body ?? string.Empty,
                History = record?.History != null ? new List<TextHistoryEntry>(record.History) : new List<TextHistoryEntry>(),
                CanRestoreAutomatic = record != null && record.TryCreateAutomaticFallback(out _)
            };
            return true;
        }

        private static bool SaveBook(Thing thing, string title, string body)
        {
            var meta = BookClassifier.Classify(thing);
            if (meta == null) return false;

            if (!BookKeyProvider.TryGetKey(thing, out var key))
                return false;

            var cache = LiteratureSaveData.Current?.SynopsisCache;
            if (cache == null) return false;

            cache.TryGet(key, out var existing);

            if (string.IsNullOrWhiteSpace(title))
                title = meta.Title ?? thing.LabelNoCount;

            var record = BookSynopsisRecord.FromManual(title, body, meta.Type, existing);
            cache.Set(key, record);
            BookTextApplier.Apply(meta, record.ToSynopsis());
            return true;
        }

        private static bool SaveArt(Thing thing, string title, string body)
        {
            if (!ArtKeyProvider.TryGetKey(thing, out var key))
                return false;

            var cache = LiteratureSaveData.Current?.ArtCache;
            if (cache == null) return false;

            cache.TryGet(key, out var existing);

            if (string.IsNullOrWhiteSpace(title))
                title = thing.LabelNoCount;

            cache.Set(key, ArtDescriptionRecord.FromManual(title, body, existing));
            return true;
        }

        private static bool RestoreBook(Thing thing)
        {
            if (!BookKeyProvider.TryGetKey(thing, out var key))
                return false;

            var cache = LiteratureSaveData.Current?.SynopsisCache;
            if (cache == null) return false;
            if (!cache.TryGet(key, out var record) || record == null)
                return false;

            var meta = BookClassifier.Classify(thing);
            if (meta == null)
                return false;

            if (!record.TryCreateAutomaticFallback(out var synopsis, out var type))
                return false;

            var restored = BookSynopsisRecord.FromGenerated(synopsis, type, record);
            cache.Set(key, restored);
            BookTextApplier.Apply(meta, restored.ToSynopsis());
            return true;
        }

        private static bool RestoreArt(Thing thing)
        {
            if (!ArtKeyProvider.TryGetKey(thing, out var key))
                return false;

            var cache = LiteratureSaveData.Current?.ArtCache;
            if (cache == null) return false;
            if (!cache.TryGet(key, out var record) || record == null)
                return false;

            if (!record.TryCreateAutomaticFallback(out var description))
                return false;

            cache.Set(key, ArtDescriptionRecord.FromGenerated(description, record));

            var meta = new ArtMeta(thing);
            if (meta != null && PendingArtQueue.Contains(key))
                return true;

            return true;
        }

        private static string ResolveCurrentArtBody(Thing thing, ArtMeta meta)
        {
            var compArt = meta?.CompArt ?? thing?.TryGetComp<CompArt>();
            if (compArt != null && compArt.Active)
            {
                try
                {
                    return compArt.GenerateImageDescription().Resolve().Trim();
                }
                catch
                {
                }
            }

            return Clean(thing?.DescriptionFlavor);
        }

        private static bool TryCreateTvContext(Thing thing, out ManualTextEditContext context)
        {
            context = null;
            var settings = LiteratureMod.Settings;
            if (settings == null || !settings.allowManualTvEdits) return false;
            if (!settings.allowTvContent) return false;
            if (!TvFilterPolicy.IsTelevision(thing)) return false;

            TvProgramRecord record = null;
            var cache = LiteratureSaveData.Current?.TvProgramCache;
            if (cache != null && TvProgramKeyProvider.TryGetKey(thing, out var key))
                cache.TryGet(key, out record);

            string title = record?.Title;
            if (string.IsNullOrWhiteSpace(title))
                title = thing.LabelNoCount;

            string body = record?.Content ?? string.Empty;

            context = new ManualTextEditContext
            {
                Thing = thing,
                Kind = ManualTextEditKind.Tv,
                TargetLabel = thing.LabelCap,
                TargetDefName = thing.def?.defName ?? string.Empty,
                Title = title ?? string.Empty,
                Body = body ?? string.Empty,
                History = record?.History != null ? new List<TextHistoryEntry>(record.History) : new List<TextHistoryEntry>(),
                CanRestoreAutomatic = record != null && record.TryCreateAutomaticFallback(out _)
            };
            return true;
        }

        private static bool SaveTv(Thing thing, string title, string body)
        {
            if (!TvProgramKeyProvider.TryGetKey(thing, out var key))
                return false;

            var cache = LiteratureSaveData.Current?.TvProgramCache;
            if (cache == null) return false;

            cache.TryGet(key, out var existing);

            if (string.IsNullOrWhiteSpace(title))
                title = thing.LabelNoCount;

            cache.Set(key, TvProgramRecord.FromManual(title, body, existing));
            return true;
        }

        private static bool RestoreTv(Thing thing)
        {
            if (!TvProgramKeyProvider.TryGetKey(thing, out var key))
                return false;

            var cache = LiteratureSaveData.Current?.TvProgramCache;
            if (cache == null) return false;
            if (!cache.TryGet(key, out var record) || record == null)
                return false;

            if (!record.TryCreateAutomaticFallback(out var program))
                return false;

            cache.Set(key, TvProgramRecord.FromGenerated(program, record));
            return true;
        }

        private static string Clean(string value)
        {
            return value?.Trim() ?? string.Empty;
        }
    }
}
