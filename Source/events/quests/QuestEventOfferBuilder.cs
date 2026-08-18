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

    internal static class QuestEventOfferBuilder
    {
    internal static bool TryBuildOfferOptions(Faction faction, out List<QuestOfferOption> options)
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

    internal static List<ThingDef> GetAllCandidates()
    {
        var source = ThingSetMakerUtility.allGeneratableItems;
        if (source == null || source.Count == 0)
            source = DefDatabase<ThingDef>.AllDefsListForReading;

        return source.Where(IsValidTradeItem).ToList();
    }

    internal static List<ThingDef> GetModCandidates(Faction faction, List<ThingDef> allCandidates)
    {
        if (faction?.def?.modContentPack == null || faction.def.modContentPack.IsCoreMod)
            return new List<ThingDef>();

        return allCandidates.Where(def => def.modContentPack == faction.def.modContentPack).ToList();
    }

    internal static bool TryCreateOfferOption(
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

    internal static bool TryBuildOffer(
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

    internal static void AddPreviewThing(List<Thing> preview, ThingDef def, int count)
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

    internal static bool IsValidTradeItem(ThingDef def)
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
    }
}
