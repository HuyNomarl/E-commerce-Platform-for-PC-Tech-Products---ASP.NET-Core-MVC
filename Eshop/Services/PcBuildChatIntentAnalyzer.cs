using Eshop.Models.Enums;
using Eshop.Models.ViewModels;
using System.Globalization;
using System.Text;

namespace Eshop.Services
{
    public sealed class PcBuildChatIntentAnalyzer
    {
        private static readonly Dictionary<PcComponentType, string[]> ComponentKeywords = new()
        {
            [PcComponentType.CPU] = new[] { "cpu", "vi xu ly", "processor" },
            [PcComponentType.Mainboard] = new[] { "mainboard", "motherboard", "bo mach chu" },
            [PcComponentType.RAM] = new[] { "ram", "memory", "bo nho" },
            [PcComponentType.SSD] = new[] { "ssd", "nvme", "m.2", "sata", "o cung" },
            [PcComponentType.HDD] = new[] { "hdd", "o cung co", "hard drive" },
            [PcComponentType.GPU] = new[] { "gpu", "vga", "card man hinh", "card do hoa", "card do hoa", "graphics card" },
            [PcComponentType.PSU] = new[] { "psu", "nguon", "bo nguon", "power supply" },
            [PcComponentType.Case] = new[] { "case", "vo case", "thung may", "vo may" },
            [PcComponentType.Cooler] = new[] { "cooler", "tan nhiet", "fan cpu", "aio", "watercooling" },
            [PcComponentType.Monitor] = new[] { "monitor", "man hinh" }
        };

        private readonly IBuildRequirementExtractor _requirementExtractor;

        public PcBuildChatIntentAnalyzer(IBuildRequirementExtractor requirementExtractor)
        {
            _requirementExtractor = requirementExtractor;
        }

        internal async Task<PcBuildChatIntentAnalysis> AnalyzeAsync(PcBuildChatRequest request)
        {
            var message = (request?.Message ?? string.Empty).Trim();
            var hasCurrentBuild = request?.Items != null && request.Items.Any();
            var effectiveHistory = SelectEffectiveHistory(request?.History, message);
            var conversationContext = BuildConversationContext(effectiveHistory, message);

            var requirement = await _requirementExtractor.ExtractAsync(conversationContext)
                ?? new BuildRequirementProfile
                {
                    Notes = conversationContext
                };

            var normalizedMessage = NormalizeText(message);
            var normalizedConversation = NormalizeText(conversationContext);
            var directTargetComponents = DetectTargetComponents(normalizedMessage);
            var targetComponents = DetectTargetComponents(normalizedConversation);
            var isRelevant = IsRelevantQuery(
                normalizedMessage,
                normalizedConversation,
                hasCurrentBuild,
                requirement,
                directTargetComponents,
                targetComponents);

            if (!isRelevant)
            {
                return new PcBuildChatIntentAnalysis
                {
                    Intent = PcBuildChatIntentKind.NotUnderstood,
                    Requirement = requirement,
                    TargetComponents = new List<PcComponentType>(),
                    EffectiveHistory = effectiveHistory,
                    NeedsClarification = false,
                    ClarificationHints = new List<string>(),
                    WantsProductSuggestions = false,
                    ShouldGenerateBuildProposal = false,
                    PotentialMisconception = false,
                    ShouldReject = true,
                    HasStrictBudgetCap = false,
                    ConversationContext = conversationContext
                };
            }

            var intent = DetectIntent(normalizedMessage, normalizedConversation, hasCurrentBuild, targetComponents);
            var needsClarification = DetermineNeedsClarification(
                normalizedMessage,
                normalizedConversation,
                requirement,
                hasCurrentBuild,
                intent,
                targetComponents);

            var clarificationHints = BuildClarificationHints(
                requirement,
                hasCurrentBuild,
                intent,
                targetComponents,
                normalizedConversation,
                needsClarification);

            return new PcBuildChatIntentAnalysis
            {
                Intent = needsClarification && intent == PcBuildChatIntentKind.GeneralHelp
                    ? PcBuildChatIntentKind.Clarification
                    : intent,
                Requirement = requirement,
                TargetComponents = targetComponents,
                EffectiveHistory = effectiveHistory,
                NeedsClarification = needsClarification,
                ClarificationHints = clarificationHints,
                WantsProductSuggestions = DetermineProductSuggestionNeed(intent, targetComponents, hasCurrentBuild, needsClarification),
                ShouldGenerateBuildProposal = !hasCurrentBuild
                    && !needsClarification
                    && intent == PcBuildChatIntentKind.NewBuild,
                PotentialMisconception = DetectPotentialMisconception(normalizedConversation),
                ShouldReject = false,
                HasStrictBudgetCap = HasStrictBudgetCap(normalizedConversation, requirement, targetComponents),
                ConversationContext = conversationContext
            };
        }

