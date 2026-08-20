/*
 * Purpose:
 * - Manage enabling and disabling prompt overrides for RimTalk requests.
 *
 * Uses:
 * - PromptOverrideContext
 * - RimTalk PromptService (via patch)
 *
 * Responsibilities:
 * - Provide a safe API to apply prompt overrides.
 * - Ensure overrides are cleared after use.
 *
 * Design notes:
 * - This is the ONLY place that controls prompt overrides.
 *
 * Do NOT:
 * - Do not hardcode prompt text here.
 * - Do not bypass RimTalk context building.
 */
using System;

namespace Ustas.RimAI.Art.PromptOverride
{
    public static class PromptOverrideService
    {
        private static PromptOverrideContext _current;

        public static IDisposable Use(PromptOverrideContext context)
        {
            _current = context;
            return new OverrideScope();
        }

        public static PromptOverrideContext Peek()
        {
            return _current;
        }

        public static PromptOverrideContext Consume()
        {
            var ctx = _current;
            _current = null;
            return ctx;
        }

        public static void Clear()
        {
            _current = null;
        }

        private sealed class OverrideScope : IDisposable
        {
            public void Dispose()
            {
                Clear();
            }
        }
    }
}
