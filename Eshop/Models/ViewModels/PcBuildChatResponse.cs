using System.Collections.Generic;

namespace Eshop.Models.ViewModels
{
    public class PcBuildChatResponse
    {
        public string Reply { get; set; } = "";
        public bool IsValid { get; set; }
        public decimal TotalPrice { get; set; }
        public int EstimatedPower { get; set; }
        public List<CompatibilityMessageDto> Messages { get; set; } = new();
    }
}
