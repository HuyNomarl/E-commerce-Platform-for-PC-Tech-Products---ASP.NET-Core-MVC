using Eshop.Models.Enums;
using System.Text.Json.Serialization;

namespace Eshop.Models.ViewModels
{
    public class PcBuildCheckItemDto
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public PcComponentType ComponentType { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class PcBuildCheckRequest
    {
        public List<PcBuildCheckItemDto> Items { get; set; } = new();
    }

    public class CompatibilityMessageDto
    {
        public string Level { get; set; } = "info"; // error, warning, info
        public string Message { get; set; } = string.Empty;
    }

    public class PcBuildCheckResponse
    {
        public bool IsValid { get; set; }
        public decimal TotalPrice { get; set; }
        public int EstimatedPower { get; set; }
        public List<CompatibilityMessageDto> Messages { get; set; } = new();
    }

    public class PcBuilderProductSpecDto
    {
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class PcBuilderProductCardDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? Image { get; set; }
        public decimal Price { get; set; }
        public string? PublisherName { get; set; }
        public int PublisherId { get; set; }
        public int Stock { get; set; }
        public string ComponentType { get; set; } = string.Empty;
        public List<string> SummarySpecs { get; set; } = new();
        public List<PcBuilderProductSpecDto> FilterSpecs { get; set; } = new();
    }

    public class SavePcBuildRequest
    {
        public string? BuildName { get; set; }
        public List<PcBuildCheckItemDto> Items { get; set; } = new();
    }
}