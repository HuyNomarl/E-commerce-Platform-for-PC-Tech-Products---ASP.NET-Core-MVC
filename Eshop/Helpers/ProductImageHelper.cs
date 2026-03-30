using Eshop.Models;

namespace Eshop.Helpers
{
    public static class ProductImageHelper
    {
        public static string? ResolveProductImage(ProductModel? product)
        {
            if (product == null)
            {
                return null;
            }

            var imageUrl = product.ProductImages?
                .OrderByDescending(x => x.IsMain)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .Select(x => x.Url)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                imageUrl = product.Image;
            }

            return ResolveAssetUrl(imageUrl);
        }

        public static string? ResolveAssetUrl(string? assetValue)
        {
            if (string.IsNullOrWhiteSpace(assetValue))
            {
                return null;
            }

            var trimmedValue = assetValue.Trim();

            if (trimmedValue.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmedValue.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return trimmedValue;
            }

            if (trimmedValue.StartsWith("/", StringComparison.Ordinal))
            {
                return trimmedValue;
            }

            if (trimmedValue.StartsWith("~/", StringComparison.Ordinal))
            {
                return trimmedValue[1..];
            }

            return "/media/products/" + Path.GetFileName(trimmedValue);
        }
    }
}
