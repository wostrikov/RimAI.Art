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
    using static QuestEventScheduler;

    internal static class QuestEventWarningFlow
    {
    internal static void TryScheduleWarning(LiteratureSaveData data, int tick)
    {
        if (_warningPending) return;
        if (data.NextWarningQuestTick <= 0)
            data.NextWarningQuestTick = tick + Rand.RangeInclusive(WarningMinIntervalTicks, WarningMaxIntervalTicks);
        if (tick < data.NextWarningQuestTick) return;

        var map = QuestEventSchedulerWorld.GetBestPlayerMap();
        var initiator = QuestEventSchedulerWorld.GetAnyColonist(map);
        if (map == null || initiator == null)
        {
            data.NextWarningQuestTick = tick + RetryTicks;
            return;
        }

        var faction = QuestEventSchedulerWorld.GetRandomHostileFaction();
        if (faction == null)
        {
            data.NextWarningQuestTick = tick + RetryTicks;
            return;
        }

        var settlement = QuestEventSchedulerWorld.GetTradeSettlement(faction);
        if (settlement == null)
        {
            data.NextWarningQuestTick = tick + RetryTicks;
            return;
        }

        int silverDemand = Mathf.Max(1, Mathf.RoundToInt(map.wealthWatcher?.WealthTotal * WarningDemandWealthFactor ?? 0f));
        float raidPoints = StorytellerUtility.DefaultThreatPointsNow(map) * WarningRaidPointsFactor;

        var request = WarningQuestRequest.BuildRequest(faction, settlement, map, silverDemand, OfferDays, DeliveryDays);
        if (request == null)
        {
            data.NextWarningQuestTick = tick + RetryTicks;
            return;
        }

        var pending = new PendingWarningQuest(map, faction, settlement, silverDemand, raidPoints);
        _warningPending = true;
        Log.Message($"{LogPrefix} Scheduling warning quest from {faction.Name}.");

        var task = IndependentBookLlmClient.QueryJsonAsync<QuestTextSpec>(request);
        task.ContinueWith(t =>
        {
            var spec = t.Status == TaskStatus.RanToCompletion ? t.Result : null;
            EnqueueAction(() => ApplyWarningResult(pending, spec));
        }, TaskScheduler.Default);
    }

    internal static void ApplyWarningResult(PendingWarningQuest pending, QuestTextSpec spec)
    {
        _warningPending = false;
        var data = LiteratureSaveData.Current;
        if (data == null || Find.TickManager == null) return;

        int tick = Find.TickManager.TicksGame;
        if (!IsPendingValid(pending))
        {
            data.NextWarningQuestTick = tick + RetryTicks;
            return;
        }

        string title = QuestEventSchedulerWorld.ResolveTitle(spec?.Title, BuildWarningFallbackTitle(pending.Faction));
        string description = QuestEventSchedulerWorld.ResolveDescription(spec?.Description, BuildWarningFallbackDescription(pending));

        var quest = BuildWarningQuest(pending, title, description);
        if (quest == null)
        {
            data.NextWarningQuestTick = tick + RetryTicks;
            return;
        }

        Find.QuestManager.Add(quest);
        QuestEventSchedulerWorld.SendQuestAvailableLetter(quest, pending.Faction);

        var record = new WarningRaidRecord
        {
            QuestId = quest.id,
            DueTick = quest.acceptanceExpireTick + Rand.RangeInclusive(RaidDelayMinTicks, RaidDelayMaxTicks),
            AcceptedTick = -1,
            Points = pending.RaidPoints,
            Faction = pending.Faction,
            TargetParent = pending.Map.Parent
        };
        data.WarningRaidQueue.Add(record);

        data.NextWarningQuestTick = tick + Rand.RangeInclusive(WarningMinIntervalTicks, WarningMaxIntervalTicks);
    }

    internal static void ProcessWarningRaidQueue(int tick)
    {
        var data = LiteratureSaveData.Current;
        if (data?.WarningRaidQueue == null || data.WarningRaidQueue.Count == 0) return;
        if (Find.QuestManager == null) return;

        for (int i = data.WarningRaidQueue.Count - 1; i >= 0; i--)
        {
            var record = data.WarningRaidQueue[i];
            if (record == null)
            {
                data.WarningRaidQueue.RemoveAt(i);
                continue;
            }

            var quest = Find.QuestManager.QuestsListForReading.FirstOrDefault(q => q.id == record.QuestId);
            if (quest == null)
            {
                data.WarningRaidQueue.RemoveAt(i);
                continue;
            }

            if (quest.State == QuestState.EndedSuccess)
            {
                data.WarningRaidQueue.RemoveAt(i);
                continue;
            }

            if (quest.EverAccepted && record.AcceptedTick < 0)
            {
                record.AcceptedTick = quest.acceptanceTick;
                record.DueTick = quest.acceptanceTick + DeliveryTicks + Rand.RangeInclusive(RaidDelayMinTicks, RaidDelayMaxTicks);
                Log.Message($"{LogPrefix} Warning quest accepted; raid rescheduled (questId={quest.id}).");
            }

            if (record.DueTick > 0 && tick >= record.DueTick)
            {
                if (TryExecuteWarningRaid(record))
                    data.WarningRaidQueue.RemoveAt(i);
                else
                    data.WarningRaidQueue.RemoveAt(i);
            }
        }
    }

    internal static bool TryExecuteWarningRaid(WarningRaidRecord record)
    {
        if (record == null) return false;
        var map = ResolveRaidMap(record);
        if (map == null) return false;
        if (record.Faction == null || record.Faction.defeated) return false;

        var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
        parms.faction = record.Faction;
        parms.points = Mathf.Max(StorytellerUtility.GlobalPointsMin(), record.Points);
        parms.forced = true;

        bool fired = IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
        Log.Message($"{LogPrefix} Warning raid fired={fired} faction={record.Faction.Name} points={parms.points:F0}.");
        return fired;
    }

    internal static Map ResolveRaidMap(WarningRaidRecord record)
    {
        if (record?.TargetParent != null && record.TargetParent.HasMap)
            return record.TargetParent.Map;
        return QuestEventSchedulerWorld.GetBestPlayerMap();
    }
    internal static Quest BuildWarningQuest(PendingWarningQuest pending, string title, string description)
    {
        var def = DefDatabase<QuestScriptDef>.GetNamed(WarningQuestDefName, false);
        if (def == null)
        {
            Log.Warning($"{LogPrefix} Missing QuestScriptDef {WarningQuestDefName}.");
            return null;
        }

        var quest = Quest.MakeRaw();
        quest.root = def;
        quest.name = title;
        quest.description = description;
        quest.challengeRating = 1;
        quest.acceptanceExpireTick = quest.appearanceTick + OfferTicks;

        var involved = new QuestPart_InvolvedFactions();
        involved.factions.Add(pending.Faction);
        quest.AddPart(involved);

        string questTag = QuestEventSchedulerWorld.MakeTradeQuestTag(quest);
        QuestUtility.AddQuestTag(pending.Settlement, questTag);
        string fulfilledSignal = $"{questTag}.{QuestUtility.QuestTargetSignalPart_TradeRequestFulfilled}";

        var tradeRequest = new QuestPart_InitiateTradeRequest
        {
            inSignal = quest.InitiateSignal,
            settlement = pending.Settlement,
            requestedThingDef = ThingDefOf.Silver,
            requestedCount = pending.SilverDemand,
            requestDuration = DeliveryTicks,
            keepAfterQuestEnds = false
        };
        quest.AddPart(tradeRequest);

        var delay = new QuestPart_Delay
        {
            inSignalEnable = quest.InitiateSignal,
            delayTicks = DeliveryTicks,
            isBad = true,
            expiryInfoPart = "RimTalkLE_Quest_Warning_Expiry".Translate()
        };
        quest.AddPart(delay);

        var endSuccess = new QuestPart_QuestEnd
        {
            inSignal = fulfilledSignal,
            outcome = QuestEndOutcome.Success,
            sendLetter = true,
            playSound = true
        };
        quest.AddPart(endSuccess);

        var endFail = new QuestPart_QuestEnd
        {
            inSignal = delay.OutSignalCompleted,
            outcome = QuestEndOutcome.Fail,
            sendLetter = true,
            playSound = true
        };
        quest.AddPart(endFail);

        return quest;
    }
    internal static bool IsPendingValid(PendingWarningQuest pending)
    {
        return pending != null
            && pending.Map != null
            && pending.Map.IsPlayerHome
            && pending.Faction != null
            && pending.Settlement != null
            && pending.Settlement.Faction == pending.Faction;
    }
    internal static string BuildWarningFallbackTitle(Faction faction)
    {
        return "RimTalkLE_Quest_Warning_TitleFallback".Translate(faction?.Name ?? "Faction");
    }

    internal static string BuildWarningFallbackDescription(PendingWarningQuest pending)
    {
        var sb = new StringBuilder();
        sb.AppendLine("RimTalkLE_Quest_Warning_DescIntro".Translate(pending.Faction.Name, pending.Settlement.LabelCap));
        sb.AppendLine("RimTalkLE_Quest_Warning_DescDemand".Translate(pending.SilverDemand.ToString()));
        sb.AppendLine("RimTalkLE_Quest_Warning_DescMethod".Translate());
        sb.AppendLine("RimTalkLE_Quest_Warning_DescThreat".Translate());
        sb.AppendLine();
        sb.AppendLine("RimTalkLE_Quest_Warning_DescTiming".Translate(OfferDays.ToString(), DeliveryDays.ToString()));
        return sb.ToString().TrimEnd();
    }
    internal sealed class PendingWarningQuest
    {
        public Map Map { get; }
        public Faction Faction { get; }
        public Settlement Settlement { get; }
        public int SilverDemand { get; }
        public float RaidPoints { get; }

        public PendingWarningQuest(Map map, Faction faction, Settlement settlement, int silverDemand, float raidPoints)
        {
            Map = map;
            Faction = faction;
            Settlement = settlement;
            SilverDemand = silverDemand;
            RaidPoints = raidPoints;
        }
    }
    }
}
