using Eshop.Models.ViewModels;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Eshop.Services
{
    public class BuildRequirementExtractor : IBuildRequirementExtractor
    {
        private readonly ILlmChatClient _llmChatClient;

        public BuildRequirementExtractor(ILlmChatClient llmChatClient)
        {
            _llmChatClient = llmChatClient;
        }

        public async Task<BuildRequirementProfile> ExtractAsync(string userMessage)
        {
            var systemPrompt = """
                    Ban la bo phan trich xuat nhu cau build PC.
                    Hay tra ve JSON hop le theo schema:

                    {
                      "PrimaryPurpose": "",
                      "GameTitle": "",
                      "ResolutionTarget": "",
                      "PerformancePriority": "",
                      "BudgetMin": null,
                      "BudgetMax": null,
                      "NeedsMonitorHighRefresh": false,
                      "NeedsStreaming": false,
                      "NeedsEditing": false,
                      "PreferredBrand": "",
                      "Notes": ""
                    }

                    Chi tra ve JSON, khong giai thich.
                    """;

            try
            {
                var raw = await _llmChatClient.AskAsync(systemPrompt, userMessage);
                var json = ExtractJson(raw);

                return JsonSerializer.Deserialize<BuildRequirementProfile>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new BuildRequirementProfile { Notes = userMessage };
            }
            catch
            {
                return new BuildRequirementProfile
                {
                    Notes = userMessage
                };
            }
        }

        private static string ExtractJson(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "{}";
            }

            var trimmed = raw.Trim();
            if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
            {
                return trimmed;
            }

            var fenced = Regex.Match(trimmed, "```(?:json)?\\s*(\\{[\\s\\S]*\\})\\s*```", RegexOptions.IgnoreCase);
            if (fenced.Success)
            {
                return fenced.Groups[1].Value;
            }

            var start = trimmed.IndexOf('{');
            var end = trimmed.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                return trimmed[start..(end + 1)];
            }

            return trimmed;
        }
    }
}
