# HISTORICAL EVIDENCE — Phase 7.5.12 Art inventory

Do not treat as current architecture. Current ownership is RimAI.Art / RimAI Core Text-AI.

# Art Deep Reform inventory — Phase 7.5.12

Measured against `RimAI.Art`. Production scope: `Source/**/*.cs` excluding `obj`/`bin`.

| Wave | Status |
| --- | --- |
| A | inventory + characterization + Core prompt contracts consumed |
| pre-B | arbiter coverage facts corrected; queue + Stop behavioral isolation frozen |
| B1 | composition Stop unwind (Talk/Scriban Unregister + `_registered` clear) — done |
| B2 | domain pending queue lifecycle under ArtComposition; Stop Clears — done |
| C | IsStarted queue barrier; Google/Player2 direct Admit; thin orchestrator/result — done |
| D-logging | RimAiLog migration + Clear counts + Admit reject logging — done |
| D | marshal enqueue IsStarted gate (no Clear); stage close — done |

---

## Product shape

`RimAI.Art` (Literature Expansion) is a **literature / text-generation** module for
art descriptions, book synopses, letters, quests, ideo text, journals, TV programs,
and related rewriting — **not** an image-generation pipeline.

```text
LiteratureMod (settings + handshake)
  → ArtComposition.Start
       → RimAIModuleRegistry
       → Harmony PatchAll
       → Patch_PromptService_Override.Register (Talk decorate)
       → Patch_ScribanParser_TvContent.Register
  → ArtComposition.Stop
       → Talk/Scriban Unregister
       → PendingArtQueue.Clear + PendingBookQueue.Clear (counts → RimAiLog.Debug)
  → LiteratureGameComponent tick hub
       → queues / processors / schedulers / rewriters
            → *PromptBuilder → LiteratureLlmRequest
                 → IndependentBookLlmClient
                      → RimTalk AIClientFactory
                      OR SharedTextAiOrchestrator (+ AiRequestMetadata art-literature)
                      OR SharedHttpTransport (Google / Player2)
            → *Cache in LiteratureSaveData (WorldComponent)
  → display Harmony patches read caches into labels / tooltips / CompArt
```

Frozen Core facts: `Ustas.RimAI.Core.Art.ArtInteriorDefaults`,
`Ustas.RimAI.Core.Art.ArtPromptDefaults`.
Characterization: `Stage7512ArtInteriorCharacterizationTests`.

### Cross-module callers

Sibling products under `sources/` do **not** import Art C# types
(`ArtComposition`, `Literature*`, synopsis/TV/persona pipelines). Integration is
**event-only**: Art subscribes to Core `TalkLifecycle.PromptDecorated` /
`ScribanRendered`; Communication publishes those events without referencing Art.

---

## Wave A baselines (committed architecture inventories)

| Bucket | Art value | Source |
| --- | --- | --- |
| Direct host logging (TEMPORARY) | **0** (Wave A was **179**) | `direct-host-logging-baseline.json` after D-logging |
| Catch-all baseline by_module | **9** | `catch-all-baseline.json` |
| Catch inventory by_module (raw) | 12 | `phase755-catch-inventory.json` (includes bare) |
| DOMAIN catch (phase755 category) | **4** | `phase755-catch-inventory.json` |
| Ambient `.Current` | **2** | `ArtComposition.Current`, `LiteratureSaveData.Current` |
| File I/O TEMPORARY | **0** | no Art keys in `direct-file-io-baseline.json` |
| Oversized WARN (phase754) | **2** | warn_by_module (historical inventory; current largest file ~731 LOC) |
| RimAiLog usages | **migrated** (category `Art`) | D-logging; Wave A was 0 |
| Live `[HarmonyPatch]` attributes | **~24** | source scan |
| Production `.cs` files / LOC | **132** / **~13.7k** | Wave A measure |

Rule: `CURRENT_TEMPORARY <= COMMITTED_TEMPORARY_BASELINE` (never upward).

---

## Lifecycle / composition

