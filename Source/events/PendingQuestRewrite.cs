using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Art.events
{
    internal sealed class PendingQuestRewrite
    {
        public int QuestId { get; }
        public Quest Quest { get; }
        public Faction IssuerFaction { get; }
        public string QuestName { get; }
        public string OriginalDescription { get; }
        public List<string> RequiredTokens { get; }
        public List<string> OptionalTokens { get; }
        public List<string> NumberTokens { get; }
        public int QueuedTick { get; }
        public int DeadlineTick { get; }
        public bool Requested { get; set; }

        public PendingQuestRewrite(
            Quest quest,
            Faction issuerFaction,
            string originalDescription,
            List<string> requiredTokens,
            List<string> optionalTokens,
            List<string> numberTokens,
            int timeoutTicks)
        {
            Quest = quest;
            QuestId = quest?.id ?? -1;
            IssuerFaction = issuerFaction;
            QuestName = quest?.name;
            OriginalDescription = originalDescription ?? string.Empty;
            RequiredTokens = requiredTokens ?? new List<string>();
            OptionalTokens = optionalTokens ?? new List<string>();
            NumberTokens = numberTokens ?? new List<string>();
            QueuedTick = Find.TickManager.TicksGame;
            DeadlineTick = QueuedTick + timeoutTicks;
        }
    }
}
