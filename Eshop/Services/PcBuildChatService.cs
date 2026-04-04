using Eshop.Models.ViewModels;
using Serilog;
using System.Text;

namespace Eshop.Services
{
    public class PcBuildChatService : IPcBuildChatService
    {
        private static readonly string[] RagNamespaces = new[]
        {
            "build_guide",
            "game_profile",
            "product_profiles"
        };

        private readonly IPcCompatibilityService _compatibilityService;
        private readonly IBuildRequirementExtractor _requirementExtractor;
        private readonly IPcBuildRecommendationService _recommendationService;
        private readonly IPcBuildSuggestionService _suggestionService;
        private readonly RagClient _ragClient;
        private readonly ILlmChatClient _llmChatClient;

        public PcBuildChatService(
            IPcCompatibilityService compatibilityService,
            IBuildRequirementExtractor requirementExtractor,
            IPcBuildRecommendationService recommendationService,
            IPcBuildSuggestionService suggestionService,
            RagClient ragClient,
            ILlmChatClient llmChatClient)
        {
            _compatibilityService = compatibilityService;
            _requirementExtractor = requirementExtractor;
            _recommendationService = recommendationService;
            _suggestionService = suggestionService;
            _ragClient = ragClient;
            _llmChatClient = llmChatClient;
        }

        public async Task<PcBuildChatResponse> AskAsync(PcBuildChatRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var safeMessage = (request.Message ?? "").Trim();
            var hasCurrentBuild = request.Items != null && request.Items.Any();

            var requirement = await _requirementExtractor.ExtractAsync(safeMessage)
                             ?? new BuildRequirementProfile();

            PcBuildCheckRequest buildRequest;
            if (hasCurrentBuild)
            {
                buildRequest = new PcBuildCheckRequest
                {
                    Items = request.Items ?? new List<PcBuildCheckItemDto>()
                };
            }
            else
            {
                buildRequest = await _recommendationService.RecommendBuildAsync(requirement)
                              ?? new PcBuildCheckRequest
                              {
                                  Items = new List<PcBuildCheckItemDto>()
                              };
            }

            buildRequest.Items ??= new List<PcBuildCheckItemDto>();

            var checkResult = await _compatibilityService.CheckAsync(buildRequest)
                             ?? new PcBuildCheckResponse();

            checkResult.Messages ??= new List<CompatibilityMessageDto>();

            var suggestions = await _suggestionService.SuggestFixesAsync(buildRequest, checkResult)
                              ?? new List<string>();

            var ragTexts = await CollectRagContextAsync(safeMessage, requirement, checkResult);

            var systemPrompt = BuildSystemPrompt();
            var userPrompt = BuildUserPrompt(
                            safeMessage,
                            hasCurrentBuild,
                            requirement,
                            buildRequest,
                            checkResult,
                            suggestions,
                            ragTexts);


            string reply;
            try
            {
                reply = await _llmChatClient.AskAsync(systemPrompt, userPrompt);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "LLM failed, using fallback reply.");
                reply = BuildFallbackReply(checkResult, suggestions, ragTexts);
            }

            return new PcBuildChatResponse
            {
                Reply = reply,
                IsValid = checkResult.IsValid,
                TotalPrice = checkResult.TotalPrice,
                EstimatedPower = checkResult.EstimatedPower,
                Messages = checkResult.Messages ?? new List<CompatibilityMessageDto>()
            };
        }

