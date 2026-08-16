using System;
using System.Collections.Generic;
using Ustas.RimAI.Art.storage;
using Verse;

namespace Ustas.RimAI.Art.tv
{
    public sealed class TvProgramRecord : IExposable
    {
        public string Title;
        public string Content;
        public int GeneratedTick;
        public string Source = TextHistorySource.Generated;
        public List<TextHistoryEntry> History = new List<TextHistoryEntry>();

        public bool IsManualOverride => string.Equals(Source, TextHistorySource.Manual, StringComparison.Ordinal);

        public TvProgramRecord()
        {
            TextHistoryUtility.EnsureInitialized(ref History);
        }

        public TvProgramRecord(TvProgramContent program)
        {
            TextHistoryUtility.EnsureInitialized(ref History);
            ReplaceContent(program?.Title, program?.Content, TextHistorySource.Generated, GenTicks.TicksGame);
        }

        public static TvProgramRecord FromManual(string title, string content, TvProgramRecord existing = null)
        {
            var record = existing ?? new TvProgramRecord();
            record.ReplaceContent(title, content, TextHistorySource.Manual, GenTicks.TicksGame);
            return record;
        }

        public static TvProgramRecord FromGenerated(TvProgramContent program, TvProgramRecord existing = null)
        {
            var record = existing ?? new TvProgramRecord();
            record.ReplaceContent(program?.Title, program?.Content, TextHistorySource.Generated, GenTicks.TicksGame);
            return record;
        }

        public bool TryCreateAutomaticFallback(out TvProgramContent program)
        {
            program = null;
            if (!TextHistoryUtility.TryGetLatestNonManual(History, out var entry))
                return false;

            program = new TvProgramContent
            {
                Title = entry.Title ?? string.Empty,
                Content = entry.Body ?? string.Empty
            };
            return true;
        }

        public TvProgramContent ToContent()
        {
            return new TvProgramContent
            {
                Title = Title ?? string.Empty,
                Content = Content ?? string.Empty
            };
        }

        private void ReplaceContent(string title, string content, string source, int tick)
        {
            Title = title ?? string.Empty;
            Content = content ?? string.Empty;
            Source = source ?? TextHistorySource.Generated;
            GeneratedTick = tick;
            TextHistoryUtility.AppendSnapshot(ref History, Source, Title, Content, GeneratedTick);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Title, "title");
            Scribe_Values.Look(ref Content, "content");
            Scribe_Values.Look(ref GeneratedTick, "generatedTick", 0);
            Scribe_Values.Look(ref Source, "source", TextHistorySource.Generated);
            Scribe_Collections.Look(ref History, "history", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                TextHistoryUtility.EnsureInitialized(ref History);
        }
    }
}
