using HarmonyLib;
using Ustas.RimAI.Art.patches;
using Ustas.RimAI.Art.scanner.queue;
using Ustas.RimAI.Core.Composition;
using Ustas.RimAI.Core.Diagnostics;
using Ustas.RimAI.Core.Handshake;
using Ustas.RimAI.Core.Modules;

namespace Ustas.RimAI.Art;

/// <summary>
/// Module composition root for RimAI.Art. Owns Harmony install (process lifetime),
/// TalkLifecycle contributor registration, domain pending queue lifecycle clear,
/// and the thin literature orchestrator.
/// </summary>
public sealed class ArtComposition : IRimAiModuleComposition
{
    public static ArtComposition Current { get; } = new();

    public string ModuleId => RimAiModuleIds.Art;

    public bool IsStarted { get; private set; }

    /// <summary>Root-owned literature generation entry. Null while stopped.</summary>
    public ArtLiteratureOrchestrator Literature { get; private set; }

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
        Literature = new ArtLiteratureOrchestrator();
        IsStarted = true;
    }

    public void Stop()
    {
        if (!IsStarted)
            return;

        // Flip IsStarted before Clear so in-flight Requeue cannot repopulate (Wave C barrier).
        IsStarted = false;
        Literature = null;

        // Do not UnpatchAll — Harmony is process-lifetime.
        // Must clear TalkLifecycle registration flags so Start can re-subscribe;
        // otherwise prompt override / TV inject stay dead after Stop→Start.
        Patch_PromptService_Override.Unregister();
        Patch_ScribanParser_TvContent.Unregister();

        // Domain pending queues are lifecycle-owned here. Marshal Queue<Action> are not
        // Cleared on Stop (CompositionStopClearsMainThreadMarshalQueues = false); Wave D
        // gates EnqueueAction on IsStarted instead so producers cannot refill after Stop.
        // D-logging: consume Clear() counts so Stop is diagnosable (not count-and-discard).
        var clearedArt = PendingArtQueue.Clear();
        var clearedBook = PendingBookQueue.Clear();
        RimAiLog.Debug(
            RimAiLogCategory.Art,
            $"[RimAI.Art] Stop cleared domain pending queues art={clearedArt} book={clearedBook}");
    }
}
