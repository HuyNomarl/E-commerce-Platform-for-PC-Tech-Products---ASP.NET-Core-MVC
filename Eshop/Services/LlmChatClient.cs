using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Eshop.Services
{
    public class LlmChatClient : ILlmChatClient
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public LlmChatClient(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<string> AskAsync(string systemPrompt, string userPrompt)
        {
            var apiKey = _configuration["OpenAI:ApiKey"];
            var model = _configuration["OpenAI:Model"] ?? "gpt-4o-mini";

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Missing OpenAI:ApiKey");

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
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";
        }
    }
}
