/*
 * Purpose:
 * - Schedule and generate LLM-driven custom quests (advertisement, warning).
 *
 * Uses:
 * - IndependentBookLlmClient.QueryJsonAsync<T> for JSON output.
 * - RimWorld Quest/QuestPart APIs for quest construction.
 *
 * Responsibilities:
 * - Decide when to create quests.
 * - Build quest parts and letters.
 * - Schedule and execute warning raids after expiry.
 *
 * Design notes:
 * - LLM requests run off-thread; quest creation is queued onto the main thread.
 * - Quest offers expire after 3 days; accepted quests expire after 3 days.
 * - Transport pod gifts can fulfill RimTalk LE trade requests via patch.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LudeonTK;
using Ustas.RimAI.Art.events;
using Ustas.RimAI.Art.settings;
using Ustas.RimAI.Art.storage.save;
using Ustas.RimAI.Art.synopsis.llm;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Art.events.quests
{

    public static class QuestEventScheduler
    {
        internal const int CheckIntervalTicks = GenDate.TicksPerHour;
        internal const int OfferDays = 3;
        internal const int DeliveryDays = 3;
        internal const int OfferTicks = GenDate.TicksPerDay * OfferDays;
        internal const int DeliveryTicks = GenDate.TicksPerDay * DeliveryDays;
        internal const int AdvertMinIntervalTicks = GenDate.TicksPerDay * 12;
        internal const int AdvertMaxIntervalTicks = GenDate.TicksPerDay * 22;
        internal const int WarningMinIntervalTicks = GenDate.TicksPerDay * 18;
        internal const int WarningMaxIntervalTicks = GenDate.TicksPerDay * 30;
        internal const int RetryTicks = GenDate.TicksPerDay * 5;
        internal const int RaidDelayMinTicks = GenDate.TicksPerDay;
        internal const int RaidDelayMaxTicks = GenDate.TicksPerDay * 2;
        internal const int OptionCount = 3;
        internal const int MinOptionValue = 500;
        internal const int MaxOptionValueBonus = 600;
        internal const int MaxStacksPerOption = 6;
        internal const float WarningDemandWealthFactor = 0.01f;
        internal const float WarningRaidPointsFactor = 1.2f;
        internal const string TradeRequestTagSuffix = "RimTalkLE_TradeRequest";
        internal const string AdvertQuestDefName = "RimTalkLE_AdvertQuest";
        internal const string WarningQuestDefName = "RimTalkLE_WarningQuest";
        internal const string LogPrefix = "[RimAI.Art] [QuestEvent]";

        internal static int _nextCheckTick;
        internal static bool _advertPending;
        internal static bool _warningPending;
        internal static readonly Queue<Action> PendingActions = new Queue<Action>();
        internal static readonly object QueueLock = new object();

        public static void Tick()
        {
            ProcessPendingActions();

            if (Find.TickManager == null) return;
            int tick = Find.TickManager.TicksGame;

            QuestEventWarningFlow.ProcessWarningRaidQueue(tick);

            var settings = LiteratureMod.Settings;
            if (settings != null && !settings.enabled) return;

            // TODO: Temporarily disable AdvertisementQuest and WarningQuest automatic scheduling due to option display issues
            // if (tick < _nextCheckTick) return;
            // _nextCheckTick = tick + CheckIntervalTicks;

            // var data = LiteratureSaveData.Current;
            // if (data == null) return;

            // QuestEventAdvertisementFlow.TryScheduleAdvertisement(data, tick);
            // QuestEventWarningFlow.TryScheduleWarning(data, tick);
        }
        public static void TryHandleGiftDelivery(Settlement settlement, List<ActiveTransporterInfo> transporters)
        {
            if (settlement == null || transporters == null || transporters.Count == 0) return;
            var comp = settlement.GetComponent<TradeRequestComp>();
            if (comp == null || !comp.ActiveRequest) return;

            if (!QuestEventSchedulerWorld.TryGetRimTalkQuestTags(settlement.questTags, out var tags)) return;
            if (comp.requestThingDef == null || comp.requestCount <= 0) return;

            int deliveredCount = QuestEventSchedulerWorld.CountThingDef(transporters, comp.requestThingDef);
            if (deliveredCount < comp.requestCount) return;

            QuestUtility.SendQuestTargetSignals(tags, QuestUtility.QuestTargetSignalPart_TradeRequestFulfilled, settlement.Named("SUBJECT"));
            comp.Disable();
            Log.Message($"{LogPrefix} Gift delivery fulfilled trade request for {settlement.LabelCap}.");
        }
        internal static void ProcessPendingActions()
        {
            lock (QueueLock)
            {
                while (PendingActions.Count > 0)
                {
                    var action = PendingActions.Dequeue();
                    action?.Invoke();
                }
            }
        }

        internal static void EnqueueAction(Action action)
        {
            if (action == null) return;
            lock (QueueLock)
                PendingActions.Enqueue(action);
        }

        [DebugAction("RimTalk LE", "Debug trigger advertisement quest", false, false, false, false, false, 0, false,
            actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTriggerAdvertisementQuest()
        {
            var data = LiteratureSaveData.Current;
            if (data == null || Find.TickManager == null) return;
            int tick = Find.TickManager.TicksGame;
            data.NextAdvertQuestTick = tick;
            QuestEventAdvertisementFlow.TryScheduleAdvertisement(data, tick);
        }

        [DebugAction("RimTalk LE", "Debug trigger warning quest", false, false, false, false, false, 0, false,
            actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTriggerWarningQuest()
        {
            var data = LiteratureSaveData.Current;
            if (data == null || Find.TickManager == null) return;
            int tick = Find.TickManager.TicksGame;
            data.NextWarningQuestTick = tick;
            QuestEventWarningFlow.TryScheduleWarning(data, tick);
        }
    }
}
