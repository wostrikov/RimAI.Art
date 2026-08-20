using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LudeonTK;
using Ustas.RimAI.Art.Events;
using Ustas.RimAI.Art.Settings;
using Ustas.RimAI.Art.Storage.Save;
using Ustas.RimAI.Art.Synopsis.LLM;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Art.Events.Quests
{
    using static QuestEventScheduler;

    internal static class QuestEventSchedulerWorld
    {
    internal static void SendQuestAvailableLetter(Quest quest, Faction faction)
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
    internal static bool TryGetRimTalkQuestTags(List<string> tags, out List<string> matches)
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

    internal static int CountThingDef(List<ActiveTransporterInfo> transporters, ThingDef def)
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

    internal static string MakeTradeQuestTag(Quest quest)
    {
        return quest == null ? string.Empty : $"Quest{quest.id}.{TradeRequestTagSuffix}";
    }
    internal static Faction GetRandomNonHostileFaction()
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

    internal static Faction GetRandomHostileFaction()
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

    internal static Settlement GetTradeSettlement(Faction faction)
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

    internal static Map GetBestPlayerMap()
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

    internal static Pawn GetAnyColonist(Map map)
    {
        var pawns = map?.mapPawns?.FreeColonistsSpawned;
        if (pawns == null || pawns.Count == 0) return null;
        return pawns[0];
    }

    internal static string ResolveTitle(string value, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        return fallback;
    }

    internal static string ResolveDescription(string value, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        return fallback;
    }
    }
}
