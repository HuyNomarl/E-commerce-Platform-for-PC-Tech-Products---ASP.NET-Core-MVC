using System.Collections.Generic;

namespace Eshop.Models.ViewModels
{
    public class PcBuildChatSuggestedProductDto
    {
        public string ComponentType { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public PcBuilderProductCardDto Product { get; set; } = new();
    }

    public class PcBuildChatResponse
    {
        public string Reply { get; set; } = "";
        public string Intent { get; set; } = "";
        public bool IsValid { get; set; }
        public decimal TotalPrice { get; set; }
        public int EstimatedPower { get; set; }
        public bool NeedsClarification { get; set; }
        public List<string> ClarificationHints { get; set; } = new();
        public List<CompatibilityMessageDto> Messages { get; set; } = new();
        public List<PcBuildChatSuggestedProductDto> SuggestedProducts { get; set; } = new();
    }
}