        private static PcBuildChatIntentKind DetectIntent(
            string normalizedMessage,
            string normalizedConversation,
            bool hasCurrentBuild,
            List<PcComponentType> targetComponents)
        {
            if (ContainsAny(normalizedMessage, "nang cap", "upgrade", "doi len", "thay the", "doi sang"))
            {
                return PcBuildChatIntentKind.UpgradeCurrentBuild;
            }

            if (hasCurrentBuild && ContainsAny(normalizedMessage, "loi", "tuong thich", "hop khong", "phu hop", "co du khong", "du khong", "vua khong", "cam duoc"))
            {
                return PcBuildChatIntentKind.CompatibilityCheck;
            }

            if (ContainsKnowledgePattern(normalizedMessage, targetComponents.Count))
            {
                return PcBuildChatIntentKind.KnowledgeCheck;
            }

            if (targetComponents.Any() && ContainsAny(normalizedMessage, "goi y", "tu van", "nen mua", "chon", "tim", "de xuat", "co con nao", "linh kien", "mau nao"))
            {
                return PcBuildChatIntentKind.ComponentRecommendation;
            }

            if (targetComponents.Any() && !ContainsAny(normalizedMessage, "build", "cau hinh"))
            {
                return PcBuildChatIntentKind.ComponentRecommendation;
            }

            if (ContainsNewBuildNeedPattern(normalizedConversation))
            {
                return PcBuildChatIntentKind.NewBuild;
            }

            return hasCurrentBuild
                ? PcBuildChatIntentKind.GeneralHelp
                : PcBuildChatIntentKind.Clarification;
        }

        private static bool DetermineNeedsClarification(
            string normalizedMessage,
            string normalizedConversation,
            BuildRequirementProfile requirement,
            bool hasCurrentBuild,
            PcBuildChatIntentKind intent,
            List<PcComponentType> targetComponents)
        {
            if (string.IsNullOrWhiteSpace(normalizedMessage))
            {
                return true;
            }

            if (intent == PcBuildChatIntentKind.UpgradeCurrentBuild && !hasCurrentBuild && !targetComponents.Any())
            {
                return true;
            }

            if (intent == PcBuildChatIntentKind.ComponentRecommendation && !targetComponents.Any())
            {
                return true;
            }

            if (intent == PcBuildChatIntentKind.ComponentRecommendation
                && !hasCurrentBuild
                && !requirement.BudgetMax.HasValue
                && !requirement.BudgetMin.HasValue)
            {
                return true;
            }

            if (intent == PcBuildChatIntentKind.NewBuild)
            {
                var hasPurpose = !string.IsNullOrWhiteSpace(requirement.PrimaryPurpose)
                    || !string.IsNullOrWhiteSpace(requirement.GameTitle)
                    || ContainsUseCaseKeywords(normalizedConversation);
                var hasBudget = requirement.BudgetMax.HasValue || requirement.BudgetMin.HasValue;
                var hasDisplayTarget = !string.IsNullOrWhiteSpace(requirement.ResolutionTarget)
                    || ContainsAny(normalizedConversation, "1080p", "1440p", "2k", "4k", "full hd");

                if (!hasCurrentBuild && (!hasPurpose || !hasBudget) && !hasDisplayTarget)
                {
                    return true;
                }
            }

            if (normalizedMessage.Length <= 10 && !hasCurrentBuild && !targetComponents.Any())
            {
                return true;
            }

            return ContainsAny(normalizedMessage, "sao nhi", "tu van voi", "goi y voi", "toi muon hoi")
                && !hasCurrentBuild
                && !targetComponents.Any()
                && !requirement.BudgetMax.HasValue
                && string.IsNullOrWhiteSpace(requirement.GameTitle)
                && string.IsNullOrWhiteSpace(requirement.PrimaryPurpose);
        }

