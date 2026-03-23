using Eshop.Models.ViewModels;

namespace Eshop.Services
{
    public class PcBuildSuggestionService : IPcBuildSuggestionService
    {
        public Task<List<string>> SuggestFixesAsync(PcBuildCheckRequest request, PcBuildCheckResponse checkResult)
        {
            var suggestions = new List<string>();

            foreach (var msg in checkResult.Messages)
            {
                var text = (msg.Message ?? "").ToLowerInvariant();

                if (text.Contains("socket"))
                    suggestions.Add("Bạn nên đổi CPU hoặc mainboard để hai bên dùng cùng socket.");

                if (text.Contains("ram") && text.Contains("không tương thích"))
                    suggestions.Add("Bạn nên chọn RAM đúng chuẩn mà mainboard hỗ trợ.");

                if (text.Contains("case"))
                    suggestions.Add("Bạn nên kiểm tra lại kích thước case, VGA và tản nhiệt.");

                if (text.Contains("nguồn") || text.Contains("psu"))
                    suggestions.Add("Bạn nên nâng công suất PSU cao hơn mức khuyến nghị.");
            }

            return Task.FromResult(suggestions.Distinct().ToList());
        }
    }
}
