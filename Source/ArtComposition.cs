using HarmonyLib;
using Ustas.RimAI.Art.patches;
using Ustas.RimAI.Art.scanner.queue;
using Ustas.RimAI.Core.Composition;
using Ustas.RimAI.Core.Handshake;
using Ustas.RimAI.Core.Modules;

namespace Ustas.RimAI.Art;

/// <summary>
/// Module composition root for RimAI.Art. Owns Harmony install (process lifetime),
/// TalkLifecycle contributor registration (prompt override + TV Scriban), and
/// lifecycle clear of domain pending generation queues.
/// </summary>
public sealed class ArtComposition : IRimAiModuleComposition
{
    public static ArtComposition Current { get; } = new();

    public string ModuleId => RimAiModuleIds.Art;

    public bool IsStarted { get; private set; }

    Harmony _harmony;

    public void Start()
    {
        if (IsStarted)
            return;

        RimAIModuleRegistry.Current.Register(new RimAIModuleDescriptor(
            "art",
            "RimAI.Art",
            "RimAI.Art",
            "Art"));

        // Harmony is process-lifetime (matches Personas/Events/Memory/Communication).
        // PatchAll on every Start is idempotent for the same harmony id; do not UnpatchAll on Stop.
        _harmony ??= new Harmony("Ustas.RimAI.Art");
        _harmony.PatchAll();
        Patch_PromptService_Override.Register();
        Patch_ScribanParser_TvContent.Register();
        IsStarted = true;
    }

    public void Stop()
    {
        if (!IsStarted)
            return;

        // Do not UnpatchAll — Harmony is process-lifetime.
        // Must clear TalkLifecycle registration flags so Start can re-subscribe;
        // otherwise prompt override / TV inject stay dead after Stop→Start.
        Patch_PromptService_Override.Unregister();
        Patch_ScribanParser_TvContent.Unregister();

        // Wave B2: domain pending queues are lifecycle-owned here. Drop deferred LLM work
        // so Stop→Start does not resume stale art/book generation. Marshal Queue<Action>
        // (letter/quest/ideo) are not cleared — they are main-thread dispatch only.
        PendingArtQueue.Clear();
        PendingBookQueue.Clear();

        IsStarted = false;
    }
}