        private static bool DetermineProductSuggestionNeed(
            PcBuildChatIntentKind intent,
            List<PcComponentType> targetComponents,
            bool hasCurrentBuild,
            bool needsClarification)
        {
            if (needsClarification)
            {
                return false;
            }

            if (intent == PcBuildChatIntentKind.NewBuild || intent == PcBuildChatIntentKind.ComponentRecommendation)
            {
                return true;
            }

            if (intent == PcBuildChatIntentKind.UpgradeCurrentBuild)
            {
                return hasCurrentBuild || targetComponents.Any();
            }

            if (intent == PcBuildChatIntentKind.CompatibilityCheck && targetComponents.Any())
            {
                return true;
            }

            return false;
        }

        private static bool IsRelevantQuery(
            string normalizedMessage,
            string normalizedConversation,
            bool hasCurrentBuild,
            BuildRequirementProfile requirement,
            List<PcComponentType> directTargetComponents,
            List<PcComponentType> targetComponents)
        {
            if (string.IsNullOrWhiteSpace(normalizedMessage))
            {
                return false;
            }

            var hasDirectBuilderSignal = HasDirectBuilderSignal(normalizedMessage, directTargetComponents);
            if (ContainsNonPcDeviceKeywords(normalizedMessage) && !hasDirectBuilderSignal)
            {
                return false;
            }

            if (hasDirectBuilderSignal)
            {
                return true;
            }

            var hasBuilderConversationContext = HasBuilderConversationContext(
                normalizedConversation,
                hasCurrentBuild,
                targetComponents);

            if (!hasBuilderConversationContext)
            {
                return false;
            }

            if (hasCurrentBuild && ContainsAny(normalizedMessage, "loi", "hop khong", "du khong", "nang cap", "thay", "doi", "toi uu"))
            {
                return true;
            }

            return HasFollowUpBuildConstraintSignal(normalizedMessage, requirement);
        }

        private static bool HasStrictBudgetCap(
            string normalizedConversation,
            BuildRequirementProfile requirement,
            List<PcComponentType> targetComponents)
        {
            if (!requirement.BudgetMax.HasValue || targetComponents.Count != 1)
            {
                return false;
            }

            if (ContainsAny(normalizedConversation, "tam", "khoang", "around", "xap xi"))
            {
                return false;
            }

            if (ContainsAny(normalizedConversation, "duoi", "toi da", "khong qua", "max", "under", "<", "<="))
            {
                return true;
            }

            return true;
        }

        private static bool DetectPotentialMisconception(string normalizedConversation)
        {
            return ContainsAny(
                normalizedConversation,
                "co dung khong",
                "co phai",
                "nghe noi",
                "that khong",
                "luon tuong thich",
                "khong can",
                "chi can",
                "bao gio cung",
                "cam truc tiep");
        }

        private static bool ContainsKnowledgePattern(string normalizedMessage, int targetComponentCount)
        {
            if (ContainsAny(normalizedMessage, "co dung khong", "co phai", "nghe noi", "that khong", "co can", "khong can"))
            {
                return true;
            }

            return targetComponentCount >= 2
                && ContainsAny(normalizedMessage, "duoc khong", "tuong thich khong", "hop khong", "lap voi", "cam vao", "dung voi");
        }

