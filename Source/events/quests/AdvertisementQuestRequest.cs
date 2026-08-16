/*
 * Purpose:
 * - Build the LLM request for advertisement quests.
 *
 * Uses:
 * - Literature Expansion standalone LLM request
 *
 * Responsibilities:
 * - Provide a concise prompt and structured context for LLM output.
 */
using System.Collections.Generic;
using System.Text;
using Ustas.RimAI.Art.llm;
using Ustas.RimAI.Art.settings;
using Ustas.RimAI.Art.settings.util;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Art.events.quests
{
    public static class AdvertisementQuestRequest
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
            IReadOnlyList<QuestOfferOption> options,
            int offerDays,
            int deliveryDays)
        {
            if (faction == null || settlement == null || options == null || options.Count == 0)
                return null;

            var prompt = BuildPrompt(offerDays, deliveryDays);
            var context = BuildContext(faction, settlement, map, options, offerDays, deliveryDays);

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
                settings?.promptQuestAdvert,
                template,
                ("LANG", RimTalkConstantShim.Lang),
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
                ("LANG", RimTalkConstantShim.Lang),
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
$@"Напиши опис торговельного рекламного завдання від імені фракції-замовника.
Пиши мовою {RimTalkConstantShim.Lang}. Виведи лише JSON.

Обов'язкові поля JSON:
- ""title""
- ""description""

Обмеження:
- Довжина title <= {TitleMaxChars} символів.
- Довжина description <= {BodyMaxChars} символів, близько {TargetTokens} токенів.
- IssuerFaction є мовцем. RecipientFaction і RecipientColony отримують пропозицію.
- Пиши від першої особи множини IssuerFaction. Ніколи не говори від імені RecipientFaction.
- Використовуй голос замовника й приземлений тон; опис має читатися як опубліковане прохання виконати завдання, а не загальна атмосферна проза.
- Додай коротку передумову, чому фракція робить цю пропозицію.
- Додай усі три варіанти з точно наданими в QuestData сумами срібла й назвами предметів.
- Згадай, що платіж можна доставити караваном або транспортними капсулами.
- Згадай строк пропозиції ({offerDays} днів) і строк доставлення ({deliveryDays} днів), не змінюючи чисел.
- Не додавай нових предметів, варіантів або чисел.
- Без markdown і додаткових ключів.";
        }

        private static string BuildContext(
            Faction faction,
            Settlement settlement,
            Map map,
            IReadOnlyList<QuestOfferOption> options,
            int offerDays,
            int deliveryDays)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[AdvertQuest]");
            sb.AppendLine($"IssuerFaction: {faction.Name}");
            sb.AppendLine($"IssuerSettlement: {settlement.LabelCap}");
            if (Faction.OfPlayer != null)
                sb.AppendLine($"RecipientFaction: {Faction.OfPlayer.Name}");
            sb.AppendLine($"OfferDays: {offerDays}");
            sb.AppendLine($"DeliveryDays: {deliveryDays}");
            if (map != null)
            {
                sb.AppendLine($"RecipientColony: {map.info?.parent?.LabelCap ?? "Colony"}");
                sb.AppendLine($"RecipientColonists: {map.mapPawns?.FreeColonistsSpawned?.Count ?? 0}");
                if (map.wealthWatcher != null)
                    sb.AppendLine($"RecipientWealth: {Mathf.RoundToInt(map.wealthWatcher.WealthTotal)}");
            }

            sb.AppendLine("Options:");
            for (int i = 0; i < options.Count; i++)
            {
                var option = options[i];
                if (option == null) continue;
                sb.AppendLine($"- {option.Key}: pay {option.SilverCost} silver -> {option.ItemsLabel}");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
