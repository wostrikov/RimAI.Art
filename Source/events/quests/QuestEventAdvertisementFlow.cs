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
using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Art.events.quests
{
    using static QuestEventScheduler;

    internal static class QuestEventAdvertisementFlow
    {
    internal static void TryScheduleAdvertisement(LiteratureSaveData data, int tick)
    {
        if (_advertPending) return;
        if (data.NextAdvertQuestTick <= 0)
            data.NextAdvertQuestTick = tick + Rand.RangeInclusive(AdvertMinIntervalTicks, AdvertMaxIntervalTicks);
        if (tick < data.NextAdvertQuestTick) return;

        var map = QuestEventSchedulerWorld.GetBestPlayerMap();
        var initiator = QuestEventSchedulerWorld.GetAnyColonist(map);
        if (map == null || initiator == null)
        {
            data.NextAdvertQuestTick = tick + RetryTicks;
            return;
        }

        var faction = QuestEventSchedulerWorld.GetRandomNonHostileFaction();
        if (faction == null)
        {
            data.NextAdvertQuestTick = tick + RetryTicks;
            return;
        }

        var settlement = QuestEventSchedulerWorld.GetTradeSettlement(faction);
        if (settlement == null)
        {
            data.NextAdvertQuestTick = tick + RetryTicks;
            return;
        }

        if (!QuestEventOfferBuilder.TryBuildOfferOptions(faction, out var options))
        {
            data.NextAdvertQuestTick = tick + RetryTicks;
            return;
        }

        var request = AdvertisementQuestRequest.BuildRequest(faction, settlement, map, options, OfferDays, DeliveryDays);
        if (request == null)
        {
            data.NextAdvertQuestTick = tick + RetryTicks;
            return;
        }

        var pending = new PendingAdvertQuest(map, faction, settlement, options);
        _advertPending = true;
        RimAiLog.Info(RimAiLogCategory.Art, $"{LogPrefix} Scheduling advertisement quest from {faction.Name}.");

        var task = IndependentBookLlmClient.QueryJsonAsync<QuestTextSpec>(request);
        task.ContinueWith(t =>
        {
            var spec = t.Status == TaskStatus.RanToCompletion ? t.Result : null;
            EnqueueAction(() => ApplyAdvertisementResult(pending, spec));
        }, TaskScheduler.Default);
    }

    internal static void ApplyAdvertisementResult(PendingAdvertQuest pending, QuestTextSpec spec)
    {
        _advertPending = false;
        var data = LiteratureSaveData.Current;
        if (data == null || Find.TickManager == null) return;

        int tick = Find.TickManager.TicksGame;
        if (!IsPendingValid(pending))
        {
            data.NextAdvertQuestTick = tick + RetryTicks;
            return;
        }

        string title = QuestEventSchedulerWorld.ResolveTitle(spec?.Title, BuildAdvertFallbackTitle(pending.Faction));
        string description = QuestEventSchedulerWorld.ResolveDescription(spec?.Description, BuildAdvertFallbackDescription(pending));

        var quest = BuildAdvertisementQuest(pending, title, description);
        if (quest == null)
        {
            data.NextAdvertQuestTick = tick + RetryTicks;
            return;
        }

        Find.QuestManager.Add(quest);
        QuestEventSchedulerWorld.SendQuestAvailableLetter(quest, pending.Faction);

        data.NextAdvertQuestTick = tick + Rand.RangeInclusive(AdvertMinIntervalTicks, AdvertMaxIntervalTicks);
    }
    internal static Quest BuildAdvertisementQuest(PendingAdvertQuest pending, string title, string description)
    {
        var def = DefDatabase<QuestScriptDef>.GetNamed(AdvertQuestDefName, false);
        if (def == null)
        {
            RimAiLog.Warning(RimAiLogCategory.Art, $"{LogPrefix} Missing QuestScriptDef {AdvertQuestDefName}.");
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

        var choicePart = new QuestPart_Choice
        {
            inSignalChoiceUsed = quest.InitiateSignal
        };

        for (int i = 0; i < pending.Options.Count; i++)
        {
            var option = pending.Options[i];
            if (option == null) continue;

            var choice = new QuestPart_Choice.Choice();

            var tradeRequest = new QuestPart_InitiateTradeRequest
            {
                inSignal = quest.InitiateSignal,
                settlement = pending.Settlement,
                requestedThingDef = ThingDefOf.Silver,
                requestedCount = option.SilverCost,
                requestDuration = DeliveryTicks,
                keepAfterQuestEnds = false
            };
            quest.AddPart(tradeRequest);
            choice.questParts.Add(tradeRequest);

            var dropPods = new QuestPart_DropPods
            {
                inSignal = fulfilledSignal,
                mapParent = pending.Map.Parent,
                useTradeDropSpot = true,
                sendStandardLetter = false,
                canRetargetAnyMap = true
            };
            dropPods.thingDefs.AddRange(option.DropItems);
            quest.AddPart(dropPods);
            choice.questParts.Add(dropPods);

            var delay = new QuestPart_Delay
            {
                inSignalEnable = quest.InitiateSignal,
                delayTicks = DeliveryTicks,
                isBad = true,
                expiryInfoPart = "RimTalkLE_Quest_Advert_Expiry".Translate()
            };
            quest.AddPart(delay);
            choice.questParts.Add(delay);

            var endSuccess = new QuestPart_QuestEnd
            {
                inSignal = fulfilledSignal,
                outcome = QuestEndOutcome.Success,
                sendLetter = true,
                playSound = true
            };
            quest.AddPart(endSuccess);
            choice.questParts.Add(endSuccess);

            var endFail = new QuestPart_QuestEnd
            {
                inSignal = delay.OutSignalCompleted,
                outcome = QuestEndOutcome.Fail,
                sendLetter = true,
                playSound = true
            };
            quest.AddPart(endFail);
            choice.questParts.Add(endFail);

            var reward = new Reward_Items();
            reward.items.AddRange(option.RewardPreview);
            choice.rewards.Add(reward);

            choicePart.choices.Add(choice);
        }

        quest.AddPart(choicePart);
        return quest;
    }
    internal static bool IsPendingValid(PendingAdvertQuest pending)
    {
        return pending != null
            && pending.Map != null
            && pending.Map.IsPlayerHome
            && pending.Faction != null
            && pending.Settlement != null
            && pending.Settlement.Faction == pending.Faction
            && pending.Options != null
            && pending.Options.Count > 0;
    }
    internal static string BuildAdvertFallbackTitle(Faction faction)
    {
        return "RimTalkLE_Quest_Advert_TitleFallback".Translate(faction?.Name ?? "Faction");
    }

    internal static string BuildAdvertFallbackDescription(PendingAdvertQuest pending)
    {
        var sb = new StringBuilder();
        sb.AppendLine("RimTalkLE_Quest_Advert_DescIntro".Translate(pending.Faction.Name, pending.Settlement.LabelCap));
        sb.AppendLine("RimTalkLE_Quest_Advert_DescInstructions".Translate());
        sb.AppendLine();
        for (int i = 0; i < pending.Options.Count; i++)
        {
            var option = pending.Options[i];
            if (option == null) continue;
            sb.AppendLine("RimTalkLE_Quest_Advert_DescOptionLine".Translate(option.Key, option.SilverCost.ToString(), option.ItemsLabel));
        }
        sb.AppendLine();
        sb.AppendLine("RimTalkLE_Quest_Advert_DescTiming".Translate(OfferDays.ToString(), DeliveryDays.ToString()));
        return sb.ToString().TrimEnd();
    }
    internal sealed class PendingAdvertQuest
    {
        public Map Map { get; }
        public Faction Faction { get; }
        public Settlement Settlement { get; }
        public List<QuestOfferOption> Options { get; }

        public PendingAdvertQuest(Map map, Faction faction, Settlement settlement, List<QuestOfferOption> options)
        {
            Map = map;
            Faction = faction;
            Settlement = settlement;
            Options = options ?? new List<QuestOfferOption>();
        }
    }
    }
}
