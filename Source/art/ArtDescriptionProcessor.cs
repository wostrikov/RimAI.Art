using System;
using System.Threading.Tasks;
using Ustas.RimAI.Art.scanner.queue;
using Ustas.RimAI.Art.storage;
using Ustas.RimAI.Art.storage.save;
using Verse;
using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Art.art
{
    public static class ArtDescriptionProcessor
    {
        private const int MaxAttempts = 3;
        private static bool _processing;
        private static bool _loggedDisabled;

        public static void Tick()
        {
            if (!ArtComposition.Current.IsStarted) return;

            if (!Ustas.RimAI.Art.integration.ArtCacheUtil.IsArtEditingEnabled())
            {
                if (!_loggedDisabled)
                {
                    RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Art processing disabled ({Ustas.RimAI.Art.integration.ArtCacheUtil.DescribeArtSettings()}).");
                    _loggedDisabled = true;
                }
                return;
            }
            _loggedDisabled = false;

            if (_processing) return;
            if (!PendingArtQueue.TryDequeue(out var record)) return;
            if (record == null || record.Meta == null)
            {
                RimAiLog.Info(RimAiLogCategory.Art, "[RimAI.Art] Art queue record invalid; skip.");
                return;
            }
            if (record.Meta.Thing == null || record.Meta.Thing.DestroyedOrNull())
            {
                RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Art record thing invalid; skip {record.Meta.DefName ?? "unknown"}.");
                return;
            }
            if (!ArtDefFilterPolicy.IsAllowed(record.Meta.Thing))
            {
                RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Art record filtered out by settings; skip {record.Meta.DefName ?? "unknown"}.");
                return;
            }

            RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Processing art {record.Meta.ThingLabel} ({record.Meta.DefName}).");

            var cache = LiteratureSaveData.Current?.ArtCache;
            if (cache == null) return;

            if (cache.TryGet(record.Key, out _))
            {
                RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Art description already cached for {record.Meta.DefName}.");
                return;
            }

            record.IncrementAttempts();
            _processing = true;

            var bladelink = record.Meta.Thing.TryGetComp<RimWorld.CompBladelinkWeapon>();
            if (bladelink != null)
            {
                if (bladelink.CodedPawn != null)
                {
                    var pawn = bladelink.CodedPawn;
                    RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Persona weapon detected; using bonded pawn context for {record.Meta.DefName}.");
                    PersonaWeaponAuthoringPipeline.StartGeneration(record.Meta.Thing, pawn, "queue", () => _processing = false);
                    return;
                }

                RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Persona weapon not bonded; skip {record.Meta.DefName}.");
                _processing = false;
                return;
            }

            var contextPawn = ResolveContextPawn(record);
            if (contextPawn == null)
            {
                RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] No context pawn available for art {record.Meta.DefName}; requeue.");
                if (record.Attempts < MaxAttempts)
                    PendingArtQueue.Requeue(record);

                _processing = false;
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    var orchestrator = ArtComposition.Current.Literature;
                    var description = orchestrator == null
                        ? null
                        : await orchestrator.GetOrGenerateArtDescriptionAsync(record.Meta, contextPawn);
                    if (description != null)
                    {
                        if (cache.TryGet(record.Key, out var existing) && existing != null && existing.IsManualOverride)
                        {
                            RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Preserved manual art override for {record.Meta.DefName}.");
                            return;
                        }

                        cache.Set(record.Key, ArtDescriptionRecord.FromGenerated(description, existing));
                        RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Saved art description for {record.Meta.DefName}.");
                        return;
                    }

                    if (record.Attempts < MaxAttempts)
                    {
                        RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] LLM returned null for art {record.Meta.DefName}.");
                        RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Art generation failed; requeue {record.Meta.DefName} (attempt {record.Attempts}).");
                        PendingArtQueue.Requeue(record);
                    }
                }
                catch (Exception ex)
                {
                    if (record.Attempts < MaxAttempts)
                    {
                        RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] LLM threw exception for art {record.Meta.DefName}: {ex.GetType().Name} - {ex.Message}");
                        RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Exception during art generation; requeue {record.Meta.DefName} (attempt {record.Attempts}).");
                        PendingArtQueue.Requeue(record);
                    }
                }
                finally
                {
                    _processing = false;
                }
            });
        }

        private static Pawn ResolveContextPawn(PendingArtRecord record)
        {
            var map = record?.Meta?.Thing?.Map;
            var pawn = TryPickFirst(map?.mapPawns?.FreeColonistsSpawned);
            if (pawn != null) return pawn;

            pawn = TryPickHumanlike(map?.mapPawns?.AllPawnsSpawned);
            if (pawn != null) return pawn;

            var maps = Find.Maps;
            if (maps != null)
            {
                for (int i = 0; i < maps.Count; i++)
                {
                    pawn = TryPickFirst(maps[i]?.mapPawns?.FreeColonistsSpawned);
                    if (pawn != null) return pawn;

                    pawn = TryPickHumanlike(maps[i]?.mapPawns?.AllPawnsSpawned);
                    if (pawn != null) return pawn;
                }
            }

            return null;
        }

        private static Pawn TryPickFirst(System.Collections.Generic.IReadOnlyList<Pawn> pawns)
        {
            if (pawns == null || pawns.Count == 0) return null;
            return pawns[0];
        }

        private static Pawn TryPickHumanlike(System.Collections.Generic.IReadOnlyList<Pawn> pawns)
        {
            if (pawns == null || pawns.Count == 0) return null;
            for (int i = 0; i < pawns.Count; i++)
            {
                var pawn = pawns[i];
                if (pawn?.RaceProps?.Humanlike == true) return pawn;
            }
            return null;
        }
    }
}
