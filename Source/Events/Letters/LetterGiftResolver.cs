using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Art.Events.Letters
{
    public static class LetterGiftResolver
    {
        private enum GiftCategory
        {
            Food,
            Medicine,
            Apparel,
            Decor
        }

        private static readonly GiftCategory[] AllCategories =
        {
            GiftCategory.Food,
            GiftCategory.Medicine,
            GiftCategory.Apparel,
            GiftCategory.Decor
        };

        public static bool TryResolveGift(string giftKind, Faction senderFaction, out Thing gift)
        {
            gift = null;

            if (!string.IsNullOrWhiteSpace(giftKind))
            {
                var defFromKind = DefDatabase<ThingDef>.GetNamedSilentFail(giftKind.Trim());
                if (defFromKind == null)
                {
                    RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] [Letter] Gift def not found for giftKind='{giftKind}'.");
                    return false;
                }
                if (!IsAllowedGiftDef(defFromKind))
                {
                    RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] [Letter] Gift def not allowed: '{defFromKind.defName}'.");
                    return false;
                }

                var categoryFromKind = ResolveCategoryFromDef(defFromKind);
                gift = CreateGiftThing(defFromKind, categoryFromKind);
                if (gift == null)
                    RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] [Letter] Gift creation failed for def '{defFromKind.defName}'.");
                return gift != null;
            }

            var category = ResolveCategory(giftKind);
            var candidates = GetCandidates(category);
            if (candidates.Count == 0 && category.HasValue)
                candidates = GetCandidates(null);
            if (candidates.Count == 0)
            {
                RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] [Letter] No gift candidates for category '{category?.ToString() ?? "any"}'.");
                return false;
            }

            var preferred = PreferModContent(candidates, senderFaction);
            var defCandidate = preferred.RandomElement();
            if (defCandidate == null)
            {
                RimAiLog.Info(RimAiLogCategory.Art, "[RimAI.Art] [Letter] Gift candidate selection returned null.");
                return false;
            }

            gift = CreateGiftThing(defCandidate, category ?? ResolveCategoryFromDef(defCandidate));
            if (gift == null)
            {
                RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] [Letter] Gift creation failed for def '{defCandidate.defName}'.");
                return false;
            }
            if (gift.stackCount <= 0)
                gift.stackCount = 1;
            RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] [Letter] Gift resolved: def='{defCandidate.defName}', count={gift.stackCount}.");
            return true;
        }

        private static GiftCategory? ResolveCategory(string giftKind)
        {
            if (string.IsNullOrWhiteSpace(giftKind)) return null;

            var lower = giftKind.Trim().ToLowerInvariant();
            if (lower.Contains("food") || lower.Contains("meal") || lower.Contains("snack"))
                return GiftCategory.Food;
            if (lower.Contains("medicine") || lower.Contains("med"))
                return GiftCategory.Medicine;
            if (lower.Contains("apparel") || lower.Contains("clothing") || lower.Contains("clothes"))
                return GiftCategory.Apparel;
            if (lower.Contains("decor") || lower.Contains("building") || lower.Contains("furniture"))
                return GiftCategory.Decor;

            return null;
        }

        private static List<ThingDef> GetCandidates(GiftCategory? category)
        {
            var source = ThingSetMakerUtility.allGeneratableItems;
            if (source == null || source.Count == 0)
                source = DefDatabase<ThingDef>.AllDefsListForReading;

            // Filter for allowed gift types only
            IEnumerable<ThingDef> filtered = source.Where(IsAllowedGiftDef);
            if (category.HasValue)
                filtered = filtered.Where(def => MatchesCategory(def, category.Value));

            return filtered.Distinct().ToList();
        }

        private static List<ThingDef> PreferModContent(List<ThingDef> candidates, Faction senderFaction)
        {
            if (senderFaction?.def?.modContentPack == null || senderFaction.def.modContentPack.IsCoreMod)
                return candidates;

            var modCandidates = candidates
                .Where(def => def?.modContentPack == senderFaction.def.modContentPack)
                .ToList();
            return modCandidates.Count > 0 ? modCandidates : candidates;
        }

        private static bool IsAllowedGiftDef(ThingDef def)
        {
            if (def == null) return false;
            if (!def.PlayerAcquirable) return false;
            if (def.tradeability == Tradeability.None) return false;
            if (def.BaseMarketValue <= 0.01f) return false;
            if (!ThingSetMakerUtility.CanGenerate(def)) return false;

            // Ensure we only select gifts that are actually allowed types
            return IsNonRawFood(def) || def.IsMedicine || def.IsApparel || IsSmallDecorOrBuilding(def);
        }

        private static bool MatchesCategory(ThingDef def, GiftCategory category)
        {
            switch (category)
            {
                case GiftCategory.Food:
                    return IsNonRawFood(def);
                case GiftCategory.Medicine:
                    return def.IsMedicine;
                case GiftCategory.Apparel:
                    return def.IsApparel;
                case GiftCategory.Decor:
                    return IsSmallDecorOrBuilding(def);
                default:
                    return false;
            }
        }

        private static bool IsNonRawFood(ThingDef def)
        {
            if (def == null || !def.IsIngestible || def.ingestible == null) return false;
            if (def.IsDrug) return false;
            var foodType = def.ingestible.foodType;
            return (foodType & (FoodTypeFlags.Meal | FoodTypeFlags.Processed | FoodTypeFlags.Liquor | FoodTypeFlags.Kibble)) != 0;
        }

        private static bool IsSmallDecorOrBuilding(ThingDef def)
        {
            if (def == null) return false;
            if (def.category != ThingCategory.Building) return false;
            if (!def.BuildableByPlayer) return false;
            if (!def.Minifiable) return false;
            if (def.building == null) return false;
            if (def.building.isNaturalRock) return false;
            int area = def.size.x * def.size.z;
            return area > 0 && area <= 4;
        }

        private static Thing CreateGiftThing(ThingDef def, GiftCategory category)
        {
            if (def == null) return null;
            ThingDef stuff = def.MadeFromStuff ? GenStuff.RandomStuffByCommonalityFor(def) : null;
            Thing thing = ThingMaker.MakeThing(def, stuff);
            if (thing == null) return null;

            int count = ResolveCount(def, category);
            if (count > 1 && thing.def.stackLimit > 1)
                thing.stackCount = Mathf.Clamp(count, 1, thing.def.stackLimit);

            if (category == GiftCategory.Decor && def.category == ThingCategory.Building)
            {
                var minified = thing.MakeMinified();
                if (minified != null)
                    thing = minified;
            }

            return thing;
        }

        private static int ResolveCount(ThingDef def, GiftCategory category)
        {
            if (def == null) return 1;
            switch (category)
            {
                case GiftCategory.Food:
                    return Rand.RangeInclusive(6, 16);
                case GiftCategory.Medicine:
                    return Rand.RangeInclusive(2, 6);
                case GiftCategory.Apparel:
                case GiftCategory.Decor:
                default:
                    return 1;
            }
        }

        private static GiftCategory ResolveCategoryFromDef(ThingDef def)
        {
            if (def == null) return GiftCategory.Food;
            if (IsNonRawFood(def)) return GiftCategory.Food;
            if (def.IsMedicine) return GiftCategory.Medicine;
            if (def.IsApparel) return GiftCategory.Apparel;
            if (IsSmallDecorOrBuilding(def)) return GiftCategory.Decor;
            return GiftCategory.Food;
        }
    }
}
