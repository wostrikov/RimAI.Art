/*
 * Purpose:
 * - Append flavor text to quest descriptions via LLM without changing quest logic.
 *
 * Uses:
 * - Literature Expansion's standalone LLM client for JSON output.
 *
 * Responsibilities:
 * - Queue quest descriptions and append flavor text if it returns before a timeout.
 *
 * Design notes:
 * - Only Quest.description is modified; no letters or quest parts are altered.
 * - Late responses (past the deadline) are discarded.
 * - Flavor text must not contain numbers or known entity names.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Globalization;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Service;
using Ustas.RimAI.Art.LLM;
using Ustas.RimAI.Art.Settings;
using Ustas.RimAI.Art.Synopsis.LLM;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Art.Events
{
    [DataContract]
    public sealed class QuestDescriptionSpec : IJsonData
    {
        [DataMember(Name = "description")]
        public string Description { get; set; }

        [DataMember(Name = "flavor")]
        public string Flavor { get; set; }

        public string GetText()
        {
            return Flavor ?? Description ?? string.Empty;
        }
    }

    public static class QuestDescriptionRewriter
    {
        private const int TimeoutSeconds = 60;
        private const int TargetTokens = 140;
        private const string LogPrefix = "[RimAI.Art] [QuestRewrite]";
        internal static readonly Regex NumberTokenRegex = new Regex(@"\d+(?:\.\d+)?%?", RegexOptions.Compiled);

        private static readonly Dictionary<int, PendingQuestRewrite> Pending = new Dictionary<int, PendingQuestRewrite>();
        private static readonly Queue<Action> PendingActions = new Queue<Action>();
        private static readonly object QueueLock = new object();

        public static void TryQueue(Quest quest)
        {
            var settings = LiteratureMod.Settings;
            if (settings != null && !settings.enabled)
            {
                RimAiLog.Info(RimAiLogCategory.Art, $"{LogPrefix} Skip: feature disabled.");
                return;
            }
            if (quest == null)
            {
                RimAiLog.Info(RimAiLogCategory.Art, $"{LogPrefix} Skip: quest is null.");
                return;
            }
            if (quest.hidden || quest.hiddenInUI)
            {
                RimAiLog.Info(RimAiLogCategory.Art, $"{LogPrefix} Skip: quest hidden (id={quest.id}).");
                return;
            }
            if (Find.TickManager == null)
            {
                RimAiLog.Info(RimAiLogCategory.Art, $"{LogPrefix} Skip: TickManager unavailable (quest id={quest.id}).");
                return;
            }

            if (!IsQuestAllowed(settings, quest))
            {
                RimAiLog.Info(RimAiLogCategory.Art, $"{LogPrefix} Skip: quest not allowed (id={quest.id}, def={quest.root?.defName ?? "null"}).");
                return;
            }

            if (Pending.ContainsKey(quest.id))
            {
                RimAiLog.Info(RimAiLogCategory.Art, $"{LogPrefix} Skip: quest already queued (id={quest.id}).");
                return;
            }

            string original = GetResolvedDescription(quest);
            if (string.IsNullOrWhiteSpace(original))
            {
                RimAiLog.Info(RimAiLogCategory.Art, $"{LogPrefix} Skip: empty description (id={quest.id}).");
                return;
            }

            var requiredEntityTokens = new HashSet<string>();
            var optionalEntityTokens = new HashSet<string>();
            CollectEntityTokens(quest, original, requiredEntityTokens, optionalEntityTokens);
            CollectQuestPartTokens(quest, original, requiredEntityTokens, optionalEntityTokens);
            var numberTokens = ExtractNumberTokens(original);
            var requiredTokens = MergeRequiredTokens(requiredEntityTokens.ToList(), numberTokens);
            var optionalTokens = optionalEntityTokens.OrderByDescending(t => t.Length).ToList();
            var issuerFaction = TryResolveIssuerFaction(quest);
            var record = new PendingQuestRewrite(
                quest,
                issuerFaction,
                original,
                requiredTokens,
                optionalTokens,
                numberTokens,
                GenTicks.SecondsToTicks(TimeoutSeconds));
            Pending[quest.id] = record;
            RimAiLog.Info(RimAiLogCategory.Art, $"{LogPrefix} Queued quest (id={quest.id}, def={quest.root?.defName ?? "null"}, name={quest.name ?? "?"}, required={record.RequiredTokens.Count}, optional={record.OptionalTokens.Count}, numbers={record.NumberTokens.Count}).");
        }

        public static void Tick()
        {
            ProcessPendingActions();

            if (Find.TickManager == null || Pending.Count == 0) return;

            int tick = Find.TickManager.TicksGame;
            var expired = Pending.Where(kvp => tick > kvp.Value.DeadlineTick).Select(kvp => kvp.Key).ToList();
            for (int i = 0; i < expired.Count; i++)
            {
                if (Pending.TryGetValue(expired[i], out var record))
                    RimAiLog.Info(RimAiLogCategory.Art, $"{LogPrefix} Expired (id={record.QuestId}, def={record.Quest?.root?.defName ?? "null"}).");
                Pending.Remove(expired[i]);
            }

            if (AIService.IsBusy()) return;

            PendingQuestRewrite next = null;
            foreach (var pending in Pending.Values)
            {
                if (pending.Requested) continue;
                if (tick > pending.DeadlineTick) continue;
                if (next == null || pending.QueuedTick < next.QueuedTick)
                    next = pending;
            }

            if (next != null)
                StartRequest(next);
        }

        private static void StartRequest(PendingQuestRewrite record)
        {
            if (record == null || record.Requested) return;
            var request = BuildRequest(record);
            if (request == null)
            {
                Pending.Remove(record.QuestId);
                RimAiLog.Info(RimAiLogCategory.Art, $"{LogPrefix} Abort: failed to build request (id={record.QuestId}).");
                return;
            }

            record.Requested = true;
            RimAiLog.Info(RimAiLogCategory.Art, $"{LogPrefix} Dispatching LLM request (id={record.QuestId}, def={record.Quest?.root?.defName ?? "null"}).");

            var task = IndependentBookLlmClient.QueryJsonAsync<QuestDescriptionSpec>(request);
            task.ContinueWith(t =>
            {
                var spec = t.Status == TaskStatus.RanToCompletion ? t.Result : null;
                if (spec == null)
                    RimAiLog.Info(RimAiLogCategory.Art, $"{LogPrefix} LLM returned null (id={record.QuestId}).");
                EnqueueAction(() => ApplyResult(record.QuestId, spec));
            }, TaskScheduler.Default);
        }

        private static LiteratureLlmRequest BuildRequest(PendingQuestRewrite record)
        {
            if (record == null) return null;

            var prompt = BuildPrompt(record.OriginalDescription, record.IssuerFaction != null);
            var context = BuildContext(record);

            return new LiteratureLlmRequest(prompt)
            {
                Context = context
            };
        }

        private static string BuildPrompt(string originalDescription, bool hasKnownIssuer)
        {
            int charLimit = Mathf.Clamp(originalDescription?.Length ?? 0, 240, 520);
            string voiceRule = hasKnownIssuer
                ? "IssuerFaction є мовцем. Пиши голосом цієї фракції й ніколи не говори від імені RecipientFaction."
                : "Надійно визначити замовника не вдалося. Використовуй нейтральну оповідь про завдання від третьої особи; не говори від імені жодної фракції.";
            string motiveRule = hasKnownIssuer
                ? "Пояснюй мотив замовника лише тоді, коли його підтверджують QuestData або OriginalText."
                : "Описуй передумови лише тоді, коли їх підтверджують QuestData або OriginalText; не вигадуй замовника чи мотив.";
            return
$@"Напиши доповнення до опису завдання RimWorld.
Пиши мовою {Constant.Lang}. Виведи лише JSON.

Обов'язкові поля JSON:
- ""flavor""

Обмеження:
- {voiceRule}
- {motiveRule}
- Текст має читатися як опис завдання й доповнення передумов, а не як відірвана атмосферна проза.
- Чітко збережи початкову мету, ставки й тон завдання, але не дублюй увесь оригінал.
- Використовуй лише факти з QuestData та OriginalText; не вигадуй винагород, строків, кількостей, місць або іменованих сутностей.
- Згадуй потрібні токени й числа лише тоді, коли вони вже є в QuestData.
- Уникай загального опису краєвидів; кожне речення має підтримувати прохання, мотив замовника або передумови завдання.
- Мінімальна довжина: близько 100 токенів.
- Довжина <= {charLimit} символів (близько {TargetTokens} токенів).
- Без markdown і додаткових ключів.";
        }

        private static string BuildContext(PendingQuestRewrite record)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[QuestDescription]");
            if (!string.IsNullOrWhiteSpace(record.QuestName))
                sb.AppendLine($"QuestName: {record.QuestName}");
            if (record.IssuerFaction != null)
                sb.AppendLine($"IssuerFaction: {record.IssuerFaction.Name}");
            if (Faction.OfPlayer != null)
                sb.AppendLine($"RecipientFaction: {Faction.OfPlayer.Name}");
            sb.AppendLine("[QuestData]");
            sb.AppendLine(BuildQuestDataJson(record));
            return sb.ToString().TrimEnd();
        }

        private static void ApplyResult(int questId, QuestDescriptionSpec spec)
        {
            if (!Pending.TryGetValue(questId, out var record))
                return;

            Pending.Remove(questId);

            if (record == null || spec == null)
            {
                RimAiLog.Info(RimAiLogCategory.Art, $"{LogPrefix} Abort: missing record or spec (id={questId}).");
                return;
            }
            if (Find.TickManager == null) return;
            if (Find.TickManager.TicksGame > record.DeadlineTick)
            {
                RimAiLog.Info(RimAiLogCategory.Art, $"{LogPrefix} Abort: response late (id={questId}).");
                return;
            }

            if (!IsQuestActive(record.Quest))
            {
                RimAiLog.Info(RimAiLogCategory.Art, $"{LogPrefix} Abort: quest no longer active (id={questId}).");
                return;
            }

            string flavor = spec.Flavor?.Trim();
            if (string.IsNullOrWhiteSpace(flavor))
                flavor = spec.Description?.Trim();

            if (string.IsNullOrWhiteSpace(flavor))
            {
                RimAiLog.Info(RimAiLogCategory.Art, $"{LogPrefix} Abort: empty LLM output (id={questId}).");
                return;
            }

            record.Quest.description = $"{record.OriginalDescription}\n\n{flavor}";
            RimAiLog.Info(RimAiLogCategory.Art, $"{LogPrefix} Applied flavor (id={questId}, def={record.Quest?.root?.defName ?? "null"}).");
        }

        private static bool IsQuestActive(Quest quest)
        {
            if (quest == null) return false;
            if (Find.QuestManager == null) return false;
            return Find.QuestManager.QuestsListForReading.Contains(quest);
        }

        private static bool IsQuestAllowed(LiteratureSettings settings, Quest quest)
        {
            if (settings == null || quest == null) return false;
            var def = quest.root;
            if (def == null || string.IsNullOrWhiteSpace(def.defName)) return false;
            var allowList = settings.questRewriteAllowList;
            if (allowList == null || allowList.Count == 0) return false;
            return allowList.Contains(def.defName);
        }


        private static string GetResolvedDescription(Quest quest)
        {
            if (quest == null) return null;
            var description = quest.description;
            if (description.NullOrEmpty()) return null;
            return description.Resolve().Trim();
        }

        private static Faction TryResolveIssuerFaction(Quest quest)
        {
            if (quest == null) return null;
            var candidates = quest.InvolvedFactions
                .Where(faction => faction != null && !faction.IsPlayer)
                .Distinct()
                .ToList();
            return candidates.Count == 1 ? candidates[0] : null;
        }

        private static bool ValidateRewrite(string rewritten, PendingQuestRewrite record, out string reason)
        {
            reason = "unknown";
            if (record == null)
            {
                reason = "missing record";
                return false;
            }
            if (string.IsNullOrWhiteSpace(rewritten))
            {
                reason = "empty description";
                return false;
            }
            if (string.Equals(rewritten.Trim(), record.OriginalDescription.Trim(), StringComparison.Ordinal))
            {
                reason = "unchanged text";
                return false;
            }
            int missingRequired = CountMissingTokens(rewritten, record.RequiredTokens);
            if (missingRequired > 0)
            {
                reason = $"missing required tokens ({missingRequired}/{record.RequiredTokens.Count})";
                return false;
            }
            if (!NumbersSubset(rewritten, record.NumberTokens))
            {
                reason = "unexpected numbers";
                return false;
            }
            return true;
        }

        private static bool ContainsAllTokens(string text, List<string> tokens)

        {

            return QuestDescriptionTokenCatalog.ContainsAllTokens(text, tokens);

        }


        private static bool NumbersSubset(string text, List<string> allowedNumbers)

        {

            return QuestDescriptionTokenCatalog.NumbersSubset(text, allowedNumbers);

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
            // Wave D: gate enqueue on composition start — do not Clear on Stop.
            if (!ArtComposition.Current.IsStarted) return;
            if (action == null) return;
            lock (QueueLock)
                PendingActions.Enqueue(action);
        }

        private static List<string> ExtractNumberTokens(string description)

        {

            return QuestDescriptionTokenCatalog.ExtractNumberTokens(description);

        }


        private static List<string> MergeRequiredTokens(List<string> entities, List<string> numbers)

        {

            return QuestDescriptionTokenCatalog.MergeRequiredTokens(entities, numbers);

        }


        private static string FormatJsonArray(List<string> tokens)

        {

            return QuestDescriptionTokenCatalog.FormatJsonArray(tokens);

        }


        internal static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static string BuildQuestDataJson(PendingQuestRewrite record)

        {

            return QuestDescriptionTokenCatalog.BuildQuestDataJson(record);

        }


        private static List<string> GetFactionTokens(Quest quest)

        {

            return QuestDescriptionTokenCatalog.GetFactionTokens(quest);

        }


        private static List<string> GetLookTargetTokens(Quest quest)

        {

            return QuestDescriptionTokenCatalog.GetLookTargetTokens(quest);

        }


        private static string FormatQuestParts(Quest quest)

        {

            return QuestDescriptionTokenCatalog.FormatQuestParts(quest);

        }


        private static string FormatQuestPart(QuestPart part)

        {

            return QuestDescriptionTokenCatalog.FormatQuestPart(part);

        }


        private static bool TryFormatJsonValue(object value, out string formatted)

        {

            return QuestDescriptionTokenCatalog.TryFormatJsonValue(value, out formatted);

        }


        private static void CollectEntityTokens(
            Quest quest,
            string description,
            HashSet<string> required,
            HashSet<string> optional)

        {

            QuestDescriptionTokenCatalog.CollectEntityTokens(quest, description, required, optional);

        }


        private static void TryAddPawnTokensFromPart(QuestPart part, HashSet<string> optional, string description)

        {

            QuestDescriptionTokenCatalog.TryAddPawnTokensFromPart(part, optional, description);

        }


        internal static void AddOptional(HashSet<string> optional, string description, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (string.IsNullOrWhiteSpace(description)) return;
            if (!description.Contains(value)) return;
            optional.Add(value);
        }

        private static void CollectQuestPartTokens(
            Quest quest,
            string description,
            HashSet<string> required,
            HashSet<string> optional)

        {

            QuestDescriptionTokenCatalog.CollectQuestPartTokens(quest, description, required, optional);

        }


        private static void CollectTokensFromValue(
            object value,
            string description,
            HashSet<string> required,
            HashSet<string> optional)

        {

            QuestDescriptionTokenCatalog.CollectTokensFromValue(value, description, required, optional);

        }


        private static void AddTokenFromDescription(
            string value,
            string description,
            HashSet<string> required,
            HashSet<string> optional)

        {

            QuestDescriptionTokenCatalog.AddTokenFromDescription(value, description, required, optional);

        }


        private static int CountMissingTokens(string text, List<string> tokens)

        {

            return QuestDescriptionTokenCatalog.CountMissingTokens(text, tokens);

        }

    }
}
