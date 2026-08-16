/*
 * Purpose:
 * - Store standalone API configuration used when NOT reusing Ustas.RimAI.Communication API settings.
 *
 * Uses:
 * - Verse.ModSettings / IExposable
 *
 * Fields (minimum):
 * - apiKey
 * - baseUrl
 * - model
 * - providerHint (optional: "OpenAI-compatible")
 *
 * Responsibilities:
 * - ExposeData() for save/load.
 * - Provide helper methods to check completeness/validity.
 *
 * Do NOT:
 * - Do not create network clients here.
 * - Do not validate with network calls; only local checks.
 */
using System.Collections.Generic;
using Ustas.RimAI.Art.settings.util;
using Verse;

namespace Ustas.RimAI.Art.settings
{
    public sealed class LiteratureSettingsApi : IExposable
    {
        public string apiKey = string.Empty;
        public string baseUrl = LiteratureSettingsDef.DefaultBaseUrl;
        public string model = LiteratureSettingsDef.DefaultModel;

        public bool IsComplete =>
            !string.IsNullOrWhiteSpace(apiKey) &&
            !string.IsNullOrWhiteSpace(baseUrl) &&
            !string.IsNullOrWhiteSpace(model);

        public void ExposeData()
        {
            Scribe_Values.Look(ref apiKey, "apiKey", string.Empty);
            Scribe_Values.Look(ref baseUrl, "baseUrl", LiteratureSettingsDef.DefaultBaseUrl);
            Scribe_Values.Look(ref model, "model", LiteratureSettingsDef.DefaultModel);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                apiKey ??= string.Empty;
                baseUrl ??= LiteratureSettingsDef.DefaultBaseUrl;
                model ??= LiteratureSettingsDef.DefaultModel;
            }
        }

        public List<string> GetValidationErrors()
        {
            var errors = new List<string>();

            if (!SettingsValidators.TryValidateBaseUrl(baseUrl, out var baseUrlError))
                errors.Add(baseUrlError);

            if (!SettingsValidators.TryValidateApiKey(apiKey, out var apiKeyError))
                errors.Add(apiKeyError);

            if (!SettingsValidators.TryValidateModel(model, out var modelError))
                errors.Add(modelError);

            return errors;
        }
    }
}
