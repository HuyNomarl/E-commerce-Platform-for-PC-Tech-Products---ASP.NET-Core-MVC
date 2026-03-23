using System.Collections.Generic;

namespace Eshop.Models.ViewModels
{
    public class PcBuildChatRequest
    {
        public string Message { get; set; } = "";
        public List<PcBuildCheckItemDto> Items { get; set; } = new();
    }
}
