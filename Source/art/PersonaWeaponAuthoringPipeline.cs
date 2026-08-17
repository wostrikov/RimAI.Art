/*
 * Purpose:
 * - Generate persona weapon name/description from the bonded pawn's memory summary.
 */
using System;
using System.Threading.Tasks;
using Ustas.RimAI.Art;
using Ustas.RimAI.Art.art.llm;
using Ustas.RimAI.Art.authoring.llm;
using Ustas.RimAI.Art.integration;
using Ustas.RimAI.Art.settings;
using Ustas.RimAI.Art.storage;
using Ustas.RimAI.Art.storage.save;
using Verse;

namespace Ustas.RimAI.Art.art
{
    public static class PersonaWeaponAuthoringPipeline
    {
        public static void StartGeneration(Thing weapon, Pawn pawn, string reason, Action onComplete = null)
        {
            if (weapon == null || pawn == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (!PlayerFactionUtility.IsPlayerFactionPawn(pawn))
            {
                Log.Message("[RimAI.Art] Persona weapon update skipped: bonded pawn is not an initialized player-faction pawn.");
                onComplete?.Invoke();
                return;
            }

            var settings = LiteratureMod.Settings;
            if (settings == null || !settings.allowArtWeaponEdits || !settings.allowArtLabelEdits)
            {
                Log.Message($"[RimAI.Art] Persona weapon update skipped: settings disabled ({ArtCacheUtil.DescribeArtSettings()}).");
                onComplete?.Invoke();
                return;
            }
            if (!ArtDefFilterPolicy.IsAllowed(weapon))
            {
                Log.Message($"[RimAI.Art] Persona weapon update skipped: filtered out by settings ({weapon.def?.defName ?? "unknown"}).");
                onComplete?.Invoke();
                return;
            }

            var cache = LiteratureSaveData.Current?.ArtCache;
            if (cache == null)
            {
                Log.Message("[RimAI.Art] Persona weapon update skipped: ArtCache unavailable.");
                onComplete?.Invoke();
                return;
            }

            if (!ArtKeyProvider.TryGetKey(weapon, out var key))
            {
                Log.Message("[RimAI.Art] Persona weapon update skipped: invalid art key.");
                onComplete?.Invoke();
                return;
            }

            var meta = new ArtMeta(weapon);
            var summaryRequest = MemorySummaryRequest.BuildRequest(pawn);
            if (summaryRequest == null)
            {
                Log.Message("[RimAI.Art] Persona weapon update skipped: unable to build memory summary request.");
                onComplete?.Invoke();
                return;
            }

            Log.Message($"[RimAI.Art] Persona weapon update start ({reason}): {meta.ThingLabel} ({meta.DefName}) for {pawn.LabelShortCap ?? pawn.Name?.ToStringShort ?? "Unknown"}.");

            Task.Run(async () =>
            {
                try
                {
                    var summary = await MemorySummaryRequest.QueryAsync(summaryRequest);
                    if (summary == null)
                    {
                        Log.Message($"[RimAI.Art] Persona weapon update failed: memory summary null ({meta.DefName}).");
                        return;
                    }

                    var description = await PersonaWeaponRequest.QueryAsync(meta, summary, pawn, summaryRequest.Context);
                    if (description == null)
                    {
                        Log.Message($"[RimAI.Art] Persona weapon update failed: LLM returned null ({meta.DefName}).");
                        return;
                    }

                    if (cache.TryGet(key, out var existing) && existing != null && existing.IsManualOverride)
                    {
                        Log.Message($"[RimAI.Art] Persona weapon manual override preserved ({meta.DefName}).");
                        return;
                    }

                    cache.Set(key, ArtDescriptionRecord.FromGenerated(description, existing));
                    Log.Message($"[RimAI.Art] Persona weapon updated: {meta.DefName}.");
                }
                catch (Exception ex)
                {
                    Log.Message($"[RimAI.Art] Persona weapon update exception: {ex.GetType().Name} - {ex.Message}");
                }
                finally
                {
                    onComplete?.Invoke();
                }
            });
        }
    }
}
