namespace Eshop.Constants
{
    public static class CacheKeys
    {
        public const string HomeProducts = "home_products";
        public const string HomeSliders = "home_sliders";
        public const string ContactInfo = "contact_info";

        public static string ProductDetail(long id) => $"product_detail_{id}";
        public const string Categories = "categories_all";
        public const string Publishers = "publishers_all";
    }
}