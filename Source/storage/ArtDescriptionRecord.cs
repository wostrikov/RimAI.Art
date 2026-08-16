using Ustas.RimAI.Art.art.model;
using System;
using System.Collections.Generic;
using Verse;

namespace Ustas.RimAI.Art.storage
{
    public sealed class ArtDescriptionRecord : IExposable
    {
        public string Title;
        public string Text;
        public int GeneratedTick;
        public string Source = TextHistorySource.Generated;
        public List<TextHistoryEntry> History = new List<TextHistoryEntry>();

        public bool IsManualOverride => string.Equals(Source, TextHistorySource.Manual, StringComparison.Ordinal);

        public ArtDescriptionRecord()
        {
            TextHistoryUtility.EnsureInitialized(ref History);
        }

        public ArtDescriptionRecord(ArtDescription description)
        {
            TextHistoryUtility.EnsureInitialized(ref History);
            ReplaceContent(description?.Title, description?.Text, TextHistorySource.Generated, GenTicks.TicksGame);
        }

        public static ArtDescriptionRecord FromManual(string title, string text, ArtDescriptionRecord existing = null)
        {
            var record = existing ?? new ArtDescriptionRecord();
            record.ReplaceContent(title, text, TextHistorySource.Manual, GenTicks.TicksGame);
            return record;
        }

        public static ArtDescriptionRecord FromGenerated(ArtDescription description, ArtDescriptionRecord existing = null)
        {
            var record = existing ?? new ArtDescriptionRecord();
            record.ReplaceContent(description?.Title, description?.Text, TextHistorySource.Generated, GenTicks.TicksGame);
            return record;
        }

        public bool TryCreateAutomaticFallback(out ArtDescription description)
        {
            description = null;
            if (!TextHistoryUtility.TryGetLatestNonManual(History, out var entry))
                return false;

            description = new ArtDescription
            {
                Title = entry.Title ?? string.Empty,
                Text = entry.Body ?? string.Empty
            };
            return true;
        }

        private void ReplaceContent(string title, string text, string source, int tick)
        {
            Title = title ?? string.Empty;
            Text = text ?? string.Empty;
            Source = source ?? TextHistorySource.Generated;
            GeneratedTick = tick;
            TextHistoryUtility.AppendSnapshot(ref History, Source, Title, Text, GeneratedTick);
        }

        public ArtDescription ToDescription()
        {
            return new ArtDescription
            {
                Title = Title ?? string.Empty,
                Text = Text ?? string.Empty
            };
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Title, "title");
            Scribe_Values.Look(ref Text, "text");
            Scribe_Values.Look(ref GeneratedTick, "generatedTick", 0);
            Scribe_Values.Look(ref Source, "source", TextHistorySource.Generated);
            Scribe_Collections.Look(ref History, "history", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                TextHistoryUtility.EnsureInitialized(ref History);
        }
    }
}
