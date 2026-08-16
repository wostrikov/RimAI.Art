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
using Ustas.RimAI.Art.llm;
using Ustas.RimAI.Art.settings;
using Ustas.RimAI.Art.synopsis.llm;
using RimWorld;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Art.events
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
        private static readonly Regex NumberTokenRegex = new Regex(@"\d+(?:\.\d+)?%?", RegexOptions.Compiled);

        private static readonly Dictionary<int, PendingQuestRewrite> Pending = new Dictionary<int, PendingQuestRewrite>();
        private static readonly Queue<Action> PendingActions = new Queue<Action>();
        private static readonly object QueueLock = new object();

        public static void TryQueue(Quest quest)
        {
            var settings = LiteratureMod.Settings;
            if (settings != null && !settings.enabled)
            {
                Log.Message($"{LogPrefix} Skip: feature disabled.");
                return;
            }
            if (quest == null)
            {
                Log.Message($"{LogPrefix} Skip: quest is null.");
                return;
            }
            if (quest.hidden || quest.hiddenInUI)
            {
                Log.Message($"{LogPrefix} Skip: quest hidden (id={quest.id}).");
                return;
            }
            if (Find.TickManager == null)
            {
                Log.Message($"{LogPrefix} Skip: TickManager unavailable (quest id={quest.id}).");
                return;
            }

            if (!IsQuestAllowed(settings, quest))
            {
                Log.Message($"{LogPrefix} Skip: quest not allowed (id={quest.id}, def={quest.root?.defName ?? "null"}).");
                return;
            }

            if (Pending.ContainsKey(quest.id))
            {
                Log.Message($"{LogPrefix} Skip: quest already queued (id={quest.id}).");
                return;
            }

            string original = GetResolvedDescription(quest);
            if (string.IsNullOrWhiteSpace(original))
            {
                Log.Message($"{LogPrefix} Skip: empty description (id={quest.id}).");
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
            Log.Message($"{LogPrefix} Queued quest (id={quest.id}, def={quest.root?.defName ?? "null"}, name={quest.name ?? "?"}, required={record.RequiredTokens.Count}, optional={record.OptionalTokens.Count}, numbers={record.NumberTokens.Count}).");
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
                    Log.Message($"{LogPrefix} Expired (id={record.QuestId}, def={record.Quest?.root?.defName ?? "null"}).");
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
                Log.Message($"{LogPrefix} Abort: failed to build request (id={record.QuestId}).");
                return;
            }

            record.Requested = true;
            Log.Message($"{LogPrefix} Dispatching LLM request (id={record.QuestId}, def={record.Quest?.root?.defName ?? "null"}).");

            var task = IndependentBookLlmClient.QueryJsonAsync<QuestDescriptionSpec>(request);
            task.ContinueWith(t =>
            {
                var spec = t.Status == TaskStatus.RanToCompletion ? t.Result : null;
                if (spec == null)
                    Log.Message($"{LogPrefix} LLM returned null (id={record.QuestId}).");
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
Пиши мовою {RimTalkConstantShim.Lang}. Виведи лише JSON.

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
                Log.Message($"{LogPrefix} Abort: missing record or spec (id={questId}).");
                return;
            }
            if (Find.TickManager == null) return;
            if (Find.TickManager.TicksGame > record.DeadlineTick)
            {
                Log.Message($"{LogPrefix} Abort: response late (id={questId}).");
                return;
            }

            if (!IsQuestActive(record.Quest))
            {
                Log.Message($"{LogPrefix} Abort: quest no longer active (id={questId}).");
                return;
            }

            string flavor = spec.Flavor?.Trim();
            if (string.IsNullOrWhiteSpace(flavor))
                flavor = spec.Description?.Trim();

            if (string.IsNullOrWhiteSpace(flavor))
            {
                Log.Message($"{LogPrefix} Abort: empty LLM output (id={questId}).");
                return;
            }

            record.Quest.description = $"{record.OriginalDescription}\n\n{flavor}";
            Log.Message($"{LogPrefix} Applied flavor (id={questId}, def={record.Quest?.root?.defName ?? "null"}).");
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
            if (tokens == null || tokens.Count == 0) return true;
            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (string.IsNullOrWhiteSpace(token)) continue;
                if (!text.Contains(token))
                    return false;
            }
            return true;
        }

        private static bool NumbersSubset(string text, List<string> allowedNumbers)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var allowed = new HashSet<string>(allowedNumbers ?? new List<string>());
            var matches = NumberTokenRegex.Matches(text);
            for (int i = 0; i < matches.Count; i++)
            {
                var value = matches[i].Value;
                if (!allowed.Contains(value))
                    return false;
            }
            return true;
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
            if (action == null) return;
            lock (QueueLock)
                PendingActions.Enqueue(action);
        }

        private sealed class PendingQuestRewrite
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

        private static List<string> ExtractNumberTokens(string description)
        {
            var tokens = new HashSet<string>();
            if (string.IsNullOrWhiteSpace(description)) return tokens.ToList();
            var matches = NumberTokenRegex.Matches(description);
            for (int i = 0; i < matches.Count; i++)
            {
                var value = matches[i].Value;
                if (!string.IsNullOrWhiteSpace(value))
                    tokens.Add(value);
            }
            return tokens.ToList();
        }

        private static List<string> MergeRequiredTokens(List<string> entities, List<string> numbers)
        {
            var tokens = new HashSet<string>();
            if (entities != null)
            {
                for (int i = 0; i < entities.Count; i++)
                {
                    var token = entities[i];
                    if (!string.IsNullOrWhiteSpace(token))
                        tokens.Add(token);
                }
            }
            if (numbers != null)
            {
                for (int i = 0; i < numbers.Count; i++)
                {
                    var token = numbers[i];
                    if (!string.IsNullOrWhiteSpace(token))
                        tokens.Add(token);
                }
            }
            return tokens.OrderByDescending(t => t.Length).ToList();
        }

        private static string FormatJsonArray(List<string> tokens)
        {
            if (tokens == null || tokens.Count == 0) return "[]";
            var sb = new StringBuilder();
            sb.Append('[');
            for (int i = 0; i < tokens.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append('"').Append(EscapeJson(tokens[i])).Append('"');
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static string BuildQuestDataJson(PendingQuestRewrite record)
        {
            var quest = record?.Quest;
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"id\": {quest?.id ?? -1},");
            sb.AppendLine($"  \"def\": \"{EscapeJson(quest?.root?.defName ?? string.Empty)}\",");
            sb.AppendLine($"  \"name\": \"{EscapeJson(quest?.name ?? string.Empty)}\",");
            sb.AppendLine($"  \"requiredTokens\": {FormatJsonArray(record?.RequiredTokens)},");
            sb.AppendLine($"  \"optionalTokens\": {FormatJsonArray(record?.OptionalTokens)},");
            sb.AppendLine($"  \"numbers\": {FormatJsonArray(record?.NumberTokens)},");
            sb.AppendLine($"  \"involvedFactions\": {FormatJsonArray(GetFactionTokens(quest))},");
            sb.AppendLine($"  \"lookTargets\": {FormatJsonArray(GetLookTargetTokens(quest))},");
            sb.AppendLine($"  \"parts\": {FormatQuestParts(quest)},");
            sb.AppendLine($"  \"originalDescription\": \"{EscapeJson(record?.OriginalDescription ?? string.Empty)}\"");
            sb.Append("}");
            return sb.ToString();
        }

        private static List<string> GetFactionTokens(Quest quest)
        {
            var tokens = new HashSet<string>();
            if (quest == null) return tokens.ToList();
            foreach (var faction in quest.InvolvedFactions)
            {
                if (faction == null) continue;
                if (!string.IsNullOrWhiteSpace(faction.Name)) tokens.Add(faction.Name);
                if (!string.IsNullOrWhiteSpace(faction.def?.label)) tokens.Add(faction.def.label);
            }
            return tokens.OrderByDescending(t => t.Length).ToList();
        }

        private static List<string> GetLookTargetTokens(Quest quest)
        {
            var tokens = new HashSet<string>();
            if (quest == null) return tokens.ToList();
            foreach (var target in quest.QuestLookTargets)
            {
                if (target.Thing == null) continue;
                var pawn = target.Thing as Pawn;
                if (pawn != null)
                {
                    if (!string.IsNullOrWhiteSpace(pawn.LabelShortCap)) tokens.Add(pawn.LabelShortCap);
                    if (!string.IsNullOrWhiteSpace(pawn.Name?.ToStringShort)) tokens.Add(pawn.Name.ToStringShort);
                }
                else
                {
                    var label = target.Thing.LabelCap;
                    if (!string.IsNullOrWhiteSpace(label)) tokens.Add(label);
                }
            }
            return tokens.OrderByDescending(t => t.Length).ToList();
        }

        private static string FormatQuestParts(Quest quest)
        {
            if (quest == null) return "[]";
            var parts = quest.PartsListForReading;
            if (parts == null || parts.Count == 0) return "[]";

            var sb = new StringBuilder();
            sb.Append('[');
            bool first = true;
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                if (part == null) continue;
                var partJson = FormatQuestPart(part);
                if (string.IsNullOrWhiteSpace(partJson)) continue;
                if (!first) sb.Append(", ");
                sb.Append(partJson);
                first = false;
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static string FormatQuestPart(QuestPart part)
        {
            var type = part.GetType();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var sb = new StringBuilder();
            sb.Append("{\"type\":\"").Append(EscapeJson(type.Name)).Append("\",\"fields\":{");

            bool first = true;
            for (int i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                if (field == null) continue;
                var value = field.GetValue(part);
                if (!TryFormatJsonValue(value, out var formatted))
                    continue;
                if (!first) sb.Append(',');
                sb.Append('\"').Append(EscapeJson(field.Name)).Append("\":").Append(formatted);
                first = false;
            }

            sb.Append("}}");
            return sb.ToString();
        }

        private static bool TryFormatJsonValue(object value, out string formatted)
        {
            formatted = null;
            if (value == null) return false;

            switch (value)
            {
                case string s:
                    formatted = $"\"{EscapeJson(s)}\"";
                    return true;
                case bool b:
                    formatted = b ? "true" : "false";
                    return true;
                case int i:
                    formatted = i.ToString(CultureInfo.InvariantCulture);
                    return true;
                case long l:
                    formatted = l.ToString(CultureInfo.InvariantCulture);
                    return true;
                case float f:
                    formatted = f.ToString(CultureInfo.InvariantCulture);
                    return true;
                case double d:
                    formatted = d.ToString(CultureInfo.InvariantCulture);
                    return true;
                case Enum e:
                    formatted = $"\"{EscapeJson(e.ToString())}\"";
                    return true;
            }

            if (value is Def def)
            {
                formatted = $"\"{EscapeJson(def.defName ?? def.label ?? string.Empty)}\"";
                return true;
            }

            if (value is Pawn pawn)
            {
                formatted = $"\"{EscapeJson(pawn.Name?.ToStringShort ?? pawn.LabelShortCap ?? string.Empty)}\"";
                return true;
            }

            if (value is Faction faction)
            {
                formatted = $"\"{EscapeJson(faction.Name ?? faction.def?.label ?? string.Empty)}\"";
                return true;
            }

            if (value is System.Collections.IEnumerable enumerable)
            {
                var items = new List<string>();
                foreach (var item in enumerable)
                {
                    if (item == null) continue;
                    if (TryFormatJsonValue(item, out var formattedItem))
                        items.Add(formattedItem);
                }
                if (items.Count == 0) return false;
                formatted = $"[{string.Join(", ", items)}]";
                return true;
            }

            return false;
        }

        private static void CollectEntityTokens(
            Quest quest,
            string description,
            HashSet<string> required,
            HashSet<string> optional)
        {
            if (quest == null || string.IsNullOrWhiteSpace(description)) return;

            void AddToken(string value, bool isRequired)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                if (!description.Contains(value)) return;
                if (isRequired)
                    required.Add(value);
                else
                    optional.Add(value);
            }

            AddToken(quest.name, false);

            foreach (var faction in quest.InvolvedFactions)
            {
                if (faction == null) continue;
                AddToken(faction.Name, true);
                if (faction.def != null)
                    AddToken(faction.def.label, false);
            }

            foreach (var target in quest.QuestLookTargets)
            {
                var pawn = target.Thing as Pawn;
                if (pawn == null) continue;
                AddToken(pawn.LabelShortCap, true);
                AddToken(pawn.Name?.ToStringShort, true);
            }

            var parts = quest.PartsListForReading;
            if (parts != null)
            {
                for (int i = 0; i < parts.Count; i++)
                {
                    var part = parts[i];
                    if (part == null) continue;
                    TryAddPawnTokensFromPart(part, optional, description);
                }
            }
        }

        private static void TryAddPawnTokensFromPart(QuestPart part, HashSet<string> optional, string description)
        {
            var type = part.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var pawnField = type.GetField("pawn", flags);
            var pawnProp = type.GetProperty("pawn", flags);
            var pawnObj = pawnField != null ? pawnField.GetValue(part) : pawnProp?.GetValue(part, null);
            if (pawnObj is Pawn pawn)
            {
                AddOptional(optional, description, pawn.LabelShortCap);
                AddOptional(optional, description, pawn.Name?.ToStringShort);
            }

            var pawnsField = type.GetField("pawns", flags);
            var pawnsProp = type.GetProperty("pawns", flags);
            var pawnList = pawnsField != null ? pawnsField.GetValue(part) : pawnsProp?.GetValue(part, null);
            if (pawnList is System.Collections.IEnumerable enumerable)
            {
                foreach (var obj in enumerable)
                {
                    if (obj is Pawn listedPawn)
                    {
                        AddOptional(optional, description, listedPawn.LabelShortCap);
                        AddOptional(optional, description, listedPawn.Name?.ToStringShort);
                    }
                }
            }
        }

        private static void AddOptional(HashSet<string> optional, string description, string value)
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
            if (quest == null || string.IsNullOrWhiteSpace(description)) return;
            var parts = quest.PartsListForReading;
            if (parts == null || parts.Count == 0) return;

            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                if (part == null) continue;
                var type = part.GetType();
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int f = 0; f < fields.Length; f++)
                {
                    var field = fields[f];
                    if (field == null) continue;
                    var value = field.GetValue(part);
                    CollectTokensFromValue(value, description, required, optional);
                }
            }
        }

        private static void CollectTokensFromValue(
            object value,
            string description,
            HashSet<string> required,
            HashSet<string> optional)
        {
            if (value == null) return;
            switch (value)
            {
                case string s:
                    AddTokenFromDescription(s, description, required, optional);
                    return;
                case Def def:
                    AddTokenFromDescription(def.label, description, required, optional);
                    AddTokenFromDescription(def.defName, description, required, optional);
                    return;
                case Pawn pawn:
                    AddTokenFromDescription(pawn.LabelShortCap, description, required, optional);
                    AddTokenFromDescription(pawn.Name?.ToStringShort, description, required, optional);
                    return;
                case Faction faction:
                    AddTokenFromDescription(faction.Name, description, required, optional);
                    AddTokenFromDescription(faction.def?.label, description, required, optional);
                    return;
            }

            if (value is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                    CollectTokensFromValue(item, description, required, optional);
            }
        }

        private static void AddTokenFromDescription(
            string value,
            string description,
            HashSet<string> required,
            HashSet<string> optional)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (string.IsNullOrWhiteSpace(description)) return;
            if (description.Contains(value))
                required.Add(value);
            else
                optional.Add(value);
        }

        private static int CountMissingTokens(string text, List<string> tokens)
        {
            if (tokens == null || tokens.Count == 0) return 0;
            int missing = 0;
            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (string.IsNullOrWhiteSpace(token)) continue;
                if (!text.Contains(token))
                    missing++;
            }
            return missing;
        }
    }
}
