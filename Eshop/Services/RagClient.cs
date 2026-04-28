using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Eshop.Services
{
    public class RagSearchItem
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("page")]
        public int? Page { get; set; }

        [JsonPropertyName("doc_id")]
        public string? DocId { get; set; }

        [JsonPropertyName("similarity_score")]
        public double SimilarityScore { get; set; }
    }

    public class RagSearchResponse
    {
        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;

        [JsonPropertyName("namespace")]
        public string Namespace { get; set; } = string.Empty;

        [JsonPropertyName("results")]
        public List<RagSearchItem> Results { get; set; } = new();
    }

    public class RagDocumentUpsertRequest
    {
        [JsonPropertyName("namespace")]
        public string Namespace { get; set; } = string.Empty;

        [JsonPropertyName("doc_id")]
        public string DocId { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, object?> Metadata { get; set; } = new();
    }

    public class RagDocumentListItem
    {
        [JsonPropertyName("doc_id")]
        public string DocId { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("updated_at")]
        public string? UpdatedAt { get; set; }
    }

    public class RagDocumentListResponse
    {
        [JsonPropertyName("namespace")]
        public string Namespace { get; set; } = string.Empty;

        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("documents")]
        public List<RagDocumentListItem> Documents { get; set; } = new();
    }

    public class RagDocumentSyncItemRequest
    {
        [JsonPropertyName("doc_id")]
        public string DocId { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, object?> Metadata { get; set; } = new();
    }

    public class RagDocumentSyncRequest
    {
        [JsonPropertyName("namespace")]
        public string Namespace { get; set; } = string.Empty;

        [JsonPropertyName("documents")]
        public List<RagDocumentSyncItemRequest> Documents { get; set; } = new();

        [JsonPropertyName("prune_missing")]
        public bool PruneMissing { get; set; }

        [JsonPropertyName("prune_prefix")]
        public string? PrunePrefix { get; set; }
    }

    public class RagDocumentSyncResponse
    {
        [JsonPropertyName("namespace")]
        public string Namespace { get; set; } = string.Empty;

        [JsonPropertyName("input_count")]
        public int InputCount { get; set; }

        [JsonPropertyName("created_count")]
        public int CreatedCount { get; set; }

        [JsonPropertyName("updated_count")]
        public int UpdatedCount { get; set; }

        [JsonPropertyName("unchanged_count")]
        public int UnchangedCount { get; set; }

        [JsonPropertyName("deleted_count")]
        public int DeletedCount { get; set; }

        [JsonPropertyName("changed")]
        public bool Changed { get; set; }
    }

    public class RagClient
    {
        private readonly HttpClient _httpClient;

        public RagClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<RagSearchResponse?> SearchAsync(
            string query,
            string ns = "build_guide",
            int k = 4,
            double minScore = 0,
            CancellationToken cancellationToken = default)
        {
            var payload = new
            {
                query,
                k,
                @namespace = ns,
                min_score = minScore
            };

            var response = await _httpClient.PostAsJsonAsync("/search", payload, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<RagSearchResponse>(cancellationToken: cancellationToken);
        }

        public async Task UpsertDocumentAsync(RagDocumentUpsertRequest request, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/documents/upsert", request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        public async Task<RagDocumentSyncResponse?> SyncDocumentsAsync(
            RagDocumentSyncRequest request,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/documents/sync", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<RagDocumentSyncResponse>(cancellationToken: cancellationToken);
        }

        public async Task DeleteDocumentAsync(string ns, string docId, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.DeleteAsync(
                $"/documents/{Uri.EscapeDataString(ns)}?doc_id={Uri.EscapeDataString(docId)}",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return;
            }

            response.EnsureSuccessStatusCode();
        }

        public async Task<RagDocumentListResponse?> ListDocumentsAsync(string ns, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync($"/documents/{Uri.EscapeDataString(ns)}", cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<RagDocumentListResponse>(cancellationToken: cancellationToken);
        }
    }
}
