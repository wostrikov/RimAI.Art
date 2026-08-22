using System;
using Ustas.RimAI.Art.Settings.Util;

namespace Ustas.RimAI.Art.Policy
{
    public enum ArtContentKind
    {
        Art,
        Book,
        Weapon,
        Building
    }

    public enum ArtStoreAction
    {
        Discard,
        PreserveManual,
        Write
    }

    public sealed class ArtGeneratedContent
    {
        public string Title;
        public string Body;
    }

    public sealed class ArtCacheSnapshot
    {
        public bool Found;
        public bool IsManualOverride;
        public string Title;
        public string Body;

        public static ArtCacheSnapshot Missing()
        {
            return new ArtCacheSnapshot();
        }
    }

    public sealed class ArtDescriptionOutcome
    {
        public ArtContentKind Kind;
        public string Key;
        public ArtGeneratedContent Content;
        public string DisplayText;
        public bool CacheHit;
        public bool Generated;
        public bool WroteCache;
        public bool PreservedManual;
        public ArtStoreAction Action;
    }

    /// <summary>
    /// Authoritative generate → cache → display decisions for art, books,
    /// quality weapons, and buildings. Prompt transport stays outside.
    /// </summary>
    public static class ArtDescriptionPipeline
    {
        public const int TitleMaxChars = 60;
        public const int BodyMaxChars = 600;

        public static string ApplyPlaceholders(string template, params (string key, string value)[] tokens)
        {
            return PromptTemplateUtil.ApplyTokens(template, tokens);
        }

        public static ArtGeneratedContent Normalize(string title, string body)
        {
            return new ArtGeneratedContent
            {
                Title = Clamp(title, TitleMaxChars),
                Body = Clamp(body, BodyMaxChars)
            };
        }

        public static bool HasDisplayableContent(ArtGeneratedContent content)
        {
            return content != null
                && (!string.IsNullOrWhiteSpace(content.Title) || !string.IsNullOrWhiteSpace(content.Body));
        }

        public static bool ShouldServeCached(ArtCacheSnapshot existing)
        {
            return existing != null && existing.Found;
        }

        public static string ComposeDisplay(string title, string body)
        {
            title = title?.Trim();
            body = body?.Trim();
            bool hasTitle = !string.IsNullOrWhiteSpace(title);
            bool hasBody = !string.IsNullOrWhiteSpace(body);
            if (!hasTitle && !hasBody)
                return string.Empty;
            if (hasTitle && hasBody)
                return title + "\n\n" + body;
            return hasTitle ? title : body;
        }

        public static ArtStoreAction DecideStore(ArtCacheSnapshot existing, ArtGeneratedContent generated)
        {
            if (!HasDisplayableContent(generated))
                return ArtStoreAction.Discard;
            if (existing != null && existing.IsManualOverride)
                return ArtStoreAction.PreserveManual;
            return ArtStoreAction.Write;
        }

        public static ArtDescriptionOutcome Resolve(
            ArtContentKind kind,
            string key,
            ArtCacheSnapshot existing,
            Func<ArtGeneratedContent> generate)
        {
            var outcome = new ArtDescriptionOutcome
            {
                Kind = kind,
                Key = key,
                Action = ArtStoreAction.Discard
            };
            if (string.IsNullOrWhiteSpace(key))
                return outcome;

            if (ShouldServeCached(existing))
            {
                outcome.CacheHit = true;
                outcome.Content = Normalize(existing.Title, existing.Body);
                outcome.DisplayText = ComposeDisplay(existing.Title, existing.Body);
                outcome.Action = ArtStoreAction.Discard;
                return outcome;
            }

            ArtGeneratedContent raw = generate == null ? null : generate();
            ArtGeneratedContent generated = Normalize(raw?.Title, raw?.Body);
            outcome.Generated = true;
            outcome.Action = DecideStore(existing, generated);
            if (outcome.Action == ArtStoreAction.PreserveManual)
            {
                outcome.PreservedManual = true;
                outcome.Content = Normalize(existing.Title, existing.Body);
                outcome.DisplayText = ComposeDisplay(existing.Title, existing.Body);
                return outcome;
            }

            if (outcome.Action == ArtStoreAction.Write)
            {
                outcome.WroteCache = true;
                outcome.Content = generated;
                outcome.DisplayText = ComposeDisplay(generated.Title, generated.Body);
                return outcome;
            }

            return outcome;
        }

        static string Clamp(string value, int max)
        {
            string text = value?.Trim() ?? string.Empty;
            if (max > 0 && text.Length > max)
                text = text.Substring(0, max).TrimEnd();
            return text;
        }
    }
}
