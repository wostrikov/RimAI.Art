/*
 * Purpose:
 * - Generate persona weapon name/description from the bonded pawn's memory summary.
 */
using System;
using System.Threading.Tasks;
using Ustas.RimAI.Art;
using Ustas.RimAI.Art.Art.LLM;
using Ustas.RimAI.Art.Authoring.LLM;
using Ustas.RimAI.Art.Integration;
using Ustas.RimAI.Art.Settings;
using Ustas.RimAI.Art.Storage;
using Ustas.RimAI.Art.Storage.Save;
using Verse;
using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Art.Art
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
                RimAiLog.Info(RimAiLogCategory.Art, "[RimAI.Art] Persona weapon update skipped: bonded pawn is not an initialized player-faction pawn.");
                onComplete?.Invoke();
                return;
            }

            var settings = LiteratureMod.Settings;
            if (settings == null || !settings.allowArtWeaponEdits || !settings.allowArtLabelEdits)
            {
                RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Persona weapon update skipped: settings disabled ({ArtCacheUtil.DescribeArtSettings()}).");
                onComplete?.Invoke();
                return;
            }
            if (!ArtDefFilterPolicy.IsAllowed(weapon))
            {
                RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Persona weapon update skipped: filtered out by settings ({weapon.def?.defName ?? "unknown"}).");
                onComplete?.Invoke();
                return;
            }

            var cache = LiteratureSaveData.Current?.ArtCache;
            if (cache == null)
            {
                RimAiLog.Info(RimAiLogCategory.Art, "[RimAI.Art] Persona weapon update skipped: ArtCache unavailable.");
                onComplete?.Invoke();
                return;
            }

            if (!ArtKeyProvider.TryGetKey(weapon, out var key))
            {
                RimAiLog.Info(RimAiLogCategory.Art, "[RimAI.Art] Persona weapon update skipped: invalid art key.");
                onComplete?.Invoke();
                return;
            }

            var meta = new ArtMeta(weapon);
            var summaryRequest = MemorySummaryRequest.BuildRequest(pawn);
            if (summaryRequest == null)
            {
                RimAiLog.Info(RimAiLogCategory.Art, "[RimAI.Art] Persona weapon update skipped: unable to build memory summary request.");
                onComplete?.Invoke();
                return;
            }

            RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Persona weapon update start ({reason}): {meta.ThingLabel} ({meta.DefName}) for {pawn.LabelShortCap ?? pawn.Name?.ToStringShort ?? "Unknown"}.");

            Task.Run(async () =>
            {
                try
                {
                    var summary = await MemorySummaryRequest.QueryAsync(summaryRequest);
                    if (summary == null)
                    {
                        RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Persona weapon update failed: memory summary null ({meta.DefName}).");
                        return;
                    }

                    var description = await PersonaWeaponRequest.QueryAsync(meta, summary, pawn, summaryRequest.Context);
                    if (description == null)
                    {
                        RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Persona weapon update failed: LLM returned null ({meta.DefName}).");
                        return;
                    }

                    if (cache.TryGet(key, out var existing) && existing != null && existing.IsManualOverride)
                    {
                        RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Persona weapon manual override preserved ({meta.DefName}).");
                        return;
                    }

                    cache.Set(key, ArtDescriptionRecord.FromGenerated(description, existing));
                    RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Persona weapon updated: {meta.DefName}.");
                }
                catch (Exception ex)
                {
                    RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Persona weapon update exception: {ex.GetType().Name} - {ex.Message}");
                }
                finally
                {
                    onComplete?.Invoke();
                }
            });
        }
    }
}
