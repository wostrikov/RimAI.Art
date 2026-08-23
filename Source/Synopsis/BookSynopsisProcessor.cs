using System;
using System.Threading.Tasks;
using Ustas.RimAI.Communication.Service;
using Ustas.RimAI.Art.Authoring;
using Ustas.RimAI.Art.Authoring.LLM;
using Ustas.RimAI.Art.Books;
using Ustas.RimAI.Art.Integration;
using Ustas.RimAI.Art.Policy;
using Ustas.RimAI.Art.Journal;
using Ustas.RimAI.Art.Scanner.Queue;
using Ustas.RimAI.Art.Settings;
using Ustas.RimAI.Art.Storage;
using Ustas.RimAI.Art.Storage.Save;
using Ustas.RimAI.Art.Synopsis.Model;
using Verse;
using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Art.Synopsis
{
    public static class BookSynopsisProcessor
    {
        private const int MaxAttempts = 3;
        private static bool _processing;

        public static void Tick()
        {
            if (!ArtComposition.Current.IsStarted) return;

            var settings = LiteratureMod.Settings;
            if (settings != null && !settings.enabled) return;

            if (_processing) return;
            if (AIService.IsBusy()) return;
            if (!PendingBookQueue.TryDequeue(out var record)) return;
            if (record == null || record.Meta == null) return;
            if (record.Meta.Thing == null || record.Meta.Thing.DestroyedOrNull()) return;
            if (!BookFilterPolicy.IsAllowed(record.Meta)) return;

            RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Processing book {record.Meta.Title} ({record.Meta.DefName}) [{record.Meta.Type}].");

            var cache = LiteratureSaveData.Current?.SynopsisCache;
            if (cache == null) return;

            if (cache.TryGet(record.Key, out var cached)
                && ArtDescriptionPipeline.ShouldServeCached(ToSnapshot(cached)))
            {
                BookTextApplier.Apply(record.Meta, cached.ToSynopsis());
                RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Applied cached synopsis for {record.Meta.DefName}.");
                return;
            }

            record.IncrementAttempts();
            _processing = true;

            var summaryRequest = record.HasAuthor ? MemorySummaryRequest.BuildRequest(record.Author) : null;
            if (record.HasAuthor && summaryRequest == null)
                RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Failed to build memory summary request for {record.Meta.DefName}.");

            var contextPawn = ResolveContextPawn(record);
            if (contextPawn == null)
            {
                RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] No context pawn available for {record.Meta.DefName}; requeue.");
                if (record.Attempts < MaxAttempts)
                    PendingBookQueue.Requeue(record);

                _processing = false;
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    BookSynopsis synopsis = null;

                    if (ArtExperienceBookPolicy.ShouldDispatchExperienceAuthoring(record.HasAuthor, summaryRequest != null))
                    {
                        //RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Prepare Generating from author memories for {record.Meta.DefName}.");
                        if (record.Meta.Type == BookType.Journal)
                        {
                            synopsis = await JournalAuthoringPipeline.GenerateFromSummaryRequestAsync(
                                record.Meta,
                                record.Author,
                                summaryRequest);
                        }
                        else
                        {
                            synopsis = await BookAuthoringPipeline.GenerateFromSummaryRequestAsync(
                                record.Meta,
                                record.Author,
                                summaryRequest);
                        }
                    }

                    if (synopsis == null && record.Meta.Type != BookType.Journal)
                    {
                        //RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Prepare Generating synopsis via LLM for {record.Meta.DefName}.");
                        synopsis = await BookSynopsisService.GetOrGenerateAsync(record.Meta, contextPawn);
                    }
                    else if (synopsis == null)
                    {
                        RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Journal generation failed for {record.Meta.DefName}; generic book fallback suppressed.");
                    }

                    if (synopsis != null)
                    {
                        cache.TryGet(record.Key, out var existing);
                        var generated = ArtDescriptionPipeline.Normalize(synopsis.Title, synopsis.Synopsis);
                        var action = ArtDescriptionPipeline.DecideStore(ToSnapshot(existing), generated);
                        if (action == ArtStoreAction.PreserveManual)
                        {
                            BookTextApplier.Apply(record.Meta, existing.ToSynopsis());
                            RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Preserved manual book override for {record.Meta.DefName}.");
                            return;
                        }

                        if (action == ArtStoreAction.Write)
                        {
                            synopsis.Title = generated.Title;
                            synopsis.Synopsis = generated.Body;
                            cache.Set(record.Key, BookSynopsisRecord.FromGenerated(synopsis, record.Meta.Type, existing));
                            BookTextApplier.Apply(record.Meta, synopsis);
                            return;
                        }
                    }

                    if (record.Attempts < MaxAttempts)
                    {
                        //RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] LLM returned null for {record.Meta.DefName}.");
                        //RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Synopsis generation failed; requeue {record.Meta.DefName} (attempt {record.Attempts}).");
                        PendingBookQueue.Requeue(record);
                    }
                }
                catch (Exception ex)
                {
                    if (record.Attempts < MaxAttempts)
                    {
                        RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] LLM threw exception for {record.Meta.DefName}: {ex.GetType().Name} - {ex.Message}");
                        RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] Exception during synopsis generation; requeue {record.Meta.DefName} (attempt {record.Attempts}).");
                        PendingBookQueue.Requeue(record);
                    }
                }
                finally
                {
                    _processing = false;
                }
            });
        }

        private static Pawn ResolveContextPawn(PendingBookRecord record)
        {
            if (record?.Author != null) return record.Author;

            var map = record?.Meta?.Thing?.Map;
            var pawns = map?.mapPawns?.FreeColonistsSpawned;
            var pawn = TryPickFirst(pawns);
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

        static ArtCacheSnapshot ToSnapshot(BookSynopsisRecord record)
        {
            if (record == null)
                return ArtCacheSnapshot.Missing();
            return new ArtCacheSnapshot
            {
                Found = true,
                IsManualOverride = record.IsManualOverride,
                Title = record.Title,
                Body = record.Synopsis
            };
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
