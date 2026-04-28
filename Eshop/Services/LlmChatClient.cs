using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Eshop.Services
{
    public class LlmChatClient : ILlmChatClient
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<LlmChatClient> _logger;

        public LlmChatClient(HttpClient httpClient, IConfiguration configuration, ILogger<LlmChatClient> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> AskAsync(string systemPrompt, string userPrompt)
        {
            var (apiKey, keySource) = ResolveApiKey();
            var model = ResolveValue("OpenAI:Model", "OPENAI_MODEL") ?? "gpt-4.1-mini";

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "Missing OpenAI API key. Set OpenAI:ApiKey or OPENAI_API_KEY in local configuration.");
            }

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            var payload = new
            {
                model,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.3
            };

            var response = await _httpClient.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", payload);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogError(
                    "OpenAI rejected API key from {KeySource} for model {Model}.",
                    keySource,
                    model);

                throw new InvalidOperationException(
                    $"OpenAI rejected the configured API key from {keySource}. Please verify or rotate that key.");
            }

            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";
        }

        private (string Value, string Source) ResolveApiKey()
        {
            var localConfigValue = (_configuration["OpenAI:ApiKey"] ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(localConfigValue))
            {
                return (localConfigValue, "OpenAI:ApiKey");
            }

            var envValue = (Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                return (envValue, "OPENAI_API_KEY");
            }

            var dotnetEnvValue = (_configuration["OpenAI__ApiKey"] ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(dotnetEnvValue))
            {
                return (dotnetEnvValue, "OpenAI__ApiKey");
            }

            return (string.Empty, "missing");
        }

        private string? ResolveValue(string configKey, string envKey)
        {
            var configValue = (_configuration[configKey] ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(configValue))
            {
                return configValue;
            }

            var envValue = (Environment.GetEnvironmentVariable(envKey) ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(envValue) ? null : envValue;
        }
    }
}
