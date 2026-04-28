using Eshop.Models;
using Eshop.Models.Configurations;
using Eshop.Models.Enums;
using Eshop.Models.ViewModels;
using Eshop.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using System.Text;

namespace Eshop.Services
{
    public class PcBuildChatService : IPcBuildChatService
    {
        private static readonly string[] StaticRagNamespaces = new[]
        {
            "build_guide",
            "game_profile"
        };

        private readonly IPcCompatibilityService _compatibilityService;
        private readonly IPcBuildRecommendationService _recommendationService;
        private readonly IPcBuildSuggestionService _suggestionService;
        private readonly PcBuildChatIntentAnalyzer _intentAnalyzer;
        private readonly PcBuildChatProductSuggestionService _productSuggestionService;
        private readonly RagClient _ragClient;
        private readonly ILlmChatClient _llmChatClient;
        private readonly DataContext _context;
        private readonly string _catalogNamespace;

        public PcBuildChatService(
            IPcCompatibilityService compatibilityService,
            IPcBuildRecommendationService recommendationService,
            IPcBuildSuggestionService suggestionService,
            PcBuildChatIntentAnalyzer intentAnalyzer,
            PcBuildChatProductSuggestionService productSuggestionService,
            RagClient ragClient,
            ILlmChatClient llmChatClient,
            DataContext context,
            IOptions<RagServiceOptions> ragOptions)
        {
            _compatibilityService = compatibilityService;
            _recommendationService = recommendationService;
            _suggestionService = suggestionService;
            _intentAnalyzer = intentAnalyzer;
            _productSuggestionService = productSuggestionService;
            _ragClient = ragClient;
            _llmChatClient = llmChatClient;
            _context = context;
            _catalogNamespace = ragOptions.Value.CatalogNamespace;
        }

        public async Task<PcBuildChatResponse> AskAsync(PcBuildChatRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var safeMessage = (request.Message ?? string.Empty).Trim();
            var hasCurrentBuild = request.Items != null && request.Items.Any();
            var analysis = await _intentAnalyzer.AnalyzeAsync(request);
            var requirement = analysis.Requirement ?? new BuildRequirementProfile();

            if (analysis.ShouldReject)
            {
                return BuildOutOfScopeResponse(analysis);
            }

            var buildRequest = await ResolveBuildRequestAsync(request, analysis, hasCurrentBuild);
            buildRequest.Items ??= new List<PcBuildCheckItemDto>();

            if (ShouldReturnNoBuildProposalReply(analysis, hasCurrentBuild, buildRequest))
            {
                return BuildNoBuildProposalResponse(analysis);
            }

            var selectedProducts = await LoadPromptProductsAsync(buildRequest.Items);
            var checkResult = await _compatibilityService.CheckAsync(buildRequest) ?? new PcBuildCheckResponse();

            checkResult.Messages ??= new List<CompatibilityMessageDto>();

            var suggestions = buildRequest.Items.Any()
                ? await _suggestionService.SuggestFixesAsync(buildRequest, checkResult) ?? new List<string>()
                : new List<string>();

            var productSuggestions = await _productSuggestionService.BuildSuggestionsAsync(
                analysis,
                hasCurrentBuild,
                buildRequest,
                selectedProducts,
                checkResult,
                safeMessage);

            productSuggestions = ConstrainSuggestedProducts(analysis, productSuggestions);

            if (ShouldReturnNoMatchingProductReply(analysis, productSuggestions))
            {
                return BuildNoMatchingProductResponse(analysis, checkResult);
            }

            var ragTexts = await CollectRagContextAsync(
                safeMessage,
                analysis,
                buildRequest,
                selectedProducts,
                checkResult,
                productSuggestions);

            var systemPrompt = BuildSystemPrompt();
            var userPrompt = BuildUserPrompt(
                request,
                analysis,
                requirement,
                hasCurrentBuild,
                buildRequest,
                selectedProducts,
                checkResult,
                suggestions,
                productSuggestions,
                ragTexts);

            string reply;
            try
            {
                reply = await _llmChatClient.AskAsync(systemPrompt, userPrompt);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "LLM failed, using fallback reply.");
                reply = BuildFallbackReply(analysis, checkResult, suggestions, productSuggestions, ragTexts);
            }

