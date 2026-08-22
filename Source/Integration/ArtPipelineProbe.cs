using System;
using System.Collections.Generic;
using RimWorld;
using Ustas.RimAI.Core.Diagnostics;
using Ustas.RimAI.Core.TestDriver;
using Verse;

namespace Ustas.RimAI.Art.Integration
{
    /// <summary>
    /// Deterministic TestDriver sweep over art/sculpture inspect paths.
    /// Exercises the same CompArt/Thing description surfaces a player click
    /// hits. Does not call a paid provider.
    /// </summary>
    public static class ArtPipelineProbe
    {
        public static void Register()
        {
            TestDriverModuleOperations.Register(
                TestDriverCommandNames.ProbeArt,
                (request, _) => new TestDriverDelegateOperation(() => Run(request)));
        }

        static TestDriverProgress Run(TestDriverRequest request)
        {
            if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null)
                return TestDriverProgress.Failed("probe_art requires a loaded game");

            var mode = request.Arguments.GetString("mode", "click_sweep");
            var correlationId = request.Arguments.GetString("correlationId", request.RequestId);
            if (!string.Equals(mode, "click_sweep", StringComparison.OrdinalIgnoreCase))
                return TestDriverProgress.Failed("mode must be click_sweep");

            return Sweep(correlationId);
        }

        static TestDriverProgress Sweep(string correlationId)
        {
            var map = Find.CurrentMap;
            var inspected = new List<TestDriverJsonWriter>();
            int artCount = 0;
            int sculptureCount = 0;
            int weaponCount = 0;
            int buildingCount = 0;
            int exceptionCount = 0;
            string firstError = null;

            var things = map.listerThings?.AllThings;
            if (things != null)
            {
                for (int i = 0; i < things.Count && inspected.Count < 24; i++)
                {
                    var thing = things[i];
                    if (thing == null || thing.Destroyed)
                        continue;
                    var art = thing.TryGetComp<CompArt>();
                    var blade = thing.TryGetComp<CompBladelinkWeapon>();
                    if (art == null && blade == null)
                        continue;

                    bool sculpture = thing.def?.IsArt == true;
                    bool weapon = thing.def?.IsWeapon == true;
                    bool building = thing.def?.category == ThingCategory.Building;
                    if (sculpture)
                        sculptureCount++;
                    else if (weapon)
                        weaponCount++;
                    else if (building)
                        buildingCount++;
                    else
                        artCount++;

                    string error = InspectThing(thing, art);
                    if (error != null)
                    {
                        exceptionCount++;
                        if (firstError == null)
                            firstError = error;
                    }

                    inspected.Add(new TestDriverJsonWriter()
                        .Text("defName", thing.def?.defName)
                        .Text("kind", sculpture ? "sculpture" : weapon ? "weapon" : building ? "building" : "art")
                        .Flag("hasCompArt", art != null)
                        .Flag("exception", error != null)
                        .Text("error", error));
                }
            }

            return TestDriverProgress.Completed(new TestDriverJsonWriter()
                .Text("mode", "click_sweep")
                .Text("correlationId", correlationId)
                .Integer("inspected", inspected.Count)
                .Integer("sculptures", sculptureCount)
                .Integer("weapons", weaponCount)
                .Integer("buildings", buildingCount)
                .Integer("otherArt", artCount)
                .Integer("exceptionCount", exceptionCount)
                .Flag("EXCEPTION_PRESENT", exceptionCount > 0)
                .Text("firstError", firstError)
                .Flag("paused", Find.TickManager?.Paused ?? true)
                .Integer("ticksGame", Find.TickManager?.TicksGame ?? 0)
                .ObjectArray("things", inspected));
        }

        static string InspectThing(Thing thing, CompArt art)
        {
            try
            {
                _ = thing.Label;
                if (thing is ThingWithComps comps)
                    _ = comps.DescriptionFlavor;

                if (art != null)
                {
                    _ = art.Title;
                    _ = art.GenerateImageDescription();
                    _ = art.GetDescriptionPart();
                    _ = art.TransformLabel(thing.def?.label ?? "art");
                }

                _ = thing.GetInspectString();
                if (thing is ThingWithComps withComps)
                {
                    foreach (var gizmo in withComps.GetGizmos())
                    {
                        if (gizmo == null)
                            continue;
                    }
                }

                ArtCacheUtil.TryGetRecord(thing, out _);
                Find.Selector?.Select(thing, playSound: false, forceDesignatorDeselect: false);
                return null;
            }
            catch (NullReferenceException ex)
            {
                RimAiLog.Error(RimAiLogCategory.Art, "[RimAI.Art] probe_art NRE: " + ex);
                return ex.GetType().Name + ": " + ex.Message;
            }
            catch (InvalidOperationException ex)
            {
                RimAiLog.Error(RimAiLogCategory.Art, "[RimAI.Art] probe_art invalid: " + ex);
                return ex.GetType().Name + ": " + ex.Message;
            }
            catch (ArgumentException ex)
            {
                RimAiLog.Error(RimAiLogCategory.Art, "[RimAI.Art] probe_art argument: " + ex);
                return ex.GetType().Name + ": " + ex.Message;
            }
        }
    }
}
