using System.Collections.Generic;

namespace Eshop.Models.ViewModels
{
    public class PcBuildChatHistoryItemDto
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
    }

    public class PcBuildChatRequest
    {
        public string Message { get; set; } = "";
        public List<PcBuildCheckItemDto> Items { get; set; } = new();
        public List<PcBuildChatHistoryItemDto> History { get; set; } = new();
    }
}