        private static List<string> BuildClarificationHints(
            BuildRequirementProfile requirement,
            bool hasCurrentBuild,
            PcBuildChatIntentKind intent,
            List<PcComponentType> targetComponents,
            string normalizedConversation,
            bool needsClarification)
        {
            if (!needsClarification)
            {
                return new List<string>();
            }

            var hints = new List<string>();

            if (intent == PcBuildChatIntentKind.UpgradeCurrentBuild && !hasCurrentBuild)
            {
                hints.Add("Gửi cấu hình hiện tại hoặc chọn sẵn các linh kiện bạn đang dùng trong Build PC.");
            }

            if (!targetComponents.Any() && intent == PcBuildChatIntentKind.ComponentRecommendation)
            {
                hints.Add("Nói rõ bạn đang muốn hỏi CPU, mainboard, RAM, GPU, SSD, PSU, case, tản nhiệt hay màn hình.");
            }

            if (!requirement.BudgetMax.HasValue && !requirement.BudgetMin.HasValue)
            {
                hints.Add("Cho mình biết ngân sách dự kiến để lọc đúng tầm giá.");
            }

            if (string.IsNullOrWhiteSpace(requirement.GameTitle)
                && string.IsNullOrWhiteSpace(requirement.PrimaryPurpose)
                && intent == PcBuildChatIntentKind.NewBuild)
            {
                hints.Add("Nói rõ bạn dùng máy để chơi game nào hoặc làm việc gì là quan trọng nhất.");
            }

            if (string.IsNullOrWhiteSpace(requirement.ResolutionTarget)
                && intent == PcBuildChatIntentKind.NewBuild
                && ContainsAny(normalizedConversation, "gaming", "choi game", "monitor", "man hinh"))
            {
                hints.Add("Nếu có thể, cho mình thêm độ phân giải hoặc mức FPS mong muốn như 1080p/144Hz, 2K hay 4K.");
            }

            if (intent == PcBuildChatIntentKind.ComponentRecommendation && targetComponents.Any())
            {
                hints.Add("Nếu bạn đã có build hiện tại, chọn sẵn linh kiện trong bảng để mình lọc theo tương thích chính xác hơn.");
            }

            return hints
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .ToList();
        }

        private static List<PcBuildChatHistoryItemDto> SelectEffectiveHistory(
            IReadOnlyCollection<PcBuildChatHistoryItemDto>? history,
            string message)
        {
            var userHistory = history?
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Content))
                .Where(x => !string.Equals(x.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(x.Role, "bot", StringComparison.OrdinalIgnoreCase))
                .TakeLast(6)
                .Select(x => new PcBuildChatHistoryItemDto
                {
                    Role = "user",
                    Content = x.Content.Trim()
                })
                .ToList()
                ?? new List<PcBuildChatHistoryItemDto>();

            if (!userHistory.Any())
            {
                return new List<PcBuildChatHistoryItemDto>();
            }

            var normalizedMessage = NormalizeText(message);
            var directTargetComponents = DetectTargetComponents(normalizedMessage);

            if (!ShouldCarryHistory(normalizedMessage, directTargetComponents))
            {
                return new List<PcBuildChatHistoryItemDto>();
            }

            return userHistory
                .TakeLast(4)
                .ToList();
        }

        private static List<PcComponentType> DetectTargetComponents(string normalizedText)
        {
            var detected = new List<PcComponentType>();

            foreach (var entry in ComponentKeywords)
            {
                if (entry.Value.Any(keyword => normalizedText.Contains(keyword, StringComparison.Ordinal)))
                {
                    detected.Add(entry.Key);
                }
            }

            if (detected.Contains(PcComponentType.HDD) && !detected.Contains(PcComponentType.SSD))
            {
                detected.Add(PcComponentType.SSD);
            }

            return detected
                .Distinct()
                .Where(x => x != PcComponentType.None)
                .ToList();
        }