            return new PcBuildChatResponse
            {
                Reply = reply,
                Intent = analysis.IntentCode,
                IsValid = checkResult.IsValid,
                TotalPrice = checkResult.TotalPrice,
                EstimatedPower = checkResult.EstimatedPower,
                NeedsClarification = analysis.NeedsClarification,
                ClarificationHints = analysis.ClarificationHints,
                Messages = checkResult.Messages ?? new List<CompatibilityMessageDto>(),
                SuggestedProducts = productSuggestions
            };
        }

        private async Task<PcBuildCheckRequest> ResolveBuildRequestAsync(
            PcBuildChatRequest request,
            PcBuildChatIntentAnalysis analysis,
            bool hasCurrentBuild)
        {
            if (hasCurrentBuild)
            {
                return new PcBuildCheckRequest
                {
                    Items = request.Items ?? new List<PcBuildCheckItemDto>()
                };
            }

            if (analysis.ShouldGenerateBuildProposal)
            {
                return await _recommendationService.RecommendBuildAsync(analysis.Requirement)
                    ?? new PcBuildCheckRequest
                    {
                        Items = new List<PcBuildCheckItemDto>()
                    };
            }

            return new PcBuildCheckRequest
            {
                Items = new List<PcBuildCheckItemDto>()
            };
        }

        private async Task<List<PcBuildChatSelectedProduct>> LoadPromptProductsAsync(IReadOnlyCollection<PcBuildCheckItemDto> items)
        {
            var safeItems = items ?? Array.Empty<PcBuildCheckItemDto>();
            var productIds = safeItems
                .Where(x => x.ProductId > 0)
                .Select(x => x.ProductId)
                .Distinct()
                .ToList();

            if (!productIds.Any())
            {
                return new List<PcBuildChatSelectedProduct>();
            }

            var products = await _context.Products
                .AsNoTracking()
                .Include(x => x.Publisher)
                .Include(x => x.Specifications)
                    .ThenInclude(x => x.SpecificationDefinition)
                .Where(x => productIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            var result = new List<PcBuildChatSelectedProduct>();

            foreach (var item in safeItems)
            {
                if (products.TryGetValue(item.ProductId, out var product))
                {
                    result.Add(new PcBuildChatSelectedProduct(item, product));
                }
            }

            return result;
        }

        private async Task<List<string>> CollectRagContextAsync(
            string userMessage,
            PcBuildChatIntentAnalysis analysis,
            PcBuildCheckRequest buildRequest,
            List<PcBuildChatSelectedProduct> selectedProducts,
            PcBuildCheckResponse checkResult,
            List<PcBuildChatSuggestedProductDto> productSuggestions)
        {
            var namespaces = StaticRagNamespaces
                .Append(_catalogNamespace)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var tasks = namespaces.Select(ns =>
                CollectNamespaceContextAsync(ns, userMessage, analysis, buildRequest, selectedProducts, checkResult, productSuggestions));

            var batches = await Task.WhenAll(tasks);

            return batches
                .SelectMany(x => x)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .Take(6)
                .ToList();
        }

        private async Task<List<string>> CollectNamespaceContextAsync(
            string ns,
            string userMessage,
            PcBuildChatIntentAnalysis analysis,
            PcBuildCheckRequest buildRequest,
            List<PcBuildChatSelectedProduct> selectedProducts,
            PcBuildCheckResponse checkResult,
            List<PcBuildChatSuggestedProductDto> productSuggestions)
        {
            if (!ShouldQueryNamespace(ns, analysis))
            {
                return new List<string>();
            }

            try
            {
                var query = BuildRagQuery(ns, userMessage, analysis, buildRequest, selectedProducts, checkResult, productSuggestions);
                if (string.IsNullOrWhiteSpace(query))
                {
                    return new List<string>();
                }

                var rag = await _ragClient.SearchAsync(query, ns, k: 4, minScore: 0.05);

                Log.Information("RAG namespace: {Namespace}", ns);
                Log.Information("RAG query: {Query}", query);
                Log.Information("RAG result count: {Count}", rag?.Results?.Count ?? 0);

                foreach (var item in rag?.Results?.Take(3) ?? Enumerable.Empty<RagSearchItem>())
                {
                    Log.Information("RAG source [{Namespace}]: {Source}", ns, item.Source);
                    Log.Information("RAG content [{Namespace}]: {Content}", ns, item.Content);
                }

                return rag?.Results?
                    .Select(x => x.Content)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Take(2)
                    .Select(x => $"[{ns}] {x}")
                    .ToList()
                    ?? new List<string>();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "RAG query failed for namespace {Namespace}", ns);
                return new List<string>();
            }
        }

