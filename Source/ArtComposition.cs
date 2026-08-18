using HarmonyLib;
using Ustas.RimAI.Art.patches;
using Ustas.RimAI.Core.Composition;
using Ustas.RimAI.Core.Handshake;
using Ustas.RimAI.Core.Modules;

namespace Ustas.RimAI.Art;

/// <summary>
/// Module composition root for RimAI.Art. Owns Harmony and prompt-service patches.
/// </summary>
public sealed class ArtComposition : IRimAiModuleComposition
{
    public static ArtComposition Current { get; } = new();

    public string ModuleId => RimAiModuleIds.Art;

    public bool IsStarted { get; private set; }

    public void Start()
    {
        if (IsStarted)
            return;

        RimAIModuleRegistry.Current.Register(new RimAIModuleDescriptor(
            "art",
            "RimAI.Art",
            "RimAI.Art",
            "Art"));

        var harmony = new Harmony("Ustas.RimAI.Art");
        harmony.PatchAll();
        Patch_PromptService_Override.Register();
        Patch_ScribanParser_TvContent.Register();
        IsStarted = true;
    }

    public void Stop()
    {
        IsStarted = false;
    }
}
