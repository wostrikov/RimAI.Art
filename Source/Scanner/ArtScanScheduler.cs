/*
 * File: ArtScanScheduler.cs
 *
 * Purpose:
 * - Control WHEN MapArtScanner runs (e.g. once per in-game day).
 */
using System.Collections.Generic;
using RimWorld;
using Ustas.RimAI.Art.Policy;
using Verse;

namespace Ustas.RimAI.Art.Scanner
{
    public static class ArtScanScheduler
    {
        private static int _lastScanTick = -1;
        private static readonly HashSet<int> ScannedMapIds = new HashSet<int>();

        public static void OnMapLoaded(Map map)
        {
            if (map == null) return;
            if (!ArtScanEnqueuePolicy.ShouldScanLoadedMap(ScannedMapIds.Contains(map.uniqueID)))
                return;

            ScannedMapIds.Add(map.uniqueID);
            MapArtScanner.Scan(map);
            _lastScanTick = GenTicks.TicksGame;
        }

        public static void TryDailyScan()
        {
            if (Find.Maps == null || Find.Maps.Count == 0) return;

            int currentTick = GenTicks.TicksGame;
            var cadence = ArtScanEnqueuePolicy.DecideCadence(_lastScanTick, currentTick, GenDate.TicksPerDay);
            if (cadence == ArtScanCadenceAction.ArmFirstTick)
            {
                _lastScanTick = currentTick;
                return;
            }
            if (cadence != ArtScanCadenceAction.ScanDue)
                return;

            _lastScanTick = currentTick;
            for (int i = 0; i < Find.Maps.Count; i++)
            {
                MapArtScanner.Scan(Find.Maps[i]);
            }
        }
    }
}
