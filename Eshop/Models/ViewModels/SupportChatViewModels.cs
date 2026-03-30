using Microsoft.AspNetCore.Http;

namespace Eshop.Models.ViewModels
{
    public class SupportChatSendInputViewModel
    {
        public string? ReceiverId { get; set; }
        public string? Content { get; set; }
        public IFormFile? Attachment { get; set; }
        public int? ProductId { get; set; }
    }

    public class SupportChatStoredContentViewModel
    {
        public string MessageType { get; set; } = Constants.ChatMessageTypes.Text;
        public string? Text { get; set; }
        public string? AttachmentUrl { get; set; }
        public string? AttachmentPublicId { get; set; }
        public string? AttachmentFileName { get; set; }
        public string? AttachmentContentType { get; set; }
        public int? ProductId { get; set; }
        public string? ProductName { get; set; }
        public decimal? ProductPrice { get; set; }
        public string? ProductImageUrl { get; set; }
    }

    public class SupportChatProductCardViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public string Url { get; set; } = string.Empty;
    }

    public class SupportChatMessagePayloadViewModel
    {
        public int Id { get; set; }
        public string SenderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string ReceiverId { get; set; } = string.Empty;
        public string ReceiverName { get; set; } = string.Empty;
        public string MessageType { get; set; } = Constants.ChatMessageTypes.Text;
        public string? Text { get; set; }
        public string Content { get; set; } = string.Empty;
        public string Preview { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string CreatedAtIso { get; set; } = string.Empty;
        public bool IsFromSupport { get; set; }
        public string? AttachmentUrl { get; set; }
        public string? AttachmentName { get; set; }
        public string? AttachmentContentType { get; set; }
        public SupportChatProductCardViewModel? Product { get; set; }
    }

    public class SupportChatConversationItemViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string LastMessagePreview { get; set; } = string.Empty;
        public string LastMessageAt { get; set; } = string.Empty;
        public string LastMessageAtIso { get; set; } = string.Empty;
        public int UnreadCount { get; set; }
    }
}
