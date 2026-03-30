using Eshop.Constants;
using Eshop.Models;
using Eshop.Models.ViewModels;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Eshop.Helpers
{
    public static class SupportChatContentHelper
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public static SupportChatStoredContentViewModel Deserialize(string? rawContent)
        {
            if (string.IsNullOrWhiteSpace(rawContent))
            {
                return new SupportChatStoredContentViewModel();
            }

            var trimmed = rawContent.Trim();

            if (trimmed.StartsWith("{", StringComparison.Ordinal))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<SupportChatStoredContentViewModel>(trimmed, JsonOptions)
                        ?? new SupportChatStoredContentViewModel();

                    parsed.Text = NormalizeText(parsed.Text);
                    parsed.MessageType = ResolveMessageType(parsed);
                    return parsed;
                }
                catch
                {
                }
            }

            return new SupportChatStoredContentViewModel
            {
                MessageType = ChatMessageTypes.Text,
                Text = trimmed
            };
        }

        public static string Serialize(SupportChatStoredContentViewModel content)
        {
            content.Text = NormalizeText(content.Text);
            content.MessageType = ResolveMessageType(content);

            if (IsPlainTextOnly(content))
            {
                return content.Text ?? string.Empty;
            }

            return JsonSerializer.Serialize(content, JsonOptions);
        }

        public static bool HasMeaningfulContent(SupportChatStoredContentViewModel content)
        {
            return !string.IsNullOrWhiteSpace(NormalizeText(content.Text))
                || !string.IsNullOrWhiteSpace(content.AttachmentUrl)
                || content.ProductId.HasValue;
        }

        public static string BuildPreview(SupportChatStoredContentViewModel content)
        {
            var text = NormalizeText(content.Text);

            return ResolveMessageType(content) switch
            {
                ChatMessageTypes.Image => string.IsNullOrWhiteSpace(text)
                    ? "[Anh]"
                    : "[Anh] " + text,
                ChatMessageTypes.Video => string.IsNullOrWhiteSpace(text)
                    ? "[Video]"
                    : "[Video] " + text,
                ChatMessageTypes.Product => !string.IsNullOrWhiteSpace(content.ProductName)
                    ? "[San pham] " + content.ProductName
                    : "[San pham]",
                _ => text ?? string.Empty
            };
        }

        public static string ResolveMessageType(SupportChatStoredContentViewModel content)
        {
            if (!string.IsNullOrWhiteSpace(content.AttachmentUrl))
            {
                var contentType = content.AttachmentContentType ?? string.Empty;
                if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
                {
                    return ChatMessageTypes.Video;
                }

                return ChatMessageTypes.Image;
            }

            if (content.ProductId.HasValue)
            {
                return ChatMessageTypes.Product;
            }

            return ChatMessageTypes.Text;
        }

        public static SupportChatProductCardViewModel BuildProductCard(ProductModel product)
        {
            return new SupportChatProductCardViewModel
            {
                Id = product.Id,
                Name = product.Name,
                Price = product.Price,
                ImageUrl = ResolveProductImage(product),
                Url = ResolveProductUrl(product.Id)
            };
        }

        public static string ResolveProductUrl(int productId) => $"/Product/Details/{productId}";

        public static string? ResolveProductImage(ProductModel? product)
        {
            return ProductImageHelper.ResolveProductImage(product);
        }

        public static string? ResolveAssetUrl(string? assetValue)
        {
            return ProductImageHelper.ResolveAssetUrl(assetValue);
        }

        private static bool IsPlainTextOnly(SupportChatStoredContentViewModel content)
        {
            return ResolveMessageType(content) == ChatMessageTypes.Text
                && string.IsNullOrWhiteSpace(content.AttachmentUrl)
                && !content.ProductId.HasValue;
        }

        private static string? NormalizeText(string? text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
    }
}
