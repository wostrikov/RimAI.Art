using System.Linq;
using System.Text;
using Ustas.RimAI.Art.llm;
using Ustas.RimAI.Art.settings;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Data;

namespace Ustas.RimAI.Art.events.letters
{
    public static class AllyDiplomacyLetterRequest
    {
        private const int TitleMaxChars = 48;
        private const int BodyMaxChars = 600;
        private const int TargetTokens = 180;

        public static LiteratureLlmRequest BuildRequest(Faction faction, Map map, string colonyName, int goodwillDelta)
        {
            if (faction == null) return null;

            var prompt = BuildPrompt(goodwillDelta);
            var context = BuildContext(faction, map, colonyName, goodwillDelta);

            return new LiteratureLlmRequest(prompt)
            {
                Context = context
            };
        }

        private static string BuildPrompt(int goodwillDelta)
        {
            return
$@"Напиши дружнього дипломатичного листа від союзної фракції до колонії гравця.
Пиши мовою {Constant.Lang}. Виведи лише JSON.

Обов'язкові поля JSON:
- ""title""
- ""body""

Обмеження:
- Довжина title <= {TitleMaxChars} символів.
- Довжина body <= {BodyMaxChars} символів, близько {TargetTokens} токенів.
- SenderFaction є автором листа. RecipientFaction і RecipientColony є адресатами.
- Пиши від першої особи множини SenderFaction. Ніколи не стверджуй, що SenderFaction — це RecipientFaction.
- Згадай союз і поліпшення відносин на {goodwillDelta}.
- За наявності використай принаймні одну конкретну деталь із контексту колонії чи ідеології.
- Дотримуйся зв'язного, приземленого тону; уникай сюрреалістичного чи випадкового вмісту.
- Без markdown і додаткових ключів.";
        }

        private static string BuildContext(Faction faction, Map map, string colonyName, int goodwillDelta)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[AllyDiplomacy]");
            sb.AppendLine($"SenderFaction: {faction.Name}");
            if (Faction.OfPlayer != null)
                sb.AppendLine($"RecipientFaction: {Faction.OfPlayer.Name}");
            if (!string.IsNullOrWhiteSpace(colonyName))
                sb.AppendLine($"RecipientColony: {colonyName}");
            if (faction.leader != null)
                sb.AppendLine($"SenderLeader: {faction.leader.LabelShortCap}");
            sb.AppendLine($"GoodwillChange: +{goodwillDelta}");

            if (map != null)
            {
                int colonistCount = map.mapPawns?.FreeColonistsSpawned?.Count ?? 0;
                sb.AppendLine($"RecipientColonists: {colonistCount}");
                if (map.wealthWatcher != null)
                    sb.AppendLine($"RecipientColonyWealth: {Mathf.RoundToInt(map.wealthWatcher.WealthTotal)}");
            }

            if (ModsConfig.IdeologyActive)
            {
                var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
                if (ideo != null)
                {
                    sb.AppendLine($"RecipientIdeology: {ideo.name}");
                    var roles = ideo.RolesListForReading;
                    if (roles != null && roles.Count > 0)
                    {
                        sb.AppendLine("RecipientIdeologyRoles:");
                        for (int i = 0; i < roles.Count; i++)
                        {
                            var role = roles[i];
                            if (role == null) continue;
                            var assigned = role.ChosenPawns();
                            var assignedLine = assigned != null ? string.Join(", ", assigned.Select(p => p.LabelShortCap)) : string.Empty;
                            if (!string.IsNullOrWhiteSpace(assignedLine))
                                sb.AppendLine($"- {role.LabelCap}: {assignedLine}");
                        }
                    }
                }
            }

            return sb.ToString().TrimEnd();
        }
    }
}
