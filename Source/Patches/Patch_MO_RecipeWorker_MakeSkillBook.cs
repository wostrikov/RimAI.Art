/*
 * Purpose:
 * - Detect Medieval Overhaul skill book production and enqueue for authoring.
 *
 * Notes:
 * - Uses RimTalk prompt context via the existing authoring pipeline.
 * - Avoids direct MO assembly references by resolving the worker type by name.
 */
using System;
using System.Reflection;
using HarmonyLib;
using Ustas.RimAI.Art.Books;
using Ustas.RimAI.Art.Scanner.Queue;
using Ustas.RimAI.Art.Settings;
using Ustas.RimAI.Art.Storage;
using Ustas.RimAI.Art.Storage.Save;
using RimWorld;
using Verse;
using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Art.Patches
{
    [HarmonyPatch]
    public static class Patch_MO_RecipeWorker_MakeSkillBook
    {
        private const string MoWorkerTypeName = "MedievalOverhaul.RecipeWorker_MakeSkillBook";

        public static bool Prepare()
        {
            var type = AccessTools.TypeByName(MoWorkerTypeName);
            if (type == null) return false;
            return AccessTools.Method(type, "PostProcessProduct") != null;
        }

        public static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName(MoWorkerTypeName);
            if (type == null) return null;
            return AccessTools.Method(type, "PostProcessProduct");
        }

        public static void Postfix(object __instance, Thing product, RecipeDef recipeDef, Pawn worker)
        {
            if (product == null || product.DestroyedOrNull() || worker == null) return;

            var settings = LiteratureMod.Settings;
            if (settings != null && !settings.enabled) return;

            if (!IsMoWorker(__instance, recipeDef)) return;

            var meta = BookClassifier.Classify(product);
            if (meta == null) return;
            if (!BookFilterPolicy.IsAllowed(meta)) return;

            if (!BookKeyProvider.TryGetKey(product, out var key)) return;

            var cache = LiteratureSaveData.Current?.SynopsisCache;
            if (cache != null && cache.Contains(key)) return;
            if (PendingBookQueue.Contains(key)) return;

            PendingBookQueue.Enqueue(meta, worker);
            RimAiLog.Info(RimAiLogCategory.Art, $"[RimAI.Art] MO book produced; enqueued {meta.DefName} for authoring.");
        }

        private static bool IsMoWorker(object instance, RecipeDef recipeDef)
        {
            if (instance != null)
            {
                var fullName = instance.GetType().FullName ?? string.Empty;
                if (string.Equals(fullName, MoWorkerTypeName, StringComparison.Ordinal))
                    return true;
            }

            var workerType = recipeDef?.workerClass;
            var workerTypeName = workerType?.FullName ?? string.Empty;
            return string.Equals(workerTypeName, MoWorkerTypeName, StringComparison.Ordinal);
        }
    }
}
