using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ustas.RimAI.Communication.Service;
using Ustas.RimAI.Art.events.letters;
using Ustas.RimAI.Art.settings;
using Ustas.RimAI.Art.storage.save;
using Ustas.RimAI.Art.synopsis.llm;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Ustas.RimAI.Art.events
{
    public static class LetterEventScheduler
    {
        private const int CheckIntervalTicks = GenDate.TicksPerHour;
        private const int DiplomacyGoodwillDelta = 6;
        private const int DiplomacyRetryTicks = GenDate.TicksPerQuadrum;
        private const int FamilyRetryTicks = GenDate.TicksPerDay * 3;
        private const int FamilyMinIntervalTicks = GenDate.TicksPerDay * 10;
        private const int FamilyMaxIntervalTicks = GenDate.TicksPerDay * 25;
        private static int _nextCheckTick;
        private static bool _diplomacyPending;
        private static bool _familyPending;
        private static readonly object QueueLock = new object();
        private static readonly Queue<Action> PendingActions = new Queue<Action>();

        public static void Tick()
        {
            ProcessPendingActions();

            if (!AreEasterLettersEnabled()) return;

            if (Find.TickManager == null) return;
            int tick = Find.TickManager.TicksGame;
            if (tick < _nextCheckTick) return;
            _nextCheckTick = tick + CheckIntervalTicks;

            var data = LiteratureSaveData.Current;
            if (data == null) return;

            TryScheduleAllyDiplomacy(data, tick);
            TryScheduleFamilyLetter(data, tick);
        }

        public static void DebugTriggerAllyDiplomacy()
        {
            var data = LiteratureSaveData.Current;
            if (data == null || Find.TickManager == null) return;
            int tick = Find.TickManager.TicksGame;
            data.NextAllyDiplomacyTick = tick;
            TryScheduleAllyDiplomacy(data, tick);
        }

        public static void DebugTriggerFamilyLetter()
        {
            var data = LiteratureSaveData.Current;
            if (data == null || Find.TickManager == null) return;
            int tick = Find.TickManager.TicksGame;
            data.NextFamilyLetterTick = tick;
            TryScheduleFamilyLetter(data, tick);
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

        private static bool AreEasterLettersEnabled()
        {
            var settings = LiteratureMod.Settings;
            return settings != null && settings.enabled && settings.allowEasterLetters;
        }

        private static void TryScheduleAllyDiplomacy(LiteratureSaveData data, int tick)
        {
            if (!AreEasterLettersEnabled()) return;
            if (_diplomacyPending) return;
            if (data.NextAllyDiplomacyTick <= 0)
                data.NextAllyDiplomacyTick = tick + Rand.RangeInclusive(GenDate.TicksPerQuadrum, GenDate.TicksPerYear);
            if (tick < data.NextAllyDiplomacyTick) return;
            if (AIService.IsBusy()) return;

            var map = GetBestPlayerMap();
            var initiator = map != null ? GetAnyColonist(map) : null;
            if (initiator == null) return;

            var faction = GetRandomAlliedFaction();
            if (faction == null)
            {
                data.NextAllyDiplomacyTick = tick + DiplomacyRetryTicks;
                return;
            }

            string colonyName = map?.info?.parent?.LabelCap ?? "Colony";
            var request = AllyDiplomacyLetterRequest.BuildRequest(faction, map, colonyName, DiplomacyGoodwillDelta);
            if (request == null)
            {
                data.NextAllyDiplomacyTick = tick + DiplomacyRetryTicks;
                return;
            }

            _diplomacyPending = true;
            Log.Message($"[RimAI.Art] [Letter] Scheduling ally diplomacy letter from {faction.Name}.");

            var task = IndependentBookLlmClient.QueryJsonAsync<AllyDiplomacyLetterSpec>(request);
            task.ContinueWith(t =>
            {
                var spec = t.Status == TaskStatus.RanToCompletion ? t.Result : null;
                EnqueueAction(() => ApplyAllyDiplomacyResult(spec, faction, map));
            }, TaskScheduler.Default);
        }

        private static void ApplyAllyDiplomacyResult(AllyDiplomacyLetterSpec spec, Faction faction, Map map)
        {
            _diplomacyPending = false;
            if (!AreEasterLettersEnabled()) return;

            var data = LiteratureSaveData.Current;
            if (data == null || Find.TickManager == null) return;
            int tick = Find.TickManager.TicksGame;

            if (spec == null || faction == null || faction.defeated)
            {
                data.NextAllyDiplomacyTick = tick + DiplomacyRetryTicks;
                Log.Message("[RimAI.Art] [Letter] Ally diplomacy letter failed; retry scheduled.");
                return;
            }

            string label = !spec.Title.NullOrEmpty()
                ? spec.Title
                : "RimTalkLE_Letter_AllyDiplomacy_Label".Translate();

            string body = !spec.Body.NullOrEmpty()
                ? spec.Body
                : "RimTalkLE_Letter_AllyDiplomacy_Fallback".Translate(faction.Name);

            var goodwillLine = "RimTalkLE_Letter_AllyDiplomacy_GoodwillLine".Translate(DiplomacyGoodwillDelta.ToString());
            if (!goodwillLine.NullOrEmpty())
                body = $"{body}\n\n{goodwillLine}";

            Faction.OfPlayer.TryAffectGoodwillWith(faction, DiplomacyGoodwillDelta, canSendMessage: false, canSendHostilityLetter: false);

            var target = map != null ? new LookTargets(map.Center, map) : LookTargets.Invalid;
            var letter = LetterMaker.MakeLetter(label, body, LetterDefOf.PositiveEvent, target, faction);
            Find.LetterStack.ReceiveLetter(letter, LetterTextRewriter.CustomLetterDebugInfo);
            data.NextAllyDiplomacyTick = tick + GenDate.TicksPerYear;
        }

        private static void TryScheduleFamilyLetter(LiteratureSaveData data, int tick)
        {
            if (!AreEasterLettersEnabled()) return;
            if (_familyPending)
            {
                Log.Message("[RimAI.Art] [Letter] Family letter pending; skip schedule.");
                return;
            }
            if (data.NextFamilyLetterTick <= 0)
                data.NextFamilyLetterTick = tick + Rand.RangeInclusive(FamilyMinIntervalTicks, FamilyMaxIntervalTicks);
            if (tick < data.NextFamilyLetterTick) return;
            if (!TryPickFamilyLetterPawns(
                    out var colonist,
                    out var relative,
                    out var senderRelationToRecipient,
                    out var recipientRelationToSender,
                    out var map))
            {
                Log.Message("[RimAI.Art] [Letter] Family letter skipped: no eligible relatives.");
                data.NextFamilyLetterTick = tick + FamilyRetryTicks;
                return;
            }

            if (!LetterGiftResolver.TryResolveGift(null, relative?.Faction, out var giftSample))
            {
                Log.Message("[RimAI.Art] [Letter] Family letter skipped: gift sampling failed.");
                data.NextFamilyLetterTick = tick + FamilyRetryTicks;
                return;
            }

            string giftDefName = giftSample?.def?.defName ?? string.Empty;
            string giftLabel = giftSample?.LabelCap ?? string.Empty;

            if (giftSample is MinifiedThing mt && mt.InnerThing != null)
            {
                giftDefName = mt.InnerThing.def?.defName ?? giftDefName;
                giftLabel = mt.InnerThing.LabelCap; // 或 mt.InnerThing.def.label.CapitalizeFirst()
            }
            Log.Message($"[RimAI.Art] [Letter] Gift sample: def='{giftDefName}', label='{giftLabel}'.");
            var request = FamilyLetterRequest.BuildRequest(
                colonist,
                relative,
                senderRelationToRecipient,
                recipientRelationToSender,
                giftDefName,
                giftLabel);
            if (request == null)
            {
                data.NextFamilyLetterTick = tick + FamilyRetryTicks;
                return;
            }

            _familyPending = true;
            Log.Message($"[RimAI.Art] [Letter] Scheduling family letter for {colonist.LabelShortCap}.");

            var task = IndependentBookLlmClient.QueryJsonAsync<FamilyLetterSpec>(request);
            task.ContinueWith(t =>
            {
                var spec = t.Status == TaskStatus.RanToCompletion ? t.Result : null;
                Log.Message($"[RimAI.Art] [Letter] Family letter LLM completed (null={spec == null}).");
                EnqueueAction(() => ApplyFamilyLetterResult(spec, colonist, relative, map, giftDefName));
            }, TaskScheduler.Default);
        }

        private static void ApplyFamilyLetterResult(FamilyLetterSpec spec, Pawn colonist, Pawn relative, Map map, string giftDefName)
        {
            _familyPending = false;
            if (!AreEasterLettersEnabled()) return;

            var data = LiteratureSaveData.Current;
            if (data == null || Find.TickManager == null) return;
            int tick = Find.TickManager.TicksGame;

            if (spec == null || colonist == null || map == null)
            {
                data.NextFamilyLetterTick = tick + FamilyRetryTicks;
                Log.Message("[RimAI.Art] [Letter] Family letter failed; retry scheduled.");
                return;
            }

            if (string.IsNullOrWhiteSpace(giftDefName) ||
                !LetterGiftResolver.TryResolveGift(giftDefName, relative?.Faction, out var gift))
            {
                Log.Message($"[RimAI.Art] [Letter] Gift resolve failed: giftDefName='{giftDefName}', specGiftKind='{spec.GiftKind ?? ""}'.");
                data.NextFamilyLetterTick = tick + FamilyRetryTicks;
                Log.Message("[RimAI.Art] [Letter] Family letter gift resolution failed; retry scheduled.");
                return;
            }

            var dropSpot = DropCellFinder.TradeDropSpot(map);
            DropPodUtility.DropThingsNear(dropSpot, map, Gen.YieldSingle(gift), canRoofPunch: false, forbid: false);

            string label = !spec.Title.NullOrEmpty()
                ? spec.Title
                : "RimTalkLE_Letter_Family_Label".Translate();

            string body = !spec.Body.NullOrEmpty()
                ? spec.Body
                : "RimTalkLE_Letter_Family_Fallback".Translate(colonist.LabelShortCap);

            string giftNote = !spec.GiftNote.NullOrEmpty()
                ? spec.GiftNote
                : "RimTalkLE_Letter_Family_GiftNoteFallback".Translate();

            if (!giftNote.NullOrEmpty())
                body = $"{body}\n\n{giftNote}";

            var giftLine = "RimTalkLE_Letter_GiftLine".Translate(gift.LabelCap, gift.stackCount.ToString());
            if (!giftLine.NullOrEmpty())
                body = $"{body}\n\n{giftLine}";

            LookTargets target = colonist != null && colonist.Spawned
                ? new LookTargets(colonist)
                : new LookTargets(map.Center, map);

            // Use the ReceiveLetter overload to ensure the letter is actually pushed to the stack (and clickable).
            var letter = LetterMaker.MakeLetter(label, body, LetterDefOf.PositiveEvent, target);
            Find.LetterStack.ReceiveLetter(letter, LetterTextRewriter.CustomLetterDebugInfo);
            data.NextFamilyLetterTick = tick + Rand.RangeInclusive(FamilyMinIntervalTicks, FamilyMaxIntervalTicks);
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

        private static Faction GetRandomAlliedFaction()
        {
            var factions = Find.FactionManager?.AllFactionsVisible;
            if (factions == null) return null;

            var candidates = new List<Faction>();
            foreach (var faction in factions)
            {
                if (faction == null || faction.IsPlayer || faction.Hidden || faction.defeated) continue;
                if (faction.RelationKindWith(Faction.OfPlayer) != FactionRelationKind.Ally) continue;
                candidates.Add(faction);
            }

            return candidates.Count > 0 ? candidates.RandomElement() : null;
        }

        private static bool TryPickFamilyLetterPawns(
            out Pawn colonist,
            out Pawn relative,
            out string senderRelationToRecipient,
            out string recipientRelationToSender,
            out Map map)
        {
            colonist = null;
            relative = null;
            senderRelationToRecipient = null;
            recipientRelationToSender = null;
            map = GetBestPlayerMap();
            if (map == null) return false;

            var colonists = map.mapPawns?.FreeColonistsSpawned;
            if (colonists == null || colonists.Count == 0) return false;

            var candidates = new List<(Pawn colonist, Pawn relative, string senderRelation, string recipientRelation, int priority)>();
            int relatedSeen = 0;
            int visible = 0;
            int offMap = 0;
            int missingDirect = 0;
            for (int i = 0; i < colonists.Count; i++)
            {
                var pawn = colonists[i];
                if (pawn?.relations == null) continue;

                var relatedPawns = pawn.relations.RelatedPawns.ToList();
                relatedSeen += relatedPawns.Count;
                for (int j = 0; j < relatedPawns.Count; j++)
                {
                    var other = relatedPawns[j];
                    if (!ShouldShowSocialRelation(pawn, other)) continue;
                    visible++;
                    if (!IsValidRelative(other, map)) continue;
                    offMap++;

                    // Prefer labels derived from RimWorld's "most important relation" helpers.
                    // This avoids relying on DirectRelations (which can be incomplete / asymmetric even when RelatedPawns contains the pawn).
                    if (!TryGetAccurateRelationLabels(
                            pawn,
                            other,
                            out var senderRelation,
                            out var recipientRelation))
                    {
                        missingDirect++;
                        continue;
                    }
                    int priority = GetOffMapRelationPriority(other, map);
                    candidates.Add((pawn, other, senderRelation, recipientRelation, priority));
                }
            }

            if (candidates.Count == 0)
            {
                Log.Message($"[RimAI.Art] [Letter] No family candidates: colonists={colonists.Count} related={relatedSeen} visible={visible} offMap={offMap} missingDirect={missingDirect}.");
                return false;
            }

            int bestPriority = candidates.Max(c => c.priority);
            var topCandidates = candidates.Where(c => c.priority == bestPriority).ToList();
            var chosen = topCandidates.RandomElement();
            colonist = chosen.colonist;
            relative = chosen.relative;
            senderRelationToRecipient = chosen.senderRelation;
            recipientRelationToSender = chosen.recipientRelation;
            Log.Message($"[RimAI.Art] [Letter] Picked family pair: recipient={colonist.LabelShortCap}, sender={relative.LabelShortCap}, senderRelation={senderRelationToRecipient}, recipientRelation={recipientRelationToSender}, priority={bestPriority}.");
            return true;
        }

        private static bool TryGetAccurateRelationLabels(
            Pawn colonist,
            Pawn relative,
            out string senderRelationToRecipient,
            out string recipientRelationToSender)
        {
            senderRelationToRecipient = null;
            recipientRelationToSender = null;

            if (colonist == null || relative == null)
                return false;

            PawnRelationDef senderRelation = null;
            PawnRelationDef recipientRelation = null;
            try
            {
                // The relation worker describes the second pawn relative to the first.
                senderRelation = colonist.GetMostImportantRelation(relative);
                recipientRelation = relative.GetMostImportantRelation(colonist);
            }
            catch
            {
                return false;
            }

            if (senderRelation == null || recipientRelation == null)
                return false;

            senderRelationToRecipient = senderRelation.GetGenderSpecificLabel(relative);
            recipientRelationToSender = recipientRelation.GetGenderSpecificLabel(colonist);
            return !senderRelationToRecipient.NullOrEmpty() &&
                   !recipientRelationToSender.NullOrEmpty();
        }

        private static bool IsValidRelative(Pawn pawn, Map homeMap)
        {
            if (pawn == null || pawn.Dead) return false;
            if (pawn.RaceProps?.Humanlike != true) return false;

            // Exclude any pawn currently held by the *home* colony map (including carried babies / pawns in containers).
            // This avoids cases where an in-colony pawn writes an "outside the colony" letter.
            if (homeMap != null && pawn.MapHeld == homeMap) return false;

            // Exclude pawns physically spawned on the home colony map.
            if (pawn.Spawned && pawn.Map == homeMap) return false;

            // Eligible "off-map" relatives include:
            // - Pawns traveling in caravans (player faction or otherwise)
            // - Pawns spawned on some other map (e.g., temporary maps, quest maps)
            // - World pawns (not currently spawned on any map)
            //
            // We intentionally DO NOT require IsWorldPawn(), because caravan pawns and pawns on other maps
            // may not be flagged as world pawns in some modded edge cases.
            if (pawn.IsCaravanMember()) return true;
            if (pawn.Spawned && pawn.Map != null && pawn.Map != homeMap) return true;
            if (pawn.IsWorldPawn()) return true;

            return false;
        }

        private static bool ShouldShowSocialRelation(Pawn pawn, Pawn other)
        {
            if (pawn?.relations == null || other == null) return false;
            if (other.relations == null || other.relations.hidePawnRelations) return false;
            if (pawn.relations.hidePawnRelations) return false;
            if (other.Name == null || other.Name.Numerical) return false;
            if (!other.relations.everSeenByPlayer) return false;
            return true;
        }

        private static int GetOffMapRelationPriority(Pawn pawn, Map homeMap)
        {
            if (pawn == null) return 0;

            // Not actually off-map.
            if (pawn.Spawned && pawn.Map == homeMap) return 0;
            if (homeMap != null && pawn.MapHeld == homeMap) return 0;

            // Prioritize:
            // 1) Player faction pawns traveling with a caravan (best "far from home" narrative fit)
            // 2) Any caravan pawn
            // 3) Player faction pawns spawned on another map (e.g., another base / quest map)
            // 4) Player faction world pawns (e.g., at settlements, downed/held elsewhere, etc.)
            // 5) Non-player world pawns (still valid relatives, but lower priority)
            bool isPlayer = pawn.Faction == Faction.OfPlayer;

            if (pawn.IsCaravanMember())
                return isPlayer ? 50 : 40;

            if (pawn.Spawned && pawn.Map != null && pawn.Map != homeMap)
                return isPlayer ? 35 : 25;

            if (pawn.IsWorldPawn())
                return isPlayer ? 30 : 10;

            return 0;
        }
    }
}
