/*
 * Purpose:
 * - Apply optional placeholder tokens to prompt templates.
 */
using System;

namespace Ustas.RimAI.Art.Settings.Util
{
    public static class PromptTemplateUtil
    {
        public static string Resolve(string overrideText, string defaultTemplate, params (string key, string value)[] tokens)
        {
            var template = string.IsNullOrWhiteSpace(overrideText) ? defaultTemplate : overrideText;
            return ApplyTokens(template, tokens);
        }

        public static string ApplyTokens(string template, params (string key, string value)[] tokens)
        {
            if (string.IsNullOrWhiteSpace(template)) return string.Empty;
            if (tokens == null || tokens.Length == 0) return template;

            string result = template;
            for (int i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i];
                if (string.IsNullOrWhiteSpace(token.key)) continue;
                string placeholder = "{{" + token.key + "}}";
                result = result.Replace(placeholder, token.value ?? string.Empty);
            }

            return result;
        }
    }
}
