using Eshop.Models;
using Eshop.Models.Enums;

namespace Eshop.Helpers
{
    public static class ProductCatalogAdminRules
    {
        public static bool IsCatalogManaged(ProductModel? product)
        {
            return product != null && IsCatalogManaged(product.ProductType);
        }

        public static bool IsCatalogManaged(ProductType productType)
        {
            return productType == ProductType.Normal || productType == ProductType.PcPrebuilt;
        }

        public static bool IsPcBuilderManaged(ProductModel? product)
        {
            return product != null && IsPcBuilderManaged(product.ProductType);
        }

        public static bool IsPcBuilderManaged(ProductType productType)
        {
            return productType == ProductType.Component || productType == ProductType.Monitor;
        }

        public static bool CanConfigureOptions(ProductModel? product)
        {
            return product != null && CanConfigureOptions(product.ProductType);
        }

        public static bool CanConfigureOptions(ProductType productType)
        {
            return productType == ProductType.PcPrebuilt;
        }

        public static bool HasLegacyOptions(ProductModel? product)
        {
            return product?.OptionGroups?.Any() == true && !CanConfigureOptions(product);
        }

        public static bool CanAccessOptionManagement(ProductModel? product)
        {
            return product?.OptionGroups?.Any() == true || CanConfigureOptions(product);
        }

        public static string GetAdminProductTypeLabel(ProductType productType)
        {
            return productType switch
            {
                ProductType.Normal => "Sản phẩm thường",
                ProductType.PcPrebuilt => "PC dựng sẵn",
                ProductType.Component => "Linh kiện PC",
                ProductType.Monitor => "Màn hình",
                _ => "Sản phẩm"
            };
        }

        public static string GetProductTypeHelperText(ProductType productType)
        {
            return productType switch
            {
                ProductType.PcPrebuilt => "PC dựng sẵn được phép cấu hình option nâng cấp sau khi lưu.",
                ProductType.Normal => "Sản phẩm thường không có option nâng cấp riêng.",
                ProductType.Component => "Linh kiện PC được quản lý ở phân hệ Linh kiện build PC.",
                ProductType.Monitor => "Màn hình dùng cho builder được quản lý ở phân hệ Linh kiện build PC.",
                _ => "Chọn đúng loại sản phẩm để hệ thống phân luồng quản trị chính xác."
            };
        }
    }
}
