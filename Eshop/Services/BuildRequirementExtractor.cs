using System.Text.Json;
using Eshop.Models.ViewModels;

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

                return JsonSerializer.Deserialize<BuildRequirementProfile>(raw, new JsonSerializerOptions
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
    }
}
