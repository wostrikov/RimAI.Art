/*
 * Purpose:
 * - Local validation helpers for settings inputs.
 *
 * Uses:
 * - System.Uri parsing
 *
 * Responsibilities:
 * - Validate baseUrl is a valid absolute URI.
 * - Validate apiKey non-empty when required.
 * - Validate model non-empty (if required by your client).
 *
 * Do NOT:
 * - Do not perform network calls.
 */
using System;
using Verse;

namespace Ustas.RimAI.Art.Settings.Util
{
    public static class SettingsValidators
    {
        public static bool TryValidateBaseUrl(string baseUrl, out string error)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                error = "RimTalkLE_Settings_Error_BaseUrlEmpty".Translate();
                return false;
            }

            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            {
                error = "RimTalkLE_Settings_Error_BaseUrlAbsolute".Translate();
                return false;
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                error = "RimTalkLE_Settings_Error_BaseUrlScheme".Translate();
                return false;
            }

            error = null;
            return true;
        }

        public static bool TryValidateApiKey(string apiKey, out string error)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                error = "RimTalkLE_Settings_Error_ApiKeyEmpty".Translate();
                return false;
            }

            error = null;
            return true;
        }

        public static bool TryValidateModel(string model, out string error)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                error = "RimTalkLE_Settings_Error_ModelEmpty".Translate();
                return false;
            }

            error = null;
            return true;
        }
    }
}
