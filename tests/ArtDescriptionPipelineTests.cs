using System;
using System.Collections.Generic;
using System.IO;
using Ustas.RimAI.Art.Policy;

internal static class ArtDescriptionPipelineTests
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

        T(ArtDescriptionPipeline.ApplyPlaceholders("A {{THING}} of {{QUALITY}}", ("THING", "statue"), ("QUALITY", "Excellent"))
            == "A statue of Excellent", "placeholder-replace");
        T(ArtDescriptionPipeline.ApplyPlaceholders("plain") == "plain", "placeholder-none");

        string longTitle = new string('T', 80);
        string longBody = new string('B', 700);
        var clamped = ArtDescriptionPipeline.Normalize(longTitle, longBody);
        T(clamped.Title.Length == ArtDescriptionPipeline.TitleMaxChars, "title-clamp");
        T(clamped.Body.Length == ArtDescriptionPipeline.BodyMaxChars, "body-clamp");

        T(ArtDescriptionPipeline.ComposeDisplay("Title", "Body") == "Title\n\nBody", "display-both");
        T(ArtDescriptionPipeline.ComposeDisplay("Title", "  ") == "Title", "display-title-only");
        T(string.IsNullOrEmpty(ArtDescriptionPipeline.ComposeDisplay(" ", "")), "display-empty");

        var kinds = new[]
        {
            ArtContentKind.Art,
            ArtContentKind.Book,
            ArtContentKind.Weapon,
            ArtContentKind.Building
        };

        foreach (ArtContentKind kind in kinds)
        {
            int generates = 0;
            var first = ArtDescriptionPipeline.Resolve(kind, kind + ":1", ArtCacheSnapshot.Missing(), () =>
            {
                generates++;
                return new ArtGeneratedContent { Title = kind + " title", Body = "generated " + kind };
            });
            T(first.WroteCache && first.Generated && generates == 1, kind + "-generate-write");
            T(first.DisplayText.Contains("generated " + kind), kind + "-display");

            var cached = new ArtCacheSnapshot
            {
                Found = true,
                Title = first.Content.Title,
                Body = first.Content.Body
            };
            var second = ArtDescriptionPipeline.Resolve(kind, kind + ":1", cached, () =>
            {
                generates++;
                return new ArtGeneratedContent { Title = "should-not", Body = "run" };
            });
            T(second.CacheHit && !second.WroteCache && generates == 1, kind + "-cache-hit");
            T(second.DisplayText == first.DisplayText, kind + "-cache-display");
        }

        var miss = ArtDescriptionPipeline.Resolve(ArtContentKind.Art, "art:null", ArtCacheSnapshot.Missing(), () => null);
        T(!miss.WroteCache && miss.Action == ArtStoreAction.Discard, "null-generate-discard");

        var empty = ArtDescriptionPipeline.Resolve(ArtContentKind.Book, "book:empty", ArtCacheSnapshot.Missing(), () =>
            new ArtGeneratedContent { Title = "  ", Body = "" });
        T(!empty.WroteCache && empty.Action == ArtStoreAction.Discard, "empty-generate-discard");

        var manual = ArtDescriptionPipeline.Resolve(
            ArtContentKind.Weapon,
            "weapon:manual",
            new ArtCacheSnapshot { Found = false, IsManualOverride = true, Title = "Keep", Body = "Manual" },
            () => new ArtGeneratedContent { Title = "New", Body = "Generated" });
        T(manual.PreservedManual && !manual.WroteCache && manual.DisplayText.Contains("Manual"), "manual-preserved");

        var invalid = ArtDescriptionPipeline.Resolve(ArtContentKind.Building, "  ", ArtCacheSnapshot.Missing(), () =>
            new ArtGeneratedContent { Title = "X", Body = "Y" });
        T(!invalid.WroteCache && !invalid.Generated, "invalid-key");

        var store = new Dictionary<string, ArtCacheSnapshot>(StringComparer.Ordinal);
        ArtCacheSnapshot existing;
        store.TryGetValue("art:flow", out existing);
        var generated = ArtDescriptionPipeline.Resolve(ArtContentKind.Art, "art:flow", existing ?? ArtCacheSnapshot.Missing(), () =>
            new ArtGeneratedContent { Title = "Flow", Body = "Cached body" });
        T(generated.WroteCache, "flow-write");
        store["art:flow"] = new ArtCacheSnapshot
        {
            Found = true,
            Title = generated.Content.Title,
            Body = generated.Content.Body
        };
        var displayed = ArtDescriptionPipeline.Resolve(ArtContentKind.Art, "art:flow", store["art:flow"], () =>
            new ArtGeneratedContent { Title = "later", Body = "ignored" });
        T(displayed.CacheHit && displayed.DisplayText == "Flow\n\nCached body", "flow-generate-cache-display");

        string processor = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ArtDescriptionProcessor.cs.src"));
        string books = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "BookSynopsisProcessor.cs.src"));
        string service = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ArtDescriptionService.cs.src"));
        string display = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ArtCacheUtil.cs.src"));
        string result = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ArtDescriptionResultProcessor.cs.src"));
        string bookService = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "BookSynopsisService.cs.src"));
        string prompt = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ArtPromptBuilder.cs.src"));
        T(processor.Contains("ArtDescriptionPipeline.DecideStore"), "art-processor-store");
        T(books.Contains("ArtDescriptionPipeline.DecideStore"), "book-processor-store");
        T(service.Contains("ArtDescriptionResultProcessor.Normalize"), "art-service-normalize");
        T(display.Contains("ArtDescriptionPipeline.ComposeDisplay"), "display-compose");
        T(result.Contains("ArtDescriptionPipeline.Normalize"), "result-normalize");
        T(bookService.Contains("ArtDescriptionPipeline.Normalize"), "book-service-normalize");
        T(prompt.Contains("PromptTemplateUtil.Resolve") || prompt.Contains("PromptTemplateUtil.ApplyTokens"), "prompt-placeholders");
        return n;
    }
}
