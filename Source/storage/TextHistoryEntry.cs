using System.Collections.Generic;
using System;
using Verse;

namespace Ustas.RimAI.Art.storage
{
    public static class TextHistorySource
    {
        public const string Generated = "generated";
        public const string Manual = "manual";
    }

    public sealed class TextHistoryEntry : IExposable
    {
        public string Source;
        public string Title;
        public string Body;
        public int Tick;

        public TextHistoryEntry()
        {
        }

        public TextHistoryEntry(string source, string title, string body, int tick)
        {
            Source = source ?? TextHistorySource.Generated;
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
            Tick = tick;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Source, "source", TextHistorySource.Generated);
            Scribe_Values.Look(ref Title, "title", string.Empty);
            Scribe_Values.Look(ref Body, "body", string.Empty);
            Scribe_Values.Look(ref Tick, "tick", 0);
        }
    }

    public static class TextHistoryUtility
    {
        public static void EnsureInitialized(ref List<TextHistoryEntry> history)
        {
            if (history == null)
                history = new List<TextHistoryEntry>();
        }

        public static void AppendSnapshot(ref List<TextHistoryEntry> history, string source, string title, string body, int tick)
        {
            EnsureInitialized(ref history);

            var normalizedSource = source ?? TextHistorySource.Generated;
            var normalizedTitle = title ?? string.Empty;
            var normalizedBody = body ?? string.Empty;
            var last = history.Count > 0 ? history[history.Count - 1] : null;
            if (last != null &&
                string.Equals(last.Source, normalizedSource, StringComparison.Ordinal) &&
                string.Equals(last.Title ?? string.Empty, normalizedTitle, StringComparison.Ordinal) &&
                string.Equals(last.Body ?? string.Empty, normalizedBody, StringComparison.Ordinal))
            {
                return;
            }

            history.Add(new TextHistoryEntry(normalizedSource, normalizedTitle, normalizedBody, tick));
        }

        public static bool TryGetLatestNonManual(List<TextHistoryEntry> history, out TextHistoryEntry entry)
        {
            entry = null;
            if (history == null || history.Count == 0)
                return false;

            for (int i = history.Count - 1; i >= 0; i--)
            {
                var candidate = history[i];
                if (candidate == null)
                    continue;
                if (string.Equals(candidate.Source, TextHistorySource.Manual, StringComparison.Ordinal))
                    continue;

                entry = candidate;
                return true;
            }

            return false;
        }
    }
}
