# Art Deep Reform inventory — Phase 7.5.12

Measured against `RimAI.Art`. Production scope: `Source/**/*.cs` excluding `obj`/`bin`.

| Wave | Status |
| --- | --- |
| A | inventory + characterization + Core prompt contracts consumed |
| pre-B | arbiter coverage facts corrected; queue + Stop behavioral isolation frozen |
| B1 | composition Stop unwind (Talk/Scriban Unregister + `_registered` clear) — done |
| B2 | orchestration + queues (not started) |
| C | prompt / transport / result / persistence (not started) |
| D-logging | RimAiLog migration (deferred; not started) |
| D | host/UI/guards/stage close (not started) |

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
| Direct host logging (TEMPORARY) | **179** | `direct-host-logging-baseline.json` / phase757 |
| Catch-all baseline by_module | **9** | `catch-all-baseline.json` |
| Catch inventory by_module (raw) | 12 | `phase755-catch-inventory.json` (includes bare) |
| DOMAIN catch (phase755 category) | **4** | `phase755-catch-inventory.json` |
| Ambient `.Current` | **2** | `ArtComposition.Current`, `LiteratureSaveData.Current` |
| File I/O TEMPORARY | **0** | no Art keys in `direct-file-io-baseline.json` |
| Oversized WARN (phase754) | **2** | warn_by_module (historical inventory; current largest file ~731 LOC) |
| RimAiLog usages | **0** | source scan |
| Live `[HarmonyPatch]` attributes | **~24** | source scan |
| Production `.cs` files / LOC | **132** / **~13.7k** | Wave A measure |

Rule: `CURRENT_TEMPORARY <= COMMITTED_TEMPORARY_BASELINE` (never upward).

---

## Lifecycle / composition

| Item | Behavior |
| --- | --- |
| Entry | `LiteratureMod` → `RimAiHandshake.TryActivate(..., ArtComposition.Current.Start)` |
| Start | Idempotent `IsStarted` guard; module register; Harmony PatchAll (process lifetime); Talk/Scriban `Register()` |
| Stop | Unregisters Talk decorate + Scriban TV (clears `_registered`); **no** UnpatchAll; **no** domain queue clear |
| Start after Stop | Re-runs PatchAll (Harmony dedupes); Talk/Scriban **re-subscribe** (flags cleared on Stop) |
| Ambient | `ArtComposition.Current` (ALLOWED facade candidate); `LiteratureSaveData.Current` |
| Long-lived services | Mostly **static** helpers/queues/processors — root ownership of queues deferred to B2 |
| Settings | `LiteratureMod.Settings` static field (live reads in prompt builders) |

Wave B1 acceptance: Stop→Start restores TalkLifecycle subscriptions (`StartAfterStopReSubscribesTalkLifecycle = true`).
Domain pending queues remain transient / uncleared on Stop.

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
3. **SharedHttpTransport** — Google / Player2 independent HTTP (**bypasses Admit**).

Frozen facts (`ArtInteriorDefaults`):

| Fact | Value |
| --- | --- |
| `CallsAiRequestArbiterDirectly` | false |
| `IndependentOpenAiCustomPathAdmitsViaSharedTextAi` | true |
| `IndependentGooglePathBypassesArbiter` | true |
| `IndependentPlayer2PathBypassesArbiter` | true |
| `WaveBArbiterCoverageScope` | `Google_and_Player2_only` |

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

Frozen policy (pre-B characterization):

| Fact | Value |
| --- | --- |
| Domain pending queues | `PendingArtQueue`, `PendingBookQueue` |
| Main-thread marshal queues | 5× `Queue<Action>` (letter/quest/ideo schedulers/rewriters) |
| `DomainPendingQueuesAreTransientByDesign` | **true** (`PendingBookQueue` header: do not persist) |
| `DomainPendingQueuesLostSilentlyOnSaveLoad` | **true** |
| `CompositionStopClearsDomainPendingQueues` | **false** |

Wave B may move ownership under `ArtComposition` but must not change persist
semantics without an explicit separate decision.

---

## Threading / host boundary

- LLM work is async (`Task` / `async`); SharedTextAi path uses `Task.Run` around orchestrator
- Display patches run on main thread reading caches
- No Unity texture / image binary pipeline
- Capture of thing/meta before async is uneven across pipelines (Wave B/C target)

---

## Known warts (characterization — do not “fix” silently in Wave A)

1. **Stop unwinds Talk/Scriban** (B1) — Harmony process-lifetime; Start after Stop re-subscribes
2. **Multiple generation families**, one shared LLM client — orchestration not owned by composition (B2)
3. **Arbiter gaps = Google + Player2 only** — OpenAI/Custom already Admit via SharedTextAi; do not outer-wrap client
4. **Logging debt** — 179 Verse baseline / ~184 call sites; `RimAiLog` = 0; **defer migration to late wave near D**
5. **Static service graph** — queues/processors not root-owned yet; domain pending queues unsaved (transient by design)
6. **TvProgram generation dormant** — builder/service without callers
7. **Largest type** — `IndependentBookLlmClient` mixes config resolve, transport, parse, logging
8. **Quest advert/warning auto schedule disabled** — code retained; DebugAction only
9. **No sibling C# callers** — isolation is TalkLifecycle events only (good boundary; keep)

### Recommended wave split (pre-B agreement)

| Wave | Scope |
| --- | --- |
| pre-B (this) | arbiter fact rename; queue + Stop behavioral isolation characterization |
| B1 | composition ownership + Stop unwind only |
| B2 | orchestration + queue ownership (keep transient policy unless decided otherwise) |
| C | prompt / transport gaps (Google/Player2 Admit) / result / persistence |
| D-logging | RimAiLog migration (mass mechanical; separate reviewable diff) |
| D | remaining host/UI/guards/stage close |

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
- [ ] Waves B2–D (orchestration + queues; transport; logging deferred)

Whimsical: **NOT EDITED**.
