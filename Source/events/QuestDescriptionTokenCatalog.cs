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

namespace Ustas.RimAI.Art.events;

internal static class QuestDescriptionTokenCatalog
{

        internal static bool TryFormatJsonValue(object value, out string formatted)
        {
            formatted = null;
            if (value == null) return false;

            switch (value)
            {
                case string s:
                    formatted = $"\"{QuestDescriptionRewriter.EscapeJson(s)}\"";
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
                    formatted = $"\"{QuestDescriptionRewriter.EscapeJson(e.ToString())}\"";
                    return true;
            }

            if (value is Def def)
            {
                formatted = $"\"{QuestDescriptionRewriter.EscapeJson(def.defName ?? def.label ?? string.Empty)}\"";
                return true;
            }

            if (value is Pawn pawn)
            {
                formatted = $"\"{QuestDescriptionRewriter.EscapeJson(pawn.Name?.ToStringShort ?? pawn.LabelShortCap ?? string.Empty)}\"";
                return true;
            }

            if (value is Faction faction)
            {
                formatted = $"\"{QuestDescriptionRewriter.EscapeJson(faction.Name ?? faction.def?.label ?? string.Empty)}\"";
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

        internal static void CollectEntityTokens(
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

        internal static void CollectTokensFromValue(
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

        internal static void TryAddPawnTokensFromPart(QuestPart part, HashSet<string> optional, string description)
        {
            var type = part.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var pawnField = type.GetField("pawn", flags);
            var pawnProp = type.GetProperty("pawn", flags);
            var pawnObj = pawnField != null ? pawnField.GetValue(part) : pawnProp?.GetValue(part, null);
            if (pawnObj is Pawn pawn)
            {
                QuestDescriptionRewriter.AddOptional(optional, description, pawn.LabelShortCap);
                QuestDescriptionRewriter.AddOptional(optional, description, pawn.Name?.ToStringShort);
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
                        QuestDescriptionRewriter.AddOptional(optional, description, listedPawn.LabelShortCap);
                        QuestDescriptionRewriter.AddOptional(optional, description, listedPawn.Name?.ToStringShort);
                    }
                }
            }
        }

        internal static void CollectQuestPartTokens(
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

        internal static List<string> MergeRequiredTokens(List<string> entities, List<string> numbers)
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

        internal static string FormatQuestPart(QuestPart part)
        {
            var type = part.GetType();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var sb = new StringBuilder();
            sb.Append("{\"type\":\"").Append(QuestDescriptionRewriter.EscapeJson(type.Name)).Append("\",\"fields\":{");

            bool first = true;
            for (int i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                if (field == null) continue;
                var value = field.GetValue(part);
                if (!TryFormatJsonValue(value, out var formatted))
                    continue;
                if (!first) sb.Append(',');
                sb.Append('\"').Append(QuestDescriptionRewriter.EscapeJson(field.Name)).Append("\":").Append(formatted);
                first = false;
            }

            sb.Append("}}");
            return sb.ToString();
        }

        internal static string FormatQuestParts(Quest quest)
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

        internal static List<string> GetLookTargetTokens(Quest quest)
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

        internal static string BuildQuestDataJson(PendingQuestRewrite record)
        {
            var quest = record?.Quest;
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"id\": {quest?.id ?? -1},");
            sb.AppendLine($"  \"def\": \"{QuestDescriptionRewriter.EscapeJson(quest?.root?.defName ?? string.Empty)}\",");
            sb.AppendLine($"  \"name\": \"{QuestDescriptionRewriter.EscapeJson(quest?.name ?? string.Empty)}\",");
            sb.AppendLine($"  \"requiredTokens\": {FormatJsonArray(record?.RequiredTokens)},");
            sb.AppendLine($"  \"optionalTokens\": {FormatJsonArray(record?.OptionalTokens)},");
            sb.AppendLine($"  \"numbers\": {FormatJsonArray(record?.NumberTokens)},");
            sb.AppendLine($"  \"involvedFactions\": {FormatJsonArray(GetFactionTokens(quest))},");
            sb.AppendLine($"  \"lookTargets\": {FormatJsonArray(GetLookTargetTokens(quest))},");
            sb.AppendLine($"  \"parts\": {FormatQuestParts(quest)},");
            sb.AppendLine($"  \"originalDescription\": \"{QuestDescriptionRewriter.EscapeJson(record?.OriginalDescription ?? string.Empty)}\"");
            sb.Append("}");
            return sb.ToString();
        }

        internal static List<string> ExtractNumberTokens(string description)
        {
            var tokens = new HashSet<string>();
            if (string.IsNullOrWhiteSpace(description)) return tokens.ToList();
            var matches = QuestDescriptionRewriter.NumberTokenRegex.Matches(description);
            for (int i = 0; i < matches.Count; i++)
            {
                var value = matches[i].Value;
                if (!string.IsNullOrWhiteSpace(value))
                    tokens.Add(value);
            }
            return tokens.ToList();
        }

        internal static string FormatJsonArray(List<string> tokens)
        {
            if (tokens == null || tokens.Count == 0) return "[]";
            var sb = new StringBuilder();
            sb.Append('[');
            for (int i = 0; i < tokens.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append('"').Append(QuestDescriptionRewriter.EscapeJson(tokens[i])).Append('"');
            }
            sb.Append(']');
            return sb.ToString();
        }

        internal static void AddTokenFromDescription(
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

        internal static int CountMissingTokens(string text, List<string> tokens)
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

        internal static List<string> GetFactionTokens(Quest quest)
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

        internal static bool ContainsAllTokens(string text, List<string> tokens)
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

        internal static bool NumbersSubset(string text, List<string> allowedNumbers)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var allowed = new HashSet<string>(allowedNumbers ?? new List<string>());
            var matches = QuestDescriptionRewriter.NumberTokenRegex.Matches(text);
            for (int i = 0; i < matches.Count; i++)
            {
                var value = matches[i].Value;
                if (!allowed.Contains(value))
                    return false;
            }
            return true;
        }
}
