using Eshop.Models;
using Eshop.Models.Enums;
using Eshop.Models.ViewModels;

namespace Eshop.Services
{
    internal enum PcBuildChatIntentKind
    {
        GeneralHelp = 0,
        NewBuild = 1,
        UpgradeCurrentBuild = 2,
        ComponentRecommendation = 3,
        CompatibilityCheck = 4,
        KnowledgeCheck = 5,
        Clarification = 6
    }

    internal sealed class PcBuildChatIntentAnalysis
    {
        public PcBuildChatIntentKind Intent { get; set; } = PcBuildChatIntentKind.GeneralHelp;
        public BuildRequirementProfile Requirement { get; set; } = new();
        public List<PcComponentType> TargetComponents { get; set; } = new();
        public bool WantsProductSuggestions { get; set; }
        public bool NeedsClarification { get; set; }
        public bool ShouldGenerateBuildProposal { get; set; }
        public bool PotentialMisconception { get; set; }
        public List<string> ClarificationHints { get; set; } = new();
        public string ConversationContext { get; set; } = string.Empty;

        public string IntentCode =>
            Intent switch
            {
                PcBuildChatIntentKind.NewBuild => "new_build",
                PcBuildChatIntentKind.UpgradeCurrentBuild => "upgrade_build",
                PcBuildChatIntentKind.ComponentRecommendation => "component_recommendation",
                PcBuildChatIntentKind.CompatibilityCheck => "compatibility_check",
                PcBuildChatIntentKind.KnowledgeCheck => "knowledge_check",
                PcBuildChatIntentKind.Clarification => "clarification",
                _ => "general_help",
            };
    }

    internal sealed class PcBuildChatSelectedProduct
    {
        public PcBuildChatSelectedProduct(PcBuildCheckItemDto item, ProductModel product)
        {
            Item = item;
            Product = product;
            Summary = ProductRagTextFormatter.BuildPromptSummary(product, item.Quantity);
        }

        public PcBuildCheckItemDto Item { get; }
        public ProductModel Product { get; }
        public string Summary { get; }
    }
}