        private async Task<List<string>> CollectRagContextAsync(
            string userMessage,
            BuildRequirementProfile requirement,
            PcBuildCheckResponse checkResult)
        {
            var ragTexts = new List<string>();

            foreach (var ns in RagNamespaces)
            {
                try
                {
                    var query = BuildRagQuery(ns, userMessage, requirement, checkResult);
                    var rag = await _ragClient.SearchAsync(query, ns);

                    Log.Information("RAG namespace: {Namespace}", ns);
                    Log.Information("RAG query: {Query}", query);
                    Log.Information("RAG result count: {Count}", rag?.Results?.Count ?? 0);

                    foreach (var item in rag?.Results?.Take(3) ?? Enumerable.Empty<RagSearchItem>())
                    {
                        Log.Information("RAG source [{Namespace}]: {Source}", ns, item.Source);
                        Log.Information("RAG content [{Namespace}]: {Content}", ns, item.Content);
                    }

                    if (rag?.Results != null && rag.Results.Any())
                    {
                        ragTexts.AddRange(
                            rag.Results
                                .Select(x => x.Content)
                                .Where(x => !string.IsNullOrWhiteSpace(x))
                                .Take(2));
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "RAG query failed for namespace {Namespace}", ns);
                }
            }

            return ragTexts
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .Take(6)
                .ToList();
        }

        private static string BuildRagQuery(
            string ns,
            string userMessage,
            BuildRequirementProfile requirement,
            PcBuildCheckResponse checkResult)
        {
            if (ns == "build_guide")
            {
                if (checkResult.Messages != null && checkResult.Messages.Any())
                {
                    return $"Giai thich va huong dan xu ly loi build PC: {string.Join("; ", checkResult.Messages.Select(x => x.Message))}";
                }

                return $"Huong dan build PC theo nhu cau: {userMessage}";
            }

            if (ns == "game_profile")
            {
                var gameOrPurpose = !string.IsNullOrWhiteSpace(requirement.GameTitle)
                    ? requirement.GameTitle
                    : requirement.PrimaryPurpose;

                return $"Nhu cau game hoac muc dich su dung: {gameOrPurpose}. Yeu cau: {userMessage}";
            }

            if (ns == "product_profiles")
            {
                var parts = new List<string>();

                if (!string.IsNullOrWhiteSpace(requirement.PrimaryPurpose))
                    parts.Add($"Mục đích: {requirement.PrimaryPurpose}");

                if (!string.IsNullOrWhiteSpace(requirement.GameTitle))
                    parts.Add($"Game: {requirement.GameTitle}");

                if (!string.IsNullOrWhiteSpace(requirement.PerformancePriority))
                    parts.Add($"Ưu tiên: {requirement.PerformancePriority}");

                if (requirement.BudgetMax.HasValue)
                    parts.Add($"Ngân sách tối đa: {requirement.BudgetMax.Value}");

                parts.Add($"Cau hoi: {userMessage}");
                return string.Join(". ", parts);
            }

            return userMessage;
        }

        private static string BuildSystemPrompt()
        {
            return """
                Bạn là trợ lý tư vấn build PC cho website bán linh kiện.

                Quy tắc:
                - compatibility_result là nguồn sự thật kỹ thuật.
                - Không tự bịa ra tương thích nếu compatibility_result không xác nhận.
                - requirement_profile mô tả nhu cầu người dùng.
                - suggestions là gợi ý sửa hoặc tối ưu.
                - rag_context là tài liệu tham khảo thêm từ guide, profile game và profile sản phẩm.
                - Trả lời bằng tiếng Việt, ngắn gọn, rõ ràng, có tính tư vấn mua hàng.
                - Nếu has_current_build = true, bạn đang phân tích cấu hình hiện tại của người dùng.
                - Nếu has_current_build = false, bạn đang mô tả một cấu hình đề xuất do hệ thống gợi ý.
                - Tuyệt đối không nói "cấu hình bạn chọn" khi has_current_build = false.
                - Nếu đang có lỗi tương thích, hãy nói rõ lỗi, lý do và cách sửa.
                - Nếu dữ liệu game chưa chắc chắn, hãy nói theo hướng định hướng build và đề nghị người dùng bổ sung ngân sách hoặc độ phân giải mong muốn.
                """;
        }


