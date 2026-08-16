using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RimTalk;
using RimTalk.Client;
using RimTalk.Data;
using RimTalk.Util;
using RimTalk_LiteratureExpansion.llm;
using RimTalk_LiteratureExpansion.settings;
using UnityEngine.Networking;
using Ustas.RimAI.Core.AI;
using Verse;

namespace RimTalk_LiteratureExpansion.synopsis.llm
{
    public static class IndependentBookLlmClient
    {
        private const int TimeoutMs = 240000;
        private const int MinOutputTokens = 256;
        private const int MaxOutputTokensCap = 2048;
        private const int OutputTokenOverhead = 120;
        private const string Player2GameClientId = "019a8368-b00b-72bc-b367-2825079dc6fb";
        private const string Player2LocalBaseUrl = "http://localhost:4315";
        private const string Player2LocalHealthUrl = Player2LocalBaseUrl + "/v1/health";
        private const string Player2LocalLoginUrl = Player2LocalBaseUrl + "/v1/login/web/" + Player2GameClientId;
        private const int Player2LocalHealthTimeoutSeconds = 2;
        private const int Player2LocalLoginTimeoutSeconds = 3;
        private static string _player2LocalKey;
        private static DateTime _player2LocalKeyCheckedAt = DateTime.MinValue;
        private static readonly TimeSpan Player2LocalCheckInterval = TimeSpan.FromSeconds(30);
        private static int _requestId;
        private static readonly Regex OpenAIResponseRegex = new Regex(
            @"""content""\s*:\s*""((?:\\.|[^""])*)""",
            RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex GoogleResponseRegex = new Regex(
            @"""text""\s*:\s*""((?:\\.|[^""])*)""",
            RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex OpenAIFinishReasonRegex = new Regex(
            @"""finish_reason""\s*:\s*""([^""]*)""",
            RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex GoogleFinishReasonRegex = new Regex(
            @"""finishReason""\s*:\s*""([^""]*)""",
            RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex OpenAIUsageRegex = new Regex(
            @"""usage""\s*:\s*\{[^}]*?""prompt_tokens""\s*:\s*(\d+)[^}]*?""completion_tokens""\s*:\s*(\d+)[^}]*?""total_tokens""\s*:\s*(\d+)",
            RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex GoogleUsageRegex = new Regex(
            @"""usageMetadata""\s*:\s*\{[^}]*?""promptTokenCount""\s*:\s*(\d+)[^}]*?""candidatesTokenCount""\s*:\s*(\d+)[^}]*?""totalTokenCount""\s*:\s*(\d+)",
            RegexOptions.Compiled | RegexOptions.Singleline);

        public static async Task<T> QueryJsonAsync<T>(LiteratureLlmRequest request) where T : class, IJsonData
        {
            if (request == null) return null;

            int requestId = Interlocked.Increment(ref _requestId);

            if (LiteratureMod.Settings?.useRimTalkApi != false)
                return await QueryViaRimTalkAsync<T>(request, requestId);

            if (!TryGetActiveConfig(out var config))
            {
                Log.Warning($"[RimTalk LE] [Req {requestId}] No active RimTalk API config for independent literature request.");
                return null;
            }

            string model = ResolveModel(config);
            if (string.IsNullOrWhiteSpace(model))
            {
                Log.Warning($"[RimTalk LE] [Req {requestId}] Missing model for independent literature request.");
                return null;
            }

            string apiKey = config.ApiKey;
            bool usePlayer2Local = false;

            if (config.Provider == AIProvider.Player2 && string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = await TryResolvePlayer2LocalKeyAsync(requestId);
                usePlayer2Local = !string.IsNullOrWhiteSpace(apiKey);
                if (!usePlayer2Local)
                {
                    Log.Warning($"[RimTalk LE] [Req {requestId}] Player2 API key is empty and no local app detected.");
                    return null;
                }
            }

            string endpoint = ResolveEndpoint(config, model);
            if (config.Provider == AIProvider.Player2 && usePlayer2Local)
                endpoint = $"{Player2LocalBaseUrl}/v1/chat/completions";
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                Log.Warning($"[RimTalk LE] [Req {requestId}] Missing endpoint for independent literature request.");
                return null;
            }

            try
            {
                Log.Message($"[RimTalk LE] [Req {requestId}] Independent LLM request start.");
                Log.Message($"[RimTalk LE] [Req {requestId}] Provider: {config.Provider}, Model: {model}");
                Log.Message($"[RimTalk LE] [Req {requestId}] Endpoint: {SanitizeEndpoint(config.Provider, endpoint)}");
                Log.Message($"[RimTalk LE] [Req {requestId}] Credential source: independent provider setting");
                if (string.IsNullOrWhiteSpace(apiKey) && config.Provider != AIProvider.Google)
                    Log.Warning($"[RimTalk LE] [Req {requestId}] API key is empty.");

                int maxTokens = ResolveMaxOutputTokens();
                Log.Message($"[RimTalk LE] [Req {requestId}] Request max tokens: {maxTokens}");
                string json = BuildRequestJson(config.Provider, model, request.Instruction, request.Context, maxTokens);
                Log.Message($"[RimTalk LE] [Req {requestId}] Request payload length: {json?.Length ?? 0}");

                string responseText = await PostJsonAsync(requestId, endpoint, json, apiKey, config.Provider);
                if (string.IsNullOrWhiteSpace(responseText))
                {
                    Log.Warning($"[RimTalk LE] [Req {requestId}] Empty response from independent literature request.");
                    return null;
                }

                Log.Message($"[RimTalk LE] [Req {requestId}] Response length: {responseText.Length}");
                LogResponseMeta(config.Provider, responseText, requestId);
                string content = ExtractContent(config.Provider, responseText, requestId);
                if (string.IsNullOrWhiteSpace(content))
                {
                    Log.Warning($"[RimTalk LE] [Req {requestId}] Failed to parse response content from independent literature request.");
                    return null;
                }

                var jsonPayload = ExtractJsonPayload(content);
                if (string.IsNullOrWhiteSpace(jsonPayload))
                {
                    var trimmed = content.Trim();
                    int startIndex = trimmed.IndexOf('{');
                    int endIndex = trimmed.LastIndexOf('}');
                    bool hasFence = trimmed.StartsWith("```", StringComparison.Ordinal);
                    Log.Warning($"[RimTalk LE] [Req {requestId}] JSON detect: fence={hasFence}, start={startIndex}, end={endIndex}, len={trimmed.Length}");
                    Log.Warning($"[RimTalk LE] [Req {requestId}] Response content was not JSON; abort deserialize.");
                    Log.Warning($"[RimTalk LE] [Req {requestId}] Content preview: {TrimPreview(content)}");
                    return null;
                }

            Log.Message($"[RimTalk LE] [Req {requestId}] Parsed JSON payload length: {jsonPayload.Length}");
            var result = JsonUtil.DeserializeFromJson<T>(jsonPayload);
            Log.Message($"[RimTalk LE] [Req {requestId}] JSON deserialization {(result == null ? "failed" : "succeeded")}.");
            return result;
        }

            catch (Exception ex)
            {
                Log.Warning($"[RimTalk LE] [Req {requestId}] Independent literature request failed: {ex.GetType().Name} - {ex.Message}");
                return null;
            }
        }

        private static async Task<T> QueryViaRimTalkAsync<T>(LiteratureLlmRequest request, int requestId)
            where T : class, IJsonData
        {
            IAIClient client = await AIClientFactory.GetAIClientAsync();
            if (client == null)
            {
                Log.Warning($"[RimTalk LE] [Req {requestId}] Конфігурацію AI RimTalk не налаштовано.");
                return null;
            }

            Payload payload = await client.GetChatCompletionAsync(
                new System.Collections.Generic.List<(Role role, string message)>
                {
                    (Role.System, request.Instruction ?? string.Empty)
                },
                new System.Collections.Generic.List<(Role role, string message)>
                {
                    (Role.User, request.Context ?? string.Empty)
                });
            if (payload == null || !string.IsNullOrEmpty(payload.ErrorMessage)) return null;
            string json = ExtractJsonPayload(payload.Response);
            return string.IsNullOrWhiteSpace(json) ? null : JsonUtil.DeserializeFromJson<T>(json);
        }

        private static bool TryGetActiveConfig(out ApiConfig config)
        {
            config = null;
            var leSettings = LiteratureMod.Settings;
            if (leSettings != null && !leSettings.useRimTalkApi)
            {
                var api = leSettings.api;
                if (api == null)
                {
                    Log.Warning("[RimTalk LE] Independent API settings are missing.");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(api.baseUrl) ||
                    string.IsNullOrWhiteSpace(api.model))
                {
                    Log.Warning("[RimTalk LE] Independent API settings are incomplete (baseUrl/model).");
                    return false;
                }

                config = new ApiConfig
                {
                    IsEnabled = true,
                    Provider = AIProvider.Custom,
                    ApiKey = api.apiKey ?? string.Empty,
                    BaseUrl = api.baseUrl ?? string.Empty,
                    CustomModelName = api.model ?? string.Empty,
                    SelectedModel = "Custom"
                };

                if (!HasUsableEndpoint(config))
                {
                    Log.Warning("[RimTalk LE] Independent API endpoint is missing.");
                    config = null;
                    return false;
                }

                return true;
            }

            var settings = Settings.Get();
            if (settings == null) return false;

            if (settings.UseSimpleConfig)
            {
                if (!string.IsNullOrWhiteSpace(settings.SimpleApiKey))
                {
                    config = new ApiConfig
                    {
                        ApiKey = settings.SimpleApiKey,
                        Provider = AIProvider.Google,
                        SelectedModel = settings.IsUsingFallbackModel ? RimTalkConstantShim.FallbackCloudModel : RimTalkConstantShim.DefaultCloudModel,
                        IsEnabled = true
                    };
                }
                return config != null;
            }

            if (settings.UseCloudProviders)
            {
                if (settings.CloudConfigs == null || settings.CloudConfigs.Count == 0) return false;

                for (int i = 0; i < settings.CloudConfigs.Count; i++)
                {
                    int index = (settings.CurrentCloudConfigIndex + i) % settings.CloudConfigs.Count;
                    var candidate = settings.CloudConfigs[index];
                    if (candidate == null || !candidate.IsValid()) continue;
                    if (!HasUsableEndpoint(candidate))
                    {
                        Log.Warning($"[RimTalk LE] Skipping config without endpoint (provider={candidate.Provider}).");
                        continue;
                    }

                    settings.CurrentCloudConfigIndex = index;
                    config = candidate;
                    return true;
                }
                return false;
            }

            var localConfig = settings.LocalConfig;
            if (localConfig != null && localConfig.IsValid())
            {
                if (!HasUsableEndpoint(localConfig))
                {
                    Log.Warning("[RimTalk LE] Local config missing Base URL for independent requests.");
                    return false;
                }
                config = localConfig;
                return true;
            }

            return false;
        }

        private static bool HasUsableEndpoint(ApiConfig config)
        {
            if (config == null) return false;

            if (config.Provider == AIProvider.Custom || config.Provider == AIProvider.Local)
                return !string.IsNullOrWhiteSpace(config.BaseUrl);

            if (config.Provider == AIProvider.Player2)
                return true;

            var endpoint = GetProviderEndpointUrl(config.Provider);
            return !string.IsNullOrWhiteSpace(endpoint);
        }

        private static string ResolveModel(ApiConfig config)
        {
            if (config == null) return string.Empty;

            if ((config.Provider == AIProvider.Local || config.Provider == AIProvider.Custom) &&
                !string.IsNullOrWhiteSpace(config.CustomModelName))
                return config.CustomModelName;

            if (string.Equals(config.SelectedModel, "Custom", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(config.CustomModelName))
                return config.CustomModelName;

            if (!string.IsNullOrWhiteSpace(config.SelectedModel) &&
                !string.Equals(config.SelectedModel, RimTalkConstantShim.ChooseModel, StringComparison.OrdinalIgnoreCase))
                return config.SelectedModel;

            return RimTalkConstantShim.DefaultCloudModel;
        }

        private static string ResolveEndpoint(ApiConfig config, string model)
        {
            if (config == null) return string.Empty;

            switch (config.Provider)
            {
                case AIProvider.Google:
                    return $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={config.ApiKey}";
                case AIProvider.Player2:
                    return NormalizePlayer2Endpoint(GetProviderEndpointUrl(config.Provider));
                case AIProvider.Local:
                case AIProvider.Custom:
                    return NormalizeEndpoint(config.BaseUrl);
                default:
                    return GetProviderEndpointUrl(config.Provider);
            }
        }

        private static string GetProviderEndpointUrl(AIProvider provider)
        {
            var endpoint = TryGetProviderEndpointFromRegistry(provider);
            if (!string.IsNullOrWhiteSpace(endpoint))
                return endpoint;

            switch (provider.ToString())
            {
                case "OpenAI":
                    return "https://api.openai.com/v1/chat/completions";
                case "DeepSeek":
                    return "https://api.deepseek.com/v1/chat/completions";
                case "Grok":
                    return "https://api.x.ai/v1/chat/completions";
                case "GLM":
                    return "https://api.z.ai/api/paas/v4/chat/completions";
                case "OpenRouter":
                    return "https://openrouter.ai/api/v1/chat/completions";
                case "AlibabaIntl":
                    return "https://dashscope-intl.aliyuncs.com/compatible-mode/v1/chat/completions";
                case "AlibabaCN":
                    return "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions";
                case "Player2":
                    return "https://api.player2.game";
                default:
                    return string.Empty;
            }
        }

        private static string TryGetProviderEndpointFromRegistry(AIProvider provider)
        {
            try
            {
                var registryType = typeof(AIProvider).Assembly.GetType("RimTalk.AIProviderRegistry");
                if (registryType == null) return null;
                var method = registryType.GetMethod("GetEndpointUrl",
                    BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(AIProvider) }, null);
                if (method == null) return null;
                return method.Invoke(null, new object[] { provider }) as string;
            }
            catch
            {
                return null;
            }
        }

        private static string NormalizePlayer2Endpoint(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) return string.Empty;
            var trimmed = baseUrl.Trim().TrimEnd('/');
            if (trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
                return trimmed;
            if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                return trimmed + "/chat/completions";
            return trimmed + "/v1/chat/completions";
        }

        private static string NormalizeEndpoint(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) return string.Empty;

            var trimmed = baseUrl.Trim().TrimEnd('/');
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
                return trimmed;

            if (string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/")
                return trimmed + "/v1/chat/completions";

            return trimmed;
        }

        private static string BuildRequestJson(AIProvider provider, string model, string instruction, string context, int maxTokens)
        {
            if (provider == AIProvider.Google)
            {
                bool isGemma = !string.IsNullOrWhiteSpace(model) &&
                               model.IndexOf("gemma", StringComparison.OrdinalIgnoreCase) >= 0;
                var userContent = context ?? string.Empty;
                GeminiContent systemInstruction = null;
                if (isGemma)
                {
                    userContent = string.IsNullOrWhiteSpace(context)
                        ? instruction ?? string.Empty
                        : $"{instruction}\n\n[Context]\n{context}";
                }
                else
                {
                    systemInstruction = new GeminiContent
                    {
                        Parts = new[] { new GeminiPart { Text = instruction ?? string.Empty } }
                    };
                }

                var request = new GeminiRequest
                {
                    SystemInstruction = systemInstruction,
                    Contents = new[]
                    {
                        new GeminiContent
                        {
                            Role = "user",
                            Parts = new[] { new GeminiPart { Text = userContent } }
                        }
                    },
                    GenerationConfig = new GeminiGenerationConfig
                    {
                        Temperature = 0.7f,
                        MaxOutputTokens = maxTokens
                    }
                };

                return JsonUtil.SerializeToJson(request);
            }

            var messages = new[]
            {
                new OpenAIMessage { Role = "system", Content = instruction ?? string.Empty },
                new OpenAIMessage { Role = "user", Content = context ?? string.Empty }
            };

            var openAiRequest = new OpenAIRequest
            {
                Model = model,
                Messages = messages,
                Temperature = 0.7f,
                MaxTokens = maxTokens
            };

            if (provider == AIProvider.Custom)
            {
                openAiRequest.MaxOutputTokens = maxTokens;
                openAiRequest.MaxCompletionTokens = maxTokens;
            }

            return JsonUtil.SerializeToJson(openAiRequest);
        }

        private static async Task<string> PostJsonAsync(int requestId, string url, string json, string apiKey, AIProvider provider)
        {
            byte[] bodyRaw;
            try
            {
                bodyRaw = Encoding.UTF8.GetBytes(json ?? string.Empty);
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk LE] [Req {requestId}] Request payload encode failed: {ex.GetType().Name} - {ex.Message}");
                throw;
            }

            if (provider != AIProvider.Google && provider != AIProvider.Player2)
            {
                var shared = await Task.Run(() => SharedTextAiOrchestrator.Complete(new TextAiRequest
                {
                    PrebuiltJson = json,
                    BaseUrl = url,
                    ApiKey = apiKey,
                    UseSharedGameplayCredential = provider == AIProvider.OpenAI,
                    ApiShape = provider == AIProvider.OpenAI ? TextAiApiShape.Responses : TextAiApiShape.ChatCompletions,
                    TimeoutMs = TimeoutMs,
                    Caller = "art-literature"
                }));
                if (!shared.Succeeded)
                {
                    Log.Warning($"[RimTalk LE] [Req {requestId}] Shared text-AI failed: {shared.ErrorKind}");
                    return null;
                }

                Log.Message($"[RimTalk LE] [Req {requestId}] Shared transport={shared.TransportKind} status={shared.StatusCode}");
                return shared.RawPayload;
            }

            Log.Message($"[RimTalk LE] [Req {requestId}] HTTP request via UnityWebRequest: provider={provider}, url={SanitizeEndpoint(provider, url)}, bodyBytes={bodyRaw.Length}");

            using var webRequest = new UnityWebRequest(url, "POST");
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            if (provider != AIProvider.Google && !string.IsNullOrWhiteSpace(apiKey))
                webRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            if (provider == AIProvider.Player2)
                webRequest.SetRequestHeader("X-Game-Client-Id", Player2GameClientId);
            webRequest.timeout = Math.Max(5, TimeoutMs / 1000);

            var sw = Stopwatch.StartNew();
            await SendUnityWebRequestAsync(webRequest);

            string responseText = webRequest.downloadHandler?.text;
            if (webRequest.responseCode >= 400 || webRequest.isNetworkError || webRequest.isHttpError)
            {
                string detail = BuildSafePreview(responseText, 300);
                Log.Warning($"[RimTalk LE] [Req {requestId}] HTTP {(int)webRequest.responseCode}: {webRequest.error ?? "(no error)"} body={detail}");
                return null;
            }

            Log.Message($"[RimTalk LE] [Req {requestId}] Response status: {webRequest.responseCode} in {sw.ElapsedMilliseconds} ms.");
            return responseText;
        }

