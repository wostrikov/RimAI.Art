using System.Text;
using Ustas.RimAI.Communication.Service;
using Ustas.RimAI.Art.llm;
using Ustas.RimAI.Art.settings;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Data;

namespace Ustas.RimAI.Art.events.letters
{
    public static class FamilyLetterRequest
    {
        private const int TitleMaxChars = 48;
        private const int BodyMaxChars = 700;
        private const int TargetTokens = 220;

        public static LiteratureLlmRequest BuildRequest(
            Pawn colonist,
            Pawn relative,
            string senderRelationToRecipient,
            string recipientRelationToSender,
            string giftDefName,
            string giftLabel)
        {
            if (colonist == null || relative == null) return null;

            var prompt = BuildPrompt();
            var context = BuildContext(
                colonist,
                relative,
                senderRelationToRecipient,
                recipientRelationToSender,
                giftDefName,
                giftLabel);

            return new LiteratureLlmRequest(prompt)
            {
                Context = context
            };
        }

        private static string BuildPrompt()
        {
            return
$@"Напиши особистого листа від родича колоніста, який живе поза колонією.
Пиши мовою {Constant.Lang}. Виведи лише JSON.

Обов'язкові поля JSON:
- ""title""
- ""body""
- ""giftKind"" (ThingDef.defName базового предмета подарунка; НІКОЛИ не використовуй `MinifiedThing`. Якщо подарунок є Building, використовуй defName будівлі — її автоматично доставлять у згорнутому вигляді)
- ""giftNote"" (1 речення про подарунок)

Обмеження:
- Довжина title <= {TitleMaxChars} символів.
- Довжина body <= {BodyMaxChars} символів, близько {TargetTokens} токенів.
- SenderName завжди є автором, а RecipientName — адресатом.
- SenderRelationToRecipient описує спорідненість відправника з адресатом.
- RecipientRelationToSender описує спорідненість адресата з відправником.
- Пиши від першої особи відправника й звертайся до адресата як ""ти"". Ніколи не міняй ці зв'язки місцями.
- Подарунок у body/giftNote МУСИТЬ точно відповідати giftKind.
- giftKind МУСИТЬ дорівнювати GiftDefName з контексту.
- Без markdown і додаткових ключів.";
        }

        private static string BuildContext(
            Pawn colonist,
            Pawn relative,
            string senderRelationToRecipient,
            string recipientRelationToSender,
            string giftDefName,
            string giftLabel)
        {
            var sb = new StringBuilder();
            var recipientMap = colonist.MapHeld ?? Find.CurrentMap;
            sb.AppendLine("[FamilyLetter]");
            sb.AppendLine($"SenderName: {relative.LabelShortCap}");
            sb.AppendLine($"RecipientName: {colonist.LabelShortCap}");
            if (!string.IsNullOrWhiteSpace(senderRelationToRecipient))
                sb.AppendLine($"SenderRelationToRecipient: {senderRelationToRecipient}");
            if (!string.IsNullOrWhiteSpace(recipientRelationToSender))
                sb.AppendLine($"RecipientRelationToSender: {recipientRelationToSender}");
            if (relative.Faction != null)
                sb.AppendLine($"SenderFaction: {relative.Faction.Name}");
            if (colonist.Faction != null)
                sb.AppendLine($"RecipientFaction: {colonist.Faction.Name}");
            if (!string.IsNullOrWhiteSpace(giftDefName))
                sb.AppendLine($"GiftDefName: {giftDefName}");
            if (!string.IsNullOrWhiteSpace(giftLabel))
                sb.AppendLine($"GiftLabel: {giftLabel}");
            sb.AppendLine($"RecipientColony: {recipientMap?.info?.parent?.LabelCap ?? "Colony"}");
            sb.AppendLine($"RecipientColonyWealth: {Mathf.RoundToInt(recipientMap?.wealthWatcher?.WealthTotal ?? 0f)}");

            var colonistProfile = PromptService.CreatePawnContext(colonist, PromptService.InfoLevel.Short);
            if (!string.IsNullOrWhiteSpace(colonistProfile))
            {
                sb.AppendLine("[RimTalkProfile:Recipient]");
                sb.AppendLine(colonistProfile);
            }

            var relativeProfile = PromptService.CreatePawnContext(relative, PromptService.InfoLevel.Short);
            if (!string.IsNullOrWhiteSpace(relativeProfile))
            {
                sb.AppendLine("[RimTalkProfile:Sender]");
                sb.AppendLine(relativeProfile);
            }

            return sb.ToString().TrimEnd();
        }
    }
}
