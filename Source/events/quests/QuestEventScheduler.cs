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
        private const int CheckIntervalTicks = GenDate.TicksPerHour;
        private const int OfferDays = 3;
        private const int DeliveryDays = 3;
        private const int OfferTicks = GenDate.TicksPerDay * OfferDays;
        private const int DeliveryTicks = GenDate.TicksPerDay * DeliveryDays;
        private const int AdvertMinIntervalTicks = GenDate.TicksPerDay * 12;
        private const int AdvertMaxIntervalTicks = GenDate.TicksPerDay * 22;
        private const int WarningMinIntervalTicks = GenDate.TicksPerDay * 18;
        private const int WarningMaxIntervalTicks = GenDate.TicksPerDay * 30;
        private const int RetryTicks = GenDate.TicksPerDay * 5;
        private const int RaidDelayMinTicks = GenDate.TicksPerDay;
        private const int RaidDelayMaxTicks = GenDate.TicksPerDay * 2;
        private const int OptionCount = 3;
        private const int MinOptionValue = 500;
        private const int MaxOptionValueBonus = 600;
        private const int MaxStacksPerOption = 6;
        private const float WarningDemandWealthFactor = 0.01f;
        private const float WarningRaidPointsFactor = 1.2f;
        private const string TradeRequestTagSuffix = "RimTalkLE_TradeRequest";
        private const string AdvertQuestDefName = "RimTalkLE_AdvertQuest";
        private const string WarningQuestDefName = "RimTalkLE_WarningQuest";
        private const string LogPrefix = "[RimAI.Art] [QuestEvent]";

        private static int _nextCheckTick;
        private static bool _advertPending;
        private static bool _warningPending;
        private static readonly Queue<Action> PendingActions = new Queue<Action>();
        private static readonly object QueueLock = new object();

        public static void Tick()
        {
            ProcessPendingActions();

            if (Find.TickManager == null) return;
            int tick = Find.TickManager.TicksGame;

            ProcessWarningRaidQueue(tick);

            var settings = LiteratureMod.Settings;
            if (settings != null && !settings.enabled) return;

            // TODO: Temporarily disable AdvertisementQuest and WarningQuest automatic scheduling due to option display issues
            // if (tick < _nextCheckTick) return;
            // _nextCheckTick = tick + CheckIntervalTicks;

            // var data = LiteratueSaveData.Current;
            // if (data == null) return;

            // TryScheduleAdvertisement(data, tick);
            // TryScheduleWarning(data, tick);
        }

        public static void TryHandleGiftDelivery(Settlement settlement, List<ActiveTransporterInfo> transporters)
        {
            if (settlement == null || transporters == null || transporters.Count == 0) return;
            var comp = settlement.GetComponent<TradeRequestComp>();
            if (comp == null || !comp.ActiveRequest) return;

            if (!TryGetRimTalkQuestTags(settlement.questTags, out var tags)) return;
            if (comp.requestThingDef == null || comp.requestCount <= 0) return;

            int deliveredCount = CountThingDef(transporters, comp.requestThingDef);
            if (deliveredCount < comp.requestCount) return;

            QuestUtility.SendQuestTargetSignals(tags, QuestUtility.QuestTargetSignalPart_TradeRequestFulfilled, settlement.Named("SUBJECT"));
            comp.Disable();
            Log.Message($"{LogPrefix} Gift delivery fulfilled trade request for {settlement.LabelCap}.");
        }

        private static void TryScheduleAdvertisement(LiteratueSaveData data, int tick)
        {
            if (_advertPending) return;
            if (data.NextAdvertQuestTick <= 0)
                data.NextAdvertQuestTick = tick + Rand.RangeInclusive(AdvertMinIntervalTicks, AdvertMaxIntervalTicks);
            if (tick < data.NextAdvertQuestTick) return;

            var map = GetBestPlayerMap();
            var initiator = GetAnyColonist(map);
            if (map == null || initiator == null)
            {
                data.NextAdvertQuestTick = tick + RetryTicks;
                return;
            }

            var faction = GetRandomNonHostileFaction();
            if (faction == null)
            {
                data.NextAdvertQuestTick = tick + RetryTicks;
                return;
            }

            var settlement = GetTradeSettlement(faction);
            if (settlement == null)
            {
                data.NextAdvertQuestTick = tick + RetryTicks;
                return;
            }

            if (!TryBuildOfferOptions(faction, out var options))
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
            Log.Message($"{LogPrefix} Scheduling advertisement quest from {faction.Name}.");

            var task = IndependentBookLlmClient.QueryJsonAsync<QuestTextSpec>(request);
            task.ContinueWith(t =>
            {
                var spec = t.Status == TaskStatus.RanToCompletion ? t.Result : null;
                EnqueueAction(() => ApplyAdvertisementResult(pending, spec));
            }, TaskScheduler.Default);
        }

        private static void ApplyAdvertisementResult(PendingAdvertQuest pending, QuestTextSpec spec)
        {
            _advertPending = false;
            var data = LiteratueSaveData.Current;
            if (data == null || Find.TickManager == null) return;

            int tick = Find.TickManager.TicksGame;
            if (!IsPendingValid(pending))
            {
                data.NextAdvertQuestTick = tick + RetryTicks;
                return;
            }

            string title = ResolveTitle(spec?.Title, BuildAdvertFallbackTitle(pending.Faction));
            string description = ResolveDescription(spec?.Description, BuildAdvertFallbackDescription(pending));

            var quest = BuildAdvertisementQuest(pending, title, description);
            if (quest == null)
            {
                data.NextAdvertQuestTick = tick + RetryTicks;
                return;
            }

            Find.QuestManager.Add(quest);
            SendQuestAvailableLetter(quest, pending.Faction);

            data.NextAdvertQuestTick = tick + Rand.RangeInclusive(AdvertMinIntervalTicks, AdvertMaxIntervalTicks);
        }

        private static void TryScheduleWarning(LiteratueSaveData data, int tick)
        {
            if (_warningPending) return;
            if (data.NextWarningQuestTick <= 0)
                data.NextWarningQuestTick = tick + Rand.RangeInclusive(WarningMinIntervalTicks, WarningMaxIntervalTicks);
            if (tick < data.NextWarningQuestTick) return;

            var map = GetBestPlayerMap();
            var initiator = GetAnyColonist(map);
            if (map == null || initiator == null)
            {
                data.NextWarningQuestTick = tick + RetryTicks;
                return;
            }

            var faction = GetRandomHostileFaction();
            if (faction == null)
            {
                data.NextWarningQuestTick = tick + RetryTicks;
                return;
            }

            var settlement = GetTradeSettlement(faction);
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

        private static void ApplyWarningResult(PendingWarningQuest pending, QuestTextSpec spec)
        {
            _warningPending = false;
            var data = LiteratueSaveData.Current;
            if (data == null || Find.TickManager == null) return;

            int tick = Find.TickManager.TicksGame;
            if (!IsPendingValid(pending))
            {
                data.NextWarningQuestTick = tick + RetryTicks;
                return;
            }

            string title = ResolveTitle(spec?.Title, BuildWarningFallbackTitle(pending.Faction));
            string description = ResolveDescription(spec?.Description, BuildWarningFallbackDescription(pending));

            var quest = BuildWarningQuest(pending, title, description);
            if (quest == null)
            {
                data.NextWarningQuestTick = tick + RetryTicks;
                return;
            }

            Find.QuestManager.Add(quest);
            SendQuestAvailableLetter(quest, pending.Faction);

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

        private static void ProcessWarningRaidQueue(int tick)
        {
            var data = LiteratueSaveData.Current;
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

        private static bool TryExecuteWarningRaid(WarningRaidRecord record)
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

        private static Map ResolveRaidMap(WarningRaidRecord record)
        {
            if (record?.TargetParent != null && record.TargetParent.HasMap)
                return record.TargetParent.Map;
            return GetBestPlayerMap();
        }

        private static Quest BuildAdvertisementQuest(PendingAdvertQuest pending, string title, string description)
        {
            var def = DefDatabase<QuestScriptDef>.GetNamed(AdvertQuestDefName, false);
            if (def == null)
            {
                Log.Warning($"{LogPrefix} Missing QuestScriptDef {AdvertQuestDefName}.");
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

            string questTag = MakeTradeQuestTag(quest);
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

        private static Quest BuildWarningQuest(PendingWarningQuest pending, string title, string description)
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

            string questTag = MakeTradeQuestTag(quest);
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

        private static void SendQuestAvailableLetter(Quest quest, Faction faction)
        {
            if (quest == null) return;

            TaggedString label = quest.root != null && !quest.root.questAvailableLetterLabel.NullOrEmpty()
                ? quest.root.questAvailableLetterLabel
                : quest.initiallyAccepted
                    ? "LetterLabelQuestAutomaticallyAcceptedTitle".Translate(quest.name)
                    : "LetterLabelQuestAvailableTitle".Translate(quest.name);

            TaggedString text = quest.root != null && quest.root.questAvailableLetterTextIsDescription
                ? quest.description
                : "LetterNewQuestFromUnknown".Translate() + "\n\n" + "LetterQuestIsNamed".Translate(quest.name);

            if (!quest.initiallyAccepted && quest.TicksUntilExpiry >= 0)
                text += "\n\n" + "LetterQuestRequiresAcceptance".Translate(quest.TicksUntilExpiry.ToStringTicksToPeriod(false, false));

            var letterDef = quest.root?.questAvailableLetterDef ?? IncidentDefOf.GiveQuest_Random.letterDef;
            var letter = LetterMaker.MakeLetter(label, text, letterDef, LookTargets.Invalid, faction, quest);
            letter.title = quest.name;
            Find.LetterStack.ReceiveLetter(letter, LetterTextRewriter.CustomLetterDebugInfo);
        }

        private static bool TryBuildOfferOptions(Faction faction, out List<QuestOfferOption> options)
        {
            options = new List<QuestOfferOption>();
            var allCandidates = GetAllCandidates();
            if (allCandidates.Count == 0) return false;

            var modCandidates = GetModCandidates(faction, allCandidates);
            var usedDefs = new HashSet<ThingDef>();

            for (int i = 0; i < OptionCount; i++)
            {
                QuestOfferOption option;
                if (!TryCreateOfferOption(modCandidates, usedDefs, out option) &&
                    !TryCreateOfferOption(allCandidates, usedDefs, out option))
                    return false;

                option.Key = $"Option {(char)('A' + i)}";
                options.Add(option);
            }

            return true;
        }

        private static List<ThingDef> GetAllCandidates()
        {
            var source = ThingSetMakerUtility.allGeneratableItems;
            if (source == null || source.Count == 0)
                source = DefDatabase<ThingDef>.AllDefsListForReading;

            return source.Where(IsValidTradeItem).ToList();
        }

        private static List<ThingDef> GetModCandidates(Faction faction, List<ThingDef> allCandidates)
        {
            if (faction?.def?.modContentPack == null || faction.def.modContentPack.IsCoreMod)
                return new List<ThingDef>();

            return allCandidates.Where(def => def.modContentPack == faction.def.modContentPack).ToList();
        }

        private static bool TryCreateOfferOption(
            List<ThingDef> candidates,
            HashSet<ThingDef> usedDefs,
            out QuestOfferOption option)
        {
            option = null;
            if (candidates == null || candidates.Count == 0) return false;

            int targetValue = Rand.RangeInclusive(MinOptionValue, MinOptionValue + MaxOptionValueBonus);
            for (int attempt = 0; attempt < 80; attempt++)
            {
                var def = candidates.RandomElement();
                if (def == null || usedDefs.Contains(def)) continue;

                if (!TryBuildOffer(def, targetValue, out var dropItems, out var rewardPreview, out var totalValue))
                    continue;

                option = new QuestOfferOption
                {
                    SilverCost = Mathf.RoundToInt(totalValue),
                    TotalValue = totalValue,
                    ItemsLabel = GenLabel.ThingsLabel(rewardPreview),
                    DropItems = dropItems,
                    RewardPreview = rewardPreview
                };
                usedDefs.Add(def);
                return true;
            }

            return false;
        }

        private static bool TryBuildOffer(
            ThingDef def,
            int targetValue,
            out List<ThingDefCountClass> dropItems,
            out List<Thing> rewardPreview,
            out float totalValue)
        {
            dropItems = new List<ThingDefCountClass>();
            rewardPreview = new List<Thing>();
            totalValue = 0f;
            if (def == null) return false;

            float baseValue = def.BaseMarketValue;
            if (baseValue <= 0.01f) return false;

            int countNeeded = Mathf.Max(1, Mathf.CeilToInt(targetValue / baseValue));
            int stackLimit = def.stackLimit > 0 ? def.stackLimit : countNeeded;
            int stacks = Mathf.CeilToInt((float)countNeeded / stackLimit);
            if (stacks > MaxStacksPerOption) return false;

            int remaining = countNeeded;
            while (remaining > 0)
            {
                int count = Mathf.Min(remaining, stackLimit);
                dropItems.Add(new ThingDefCountClass(def, count));
                AddPreviewThing(rewardPreview, def, count);
                totalValue += baseValue * count;
                remaining -= count;
            }

            return rewardPreview.Count > 0;
        }

        private static void AddPreviewThing(List<Thing> preview, ThingDef def, int count)
        {
            if (preview == null || def == null || count <= 0) return;
            ThingDef stuff = def.MadeFromStuff ? GenStuff.RandomStuffByCommonalityFor(def) : null;
            var thing = ThingMaker.MakeThing(def, stuff);
            thing.stackCount = count;
            var comp = thing.TryGetComp<CompQuality>();
            if (comp != null)
                comp.SetQuality(QualityCategory.Normal, null);
            preview.Add(thing);
        }

        private static bool IsValidTradeItem(ThingDef def)
        {
            if (def == null) return false;
            if (def == ThingDefOf.Silver) return false;
            if (!def.PlayerAcquirable) return false;
            if (def.tradeability == Tradeability.None) return false;
            if (def.BaseMarketValue <= 0.01f) return false;
            if (!ThingSetMakerUtility.CanGenerate(def)) return false;
            if (def.IsIngestible) return false;
            return true;
        }

        private static bool TryGetRimTalkQuestTags(List<string> tags, out List<string> matches)
        {
            matches = null;
            if (tags == null || tags.Count == 0) return false;
            for (int i = 0; i < tags.Count; i++)
            {
                var tag = tags[i];
                if (string.IsNullOrWhiteSpace(tag)) continue;
                if (!tag.Contains(TradeRequestTagSuffix)) continue;
                matches ??= new List<string>();
                matches.Add(tag);
            }
            return matches != null && matches.Count > 0;
        }

        private static int CountThingDef(List<ActiveTransporterInfo> transporters, ThingDef def)
        {
            if (transporters == null || def == null) return 0;
            int total = 0;
            for (int i = 0; i < transporters.Count; i++)
            {
                var container = transporters[i]?.innerContainer;
                if (container == null) continue;
                for (int j = 0; j < container.Count; j++)
                {
                    var thing = container[j];
                    if (thing?.def != def) continue;
                    total += thing.stackCount;
                }
            }
            return total;
        }

        private static string MakeTradeQuestTag(Quest quest)
        {
            return quest == null ? string.Empty : $"Quest{quest.id}.{TradeRequestTagSuffix}";
        }

        private static bool IsPendingValid(PendingAdvertQuest pending)
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

        private static bool IsPendingValid(PendingWarningQuest pending)
        {
            return pending != null
                && pending.Map != null
                && pending.Map.IsPlayerHome
                && pending.Faction != null
                && pending.Settlement != null
                && pending.Settlement.Faction == pending.Faction;
        }

        private static Faction GetRandomNonHostileFaction()
        {
            var factions = Find.FactionManager?.AllFactionsVisible;
            if (factions == null) return null;
            var candidates = new List<Faction>();
            foreach (var faction in factions)
            {
                if (faction == null || faction.IsPlayer || faction.Hidden || faction.defeated) continue;
                if (faction.HostileTo(Faction.OfPlayer)) continue;
                if (faction.def?.permanentEnemy == true) continue;
                candidates.Add(faction);
            }
            return candidates.Count > 0 ? candidates.RandomElement() : null;
        }

        private static Faction GetRandomHostileFaction()
        {
            var factions = Find.FactionManager?.AllFactionsVisible;
            if (factions == null) return null;
            var candidates = new List<Faction>();
            foreach (var faction in factions)
            {
                if (faction == null || faction.IsPlayer || faction.Hidden || faction.defeated) continue;
                if (!faction.HostileTo(Faction.OfPlayer)) continue;
                if (faction.def?.permanentEnemy == true) continue;
                candidates.Add(faction);
            }
            return candidates.Count > 0 ? candidates.RandomElement() : null;
        }

        private static Settlement GetTradeSettlement(Faction faction)
        {
            if (faction == null) return null;
            var settlements = Find.WorldObjects?.Settlements;
            if (settlements == null) return null;

            var candidates = new List<Settlement>();
            for (int i = 0; i < settlements.Count; i++)
            {
                var settlement = settlements[i];
                if (settlement == null || settlement.Faction != faction) continue;
                var comp = settlement.GetComponent<TradeRequestComp>();
                if (comp == null || comp.ActiveRequest) continue;
                candidates.Add(settlement);
            }

            return candidates.Count > 0 ? candidates.RandomElement() : null;
        }

        private static Map GetBestPlayerMap()
        {
            var maps = Find.Maps;
            if (maps == null) return null;
            for (int i = 0; i < maps.Count; i++)
            {
                var map = maps[i];
                if (map != null && map.IsPlayerHome && map.mapPawns?.FreeColonistsSpawned?.Count > 0)
                    return map;
            }
            return null;
        }

        private static Pawn GetAnyColonist(Map map)
        {
            var pawns = map?.mapPawns?.FreeColonistsSpawned;
            if (pawns == null || pawns.Count == 0) return null;
            return pawns[0];
        }

        private static string ResolveTitle(string value, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            return fallback;
        }

        private static string ResolveDescription(string value, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            return fallback;
        }

        private static string BuildAdvertFallbackTitle(Faction faction)
        {
            return "RimTalkLE_Quest_Advert_TitleFallback".Translate(faction?.Name ?? "Faction");
        }

        private static string BuildAdvertFallbackDescription(PendingAdvertQuest pending)
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

        private static string BuildWarningFallbackTitle(Faction faction)
        {
            return "RimTalkLE_Quest_Warning_TitleFallback".Translate(faction?.Name ?? "Faction");
        }

        private static string BuildWarningFallbackDescription(PendingWarningQuest pending)
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

        private static void ProcessPendingActions()
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

        private static void EnqueueAction(Action action)
        {
            if (action == null) return;
            lock (QueueLock)
                PendingActions.Enqueue(action);
        }

        [DebugAction("RimTalk LE", "Debug trigger advertisement quest", false, false, false, false, false, 0, false,
            actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTriggerAdvertisementQuest()
        {
            var data = LiteratueSaveData.Current;
            if (data == null || Find.TickManager == null) return;
            int tick = Find.TickManager.TicksGame;
            data.NextAdvertQuestTick = tick;
            TryScheduleAdvertisement(data, tick);
        }

        [DebugAction("RimTalk LE", "Debug trigger warning quest", false, false, false, false, false, 0, false,
            actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTriggerWarningQuest()
        {
            var data = LiteratueSaveData.Current;
            if (data == null || Find.TickManager == null) return;
            int tick = Find.TickManager.TicksGame;
            data.NextWarningQuestTick = tick;
            TryScheduleWarning(data, tick);
        }

        private sealed class PendingAdvertQuest
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

        private sealed class PendingWarningQuest
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