        private static string ExtractContent(AIProvider provider, string responseText, int requestId)
        {
            var regex = provider == AIProvider.Google ? GoogleResponseRegex : OpenAIResponseRegex;
            var matches = regex.Matches(responseText);
            if (matches.Count == 0)
            {
                Log.Warning($"[RimTalk LE] [Req {requestId}] No content fragments matched in response.");
                return null;
            }

            var sb = new StringBuilder();
            foreach (Match match in matches)
                sb.Append(match.Groups[1].Value);

            Log.Message($"[RimTalk LE] [Req {requestId}] Content fragments: {matches.Count}.");
            return Regex.Unescape(sb.ToString());
        }

        private static string ExtractJsonPayload(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return null;

            var trimmed = content.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                int firstLine = trimmed.IndexOf('\n');
                int lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
                if (firstLine >= 0 && lastFence > firstLine)
                    trimmed = trimmed.Substring(firstLine + 1, lastFence - firstLine - 1).Trim();
            }

            int start = trimmed.IndexOf('{');
            int end = trimmed.LastIndexOf('}');
            if (start >= 0 && end > start)
                return trimmed.Substring(start, end - start + 1).Trim();

            if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
                return trimmed;

            return null;
        }

        private static string TrimPreview(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "(empty)";
            const int maxLen = 160;
            var trimmed = text.Trim();
            return trimmed.Length <= maxLen ? trimmed : trimmed.Substring(0, maxLen) + "...";
        }

