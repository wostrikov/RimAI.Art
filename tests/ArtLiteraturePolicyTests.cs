using System;
using System.IO;
using Ustas.RimAI.Art.Policy;

internal static class ArtLiteraturePolicyTests
{
    public static int Run()
    {
        int n = 0;
        void T(bool x, string s)
        {
            if (!x)
                throw new Exception("FAILED " + s);
            n++;
        }

        T(!ArtExperienceBookPolicy.CanAuthorFromExperience(false, "raid memories"), "exp-no-author");
        T(!ArtExperienceBookPolicy.CanAuthorFromExperience(true, "  "), "exp-empty-summary");
        T(ArtExperienceBookPolicy.CanAuthorFromExperience(true, "survived the raid"), "exp-author-summary");
        T(!ArtExperienceBookPolicy.ShouldDispatchExperienceAuthoring(true, false), "exp-no-request");
        T(ArtExperienceBookPolicy.ShouldDispatchExperienceAuthoring(true, true), "exp-dispatch");
        T(ArtExperienceBookPolicy.ComposeMemoryContext(null, "  ", null, null) == null, "exp-context-fail-closed");
        string ctx = ArtExperienceBookPolicy.ComposeMemoryContext(
            "pawn: Ada",
            "remembered the raid",
            new[] { "raid", "fire" },
            "grim");
        T(ctx.Contains(ArtExperienceBookPolicy.MemorySummaryMarker), "exp-marker");
        T(ctx.Contains("remembered the raid"), "exp-summary");
        T(ctx.Contains(ArtExperienceBookPolicy.KeywordsPrefix + "raid, fire"), "exp-keywords");
        T(ctx.Contains(ArtExperienceBookPolicy.TonePrefix + "grim"), "exp-tone");
        T(ctx.Contains("pawn: Ada"), "exp-base-context");

        T(ArtReadingDialoguePolicy.Decide(false, true, true, true) == ArtReadingInjectAction.SkipDisabled, "read-disabled");
        T(ArtReadingDialoguePolicy.Decide(true, false, false, false) == ArtReadingInjectAction.SkipNoBook, "read-no-book");
        T(ArtReadingDialoguePolicy.Decide(true, true, false, true) == ArtReadingInjectAction.SkipFiltered, "read-filtered");
        T(ArtReadingDialoguePolicy.Decide(true, true, true, false) == ArtReadingInjectAction.EnqueueMissing, "read-enqueue");
        T(ArtReadingDialoguePolicy.Decide(true, true, true, true) == ArtReadingInjectAction.Inject, "read-inject");
        T(ArtReadingDialoguePolicy.BuildSnippet("  ", "  ", 40) == null, "read-empty-snippet");
        string snippet = ArtReadingDialoguePolicy.BuildSnippet("Winter", new string('x', 80), 20);
        T(snippet.Contains(ArtReadingDialoguePolicy.BookMarker), "read-marker");
        T(snippet.Contains(ArtReadingDialoguePolicy.TitlePrefix + "Winter"), "read-title");
        T(snippet.Contains(ArtReadingDialoguePolicy.TextPrefix) && !snippet.Contains(new string('x', 80)), "read-clamp");
        T(ArtReadingDialoguePolicy.AppendToContext(null, snippet) == snippet, "read-append-empty");
        T(ArtReadingDialoguePolicy.AppendToContext("talk", snippet).StartsWith("talk\n\n" + ArtReadingDialoguePolicy.BookMarker), "read-append-existing");

        T(!ArtExperimentalGenerationPolicy.AllowIdeology(false), "ideo-off");
        T(ArtExperimentalGenerationPolicy.AllowIdeology(true), "ideo-on");
        T(!ArtExperimentalGenerationPolicy.AllowLetterRewrite(false), "letter-rewrite-off");
        T(!ArtExperimentalGenerationPolicy.AllowEasterLetters(false, true), "easter-master-off");
        T(!ArtExperimentalGenerationPolicy.AllowEasterLetters(true, false), "easter-toggle-off");
        T(ArtExperimentalGenerationPolicy.AllowEasterLetters(true, true), "easter-on");
        T(!ArtExperimentalGenerationPolicy.AllowListed("TradeOffer", null), "allow-null");
        T(!ArtExperimentalGenerationPolicy.AllowListed("TradeOffer", new string[0]), "allow-empty-fail-closed");
        T(!ArtExperimentalGenerationPolicy.AllowListed("Other", new[] { "TradeOffer" }), "allow-miss");
        T(ArtExperimentalGenerationPolicy.AllowListed("TradeOffer", new[] { "TradeOffer" }), "allow-hit");
        T(!ArtExperimentalGenerationPolicy.AllowQuestRewrite(false, "TradeRequest", new[] { "TradeRequest" }), "quest-rewrite-disabled");
        T(ArtExperimentalGenerationPolicy.AllowQuestRewrite(true, "TradeRequest", new[] { "TradeRequest" }), "quest-rewrite-on");
        T(!ArtExperimentalGenerationPolicy.AllowAutoExperimentalQuest(true), "quest-auto-off");
        T(ArtExperimentalGenerationPolicy.AllowManualExperimentalQuest(true), "quest-manual-on");
        T(!ArtExperimentalGenerationPolicy.AllowManualExperimentalQuest(false), "quest-manual-off");
        T(!ArtExperimentalGenerationPolicy.AutoScheduleExperimentalQuests, "quest-auto-constant");

        T(ArtScanEnqueuePolicy.DecideCadence(-1, 10, 60000) == ArtScanCadenceAction.ArmFirstTick, "scan-arm");
        T(ArtScanEnqueuePolicy.DecideCadence(0, 59999, 60000) == ArtScanCadenceAction.NotDue, "scan-not-due");
        T(ArtScanEnqueuePolicy.DecideCadence(0, 60000, 60000) == ArtScanCadenceAction.ScanDue, "scan-due");
        T(ArtScanEnqueuePolicy.ShouldScanLoadedMap(false), "scan-map-first");
        T(!ArtScanEnqueuePolicy.ShouldScanLoadedMap(true), "scan-map-once");
        T(ArtScanEnqueuePolicy.DecideEnqueue(false, true, true, false, false) == ArtScanEnqueueAction.SkipNotStarted, "enq-not-started");
        T(ArtScanEnqueuePolicy.DecideEnqueue(true, false, true, false, false) == ArtScanEnqueueAction.SkipIneligible, "enq-ineligible");
        T(ArtScanEnqueuePolicy.DecideEnqueue(true, true, false, false, false) == ArtScanEnqueueAction.SkipNoKey, "enq-no-key");
        T(ArtScanEnqueuePolicy.DecideEnqueue(true, true, true, true, false) == ArtScanEnqueueAction.SkipCached, "enq-cached");
        T(ArtScanEnqueuePolicy.DecideEnqueue(true, true, true, false, true) == ArtScanEnqueueAction.SkipDuplicate, "enq-dup");
        T(ArtScanEnqueuePolicy.DecideEnqueue(true, true, true, false, false) == ArtScanEnqueueAction.Enqueue, "enq-yes");

        string pipeline = Read("BookAuthoringPipeline.cs.src");
        string fromSummary = Read("BookFromSummaryRequest.cs.src");
        string bookService = Read("BookSynopsisService.cs.src");
        string processor = Read("BookSynopsisProcessor.cs.src");
        T(pipeline.Contains("ArtExperienceBookPolicy.CanAuthorFromExperience"), "host-authoring");
        T(fromSummary.Contains("ArtExperienceBookPolicy.ComposeMemoryContext"), "host-from-summary");
        T(bookService.Contains("ArtExperienceBookPolicy.CanAuthorFromExperience"), "host-book-service");
        T(processor.Contains("ArtExperienceBookPolicy.ShouldDispatchExperienceAuthoring"), "host-processor");

        string injector = Read("TalkPromptBookInjector.cs.src");
        string talkPatch = Read("Patch_PromptService_Override.cs.src");
        T(injector.Contains("ArtReadingDialoguePolicy.Decide"), "host-reading-decide");
        T(injector.Contains("ArtReadingDialoguePolicy.BuildSnippet"), "host-reading-snippet");
        T(talkPatch.Contains("TalkPromptBookInjector.InjectIfAvailable"), "host-talk-path");

        string ideo = Read("IdeoDescriptionRewriter.cs.src");
        string letters = Read("LetterEventScheduler.cs.src");
        string letterRewrite = Read("LetterTextRewriter.cs.src");
        string questRewrite = Read("QuestDescriptionRewriter.cs.src");
        string questSched = Read("QuestEventScheduler.cs.src");
        T(ideo.Contains("ArtExperimentalGenerationPolicy.AllowIdeology"), "host-ideo");
        T(letters.Contains("ArtExperimentalGenerationPolicy.AllowEasterLetters"), "host-easter");
        T(letterRewrite.Contains("ArtExperimentalGenerationPolicy.AllowLetterRewrite"), "host-letter-rewrite");
        T(letterRewrite.Contains("ArtExperimentalGenerationPolicy.AllowListed"), "host-letter-list");
        T(questRewrite.Contains("ArtExperimentalGenerationPolicy.AllowQuestRewrite"), "host-quest-rewrite");
        T(questSched.Contains("ArtExperimentalGenerationPolicy.AllowAutoExperimentalQuest"), "host-quest-auto");
        T(questSched.Contains("ArtExperimentalGenerationPolicy.AllowManualExperimentalQuest"), "host-quest-manual");

        string artSched = Read("ArtScanScheduler.cs.src");
        string bookSched = Read("BookScanScheduler.cs.src");
        string artScan = Read("MapArtScanner.cs.src");
        string bookScan = Read("MapBookScanner.cs.src");
        string artProd = Read("ArtProductionTracker.cs.src");
        string bookProd = Read("BookProductionTracker.cs.src");
        string daily = Read("Patch_Tick_DailyScan.cs.src");
        string loaded = Read("Patch_MapLoaded_Scan.cs.src");
        T(artSched.Contains("ArtScanEnqueuePolicy.DecideCadence"), "host-art-cadence");
        T(bookSched.Contains("ArtScanEnqueuePolicy.DecideCadence"), "host-book-cadence");
        T(artScan.Contains("ArtScanEnqueuePolicy.DecideEnqueue"), "host-art-enqueue");
        T(bookScan.Contains("ArtScanEnqueuePolicy.DecideEnqueue"), "host-book-enqueue");
        T(artProd.Contains("ArtScanEnqueuePolicy.DecideEnqueue"), "host-art-prod");
        T(bookProd.Contains("ArtScanEnqueuePolicy.DecideEnqueue"), "host-book-prod");
        T(daily.Contains("ArtScanScheduler.TryDailyScan") && daily.Contains("BookScanScheduler.TryDailyScan"), "host-daily");
        T(loaded.Contains("ArtScanScheduler.OnMapLoaded") && loaded.Contains("BookScanScheduler.OnMapLoaded"), "host-map-load");
        return n;
    }

    static string Read(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, name);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
