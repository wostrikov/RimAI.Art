/*
 * Purpose:
 * - Build the LLM request for warning quests.
 *
 * Uses:
 * - Literature Expansion standalone LLM request
 *
 * Responsibilities:
 * - Provide a concise prompt and structured context for LLM output.
 */
using System.Text;
using Ustas.RimAI.Art.LLM;
using Ustas.RimAI.Art.Settings;
using Ustas.RimAI.Art.Settings.Util;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Data;

namespace Ustas.RimAI.Art.Events.Quests
{
    public static class WarningQuestRequest
    {
        private const int TitleMaxChars = 56;
        private const int BodyMaxChars = 900;
        private const int TargetTokens = 220;
        private const int DefaultOfferDays = 3;
        private const int DefaultDeliveryDays = 3;

        public static LiteratureLlmRequest BuildRequest(
            Faction faction,
            Settlement settlement,
            Map map,
            int silverDemand,
            int offerDays,
            int deliveryDays)
        {
            if (faction == null || settlement == null) return null;

            var prompt = BuildPrompt(offerDays, deliveryDays);
            var context = BuildContext(faction, settlement, map, silverDemand, offerDays, deliveryDays);

            return new LiteratureLlmRequest(prompt)
            {
                Context = context
            };
        }

        private static string BuildPrompt(int offerDays, int deliveryDays)
        {
            var settings = LiteratureMod.Settings;
            string template = BuildTemplate(offerDays, deliveryDays);
            var prompt = PromptTemplateUtil.Resolve(
                settings?.promptQuestWarning,
                template,
                ("LANG", Constant.Lang),
                ("TITLE_MAX_CHARS", TitleMaxChars.ToString()),
                ("BODY_MAX_CHARS", BodyMaxChars.ToString()),
                ("TARGET_TOKENS", TargetTokens.ToString()),
                ("OFFER_DAYS", offerDays.ToString()),
                ("DELIVERY_DAYS", deliveryDays.ToString()));
            return ApplyIssuerBoundary(prompt);
        }

        public static string BuildDefaultPrompt()
        {
            string template = BuildTemplate(DefaultOfferDays, DefaultDeliveryDays);
            var prompt = PromptTemplateUtil.ApplyTokens(
                template,
                ("LANG", Constant.Lang),
                ("TITLE_MAX_CHARS", TitleMaxChars.ToString()),
                ("BODY_MAX_CHARS", BodyMaxChars.ToString()),
                ("TARGET_TOKENS", TargetTokens.ToString()),
                ("OFFER_DAYS", DefaultOfferDays.ToString()),
                ("DELIVERY_DAYS", DefaultDeliveryDays.ToString()));
            return ApplyIssuerBoundary(prompt);
        }

        private static string ApplyIssuerBoundary(string prompt)
        {
            const string marker = "[TaskBoundary:QuestIssuer]";
            if (string.IsNullOrWhiteSpace(prompt)) return marker;
            if (prompt.Contains(marker)) return prompt;
            return
$@"{prompt.TrimEnd()}

{marker}
- IssuerFaction є мовцем.
- RecipientFaction і RecipientColony є адресатами.
- Ніколи не міняй місцями замовника й адресата.";
        }

        private static string BuildTemplate(int offerDays, int deliveryDays)
        {
            return
$@"Напиши ворожий опис завдання-попередження від імені фракції-замовника.
Пиши мовою {Constant.Lang}. Виведи лише JSON.

Обов'язкові поля JSON:
- ""title""
- ""description""

Обмеження:
- Довжина title <= {TitleMaxChars} символів.
- Довжина description <= {BodyMaxChars} символів, близько {TargetTokens} токенів.
- IssuerFaction є мовцем. RecipientFaction і RecipientColony отримують вимогу.
- Пиши від першої особи множини IssuerFaction. Ніколи не говори від імені RecipientFaction.
- Використовуй голос замовника й напружений тон; опис має читатися як підкріплена погрозою вимога виконати завдання, а не загальна атмосферна проза.
- Додай коротку передумову або образу, що пояснює вимогу оплати.
- Вкажи точну суму срібла з QuestData.
- Згадай, що платіж можна доставити караваном або транспортними капсулами.
- Згадай строк пропозиції ({offerDays} днів) і строк доставлення ({deliveryDays} днів), не змінюючи чисел.
- Згадай, що в разі несплати рейд станеться через 1–2 дні після кінцевого строку.
- Не додавай нових чисел, фракцій або винагород.
- Без markdown і додаткових ключів.";
        }

        private static string BuildContext(
            Faction faction,
            Settlement settlement,
            Map map,
            int silverDemand,
            int offerDays,
            int deliveryDays)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[WarningQuest]");
            sb.AppendLine($"IssuerFaction: {faction.Name}");
            sb.AppendLine($"IssuerSettlement: {settlement.LabelCap}");
            if (Faction.OfPlayer != null)
                sb.AppendLine($"RecipientFaction: {Faction.OfPlayer.Name}");
            sb.AppendLine($"SilverDemand: {silverDemand}");
            sb.AppendLine($"OfferDays: {offerDays}");
            sb.AppendLine($"DeliveryDays: {deliveryDays}");
            if (map != null)
            {
                sb.AppendLine($"RecipientColony: {map.info?.parent?.LabelCap ?? "Colony"}");
                sb.AppendLine($"RecipientColonists: {map.mapPawns?.FreeColonistsSpawned?.Count ?? 0}");
                if (map.wealthWatcher != null)
                    sb.AppendLine($"RecipientWealth: {Mathf.RoundToInt(map.wealthWatcher.WealthTotal)}");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