        private static string BuildRagQuery(
            string ns,
            string userMessage,
            PcBuildChatIntentAnalysis analysis,
            PcBuildCheckRequest buildRequest,
            List<PcBuildChatSelectedProduct> selectedProducts,
            PcBuildCheckResponse checkResult,
            List<PcBuildChatSuggestedProductDto> productSuggestions)
        {
            if (ns == "build_guide")
            {
                if (analysis.NeedsClarification)
                {
                    return $"Hướng dẫn đặt câu hỏi build PC rõ hơn và cách chốt nhu cầu: {analysis.ConversationContext}";
                }

                if (checkResult.Messages != null && checkResult.Messages.Any())
                {
                    return $"Giải thích và hướng dẫn xử lý lỗi build PC: {string.Join("; ", checkResult.Messages.Select(x => x.Message))}";
                }

                if (selectedProducts.Any())
                {
                    return $"Hướng dẫn build PC cho cấu hình đang xét: {string.Join("; ", selectedProducts.Select(x => x.Product.Name))}. Câu hỏi: {userMessage}";
                }

                return $"Hướng dẫn build PC theo nhu cầu: {analysis.ConversationContext}";
            }

            if (ns == "game_profile")
            {
                if (!ShouldQueryGameProfile(analysis))
                {
                    return string.Empty;
                }

                var gameOrPurpose = !string.IsNullOrWhiteSpace(analysis.Requirement.GameTitle)
                    ? analysis.Requirement.GameTitle
                    : analysis.Requirement.PrimaryPurpose;

                return $"Nhu cầu game hoặc mục đích sử dụng: {gameOrPurpose}. Yêu cầu: {analysis.ConversationContext}";
            }

            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(analysis.Requirement.PrimaryPurpose))
            {
                parts.Add($"Mục đích: {analysis.Requirement.PrimaryPurpose}");
            }

            if (!string.IsNullOrWhiteSpace(analysis.Requirement.GameTitle))
            {
                parts.Add($"Game: {analysis.Requirement.GameTitle}");
            }

            if (!string.IsNullOrWhiteSpace(analysis.Requirement.ResolutionTarget))
            {
                parts.Add($"Độ phân giải: {analysis.Requirement.ResolutionTarget}");
            }

            if (!string.IsNullOrWhiteSpace(analysis.Requirement.PerformancePriority))
            {
                parts.Add($"Ưu tiên: {analysis.Requirement.PerformancePriority}");
            }

            if (analysis.Requirement.BudgetMax.HasValue)
            {
                parts.Add($"Ngân sách tối đa: {analysis.Requirement.BudgetMax.Value}");
            }

            if (analysis.TargetComponents.Any())
            {
                parts.Add("Linh kiện quan tâm: " + string.Join(", ", analysis.TargetComponents));
            }

