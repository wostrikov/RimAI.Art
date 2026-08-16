using Ustas.RimAI.Art.book;
using Ustas.RimAI.Art.synopsis.model;
using System;
using System.Collections.Generic;
using Verse;

namespace Ustas.RimAI.Art.storage
{
    public sealed class BookSynopsisRecord : IExposable
    {
        public string Title;
        public string Synopsis;
        public BookType Type = BookType.Unknown;
        public int GeneratedTick;
        public string Source = TextHistorySource.Generated;
        public List<TextHistoryEntry> History = new List<TextHistoryEntry>();

        public bool IsManualOverride => string.Equals(Source, TextHistorySource.Manual, StringComparison.Ordinal);

        public BookSynopsisRecord()
        {
            TextHistoryUtility.EnsureInitialized(ref History);
        }

        public BookSynopsisRecord(BookSynopsis synopsis, BookType type)
        {
            TextHistoryUtility.EnsureInitialized(ref History);
            ReplaceContent(synopsis?.Title, synopsis?.Synopsis, type, TextHistorySource.Generated, GenTicks.TicksGame);
        }

        public static BookSynopsisRecord FromManual(string title, string synopsis, BookType type, BookSynopsisRecord existing = null)
        {
            var record = existing ?? new BookSynopsisRecord();
            record.ReplaceContent(title, synopsis, type, TextHistorySource.Manual, GenTicks.TicksGame);
            return record;
        }

        public static BookSynopsisRecord FromGenerated(BookSynopsis synopsis, BookType type, BookSynopsisRecord existing = null)
        {
            var record = existing ?? new BookSynopsisRecord();
            record.ReplaceContent(synopsis?.Title, synopsis?.Synopsis, type, TextHistorySource.Generated, GenTicks.TicksGame);
            return record;
        }

        public bool TryCreateAutomaticFallback(out BookSynopsis synopsis, out BookType type)
        {
            synopsis = null;
            type = Type;
            if (!TextHistoryUtility.TryGetLatestNonManual(History, out var entry))
                return false;

            synopsis = new BookSynopsis
            {
                Title = entry.Title ?? string.Empty,
                Synopsis = entry.Body ?? string.Empty
            };
            return true;
        }

        private void ReplaceContent(string title, string synopsis, BookType type, string source, int tick)
        {
            Title = title ?? string.Empty;
            Synopsis = synopsis ?? string.Empty;
            Type = type;
            Source = source ?? TextHistorySource.Generated;
            GeneratedTick = tick;
            TextHistoryUtility.AppendSnapshot(ref History, Source, Title, Synopsis, GeneratedTick);
        }

        public BookSynopsis ToSynopsis()
        {
            return new BookSynopsis
            {
                Title = Title ?? string.Empty,
                Synopsis = Synopsis ?? string.Empty
            };
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Title, "title");
            Scribe_Values.Look(ref Synopsis, "synopsis");
            Scribe_Values.Look(ref Type, "type", BookType.Unknown);
            Scribe_Values.Look(ref GeneratedTick, "generatedTick", 0);
            Scribe_Values.Look(ref Source, "source", TextHistorySource.Generated);
            Scribe_Collections.Look(ref History, "history", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                TextHistoryUtility.EnsureInitialized(ref History);
        }
    }
}
