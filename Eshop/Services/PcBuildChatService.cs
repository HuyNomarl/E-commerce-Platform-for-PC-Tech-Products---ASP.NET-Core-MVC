using Eshop.Models;
using Eshop.Models.Configurations;
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

            var buildRequest = await ResolveBuildRequestAsync(request, analysis, hasCurrentBuild);
            buildRequest.Items ??= new List<PcBuildCheckItemDto>();
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
            try
            {
                var query = BuildRagQuery(ns, userMessage, analysis, buildRequest, selectedProducts, checkResult, productSuggestions);
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
                    return $"Huong dan dat cau hoi build PC ro hon va cach chot nhu cau: {analysis.ConversationContext}";
                }

                if (checkResult.Messages != null && checkResult.Messages.Any())
                {
                    return $"Giai thich va huong dan xu ly loi build PC: {string.Join("; ", checkResult.Messages.Select(x => x.Message))}";
                }

                if (selectedProducts.Any())
                {
                    return $"Huong dan build PC cho cau hinh dang xet: {string.Join("; ", selectedProducts.Select(x => x.Product.Name))}. Cau hoi: {userMessage}";
                }

                return $"Huong dan build PC theo nhu cau: {analysis.ConversationContext}";
            }

            if (ns == "game_profile")
            {
                var gameOrPurpose = !string.IsNullOrWhiteSpace(analysis.Requirement.GameTitle)
                    ? analysis.Requirement.GameTitle
                    : analysis.Requirement.PrimaryPurpose;

                return $"Nhu cau game hoac muc dich su dung: {gameOrPurpose}. Yeu cau: {analysis.ConversationContext}";
            }

            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(analysis.Requirement.PrimaryPurpose))
            {
                parts.Add($"Muc dich: {analysis.Requirement.PrimaryPurpose}");
            }

            if (!string.IsNullOrWhiteSpace(analysis.Requirement.GameTitle))
            {
                parts.Add($"Game: {analysis.Requirement.GameTitle}");
            }

            if (!string.IsNullOrWhiteSpace(analysis.Requirement.ResolutionTarget))
            {
                parts.Add($"Do phan giai: {analysis.Requirement.ResolutionTarget}");
            }

            if (!string.IsNullOrWhiteSpace(analysis.Requirement.PerformancePriority))
            {
                parts.Add($"Uu tien: {analysis.Requirement.PerformancePriority}");
            }

            if (analysis.Requirement.BudgetMax.HasValue)
            {
                parts.Add($"Ngan sach toi da: {analysis.Requirement.BudgetMax.Value}");
            }

            if (analysis.TargetComponents.Any())
            {
                parts.Add("Linh kien quan tam: " + string.Join(", ", analysis.TargetComponents));
            }

            if (selectedProducts.Any())
            {
                parts.Add("Cau hinh dang xet: " + string.Join("; ", selectedProducts.Select(x => x.Product.Name)));
            }
            else if (buildRequest?.Items != null && buildRequest.Items.Any())
            {
                parts.Add("Cau hinh goi y: " + string.Join("; ", buildRequest.Items.Select(x => $"{x.ComponentType}:{x.ProductId}")));
            }

            if (productSuggestions.Any())
            {
                parts.Add("San pham dang goi y: " + string.Join("; ", productSuggestions.Select(x => x.Product.Name)));
            }

            parts.Add($"Cau hoi: {analysis.ConversationContext}");
            return string.Join(". ", parts);
        }

        private static string BuildSystemPrompt()
        {
            return """
                Ban la tro ly tu van build PC cho website ban linh kien.

                Quy tac:
                - compatibility_result la nguon su that ky thuat.
                - selected_build_products la danh sach san pham that tu catalog DB, uu tien thong tin nay khi phan tich cau hinh.
                - catalog_suggestions la cac san pham that se duoc hien thanh card trong UI chat.
                - intent_analysis mo ta loai cau hoi, linh kien dang duoc quan tam va cac diem con thieu.
                - requirement_profile mo ta nhu cau nguoi dung.
                - suggestions la goi y sua hoac toi uu.
                - rag_context la tai lieu tham khao them tu huong dan build, profile game va catalog san pham.
                - Tra loi bang tieng Viet, ngan gon, ro rang, co tinh tu van mua hang.
                - Neu has_current_build = true, ban dang phan tich cau hinh hien tai cua nguoi dung.
                - Neu has_current_build = false, ban dang tu van dua tren nhu cau va lich su hoi dap, khong duoc gia vo la da co cau hinh hoan chinh neu build dang rong.
                - Tuyet doi khong noi "cau hinh ban chon" khi has_current_build = false va selected_build_products dang rong.
                - Neu co loi tuong thich, hay noi ro loi, ly do va cach sua.
                - Neu nguoi dung hoi sai kien thuc hoac premiss sai, hay noi ro diem sai mot cach lich su, sau do dua thong tin dung.
                - Neu needs_clarification = true, hay noi ro con thieu thong tin gi va dat 1-3 cau hoi bo sung cu the.
                - Neu dang hoi linh kien rieng le, uu tien tra loi xoay quanh linh kien do thay vi lat ra ca build.
                - Neu rag_context va selected_build_products co xung dot, uu tien selected_build_products va compatibility_result.
                - Neu catalog_suggestions co du lieu, co the nhac den 2-4 ten san pham noi bat va noi rang card san pham dang duoc hien ben duoi de nguoi dung tick chon vao Build PC.
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
            sb.AppendLine($"potential_misconception: {analysis.PotentialMisconception}");

            sb.AppendLine("recent_chat_history:");
            foreach (var item in request.History?.TakeLast(8) ?? new List<PcBuildChatHistoryItemDto>())
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
                sb.AppendLine("- Khong co build hien tai.");
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
                sb.AppendLine($"- [{suggestion.ComponentType}] {suggestion.Product.Name} | Gia: {suggestion.Product.Price} | Ly do: {suggestion.Reason}");
            }

            sb.AppendLine("rag_context:");
            foreach (var text in ragTexts ?? new List<string>())
            {
                sb.AppendLine($"- {text}");
            }

            sb.AppendLine("""
                Quy tac tra loi:
                - Neu needs_clarification = true, phan dau tien phai noi ro con thieu thong tin gi.
                - Neu chat_intent = component_recommendation, tap trung vao linh kien nguoi dung dang hoi va de xuat phuong an de chon trong builder.
                - Neu chat_intent = knowledge_check, phai sua thong tin sai neu co va giai thich ngan gon, de hieu.
                - Neu chat_intent = upgrade_build, hay noi uu tien nang cap nao dang loi nhat truoc.
                - Neu has_current_build = false va selected_build_products rong, khong duoc biet ra tong gia hay cong suat nhu mot build da chot.
                - Khi nhac den san pham cu the, uu tien ten san pham va thong so trong selected_build_products hoac catalog_suggestions.
                - Khong lap lai nguyen van du lieu prompt.
                """);

            sb.AppendLine("Hay tao cau tra loi cuoi cung cho nguoi dung.");
            return sb.ToString();
        }

        private static string BuildFallbackReply(
            PcBuildChatIntentAnalysis analysis,
            PcBuildCheckResponse checkResult,
            List<string> suggestions,
            List<PcBuildChatSuggestedProductDto> productSuggestions,
            List<string> ragTexts)
        {
            var parts = new List<string>();

            if (analysis.NeedsClarification)
            {
                parts.Add("Mình cần thêm vài thông tin để tư vấn sát hơn:");
                parts.AddRange(analysis.ClarificationHints.Select(x => $"- {x}"));
            }
            else if (checkResult?.Messages?.Any() == true)
            {
                parts.Add("Mình thấy vài điểm đáng chú ý trong cấu hình:");
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
    }
}
