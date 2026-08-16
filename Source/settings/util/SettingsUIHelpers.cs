/*
 * Purpose:
 * - UI helper utilities to keep LiteratureSettingsWindow clean.
 *
 * Uses:
 * - Verse.Widgets / Rect calculations
 *
 * Responsibilities:
 * - Draw labeled text fields consistently.
 * - Optionally draw password-like fields (best-effort masking).
 * - Draw warnings (e.g., "API key is stored in config").
 *
 * Do NOT:
 * - Do not depend on RimTalk UI classes unless you intentionally want to.
 */
using System;
using UnityEngine;
using Ustas.RimAI.Art.settings;
using Verse;

namespace Ustas.RimAI.Art.settings.util
{
    public static class SettingsUIHelpers
    {

        public static string TextFieldLabeled(Listing_Standard listing, string label, string value, int maxLength)
        {
            if (listing == null) return value ?? string.Empty;

            Rect rowRect = listing.GetRect(LiteratureSettingsDef.RowHeight);
            float labelWidth = Mathf.Max(120f, rowRect.width * 0.35f);
            Rect labelRect = new Rect(rowRect.x, rowRect.y, labelWidth, rowRect.height);
            Rect fieldRect = new Rect(labelRect.xMax + LiteratureSettingsDef.FieldGap, rowRect.y,
                rowRect.width - labelWidth - LiteratureSettingsDef.FieldGap, rowRect.height);

            var anchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, label);
            Text.Anchor = anchor;

            value ??= string.Empty;
            value = Widgets.TextField(fieldRect, value);

            return Clamp(value, maxLength);
        }

        public static int IntFieldLabeled(Listing_Standard listing, string label, int value, int min, int max)
        {
            if (listing == null) return value;

            Rect rowRect = listing.GetRect(LiteratureSettingsDef.RowHeight);
            float labelWidth = Mathf.Max(120f, rowRect.width * 0.35f);
            Rect labelRect = new Rect(rowRect.x, rowRect.y, labelWidth, rowRect.height);
            Rect fieldRect = new Rect(labelRect.xMax + LiteratureSettingsDef.FieldGap, rowRect.y,
                rowRect.width - labelWidth - LiteratureSettingsDef.FieldGap, rowRect.height);

            var anchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, label);
            Text.Anchor = anchor;

            string text = Widgets.TextField(fieldRect, value.ToString());
            if (int.TryParse(text, out var parsed))
                value = parsed;

            if (value < min) value = min;
            if (value > max) value = max;

            return value;
        }

        public static string PasswordFieldLabeled(Listing_Standard listing, string label, string value, int maxLength)
        {
            if (listing == null) return value ?? string.Empty;

            Rect rowRect = listing.GetRect(LiteratureSettingsDef.RowHeight);
            float labelWidth = Mathf.Max(120f, rowRect.width * 0.35f);
            Rect labelRect = new Rect(rowRect.x, rowRect.y, labelWidth, rowRect.height);
            Rect fieldRect = new Rect(labelRect.xMax + LiteratureSettingsDef.FieldGap, rowRect.y,
                rowRect.width - labelWidth - LiteratureSettingsDef.FieldGap, rowRect.height);

            var anchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, label);
            Text.Anchor = anchor;

            value ??= string.Empty;

            value = GUI.PasswordField(fieldRect, value, '•');

            return Clamp(value, maxLength);
        }

        public static void DrawValidationMessages(Listing_Standard listing, string header, string[] messages)
        {
            if (listing == null || messages == null || messages.Length == 0) return;

            Text.Font = GameFont.Tiny;
            GUI.color = Color.yellow;
            if (!string.IsNullOrWhiteSpace(header))
                listing.Label(header);

            for (int i = 0; i < messages.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(messages[i])) continue;
                listing.Label(messages[i]);
            }

            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private static string Mask(string value)
        {
            if (string.IsNullOrEmpty(value)) return "RimTalkLE_Settings_Empty".Translate();
            int len = Math.Min(value.Length, 12);
            return new string('*', len);
        }

        private static string Clamp(string value, int maxLength)
        {
            if (maxLength <= 0 || value == null) return value ?? string.Empty;
            return value.Length > maxLength ? value.Substring(0, maxLength) : value;
        }
    }
}
