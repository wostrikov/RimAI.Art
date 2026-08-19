/*
 * Purpose:
 * - Helper object to construct a standalone LLM request for memory summarization.
 *
 * Uses:
 * - PromptService.BuildContext
 * - IndependentBookLlmClient (independent LLM request)
 *
 * Responsibilities:
 * - Provide prompt instructions that request MemorySummarySpec JSON output.
 *
 * Design notes:
 * - BuildRequest must run on the main thread if it calls PromptService.
 * - QueryAsync sends via the independent LLM client (no RimTalk queue).
 *
 * Do NOT:
 * - Do not directly invoke AIService.
 * - Do not inject Constant.Instruction.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Ustas.RimAI.Communication.Service;
using Ustas.RimAI.Art.llm;
using Ustas.RimAI.Art.settings;
using Ustas.RimAI.Art.settings.util;
using Ustas.RimAI.Art.synopsis;
using Ustas.RimAI.Art.synopsis.llm;
using Verse;
using RimWorld;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Art.authoring.llm
{
    public static class MemorySummaryRequest
    {
        private const string TemplateResourceName =
            "Ustas.RimAI.Art.promptoverride.templates.Prompt_MemorySummary.txt";

        public static LiteratureLlmRequest BuildRequest(Pawn pawn)
        {
            if (pawn == null) return null;

            // Guard against early initialization issues without touching Faction.OfPlayer,
            // which logs an error before the player faction exists.
            if (!PlayerFactionUtility.TryGetPlayerFaction(out _))
            {
                RimAiLog.Warning(RimAiLogCategory.Art, "[RimAI.Art] MemorySummaryRequest.BuildRequest skipped: Faction manager or player faction not initialized.");
                return null;
            }

            try
            {
                var context = PromptService.BuildContext(new List<Pawn> { pawn });
                var prompt = BuildPrompt();

                return new LiteratureLlmRequest(prompt)
                {
                    Context = context
                };
            }
            catch (NullReferenceException ex)
            {
                RimAiLog.Warning(RimAiLogCategory.Art, $"[RimAI.Art] MemorySummaryRequest.BuildRequest failed: {ex.Message}. This may happen during pawn generation when internal caches are not ready.");
                return null;
            }
        }

        public static Task<MemorySummarySpec> QueryAsync(LiteratureLlmRequest request)
        {
            if (request == null) return Task.FromResult<MemorySummarySpec>(null);
            return IndependentBookLlmClient.QueryJsonAsync<MemorySummarySpec>(request);
        }

        private static string BuildPrompt()
        {
            var template = LoadTemplate();
            var settings = LiteratureMod.Settings;
            return PromptTemplateUtil.Resolve(
                settings?.promptMemorySummary,
                template,
                ("LANG", Constant.Lang),
                ("SUMMARY_MAX_CHARS", SynopsisTokenPolicy.PromptSynopsisMaxChars.ToString()),
                ("SUMMARY_MAX_SENTENCES", SynopsisTokenPolicy.SynopsisMaxSentences.ToString()));
        }

        public static string BuildDefaultPrompt()
        {
            var template = LoadTemplate();
            return PromptTemplateUtil.ApplyTokens(
                template,
                ("LANG", Constant.Lang),
                ("SUMMARY_MAX_CHARS", SynopsisTokenPolicy.PromptSynopsisMaxChars.ToString()),
                ("SUMMARY_MAX_SENTENCES", SynopsisTokenPolicy.SynopsisMaxSentences.ToString()));
        }

        private static string LoadTemplate()
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(TemplateResourceName);
            if (stream == null) return DefaultTemplate();

            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();
            return string.IsNullOrWhiteSpace(text) ? DefaultTemplate() : text.Trim();
        }

        private static string DefaultTemplate()
        {
            return
                "Підсумуй нещодавні спогади pawn на основі наданого контексту.\n" +
                "Пиши мовою {{LANG}}. Виведи лише JSON.\n\n" +
                "Обов'язкові поля JSON:\n" +
                "- \"summary\": <= {{SUMMARY_MAX_CHARS}} символів і {{SUMMARY_MAX_SENTENCES}} речень\n" +
                "- \"keywords\": 3–6 коротких ключових слів\n" +
                "- \"tone\": 1–2 слова\n\n" +
                "Використовуй лише наданий контекст; не вигадуй нових подій.";
        }
    }
}