        private static string BuildConversationContext(
            IReadOnlyCollection<PcBuildChatHistoryItemDto>? history,
            string message)
        {
            var sb = new StringBuilder();

            foreach (var item in history?
                         .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Content))
                         .TakeLast(8)
                      ?? Enumerable.Empty<PcBuildChatHistoryItemDto>())
            {
                var isAssistant = string.Equals(item.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Role, "bot", StringComparison.OrdinalIgnoreCase);
                if (isAssistant)
                {
                    continue;
                }

                sb.AppendLine($"user: {item.Content.Trim()}");
            }

            sb.AppendLine($"user: {message}");
            return sb.ToString().Trim();
        }

        private static bool ShouldCarryHistory(
            string normalizedMessage,
            IReadOnlyCollection<PcComponentType> directTargetComponents)
        {
            if (string.IsNullOrWhiteSpace(normalizedMessage))
            {
                return false;
            }

            if (LooksLikeStandaloneQuery(normalizedMessage, directTargetComponents))
            {
                return false;
            }

            return HasContextualReference(normalizedMessage)
                || HasFollowUpConstraintSignal(normalizedMessage);
        }

        private static string NormalizeText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Normalize(NormalizationForm.FormD);
            var buffer = new StringBuilder(normalized.Length);

            foreach (var ch in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                {
                    buffer.Append(ch);
                }
            }

            return buffer
                .ToString()
                .Normalize(NormalizationForm.FormC)
                .Replace('đ', 'd')
                .Replace('Đ', 'D')
                .ToLowerInvariant();
        }

        private static bool ContainsAny(string source, params string[] values)
        {
            return values.Any(value => source.Contains(value, StringComparison.Ordinal));
        }

        private static bool LooksLikeStandaloneQuery(
            string normalizedMessage,
            IReadOnlyCollection<PcComponentType> directTargetComponents)
        {
            if (HasContextualReference(normalizedMessage))
            {
                return false;
            }

            if (ContainsNewBuildNeedPattern(normalizedMessage))
            {
                return true;
            }

            if (!HasDirectBuilderSignal(normalizedMessage, directTargetComponents))
            {
                return false;
            }

            if (directTargetComponents.Any())
            {
                return true;
            }

            return ContainsAny(
                normalizedMessage,
                "toi muon",
                "minh muon",
                "toi can",
                "minh can",
                "hay goi y",
                "goi y",
                "tu van",
                "nen mua",
                "chon",
                "tim",
                "de xuat",
                "lap",
                "rap");
        }

        private static bool HasContextualReference(string normalizedMessage)
        {
            return ContainsAny(
                normalizedMessage,
                "cau hinh nay",
                "build nay",
                "bo nay",
                "linh kien nay",
                "may nay",
                "bo do",
                "cai nay",
                "cai do",
                "con nay",
                "con do",
                "cpu do",
                "gpu do",
                "main do",
                "ram do",
                "ssd do",
                "nguon do",
                "case do",
                "man hinh do",
                "the con",
                "vay con",
                "con neu",
                "neu vay",
                "the neu");
        }

        private static bool HasFollowUpConstraintSignal(string normalizedMessage)
        {
            if (normalizedMessage.Length > 96)
            {
                return false;
            }

            return ContainsAny(
                normalizedMessage,
                "duoi",
                "tren",
                "toi da",
                "khong qua",
                "khoang",
                "budget",
                "ngan sach",
                "trieu",
                "1080p",
                "1440p",
                "2k",
                "4k",
                "144hz",
                "165hz",
                "240hz",
                "nvidia",
                "geforce",
                "amd",
                "radeon",
                "ryzen",
                "intel",
                "im lang",
                "yen tinh",
                "it on",
                "khong rgb",
                "nao tot hon",
                "nao on hon",
                "nao hop hon",
                "cai nao",
                "con nao",
                "ban nao",
                "mau nao",
                "phuong an nao");
        }

        private static bool HasDirectBuilderSignal(
            string normalizedMessage,
            IReadOnlyCollection<PcComponentType> directTargetComponents)
        {
            return directTargetComponents.Any()
                || ContainsAny(
                    normalizedMessage,
                    "pc",
                    "build",
                    "cau hinh",
                    "linh kien",
                    "tuong thich",
                    "compatibility",
                    "nang cap",
                    "upgrade",
                    "socket",
                    "lap rap",
                    "builder")
                || ContainsNewBuildNeedPattern(normalizedMessage);
        }

        private static bool HasBuilderConversationContext(
            string normalizedConversation,
            bool hasCurrentBuild,
            IReadOnlyCollection<PcComponentType> targetComponents)
        {
            return hasCurrentBuild
                || targetComponents.Any()
                || ContainsNewBuildNeedPattern(normalizedConversation)
                || ContainsAny(
                    normalizedConversation,
                    "pc",
                    "build",
                    "cau hinh",
                    "linh kien",
                    "tuong thich",
                    "socket",
                    "lap rap",
                    "builder");
        }

        private static bool HasFollowUpBuildConstraintSignal(string normalizedMessage, BuildRequirementProfile requirement)
        {
            var hasBudgetSignal = (requirement.BudgetMax.HasValue || requirement.BudgetMin.HasValue)
                && ContainsAny(
                    normalizedMessage,
                    "duoi",
                    "tren",
                    "toi da",
                    "khong qua",
                    "tam",
                    "khoang",
                    "budget",
                    "ngan sach",
                    "trieu",
                    "tr",
                    "k");

            var hasResolutionSignal = !string.IsNullOrWhiteSpace(requirement.ResolutionTarget)
                || ContainsAny(normalizedMessage, "1080p", "1440p", "2k", "4k", "144hz", "165hz", "240hz");

            var hasBrandSignal = !string.IsNullOrWhiteSpace(requirement.PreferredBrand)
                && ContainsAny(normalizedMessage, "nvidia", "geforce", "amd", "radeon", "ryzen", "intel");

            var hasWorkloadSignal = !string.IsNullOrWhiteSpace(requirement.GameTitle)
                || (!string.IsNullOrWhiteSpace(requirement.PrimaryPurpose)
                    && ContainsUseCaseKeywords(normalizedMessage));

            var hasRecommendationFollowUpSignal = ContainsAny(
                normalizedMessage,
                "goi y",
                "de xuat",
                "nen mua",
                "chon",
                "ngon hon",
                "manh hon",
                "tot hon",
                "phu hop hon");

            return hasBudgetSignal
                || hasResolutionSignal
                || hasBrandSignal
                || hasWorkloadSignal
                || hasRecommendationFollowUpSignal;
        }

        private static bool ContainsNonPcDeviceKeywords(string normalizedConversation)
        {
            return ContainsAny(
                normalizedConversation,
                "laptop",
                "notebook",
                "macbook",
                "iphone",
                "ipad",
                "android",
                "dien thoai",
                "tablet",
                "playstation",
                "ps5",
                "xbox",
                "nintendo switch",
                "steam deck");
        }

        private static bool ContainsNewBuildNeedPattern(string normalizedText)
        {
            return ContainsAny(
                       normalizedText,
                       "build",
                       "cau hinh",
                       "pc gaming",
                       "pc render",
                       "pc do hoa",
                       "pc hoc tap",
                       "pc van phong",
                       "pc choi game",
                       "lap rap")
                   || (ContainsAny(normalizedText, "may tinh", "bo may")
                       && ContainsUseCaseKeywords(normalizedText));
        }

        private static bool ContainsUseCaseKeywords(string normalizedText)
        {
            return ContainsAny(
                normalizedText,
                "choi game",
                "gaming",
                "render",
                "edit",
                "stream",
                "lap trinh",
                "hoc tap",
                "van phong",
                "nghe nhac",
                "studio",
                "thu am",
                "lam nhac",
                "mix nhac",
                "audio",
                "ai",
                "machine learning",
                "deep learning",
                "llm",
                "stable diffusion",
                "cad",
                "revit",
                "autocad",
                "solidworks",
                "sketchup",
                "blender",
                "photoshop",
                "lightroom",
                "edit anh",
                "server",
                "nas",
                "backup",
                "plex",
                "trading",
                "chung khoan",
                "da man hinh",
                "multi monitor",
                "xem phim",
                "giai tri",
                "media",
                "office");
        }

        private static bool ContainsPcKeywords(string normalizedConversation)
        {
            return ContainsAny(
                normalizedConversation,
                "pc",
                "build",
                "cau hinh",
                "socket",
                "tuong thich",
                "desktop",
                "workstation",
                "linh kien");
        }

    }
}
