/*
 * Purpose:
 * - Shared option data for quest advertisements.
 *
 * Uses:
 * - RimWorld ThingDefCountClass
 *
 * Responsibilities:
 * - Carry option details for prompt context, rewards, and drop pods.
 *
 * Do NOT:
 * - Do not put logic here.
 */
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Art.Events.Quests
{
    public sealed class QuestOfferOption
    {
        public string Key;
        public int SilverCost;
        public float TotalValue;
        public string ItemsLabel;
        public List<ThingDefCountClass> DropItems = new List<ThingDefCountClass>();
        public List<Thing> RewardPreview = new List<Thing>();
    }
}
