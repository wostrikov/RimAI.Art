/*
 * File: BookScanScheduler.cs
 *
 * Purpose:
 * - Control WHEN MapBookScanner runs (e.g. once per in-game day).
 *
 * Dependencies:
 * - RimWorld tick system
 * - Game time utilities
 *
 * Responsibilities:
 * - Track last scan tick.
 * - Trigger scan at configured intervals.
 *
 * Do NOT:
 * - Do not scan maps directly.
 * - Do not enqueue books yourself.
 */
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Art.scanner
{
    public static class BookScanScheduler
    {
        private static int _lastScanTick = -1;
        private static readonly HashSet<int> ScannedMapIds = new HashSet<int>();

        public static void OnMapLoaded(Map map)
        {
            if (map == null) return;
            if (ScannedMapIds.Contains(map.uniqueID)) return;

            ScannedMapIds.Add(map.uniqueID);
            MapBookScanner.Scan(map);
            _lastScanTick = GenTicks.TicksGame;
        }

        public static void TryDailyScan()
        {
            if (Find.Maps == null || Find.Maps.Count == 0) return;

            int currentTick = GenTicks.TicksGame;
            if (_lastScanTick < 0)
            {
                _lastScanTick = currentTick;
                return;
            }

            if (currentTick - _lastScanTick < GenDate.TicksPerDay) return;

            _lastScanTick = currentTick;
            for (int i = 0; i < Find.Maps.Count; i++)
            {
                MapBookScanner.Scan(Find.Maps[i]);
            }
        }
    }
}