| Item | Behavior |
| --- | --- |
| Entry | `LiteratureMod` → `RimAiHandshake.TryActivate(..., ArtComposition.Current.Start)` |
| Start | Idempotent `IsStarted` guard; module register; Harmony PatchAll (process lifetime); Talk/Scriban `Register()` |
| Stop | Sets `IsStarted=false` first; nulls `Literature` orchestrator; Unregisters Talk/Scriban; **Clears** domain pending queues and **logs Clear counts** (Debug); **no** UnpatchAll; **no** marshal Clear (enqueue gated) |
| Start after Stop | Re-runs PatchAll; Talk/Scriban re-subscribe; new `ArtLiteratureOrchestrator`; domain queues empty |
| Ambient | `ArtComposition.Current` (ALLOWED facade candidate); `LiteratureSaveData.Current` |
| Long-lived services | Domain pending queue lifecycle + `ArtLiteratureOrchestrator` owned by composition; processors still static |
| Queue barrier (Wave C) | Enqueue / TryDequeue / Requeue require `IsStarted` — in-flight Requeue after Stop cannot repopulate |
| Marshal barrier (Wave D) | `EnqueueAction` requires `IsStarted`; Stop does **not** Clear marshal queues; already-queued actions may still drain |
| Independent HTTP arbiter (Wave C) | Google/Player2: direct `AiRequestArbiter.Admit`; OpenAI/Custom: SharedTextAi only (no outer Admit) |
| Settings | `LiteratureMod.Settings` static field (live reads in prompt builders) |

Wave B1 acceptance: Stop→Start restores TalkLifecycle subscriptions (`StartAfterStopReSubscribesTalkLifecycle = true`).
Wave B2 acceptance: Stop Clears domain pending queues (`CompositionStopClearsDomainPendingQueues = true`);
save→quit→load still drops unsaved work (transient-by-design). Marshal queues untouched.

---

## Generation triggers (inventory)

### Production / scan → queues

| Trigger | Key types |
| --- | --- |
| CompArt initialize | `Patch_CompArt_Initialize` → `ArtProductionTracker` → `PendingArtQueue` |
| Persona weapon bond | `Patch_CompBladelinkWeapon_Bonded` → `PersonaWeaponAuthoringPipeline` |
| Book bill / recipes | `Patch_BillProduction_Finish`, `Patch_GenRecipe_MakeRecipeProducts`, Kiiro/MO patches → book queues |
| Map load / daily scan | `Patch_MapLoaded_Scan`, `Patch_Tick_DailyScan` → art/book scanners → queues |
| Letters received | `Patch_LetterStack_Receive` → `LetterTextRewriter` |
| Quests | `Patch_QuestDescription_Queue` → `QuestDescriptionRewriter` |
| Ideo regenerate | `Patch_Ideo_RegenerateDescription` → `IdeoDescriptionRewriter` |
| Gift delivery | `Patch_TransportersArrivalAction_GiveGift` → quest event scheduler |
| Journal job | `JobDriver_WriteJournal` / float menu → book queue |
| Talk inject | `TalkPromptBookInjector` via prompt override (may enqueue missing book) |
| Scheduled letters | `LetterEventScheduler` (ally/family easter letters when enabled) |
| Quest advert/warning | `QuestEventScheduler` flows exist; **auto tick schedule DISABLED** (TODO); **DebugAction** only |

### Tick hub

`LiteratureGameComponent.GameComponentTick` drives processors and schedulers.

### UI / debug (not all LLM)

| Entry | Notes |
| --- | --- |
| Manual text gizmo | `ManualTextEditService` — cache edit/restore, not regen |
| Write journal float menu | starts job → later LLM |
| Debug letter/quest actions | debug only (incl. advert/warning quests) |
| Settings clear-cache | `LiteratureSettingsWindow` — not LLM |

### Display-only (not generation)

CompArt / Thing label/tooltip/description patches; Scriban TV content inject from cache.

### Dead / dormant path

`TvProgramService.GetOrGenerateAsync` + `TvProgramPromptBuilder` exist; **no production callers** found in Wave A scan.
TV inject reads cache only; manual edit can populate cache.

---

## Pipelines (all share `IndependentBookLlmClient`)

Frozen list (`ArtInteriorDefaults.GenerationPipelines`, 14):

ArtDescription, PersonaWeapon, BookSynopsis, TvProgram, LetterRewrite, LetterScheduler,
QuestRewrite, QuestAdvertisement, QuestWarning, IdeoRewrite, JournalFromSummary,
BookFromSummary, MemorySummary, ManualTextEdit.

Canonical art-description chain:

```text
trigger → ArtMeta / subject
  → ArtDescriptionService.GetOrGenerateAsync
  → ArtPromptBuilder (instruction + context)
  → IndependentBookLlmClient.QueryJsonAsync
  → ArtDescriptionCache (LiteratureSaveData)
  → CompArt / tooltip display patches
```

---

## Prompt builders

| Type | File | Notes |
| --- | --- | --- |
| `ArtPromptBuilder` | `Source/art/ArtPromptBuilder.cs` | Consumes `ArtPromptDefaults` (Wave A) |
| `SynopsisPromptBuilder` | `Source/synopsis/SynopsisPromptBuilder.cs` | Book synopses |
| `TvProgramPromptBuilder` | `Source/tv/TvProgramPromptBuilder.cs` | Dormant caller-wise |

