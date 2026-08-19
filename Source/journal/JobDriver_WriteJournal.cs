/*
 * Purpose:
 * - Pawn writes a journal entry at a table after collecting logs.
 */
using System.Collections.Generic;
using Ustas.RimAI.Art.book;
using Ustas.RimAI.Art.integration;
using Ustas.RimAI.Art.scanner.queue;
using Ustas.RimAI.Art.storage;
using Ustas.RimAI.Art.synopsis.model;
using RimWorld;
using Verse;
using Verse.AI;
using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Art.journal
{
    public sealed class JobDriver_WriteJournal : JobDriver
    {
        private const int WriteDurationTicks = 2400;

        private Building Table => job.targetA.Thing as Building;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            bool reserved = pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
            RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] [Journal] Reserve table={job.targetA.Thing?.LabelCap ?? "null"} result={reserved} pawn={pawn?.LabelShort ?? "null"}");
            return reserved;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => Table == null || !JournalUtility.IsTable(Table));

            yield return Toils_Reserve.Reserve(TargetIndex.A);

            for (int i = 0; i < JournalUtility.LogsRequired; i++)
            {
                yield return FindNextLogToil();
                yield return Toils_Reserve.Reserve(TargetIndex.B);
                yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch);
                yield return Toils_Haul.StartCarryThing(TargetIndex.B, true);
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
                yield return ConsumeCarriedThingToil();
            }

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            var write = Toils_General.Wait(WriteDurationTicks);
            write.WithProgressBarToilDelay(TargetIndex.A);
            write.FailOnCannotTouch(TargetIndex.A, PathEndMode.InteractionCell);
            yield return write;

            yield return FinishJournalToil();
        }

        private Toil FindNextLogToil()
        {
            var toil = new Toil();
            toil.initAction = () =>
            {
                var log = JournalUtility.FindClosestLog(pawn);
                if (log == null)
                {
                    RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] [Journal] No available logs for {pawn?.LabelShort ?? "null"}; ending job.");
                    JobFailReason.Is("RimTalkLE_FloatMenu_WriteJournalMissingLogs".Translate());
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                job.targetB = log;
                job.count = 1;
                RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] [Journal] Found log {log.LabelCap} at {log.Position}.");
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        private Toil ConsumeCarriedThingToil()
        {
            var toil = new Toil();
            toil.initAction = () =>
            {
                var carried = pawn.carryTracker?.CarriedThing;
                if (carried != null)
                {
                    RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] [Journal] Consuming carried log {carried.LabelCap} x{carried.stackCount}.");
                    pawn.carryTracker.DestroyCarriedThing();
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        private Toil FinishJournalToil()
        {
            var toil = new Toil();
            toil.initAction = () =>
            {
                var def = JournalDefOf.RimTalk_JournalBook;
                if (def == null)
                {
                    def = DefDatabase<ThingDef>.GetNamedSilentFail("RimTalk_JournalBook");
                    if (def != null)
                        RimAiLog.Info(RimAiLogCategory.Art, "[RimAI.Art] [Journal] Loaded RimTalk_JournalBook via DefDatabase fallback.");
                }
                if (def == null || Table?.Map == null)
                {
                    RimAiLog.Info(RimAiLogCategory.Art, "[RimAI.Art] [Journal] Missing journal def or map; cannot spawn journal book.");
                    return;
                }

                var journal = ThingMaker.MakeThing(def);
                var map = Table.Map;
                var dropCell = Table.InteractionCell;
                GenPlace.TryPlaceThing(journal, dropCell, map, ThingPlaceMode.Near);
                RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] [Journal] Spawned journal book at {dropCell}.");

                var meta = BookClassifier.Classify(journal);
                if (meta != null)
                {
                    if (BookFilterPolicy.IsAllowed(meta))
                    {
                        ApplyPlaceholder(meta, pawn);
                        PendingBookQueue.Enqueue(meta, pawn);
                        RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] [Journal] Enqueued journal book for LLM (type={meta.Type}).");
                    }
                    else
                    {
                        RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] [Journal] Journal book filtered out by settings ({meta.DefName}).");
                    }
                }
                else
                {
                    RimAiLog.Info(RimAiLogCategory.Art, "[RimAI.Art] [Journal] Failed to classify spawned journal book.");
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        private static void ApplyPlaceholder(BookMeta meta, Pawn author)
        {
            if (meta == null || author == null) return;
            if (meta.Type != BookType.Journal) return;

            var authorName = author.LabelShortCap ?? author.Name?.ToStringShort ?? "Unknown";
            var title = "RimTalkLE_JournalPlaceholderTitle".Translate(authorName);
            var text = "RimTalkLE_JournalPlaceholderText".Translate(authorName);
            var synopsis = new BookSynopsis
            {
                Title = title,
                Synopsis = text
            };

            BookTextApplier.Apply(meta, synopsis);
        }
    }
}
