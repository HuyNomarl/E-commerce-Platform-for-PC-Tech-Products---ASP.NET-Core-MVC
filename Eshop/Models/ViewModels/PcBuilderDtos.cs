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

    public class PcBuilderResolvedItemDto
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public PcComponentType ComponentType { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; } = 1;
        public PcBuilderProductCardDto Product { get; set; } = new();
    }

    public class PcBuilderBuildDetailDto
    {
        public int? BuildId { get; set; }
        public string? BuildCode { get; set; }
        public string BuildName { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public decimal TotalPrice { get; set; }
        public int EstimatedPower { get; set; }
        public List<CompatibilityMessageDto> Messages { get; set; } = new();
        public List<PcBuilderResolvedItemDto> Items { get; set; } = new();
    }

    public class PcBuildSaveResultDto
    {
        public int BuildId { get; set; }
        public string BuildCode { get; set; } = string.Empty;
        public PcBuilderBuildDetailDto Detail { get; set; } = new();
    }

    public class PcBuildShareRequest
    {
        public string? BuildName { get; set; }
        public string ReceiverId { get; set; } = string.Empty;
        public string? Note { get; set; }
        public List<PcBuildCheckItemDto> Items { get; set; } = new();
    }

    public class PcBuildShareCreatedDto
    {
        public string ShareCode { get; set; } = string.Empty;
        public string ReceiverName { get; set; } = string.Empty;
        public string BuildCode { get; set; } = string.Empty;
        public string BuildName { get; set; } = string.Empty;
    }

    public class PcBuildShareListItemDto
    {
        public string ShareCode { get; set; } = string.Empty;
        public string BuildCode { get; set; } = string.Empty;
        public string BuildName { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string? SenderEmail { get; set; }
        public string? Note { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string CreatedAtText { get; set; } = string.Empty;
        public bool IsOpened { get; set; }
    }

    public class PcBuildShareUserLookupDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
    }

    public class PcBuildWorkbookImportModel
    {
        public string? BuildName { get; set; }
        public List<PcBuildWorkbookRowModel> Rows { get; set; } = new();
    }

    public class PcBuildWorkbookRowModel
    {
        public PcComponentType? ComponentType { get; set; }
        public int? ProductId { get; set; }
        public string? ProductName { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class PcBuildImportResultDto : PcBuilderBuildDetailDto
    {
        public string? SourceFileName { get; set; }
        public int ImportedRowCount { get; set; }
        public List<string> Warnings { get; set; } = new();
    }
}