Token policy constants: `SynopsisTokenPolicy` / `LiteratureSettingsDef` alias
`ArtPromptDefaults` Title/Synopsis/token clamp values.

Related (not `*PromptBuilder` types, still build prompts):

- `PersonaWeaponRequest`, `JournalFromSummaryRequest`, `BookFromSummaryRequest`, `MemorySummaryRequest`
- `LetterTextRewriter`, `IdeoDescriptionRewriter`, `QuestDescriptionRewriter`
- `AdvertisementQuestRequest`, `WarningQuestRequest`
- **Inline UA string prompts (no settings template):** `FamilyLetterRequest`, `AllyDiplomacyLetterRequest`
- Embedded resource: `promptoverride/templates/Prompt_MemorySummary.txt`
- `PromptTemplateUtil`, settings `prompt*` overrides, Talk book injector (read-only append)

---

## AI / provider transport

`IndependentBookLlmClient` modes:

1. **QueryViaRimTalk** — `AIClientFactory.GetAIClientAsync` → chat completion.
   Art does not set `art-literature` metadata; any arbitration happens inside
   Communication/shared client paths (caller identity may not be Art Background).
2. **SharedTextAiOrchestrator.Complete** — independent OpenAI/Custom path; sets
   `Arbitration = AiRequestMetadata.FromCaller("art-literature")` →
   **`AiRequestArbiter.Current.Admit`** (transitive) → **Background**.
3. **SharedHttpTransport** — Google / Player2 independent HTTP with **direct** `AiRequestArbiter.Admit` (Wave C).

Frozen facts (`ArtInteriorDefaults`):

| Fact | Value |
| --- | --- |
| `CallsAiRequestArbiterDirectly` | true (Google/Player2 HTTP only) |
| `IndependentOpenAiCustomPathAdmitsViaSharedTextAi` | true |
| `IndependentGooglePathBypassesArbiter` | false |
| `IndependentPlayer2PathBypassesArbiter` | false |
| `WaveCIndependentHttpDirectAdmitComplete` | true |
| `WaveBArbiterCoverageScope` | `Google_and_Player2_only` (tripwire: never whole-client outer Admit) |

**Wave B hazard:** wrapping the whole client in an outer `Admit` double-admits
OpenAI/Custom on the same thread → `nested_request` rejection / deadlock guard.

Timeout: 240000 ms. Retries: none dedicated beyond provider/client behavior.
No image-generation transport.

---

## Persistence / cache

`LiteratureSaveData` (`WorldComponent`) scribe labels:

`synopsisCache`, `artCache`, `ideoCache`, `tvProgramCache`,
`nextAllyDiplomacyTick`, `nextFamilyLetterTick`, `nextAdvertQuestTick`,
`nextWarningQuestTick`, `warningRaidQueue`.

No direct `System.IO.File.*` / `ILocalStorage` / `AtomicFileWriter` in Art Wave A measure.
Caches are save-game JSON via Verse Scribe — formats must be preserved.

**Not persisted:** `PendingBookQueue` / `PendingArtQueue` and rewriter pending dictionaries
(static in-memory only).

Frozen policy (B2):

| Fact | Value |
| --- | --- |
| Domain pending queues | `PendingArtQueue`, `PendingBookQueue` |
| Main-thread marshal queues | 5× `Queue<Action>` (letter/quest/ideo schedulers/rewriters) |
| `DomainPendingQueuesAreTransientByDesign` | **true** (not persisted; scan rebuilds) |
| `DomainPendingQueuesLostSilentlyOnSaveLoad` | **true** (process death; still silent) |
| `DomainPendingQueuesLifecycleOwnedByArtComposition` | **true** |
| `CompositionStopClearsDomainPendingQueues` | **true** |
| `CompositionStopClearsMainThreadMarshalQueues` | **false** |
| `MainThreadMarshalQueuesRequireCompositionStarted` | **true** (Wave D enqueue gate) |
| `MainThreadMarshalEnqueueRejectedWhenStopped` | **true** |
| `MainThreadMarshalDrainContinuesAfterStop` | **true** |
| `DomainPendingQueuesRequireCompositionStarted` | **true** (Wave C barrier) |
| `PostStopInFlightRequeueCannotRepopulateQueues` | **true** |
| `WaveCIndependentHttpDirectAdmitComplete` | **true** (Google/Player2 direct Admit) |
| `IndependentProvidersThatBypassArbiter` | empty |
| `DLoggingChecklistClearCountsConsumed` | **true** |
| `DLoggingChecklistAdmitRejectionLogged` | **true** |
| `DLoggingChecklistVerseMigratedToRimAiLog` | **true** |
| `CompositionStopLogsClearedDomainPendingQueueCounts` | **true** |
| `IndependentHttpAdmitRejectionIsLogged` | **true** |
| `RimAiLogMigrated` / Art Verse host-log baseline | **true** / **0** |

