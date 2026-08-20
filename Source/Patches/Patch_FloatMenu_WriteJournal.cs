/*
 * Purpose:
 * - Add a right-click float menu option to write a journal at any table.
 */
using System;
using Ustas.RimAI.Art.Journal;
using Ustas.RimAI.Art.Settings;
using RimWorld;
using Verse;
using Verse.AI;
using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Art.Patches
{
    public sealed class FloatMenuOptionProvider_WriteJournal : FloatMenuOptionProvider
    {
        protected override bool Drafted => false;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;

        protected override bool AppliesInt(FloatMenuContext context)
        {
            var settings = LiteratureMod.Settings;
            return settings == null || settings.enabled;
        }

        protected override FloatMenuOption GetSingleOption(FloatMenuContext context)
        {
            if (context == null) return null;

            Pawn pawn = context.FirstSelectedPawn;
            if (pawn == null || !pawn.IsColonistPlayerControlled || pawn.Downed || pawn.Dead) return null;

            if (context.map == null || context.ClickedThings == null) return null;

            Thing table = null;
            for (int i = 0; i < context.ClickedThings.Count; i++)
            {
                Thing thing = context.ClickedThings[i];
                if (JournalUtility.IsTable(thing))
                {
                    table = thing;
                    break;
                }
            }

            if (table == null || table.DestroyedOrNull() || !table.Spawned) return null;

            var jobDef = JournalDefOf.RimTalk_WriteJournal;
            if (jobDef == null)
            {
                jobDef = DefDatabase<JobDef>.GetNamedSilentFail("RimTalk_WriteJournal");
                if (jobDef != null)
                    RimAiLog.Info(RimAiLogCategory.Art, "[RimAI.Art] [Journal] Loaded RimTalk_WriteJournal via DefDatabase fallback.");
                else
                {
                    RimAiLog.Error(RimAiLogCategory.Art, "[RimAI.Art] [Journal] Missing JobDef RimTalk_WriteJournal. Check Defs/Journal/JournalDefs.xml.");
                    return null;
                }
            }

            int logCount = JournalUtility.CountAvailableLogs(context.map);
            if (logCount < JournalUtility.LogsRequired)
            {
                return new FloatMenuOption(
                    "RimTalkLE_FloatMenu_WriteJournalMissingLogs".Translate(),
                    null);
            }

            if (!pawn.CanReach(table, PathEndMode.InteractionCell, Danger.Some))
            {
                return new FloatMenuOption(
                    "RimTalkLE_FloatMenu_WriteJournalNoPath".Translate(),
                    null);
            }

            return new FloatMenuOption(
                "RimTalkLE_FloatMenu_WriteJournal".Translate(),
                () =>
                {
                    try
                    {
                        if (pawn?.jobs == null)
                        {
                            RimAiLog.Warning(RimAiLogCategory.Art, "[RimAI.Art] [Journal] Pawn or job tracker missing; cannot start journal job.");
                            return;
                        }

                        if (table == null || table.DestroyedOrNull() || !table.Spawned)
                        {
                            RimAiLog.Warning(RimAiLogCategory.Art, "[RimAI.Art] [Journal] Table missing when issuing journal job.");
                            return;
                        }

                        var job = JobMaker.MakeJob(jobDef, table);
                        bool started = pawn.jobs.TryTakeOrderedJob(job);
                        RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] [Journal] Write journal job issued for {pawn.LabelShort}. started={started}");
                    }
                    catch (Exception ex)
                    {
                        RimAiLog.Error(RimAiLogCategory.Art, $"[RimAI.Art] [Journal] Failed to start journal job: {ex}");
                    }
                },
                MenuOptionPriority.Default,
                null,
                table);
        }
    }
}
