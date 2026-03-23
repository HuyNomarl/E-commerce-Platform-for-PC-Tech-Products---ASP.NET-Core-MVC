using System.Net.Http.Json;

namespace Eshop.Services
{
    public class RagSearchItem
    {
        public string Content { get; set; } = "";
        public string Source { get; set; } = "";
        public int? Page { get; set; }
        public double Similarity_Score { get; set; }
    }

    public class RagSearchResponse
    {
        public string Query { get; set; } = "";
        public string Namespace { get; set; } = "";
        public List<RagSearchItem> Results { get; set; } = new();
    }

    public class RagClient
    {
        private readonly HttpClient _httpClient;

        public RagClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<RagSearchResponse?> SearchAsync(string query, string ns = "build_guide")
        {
            var payload = new
            {
                query,
                k = 3,
                @namespace = ns
            };

            var response = await _httpClient.PostAsJsonAsync("/search", payload);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<RagSearchResponse>();
        }
    }
}