Static enqueue/dequeue API retained for scanners/processors; Clear + IsStarted gate are composition-owned.
`ArtDescriptionResultProcessor` owns art JSON normalize; `ArtLiteratureOrchestrator` is the thin root-owned art-description entry.

---

## Threading / host boundary

- LLM work is async (`Task` / `async`); SharedTextAi path uses `Task.Run` around orchestrator
- Display patches run on main thread reading caches
- No Unity texture / image binary pipeline
- Capture of thing/meta before async is uneven across pipelines (Wave B/C target)

---

## Known warts (characterization — do not “fix” silently in Wave A)

1. **Stop unwinds Talk/Scriban** (B1) — Harmony process-lifetime; Start after Stop re-subscribes
2. **Stop Clears domain pending queues** (B2) + **IsStarted gate** (C) — in-flight Requeue rejected
3. **Google/Player2 direct Admit** (C) — OpenAI/Custom still SharedTextAi only; whole-client outer Admit forbidden
4. **Thin orchestrator / result processor** (C) — `ArtLiteratureOrchestrator`, `ArtDescriptionResultProcessor`; full multi-pipeline orchestrator still deferred
5. **Logging (D-logging done)** — Verse host-log baseline Art → **0**; RimAiLog category `Art`; checklist: Clear() counts logged on Stop; independent HTTP Admit rejection logged; mass migration complete
6. **Marshal queues** (Wave D) — enqueue gated on `IsStarted`; Stop still does not Clear; drain of already-queued actions continues; processors remain static
7. **TvProgram generation dormant** — builder/service without callers
8. **Largest type** — `IndependentBookLlmClient` still mixes config/transport/parse/logging
9. **Quest advert/warning auto schedule disabled** — code retained; DebugAction only
10. **No sibling C# callers** — isolation is TalkLifecycle events only (good boundary; keep)
11. **RimTalk path** — still uses AIClientFactory; Art-owned Admit not applied there

### Recommended wave split (pre-B agreement)

| Wave | Scope |
| --- | --- |
| pre-B | arbiter fact rename; queue + Stop behavioral isolation characterization |
| B1 | composition ownership + Stop unwind only — **done** |
| B2 | domain pending queue lifecycle + Stop Clear — **done** |
| C | IsStarted barrier; Google/Player2 Admit; thin orchestrator/result — **done** |
| D-logging | RimAiLog migration + Clear counts + Admit reject logging — **done** |
| D | marshal enqueue IsStarted gate (no Clear); stage close — **done** |

---

## Target architecture (post Deep Reform — Waves B–D)

```text
ArtComposition
  → Art generation/application orchestrator(s)
  → subject/context resolver
  → prompt builder(s) (deterministic; Core defaults)
  → LLM client boundary (arbiter-aware where text-AI applies)
  → result parser/processor
  → persistence/cache ownership
  → thin Harmony / UI adapters
```

Do **not** redesign artistic styles, providers, or settings UI in this stage.

---

## Wave A / pre-B deliverables

- [x] Responsibility / trigger / pipeline map (this document)
- [x] Core `ArtInteriorDefaults` + `ArtPromptDefaults`
- [x] Production consume of prompt defaults (`ArtPromptBuilder`, `SynopsisTokenPolicy`, settings token constants)
- [x] `Stage7512ArtInteriorCharacterizationTests` goldens / architecture facts
- [x] Arbiter coverage nuance (`Google_and_Player2_only`; no whole-client outer Admit)
- [x] Domain pending queues characterized as transient-by-design + silent save/load loss
- [x] Composition Stop behavioral isolation frozen (pre-B) then **updated in B1** (Unregister + re-subscribe)
- [x] Wave B1: Talk/Scriban Unregister; no UnpatchAll; no queue/logging/orchestration
- [x] Wave B2: pin `CompositionStopClearsDomainPendingQueues`; `PendingArtQueue`/`PendingBookQueue`.Clear from `ArtComposition.Stop`
- [x] Wave C: IsStarted queue barrier; Google/Player2 direct Admit; `ArtLiteratureOrchestrator` + `ArtDescriptionResultProcessor`
- [x] D-logging: RimAiLog migration; Clear counts; Admit reject logging; Art host-log baseline → 0
- [x] Wave D: marshal `EnqueueAction` IsStarted gate (`CompositionStopClearsMainThreadMarshalQueues` stays false); stage close

Whimsical: **NOT EDITED**.
`ArtDeepReformStage7512Complete = true`. Roadmap CURRENT → M2.5.