            if (selectedProducts.Any())
            {
                parts.Add("Cấu hình đang xét: " + string.Join("; ", selectedProducts.Select(x => x.Product.Name)));
            }
            else if (buildRequest?.Items != null && buildRequest.Items.Any())
            {
                parts.Add("Cấu hình gợi ý: " + string.Join("; ", buildRequest.Items.Select(x => $"{x.ComponentType}:{x.ProductId}")));
            }

            if (productSuggestions.Any())
            {
                parts.Add("Sản phẩm đang gợi ý: " + string.Join("; ", productSuggestions.Select(x => x.Product.Name)));
            }

            parts.Add($"Câu hỏi: {analysis.ConversationContext}");
            return string.Join(". ", parts);
        }

        private static string BuildSystemPrompt()
        {
            return """
            Bạn là trợ lý tư vấn build PC cho website bán linh kiện.

            Quy tắc:
            - compatibility_result là nguồn sự thật kỹ thuật.
            - selected_build_products là danh sách sản phẩm thật từ catalog DB, ưu tiên thông tin này khi phân tích cấu hình.
            - catalog_suggestions là các sản phẩm thật sẽ được hiển thị thành card trong UI chat.
            - intent_analysis mô tả loại câu hỏi, linh kiện đang được quan tâm và các điểm còn thiếu.
            - requirement_profile mô tả nhu cầu người dùng.
            - suggestions là gợi ý sửa hoặc tối ưu.
            - rag_context là tài liệu tham khảo thêm từ hướng dẫn build, profile game và catalog sản phẩm.
            - Trả lời bằng tiếng Việt, ngắn gọn, rõ ràng, có tính tư vấn mua hàng.
            - Nếu has_current_build = true, bạn đang phân tích cấu hình hiện tại của người dùng.
            - Nếu has_current_build = false, bạn đang tư vấn dựa trên nhu cầu và lịch sử hỏi đáp, không được giả vờ là đã có cấu hình hoàn chỉnh nếu build đang rỗng.
            - Tuyệt đối không nói "cấu hình bạn chọn" khi has_current_build = false và selected_build_products đang rỗng.
            - Nếu có lỗi tương thích, hãy nói rõ lỗi, lý do và cách sửa.
            - Nếu người dùng hỏi sai kiến thức hoặc premise sai, hãy nói rõ điểm sai một cách lịch sự, sau đó đưa thông tin đúng.
            - Nếu needs_clarification = true, hãy nói rõ còn thiếu thông tin gì và đặt 1-3 câu hỏi bổ sung cụ thể.
            - Nếu đang hỏi linh kiện riêng lẻ, ưu tiên trả lời xoay quanh linh kiện đó thay vì lật ra cả build.
            - Nếu rag_context và selected_build_products có xung đột, ưu tiên selected_build_products và compatibility_result.
            - Nếu chat_intent = not_understood, câu trả lời phải là chính xác: "Tôi không rõ, tôi chỉ có thể tư vấn bạn build thôi."
            - Nếu là truy vấn linh kiện đơn lẻ có BudgetMax, tuyệt đối không đề xuất sản phẩm vượt BudgetMax.
            - Nếu catalog_suggestions rỗng cho một truy vấn linh kiện đã có ràng buộc rõ, hãy nói chưa tìm thấy sản phẩm phù hợp thay vì đoán tên sản phẩm.
            - Nếu catalog_suggestions có dữ liệu, có thể nhắc đến 2-4 tên sản phẩm nổi bật và nói rằng card sản phẩm đang được hiển thị bên dưới để người dùng tick chọn vào Build PC.
            """;
        }

        private static string BuildUserPrompt(
            PcBuildChatRequest request,
            PcBuildChatIntentAnalysis analysis,
            BuildRequirementProfile requirement,
            bool hasCurrentBuild,
            PcBuildCheckRequest buildRequest,
            List<PcBuildChatSelectedProduct> selectedProducts,
            PcBuildCheckResponse checkResult,
            List<string> suggestions,
            List<PcBuildChatSuggestedProductDto> productSuggestions,
            List<string> ragTexts)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"user_message: {request.Message}");
            sb.AppendLine($"has_current_build: {hasCurrentBuild}");
            sb.AppendLine($"chat_intent: {analysis.IntentCode}");
            sb.AppendLine($"needs_clarification: {analysis.NeedsClarification}");
            sb.AppendLine($"strict_budget_cap: {analysis.HasStrictBudgetCap}");
            sb.AppendLine($"potential_misconception: {analysis.PotentialMisconception}");

            sb.AppendLine("recent_chat_history:");
            foreach (var item in analysis.EffectiveHistory
                         .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Content))
                         .TakeLast(4))
            {
                sb.AppendLine($"- {item.Role}: {item.Content}");
            }

            sb.AppendLine("intent_analysis:");
            sb.AppendLine($"- TargetComponents: {string.Join(", ", analysis.TargetComponents)}");
            foreach (var hint in analysis.ClarificationHints)
            {
                sb.AppendLine($"- ClarificationHint: {hint}");
            }

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

            sb.AppendLine("selected_build_products:");
            if (selectedProducts.Any())
            {
                foreach (var item in selectedProducts)
                {
                    sb.AppendLine($"- ProductId: {item.Product.Id}, ComponentType: {item.Item.ComponentType}, Summary: {item.Summary}");
                }
            }
            else if (buildRequest?.Items != null && buildRequest.Items.Any())
            {
                foreach (var item in buildRequest.Items)
                {
                    sb.AppendLine($"- ComponentType: {item.ComponentType}, ProductId: {item.ProductId}, Quantity: {item.Quantity}");
                }
            }
            else
            {
                sb.AppendLine("- Không có build hiện tại.");
            }

            sb.AppendLine("compatibility_result:");
            sb.AppendLine($"- IsValid: {checkResult?.IsValid}");
            sb.AppendLine($"- EstimatedPower: {checkResult?.EstimatedPower}W");
            sb.AppendLine($"- TotalPrice: {checkResult?.TotalPrice}");

            foreach (var message in checkResult?.Messages ?? new List<CompatibilityMessageDto>())
            {
                sb.AppendLine($"- [{message.Level}] {message.Message}");
            }

            sb.AppendLine("suggestions:");
            foreach (var suggestion in suggestions ?? new List<string>())
            {
                sb.AppendLine($"- {suggestion}");
            }

            sb.AppendLine("catalog_suggestions:");
            foreach (var suggestion in productSuggestions ?? new List<PcBuildChatSuggestedProductDto>())
            {
                sb.AppendLine($"- [{suggestion.ComponentType}] {suggestion.Product.Name} | Giá: {suggestion.Product.Price} | Lý do: {suggestion.Reason}");
            }

            sb.AppendLine("rag_context:");
            foreach (var text in ragTexts ?? new List<string>())
            {
                sb.AppendLine($"- {text}");
            }

            sb.AppendLine("""
                Quy tắc trả lời:
                - Nếu chat_intent = not_understood, chỉ được trả lời: Tôi không rõ, tôi chỉ có thể tư vấn bạn build thôi.
                - Nếu needs_clarification = true, phần đầu tiên phải nói rõ còn thiếu thông tin gì.
                - Nếu chat_intent = component_recommendation, tập trung vào linh kiện người dùng đang hỏi và đề xuất phương án để chọn trong builder.
                - Nếu chat_intent = knowledge_check, phải sửa thông tin sai nếu có và giải thích ngắn gọn, dễ hiểu.
                - Nếu chat_intent = upgrade_build, hãy nói ưu tiên nâng cấp nào đáng lời nhất trước.
                - Nếu has_current_build = false và selected_build_products rỗng, không được biết ra tổng giá hay công suất như một build đã chốt.
                - Khi nhắc đến sản phẩm cụ thể, ưu tiên tên sản phẩm và thông số trong selected_build_products hoặc catalog_suggestions.
                - Nếu strict_budget_cap = true thì không được đề xuất sản phẩm vượt ngân sách.
                - Nếu catalog_suggestions rỗng cho query linh kiện đã có ràng buộc rõ, phải nói là chưa tìm thấy sản phẩm phù hợp trong catalog.
                - Không lặp lại nguyên văn dữ liệu prompt.
                """);

            sb.AppendLine("Hãy tạo câu trả lời cuối cùng cho người dùng.");
            return sb.ToString();
        }

        private static bool ShouldQueryNamespace(string ns, PcBuildChatIntentAnalysis analysis)
        {
            if (string.Equals(ns, "game_profile", StringComparison.OrdinalIgnoreCase))
            {
                return ShouldQueryGameProfile(analysis);
            }

            return true;
        }

        private static bool ShouldQueryGameProfile(PcBuildChatIntentAnalysis analysis)
        {
            if (analysis?.Requirement == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(analysis.Requirement.GameTitle))
            {
                return true;
            }

            return string.Equals(
                analysis.Requirement.PrimaryPurpose,
                "Gaming",
                StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildFallbackReply(
            PcBuildChatIntentAnalysis analysis,
            PcBuildCheckResponse checkResult,
            List<string> suggestions,
            List<PcBuildChatSuggestedProductDto> productSuggestions,
            List<string> ragTexts)
        {
            if (analysis.Intent == PcBuildChatIntentKind.NotUnderstood || analysis.ShouldReject)
            {
                return "Tôi không rõ, tôi chỉ có thể tư vấn bạn build thôi.";
            }

            if (ShouldReturnNoMatchingProductReply(analysis, productSuggestions))
            {
                return BuildNoMatchingProductReply(analysis);
            }

            var parts = new List<string>();

            if (analysis.NeedsClarification)
            {
                parts.Add("Mình cần thêm vài thông tin để tư vấn sát hơn:");
                parts.AddRange(analysis.ClarificationHints.Select(x => $"- {x}"));
            }
            else if (checkResult?.Messages?.Any() == true)
            {
                parts.Add("Recommend:");
                parts.AddRange(checkResult.Messages.Select(x => $"- {x.Message}"));
            }
            else if (checkResult != null && checkResult.EstimatedPower > 0)
            {
                parts.Add($"Cấu hình hiện tại chưa có cảnh báo lớn. Công suất ước tính khoảng {checkResult.EstimatedPower}W.");
            }
            else
            {
                parts.Add("Mình chưa đủ dữ liệu để chốt ngay, nhưng vẫn có thể gợi ý theo nhu cầu nếu bạn bổ sung thêm thông tin.");
            }

            if ((suggestions ?? new List<string>()).Any())
            {
                parts.Add("Gợi ý nhanh:");
                parts.AddRange(suggestions.Select(x => $"- {x}"));
            }

            if ((productSuggestions ?? new List<PcBuildChatSuggestedProductDto>()).Any())
            {
                parts.Add("Một vài sản phẩm đang được gợi ý trong khung chat:");
                parts.AddRange(productSuggestions.Take(4).Select(x => $"- {x.Product.Name}: {x.Reason}"));
            }

            if ((ragTexts ?? new List<string>()).Any())
            {
                parts.Add("Tham khảo thêm:");
                parts.AddRange(ragTexts.Take(2).Select(x => $"- {x}"));
            }

            return string.Join("\n", parts);
        }

        private static bool ShouldReturnNoMatchingProductReply(
            PcBuildChatIntentAnalysis analysis,
            IReadOnlyCollection<PcBuildChatSuggestedProductDto> productSuggestions)
        {
            if (analysis.NeedsClarification || productSuggestions.Any())
            {
                return false;
            }

            if (analysis.TargetComponents.Count != 1)
            {
                return false;
            }

            if (analysis.Intent != PcBuildChatIntentKind.ComponentRecommendation
                && analysis.Intent != PcBuildChatIntentKind.UpgradeCurrentBuild)
            {
                return false;
            }

            return analysis.Requirement.BudgetMax.HasValue
                || analysis.Requirement.BudgetMin.HasValue
                || !string.IsNullOrWhiteSpace(analysis.Requirement.PreferredBrand);
        }

        private static bool ShouldReturnNoBuildProposalReply(
            PcBuildChatIntentAnalysis analysis,
            bool hasCurrentBuild,
            PcBuildCheckRequest buildRequest)
        {
            if (hasCurrentBuild || !analysis.ShouldGenerateBuildProposal)
            {
                return false;
            }

            return !HasRecommendedCoreBuild(buildRequest.Items, analysis.Requirement);
        }

        private static bool HasRecommendedCoreBuild(
            IReadOnlyCollection<PcBuildCheckItemDto>? items,
            BuildRequirementProfile requirement)
        {
            var selected = (items ?? Array.Empty<PcBuildCheckItemDto>())
                .Select(x => x.ComponentType)
                .ToHashSet();

            var required = new HashSet<PcComponentType>
            {
                PcComponentType.CPU,
                PcComponentType.Mainboard,
                PcComponentType.RAM,
                PcComponentType.SSD,
                PcComponentType.PSU,
                PcComponentType.Case
            };

            if (NeedsDedicatedGpu(requirement))
            {
                required.Add(PcComponentType.GPU);
            }

            return required.All(selected.Contains);
        }

        private static bool NeedsDedicatedGpu(BuildRequirementProfile requirement)
        {
            var purpose = requirement.PrimaryPurpose ?? string.Empty;

            return purpose.Equals("Gaming", StringComparison.OrdinalIgnoreCase)
                || purpose.Equals("Content Creation", StringComparison.OrdinalIgnoreCase)
                || purpose.Equals("CAD / 3D", StringComparison.OrdinalIgnoreCase)
                || purpose.Equals("AI / Machine Learning", StringComparison.OrdinalIgnoreCase)
                || purpose.Equals("Streaming", StringComparison.OrdinalIgnoreCase)
                || requirement.NeedsStreaming
                || requirement.NeedsEditing
                || !string.IsNullOrWhiteSpace(requirement.GameTitle);
        }

        private static PcBuildChatResponse BuildNoBuildProposalResponse(PcBuildChatIntentAnalysis analysis)
        {
            var budgetText = analysis.Requirement.BudgetMax.HasValue
                ? $" trong khoảng {analysis.Requirement.BudgetMax.Value:N0} VND"
                : " với catalog hiện tại";

            return new PcBuildChatResponse
            {
                Reply = $"Mình chưa ghép được một cấu hình trọn vẹn{budgetText} cho nhu cầu này. Bạn có thể tăng ngân sách hoặc nới yêu cầu để mình đề xuất sát hơn.",
                Intent = analysis.IntentCode,
                IsValid = false,
                TotalPrice = 0,
                EstimatedPower = 0,
                NeedsClarification = false,
                ClarificationHints = new List<string>(),
                Messages = new List<CompatibilityMessageDto>(),
                SuggestedProducts = new List<PcBuildChatSuggestedProductDto>()
            };
        }

        private static PcBuildChatResponse BuildOutOfScopeResponse(PcBuildChatIntentAnalysis analysis)
        {
            return new PcBuildChatResponse
            {
                Reply = "Tôi không rõ, tôi chỉ có thể tư vấn bạn build thôi.",
                Intent = analysis.IntentCode,
                IsValid = false,
                TotalPrice = 0,
                EstimatedPower = 0,
                NeedsClarification = false,
                ClarificationHints = new List<string>(),
                Messages = new List<CompatibilityMessageDto>(),
                SuggestedProducts = new List<PcBuildChatSuggestedProductDto>()
            };
        }

        private static PcBuildChatResponse BuildRejectedResponse(PcBuildChatIntentAnalysis analysis)
        {
            return new PcBuildChatResponse
            {
                Reply = "Tôi không hiểu.",
                Intent = analysis.IntentCode,
                IsValid = false,
                TotalPrice = 0,
                EstimatedPower = 0,
                NeedsClarification = false,
                ClarificationHints = new List<string>(),
                Messages = new List<CompatibilityMessageDto>(),
                SuggestedProducts = new List<PcBuildChatSuggestedProductDto>()
            };
        }

        private static PcBuildChatResponse BuildNoMatchingProductResponse(
            PcBuildChatIntentAnalysis analysis,
            PcBuildCheckResponse checkResult)
        {
            return new PcBuildChatResponse
            {
                Reply = BuildNoMatchingProductReply(analysis),
                Intent = analysis.IntentCode,
                IsValid = checkResult.IsValid,
                TotalPrice = checkResult.TotalPrice,
                EstimatedPower = checkResult.EstimatedPower,
                NeedsClarification = false,
                ClarificationHints = new List<string>(),
                Messages = checkResult.Messages ?? new List<CompatibilityMessageDto>(),
                SuggestedProducts = new List<PcBuildChatSuggestedProductDto>()
            };
        }

        private static string BuildNoMatchingProductReply(PcBuildChatIntentAnalysis analysis)
        {
            var component = analysis.TargetComponents.FirstOrDefault().ToString();
            var budgetText = analysis.Requirement.BudgetMax.HasValue
                ? $" trong mức {analysis.Requirement.BudgetMax.Value:N0} VND"
                : string.Empty;
            var brandText = !string.IsNullOrWhiteSpace(analysis.Requirement.PreferredBrand)
                ? $" với ưu tiên {analysis.Requirement.PreferredBrand}"
                : string.Empty;

            return $"Mình chưa tìm thấy {component} phù hợp{budgetText}{brandText} trong catalog hiện tại. Bạn có thể nới ngân sách, đổi hãng, hoặc nói rõ thêm nhu cầu để mình lọc lại sát hơn.";
        }

        private static List<PcBuildChatSuggestedProductDto> ConstrainSuggestedProducts(
            PcBuildChatIntentAnalysis analysis,
            IReadOnlyCollection<PcBuildChatSuggestedProductDto> productSuggestions)
        {
            var constrained = (productSuggestions ?? Array.Empty<PcBuildChatSuggestedProductDto>())
                .Where(x => x?.Product != null)
                .ToList();

            if (analysis.TargetComponents.Any())
            {
                var allowedTypes = analysis.TargetComponents
                    .Select(x => x.ToString())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                constrained = constrained
                    .Where(x => allowedTypes.Contains(x.ComponentType)
                        || allowedTypes.Contains(x.Product.ComponentType))
                    .ToList();
            }

            if (analysis.TargetComponents.Count == 1)
            {
                if (analysis.Requirement.BudgetMax.HasValue)
                {
                    constrained = constrained
                        .Where(x => x.Product.Price <= analysis.Requirement.BudgetMax.Value)
                        .ToList();
                }

                if (analysis.Requirement.BudgetMin.HasValue)
                {
                    constrained = constrained
                        .Where(x => x.Product.Price >= analysis.Requirement.BudgetMin.Value)
                        .ToList();
                }
            }

            if (!string.IsNullOrWhiteSpace(analysis.Requirement.PreferredBrand))
            {
                constrained = constrained
                    .Where(x => x.Product.PublisherName?.Contains(analysis.Requirement.PreferredBrand, StringComparison.OrdinalIgnoreCase) == true
                        || x.Product.Name?.Contains(analysis.Requirement.PreferredBrand, StringComparison.OrdinalIgnoreCase) == true)
                    .ToList();
            }

            return constrained
                .GroupBy(x => x.Product.Id)
                .Select(x => x.First())
                .ToList();
        }
    }
}