        private static string BuildSafePreview(string value, int maxChars)
        {
            if (string.IsNullOrEmpty(value)) return "(empty)";

            var sb = new StringBuilder();
            int limit = Math.Max(0, maxChars);
            for (int i = 0; i < value.Length && sb.Length < limit; i++)
            {
                char ch = value[i];

                if (char.IsControl(ch))
                {
                    if (ch == '\r' || ch == '\n' || ch == '\t')
                        sb.Append(' ');
                    else
                        sb.Append($"\\u{(int)ch:X4}");
                    continue;
                }

                if (char.IsHighSurrogate(ch))
                {
                    if (i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                    {
                        sb.Append(ch);
                        sb.Append(value[i + 1]);
                        i++;
                    }
                    else
                    {
                        sb.Append($"\\u{(int)ch:X4}");
                    }
                    continue;
                }

                if (char.IsLowSurrogate(ch))
                {
                    sb.Append($"\\u{(int)ch:X4}");
                    continue;
                }

                sb.Append(ch);
            }

            if (value.Length > maxChars)
                sb.Append("...");

            return sb.ToString();
        }

        private static string SanitizeEndpoint(AIProvider provider, string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint)) return "(empty)";
            if (provider != AIProvider.Google) return endpoint;
            int keyIndex = endpoint.IndexOf("key=", StringComparison.OrdinalIgnoreCase);
            if (keyIndex < 0) return endpoint;
            return endpoint.Substring(0, keyIndex + 4) + "***";
        }

        private static async Task<string> TryResolvePlayer2LocalKeyAsync(int requestId)
        {
            if (!string.IsNullOrWhiteSpace(_player2LocalKey))
                return _player2LocalKey;

            var now = DateTime.UtcNow;
            if (now - _player2LocalKeyCheckedAt < Player2LocalCheckInterval)
                return null;

            _player2LocalKeyCheckedAt = now;

            if (!await IsPlayer2LocalHealthyAsync(requestId))
                return null;

            var localKey = await RequestPlayer2LocalKeyAsync(requestId);
            if (!string.IsNullOrWhiteSpace(localKey))
            {
                _player2LocalKey = localKey;
                Log.Message($"[RimTalk LE] [Req {requestId}] Player2 local app authenticated.");
            }

            return _player2LocalKey;
        }

        private static async Task<bool> IsPlayer2LocalHealthyAsync(int requestId)
        {
            try
            {
                using var healthRequest = UnityWebRequest.Get(Player2LocalHealthUrl);
                healthRequest.timeout = Player2LocalHealthTimeoutSeconds;
                await SendUnityWebRequestAsync(healthRequest);
                if (healthRequest.isNetworkError || healthRequest.isHttpError)
                {
                    Log.Message($"[RimTalk LE] [Req {requestId}] Player2 local health check failed: {healthRequest.responseCode} - {healthRequest.error}");
                    return false;
                }
                return healthRequest.responseCode == (long)HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                Log.Message($"[RimTalk LE] [Req {requestId}] Player2 local health check exception: {ex.Message}");
                return false;
            }
        }

        private static async Task<string> RequestPlayer2LocalKeyAsync(int requestId)
        {
            try
            {
                using var loginRequest = new UnityWebRequest(Player2LocalLoginUrl, "POST");
                loginRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
                loginRequest.downloadHandler = new DownloadHandlerBuffer();
                loginRequest.SetRequestHeader("Content-Type", "application/json");
                loginRequest.timeout = Player2LocalLoginTimeoutSeconds;

                await SendUnityWebRequestAsync(loginRequest);
                if (loginRequest.isNetworkError || loginRequest.isHttpError)
                {
                    Log.Message($"[RimTalk LE] [Req {requestId}] Player2 local login failed: {loginRequest.responseCode} - {loginRequest.error}");
                    return null;
                }

                var auth = JsonUtil.DeserializeFromJson<Player2LocalAuthResponse>(loginRequest.downloadHandler.text);
                if (!string.IsNullOrWhiteSpace(auth?.ApiKey))
                    return auth.ApiKey;

                Log.Warning($"[RimTalk LE] [Req {requestId}] Player2 local app responded but no API key in response.");
                return null;
            }
            catch (Exception ex)
            {
                Log.Message($"[RimTalk LE] [Req {requestId}] Player2 local login exception: {ex.Message}");
                return null;
            }
        }

        private static Task SendUnityWebRequestAsync(UnityWebRequest request)
        {
            var tcs = new TaskCompletionSource<bool>();
            request.SendWebRequest().completed += _ => tcs.SetResult(true);
            return tcs.Task;
        }

        [DataContract]
        private sealed class OpenAIRequest
        {
            [DataMember(Name = "model")] public string Model;
            [DataMember(Name = "messages")] public OpenAIMessage[] Messages;
            [DataMember(Name = "temperature", EmitDefaultValue = false)] public float Temperature;
            [DataMember(Name = "max_tokens", EmitDefaultValue = false)] public int? MaxTokens;
            [DataMember(Name = "max_output_tokens", EmitDefaultValue = false)] public int? MaxOutputTokens;
            [DataMember(Name = "max_completion_tokens", EmitDefaultValue = false)] public int? MaxCompletionTokens;
        }

        [DataContract]
        private sealed class OpenAIMessage
        {
            [DataMember(Name = "role")] public string Role;
            [DataMember(Name = "content")] public string Content;
        }

        [DataContract]
        private sealed class GeminiRequest
        {
            [DataMember(Name = "system_instruction", EmitDefaultValue = false)]
            public GeminiContent SystemInstruction;
            [DataMember(Name = "contents")] public GeminiContent[] Contents;
            [DataMember(Name = "generationConfig")] public GeminiGenerationConfig GenerationConfig;
        }

        [DataContract]
        private sealed class GeminiContent
        {
            [DataMember(Name = "role", EmitDefaultValue = false)] public string Role;
            [DataMember(Name = "parts")] public GeminiPart[] Parts;
        }

        [DataContract]
        private sealed class GeminiPart
        {
            [DataMember(Name = "text")] public string Text;
        }

        [DataContract]
        private sealed class GeminiGenerationConfig
        {
            [DataMember(Name = "temperature")] public float Temperature;
            [DataMember(Name = "maxOutputTokens")] public int MaxOutputTokens;
        }

        [DataContract]
        private sealed class Player2LocalAuthResponse
        {
            [DataMember(Name = "p2Key")] public string ApiKey = string.Empty;
        }


        private static int ResolveMaxOutputTokens()
        {
            var settings = LiteratureMod.Settings;
            int target = settings?.synopsisTokenTarget ?? LiteratureSettingsDef.DefaultSynopsisTokenTarget;
            int maxTokens = target + LiteratureSettingsDef.StoryTokenBonus + OutputTokenOverhead;
            if (maxTokens < MinOutputTokens) maxTokens = MinOutputTokens;
            if (maxTokens > MaxOutputTokensCap) maxTokens = MaxOutputTokensCap;
            return maxTokens;
        }

        private static void LogResponseMeta(AIProvider provider, string responseText, int requestId)
        {
            if (string.IsNullOrWhiteSpace(responseText)) return;

            string finishReason = null;
            if (provider == AIProvider.Google)
            {
                var match = GoogleFinishReasonRegex.Match(responseText);
                if (match.Success) finishReason = match.Groups[1].Value;
            }
            else
            {
                var match = OpenAIFinishReasonRegex.Match(responseText);
                if (match.Success) finishReason = match.Groups[1].Value;
            }

            if (!string.IsNullOrWhiteSpace(finishReason))
                Log.Message($"[RimTalk LE] [Req {requestId}] finish_reason: {finishReason}");

            if (provider == AIProvider.Google)
            {
                if (TryParseUsage(GoogleUsageRegex, responseText, out var prompt, out var completion, out var total))
                    Log.Message($"[RimTalk LE] [Req {requestId}] token usage: prompt={prompt}, completion={completion}, total={total}");
            }
            else
            {
                if (TryParseUsage(OpenAIUsageRegex, responseText, out var prompt, out var completion, out var total))
                    Log.Message($"[RimTalk LE] [Req {requestId}] token usage: prompt={prompt}, completion={completion}, total={total}");
            }
        }

        private static bool TryParseUsage(Regex regex, string text, out int prompt, out int completion, out int total)
        {
            prompt = 0;
            completion = 0;
            total = 0;

            if (regex == null || string.IsNullOrWhiteSpace(text)) return false;

            var match = regex.Match(text);
            if (!match.Success || match.Groups.Count < 4) return false;

            return int.TryParse(match.Groups[1].Value, out prompt) &&
                   int.TryParse(match.Groups[2].Value, out completion) &&
                   int.TryParse(match.Groups[3].Value, out total);
        }
    }
}