        private static string BuildUserPrompt(
    string userMessage,
    bool hasCurrentBuild,
    BuildRequirementProfile requirement,
    PcBuildCheckRequest buildRequest,
    PcBuildCheckResponse checkResult,
    List<string> suggestions,
    List<string> ragTexts)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"user_message: {userMessage}");
            sb.AppendLine($"has_current_build: {hasCurrentBuild}");

            sb.AppendLine("requirement_profile:");
            sb.AppendLine($"- PrimaryPurpose: {requirement?.PrimaryPurpose}");
            sb.AppendLine($"- GameTitle: {requirement?.GameTitle}");
            sb.AppendLine($"- ResolutionTarget: {requirement?.ResolutionTarget}");
            sb.AppendLine($"- PerformancePriority: {requirement?.PerformancePriority}");
            sb.AppendLine($"- BudgetMin: {requirement?.BudgetMin}");
            sb.AppendLine($"- BudgetMax: {requirement?.BudgetMax}");
            sb.AppendLine($"- NeedsMonitorHighRefresh: {requirement?.NeedsMonitorHighRefresh}");
            sb.AppendLine($"- NeedsStreaming: {requirement?.NeedsStreaming}");
            sb.AppendLine($"- NeedsEditing: {requirement?.NeedsEditing}");
            sb.AppendLine($"- PreferredBrand: {requirement?.PreferredBrand}");
            sb.AppendLine($"- Notes: {requirement?.Notes}");

            sb.AppendLine("recommended_or_current_build_items:");
            foreach (var item in buildRequest?.Items ?? new List<PcBuildCheckItemDto>())
            {
                sb.AppendLine($"- ComponentType: {item.ComponentType}, ProductId: {item.ProductId}, Quantity: {item.Quantity}");
            }

            sb.AppendLine("compatibility_result:");
            sb.AppendLine($"- IsValid: {checkResult?.IsValid}");
            sb.AppendLine($"- EstimatedPower: {checkResult?.EstimatedPower}W");
            sb.AppendLine($"- TotalPrice: {checkResult?.TotalPrice}");

            foreach (var m in checkResult?.Messages ?? new List<CompatibilityMessageDto>())
            {
                sb.AppendLine($"- [{m.Level}] {m.Message}");
            }

            sb.AppendLine("suggestions:");
            foreach (var s in suggestions ?? new List<string>())
            {
                sb.AppendLine($"- {s}");
            }

            sb.AppendLine("rag_context:");
            foreach (var t in ragTexts ?? new List<string>())
            {
                sb.AppendLine($"- {t}");
            }

            sb.AppendLine("""
                Quy tắc trả lời:
                - Nếu has_current_build = true, hãy nói theo ngữ cảnh cấu hình hiện tại của người dùng.
                - Nếu has_current_build = false, đây là cấu hình đề xuất do hệ thống gợi ý, KHÔNG được nói là "cấu hình bạn đã chọn".
                - Nếu chưa có build hiện tại, hãy nói theo kiểu tư vấn: "mình gợi ý", "với nhu cầu này", "một cấu hình phù hợp có thể là".
                - Nếu game người dùng hỏi không có nhiều dữ liệu cụ thể, hãy nói rõ đây là định hướng build đề xuất và nên nói rõ ngân sách hoặc độ phân giải.
                """);

            sb.AppendLine("Hãy tạo câu trả lời cuối cùng cho người dùng.");
            return sb.ToString();
        }


        private static string BuildFallbackReply(
            PcBuildCheckResponse checkResult,
            List<string> suggestions,
            List<string> ragTexts)
        {
            var messages = checkResult?.Messages ?? new List<CompatibilityMessageDto>();

            var baseText = messages.Any()
                ? string.Join("\n", messages.Select(x => $"- {x.Message}"))
                : $"Cấu hình hiện tại chưa có cảnh báo. Công suất ước tính khoảng {checkResult?.EstimatedPower ?? 0}W.";

            var suggestText = (suggestions ?? new List<string>()).Any()
                ? "\n\nGợi ý:\n" + string.Join("\n", suggestions.Select(x => $"- {x}"))
                : "";

            var ragText = (ragTexts ?? new List<string>()).Any()
                ? "\n\nTham khảo thêm:\n" + string.Join("\n", ragTexts.Take(3).Select(x => $"- {x}"))
                : "";

            return baseText + suggestText + ragText;
        }
    }
}
